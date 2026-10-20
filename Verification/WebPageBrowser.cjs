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
  await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.locator('#fullscreen-toggle').waitFor({state:'visible'});
  assert.equal(await page.locator('dialog, #rooms-toggle, #controls-toggle, #map-open').count(),0,'menus, level select and key settings live in the game');
  assert.deepEqual(await page.locator('#game-tools button').evaluateAll(b=>b.map(e=>e.id)),['fullscreen-toggle'],'the page keeps only fullscreen');
  assert.equal(await page.locator('body').innerText(),'');
  await page.screenshot({path:path.join(reports,'page-desktop.png')});
  // The backbuffer matches the display; right-click (the editor's erase) never opens the browser menu.
  const canvasSize=()=>page.locator('#unity-canvas').evaluate(c=>[c.width,c.height,c.clientWidth,c.clientHeight]);
  assert.deepEqual(await canvasSize(),[1280,720,1280,720]);
  await page.evaluate(()=>{window.__menus=[];addEventListener('contextmenu',e=>__menus.push(e.defaultPrevented));});
  await page.locator('#unity-canvas').click({button:'right'});
  assert.deepEqual(await page.evaluate(()=>__menus),[true],'right-click on the game never opens the browser menu');
  // Keys reach Unity only from the canvas; Tab and keys on page buttons stay in the page.
  await page.evaluate(()=>{window.__keys=[];addEventListener('keydown',e=>__keys.push(e.code));});
  await page.locator('#unity-canvas').focus();await page.keyboard.press('Escape');await page.keyboard.press('Tab');
  assert.equal(await page.evaluate(()=>document.activeElement.id),'fullscreen-toggle');
  await page.keyboard.press('KeyZ');
  assert.deepEqual(await page.evaluate(()=>__keys),['Escape'],'only canvas keys reach the game');
  await page.evaluate(()=>{dispatchEvent(new Event('blur'));});
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'true','leaving the page pauses play');
  await page.evaluate(()=>{dispatchEvent(new Event('focus'));});
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'false','returning resumes play');
  await page.setViewportSize({width:390,height:844});
  await page.waitForFunction(()=>document.getElementById('unity-canvas').width===390);
  assert.deepEqual(await canvasSize(),[390,844,390,844]);
  assert(await page.evaluate(()=>document.getElementById('fullscreen-toggle').getBoundingClientRect().right<=innerWidth));
  await page.screenshot({path:path.join(reports,'page-mobile.png')});
  await page.setViewportSize({width:1280,height:720});
  const cdp=await page.context().newCDPSession(page);
  for(let i=0;i<3;i++){
   await page.locator('#fullscreen-toggle').click();await page.waitForFunction(()=>!!document.fullscreenElement);
   await cdp.send('Emulation.setDeviceMetricsOverride',{width:636+i*20,height:827-i*20,deviceScaleFactor:1,mobile:false});
   await page.locator('#fullscreen-toggle').click();await page.waitForFunction(()=>!document.fullscreenElement);
   assert.equal(await page.locator('#fullscreen-toggle').getAttribute('aria-pressed'),'false');
  }
  const gameJson='{"name":"in-game"}',gameDownload=page.waitForEvent('download');
  await page.evaluate(json=>dispatchEvent(new CustomEvent('phase-map-export',{detail:{json}})),gameJson);
  const gameFile=await gameDownload;assert.equal(gameFile.suggestedFilename(),'phase-custom-map.json');
  assert.equal(fs.readFileSync(await gameFile.path(),'utf8'),gameJson,'in-game export saves Unity JSON unchanged');
  const gameChooser=page.waitForEvent('filechooser');await page.evaluate(()=>dispatchEvent(new CustomEvent('phase-map-import')));
  await (await gameChooser).setFiles({name:'in-game.json',mimeType:'application/json',buffer:Buffer.from(gameJson)});
  await page.waitForFunction(()=>__calls.some(c=>c.m==='ImportDraft'));
  assert.deepEqual(await page.evaluate(()=>__calls.filter(c=>c.m==='ImportDraft')),[{o:'PHASE',m:'ImportDraft',v:gameJson}],'in-game import sends the file to Unity');
  assert.deepEqual(errors,[]);console.log('PASS: fullscreen-only page, display-sized backbuffer, no canvas context menu, canvas-only keys, background pause, mobile, repeated resized fullscreen and in-game editor file bridge');
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
