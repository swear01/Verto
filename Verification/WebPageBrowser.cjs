const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../Assets/WebGLTemplates/Phase');
const reports=path.resolve(__dirname,'../TestResults/WebPage');fs.mkdirSync(reports,{recursive:true});
const mock=`<script>window.__calls=[];window.createUnityInstance=async()=>({SendMessage:(o,m,v)=>{__calls.push({o,m,v});}});</script>`;
let html=fs.readFileSync(path.join(root,'index.html'),'utf8').replace(/<script src="Build\/.*?<\/script>/,'').replace(/\{\{\{.*?\}\}\}/g,'"mock"').replace('</head>',mock+'</head>');
const server=http.createServer((req,res)=>{
 const file=decodeURIComponent(req.url.split('?')[0]);
 if(file==='/'){res.setHeader('Content-Type','text/html');res.end(html);return;}
 const name=path.basename(file);if(!['maps.js','webgl-compat.js'].includes(name)){res.writeHead(404);res.end();return;}
 res.setHeader('Content-Type','text/javascript');res.end(fs.readFileSync(path.join(root,name)));
});
(async()=>{
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));let browser;
 try{
  browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1280,height:720}});
  const errors=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.locator('#loading').waitFor({state:'hidden'});
  assert.equal(await page.locator('button, nav, dialog, input, a').count(),0,'menus, level select, settings and fullscreen live in the game');
  assert.equal(await page.locator('body').innerText(),'');
  await page.screenshot({path:path.join(reports,'page-desktop.png')});
  // The backbuffer matches the display; right-click (the editor's erase) never opens the browser menu.
  await page.evaluate(()=>{window.__canvas=()=>{const c=document.getElementById('unity-canvas'),r=c.getBoundingClientRect();return [c.width,c.height,r.x,r.y,r.width,r.height];};});
  async function fills(width,height){
   const expected=[width,height,0,0,width,height];
   await page.waitForFunction(e=>__canvas().join()===e.join(),expected,{timeout:5000}).catch(()=>{});
   assert.deepEqual(await page.evaluate(()=>__canvas()),expected);
  }
  await fills(1280,720);
  await page.evaluate(()=>{window.__menus=[];addEventListener('contextmenu',e=>__menus.push(e.defaultPrevented));});
  await page.locator('#unity-canvas').click({button:'right'});
  assert.deepEqual(await page.evaluate(()=>__menus),[true],'right-click on the game never opens the browser menu');
  // Keys reach Unity only from the canvas; Tab and keys elsewhere stay in the page.
  await page.evaluate(()=>{window.__keys=[];addEventListener('keydown',e=>__keys.push(e.code));});
  await page.locator('#unity-canvas').focus();await page.keyboard.press('Escape');await page.keyboard.press('Tab');
  assert.notEqual(await page.evaluate(()=>document.activeElement.id),'unity-canvas');
  await page.keyboard.press('KeyZ');
  assert.deepEqual(await page.evaluate(()=>__keys),['Escape'],'only canvas keys reach the game');
  await page.evaluate(()=>{dispatchEvent(new Event('blur'));});
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'true','leaving the page pauses play');
  await page.evaluate(()=>{dispatchEvent(new Event('focus'));});
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'false','returning resumes play');
  await page.setViewportSize({width:390,height:844});await fills(390,844);
  await page.screenshot({path:path.join(reports,'page-mobile.png')});
  await page.setViewportSize({width:1280,height:720});await fills(1280,720);
  // Unity's Screen.fullScreen puts the canvas in fullscreen; itch.io's button resizes the embedding frame instead.
  const cdp=await page.context().newCDPSession(page);
  for(let i=0;i<3;i++){
   await page.evaluate(()=>document.getElementById('unity-canvas').requestFullscreen());await page.waitForFunction(()=>document.fullscreenElement?.id==='unity-canvas');
   await cdp.send('Emulation.setDeviceMetricsOverride',{width:636+i*20,height:827-i*20,deviceScaleFactor:1,mobile:false});await fills(636+i*20,827-i*20);
   await page.evaluate(()=>document.exitFullscreen());await page.waitForFunction(()=>!document.fullscreenElement);
   await cdp.send('Emulation.setDeviceMetricsOverride',{width:1000+i*40,height:600,deviceScaleFactor:1,mobile:false});await fills(1000+i*40,600);
  }
  const gameJson='{"name":"in-game"}',gameDownload=page.waitForEvent('download');
  await page.evaluate(json=>dispatchEvent(new CustomEvent('phase-map-export',{detail:{json}})),gameJson);
  const gameFile=await gameDownload;assert.equal(gameFile.suggestedFilename(),'phase-custom-map.json');
  assert.equal(fs.readFileSync(await gameFile.path(),'utf8'),gameJson,'in-game export saves Unity JSON unchanged');
  const gameChooser=page.waitForEvent('filechooser');await page.evaluate(()=>dispatchEvent(new CustomEvent('phase-map-import')));
  await (await gameChooser).setFiles({name:'in-game.json',mimeType:'application/json',buffer:Buffer.from(gameJson)});
  await page.waitForFunction(()=>__calls.some(c=>c.m==='ImportDraft'));
  assert.deepEqual(await page.evaluate(()=>__calls.filter(c=>c.m==='ImportDraft')),[{o:'PHASE',m:'ImportDraft',v:gameJson}],'in-game import sends the file to Unity');
  assert.deepEqual(errors,[]);console.log('PASS: control-free page, display-sized backbuffer, no canvas context menu, canvas-only keys, background pause, viewport sizing, repeated resized canvas fullscreen and in-game editor file bridge');
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
