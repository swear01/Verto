const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../Assets/WebGLTemplates/Phase');
const reports=path.resolve(__dirname,'../TestResults/WebPage');fs.mkdirSync(reports,{recursive:true});
const mock=`<script>window.__calls=[];window.__fail=false;window.createUnityInstance=async()=>({SendMessage:(o,m,v)=>{__calls.push({o,m,v});if(!window.__silent&&m==='SelectDemoRoom')setTimeout(()=>dispatchEvent(new CustomEvent('phase-map-result',{detail:{success:!__fail,message:__fail?'測試：載入失敗':''}})),30);}});</script>`;
let html=fs.readFileSync(path.join(root,'index.html'),'utf8').replace(/<script src="Build\/.*?<\/script>/,'').replace(/\{\{\{.*?\}\}\}/g,'"mock"').replace('</head>',mock+'</head>');
const server=http.createServer((req,res)=>{
 const file=decodeURIComponent(req.url.split('?')[0]);
 if(file==='/'){res.setHeader('Content-Type','text/html');res.end(html);return;}
 const name=path.basename(file);if(!['controls.js','maps.js','webgl-compat.js'].includes(name)){res.writeHead(404);res.end();return;}
 res.setHeader('Content-Type','text/javascript');res.end(fs.readFileSync(path.join(root,name)));
});
(async()=>{
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));let browser;
 try{
  browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1280,height:720}});
  const errors=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.locator('#rooms-toggle').waitFor({state:'visible'});
  assert.equal(await page.locator('#map-open, #map-editor').count(),0,'the HTML map editor is gone');
  await page.locator('#rooms-toggle').click();await page.screenshot({path:path.join(reports,'rooms-desktop.png')});
  await page.locator('[data-room="5"]').click();await page.waitForFunction(()=>!document.getElementById('rooms').open);
  assert(await page.evaluate(()=>__calls.some(c=>c.m==='SelectDemoRoom'&&c.v==='5')));
  assert.equal(await page.locator('[data-room]').count(),12,'selector includes crystals and level12');
  assert.deepEqual(await page.locator('[data-room]').allInnerTexts(),Array.from({length:12},(_,i)=>String(i+1)),'levels are numbered1..12');
  for(const room of ['9','10','11','12']) {
    await page.locator('#rooms-toggle').click();await page.locator(`[data-room="${room}"]`).click();
    await page.waitForFunction(()=>!document.getElementById('rooms').open);
    assert(await page.evaluate(value=>__calls.some(c=>c.m==='SelectDemoRoom'&&c.v===value),room));
  }
  await page.evaluate(()=>__fail=true);await page.locator('#rooms-toggle').click();await page.locator('[data-room="4"]').click();
  await page.waitForFunction(()=>document.getElementById('rooms-status').textContent==='測試：載入失敗');
  assert(await page.locator('#rooms').isVisible(),'a failed load keeps the selector open');
  await page.evaluate(()=>__fail=false);await page.locator('#rooms-close').click();
  await page.evaluate(()=>dispatchEvent(new CustomEvent('phase-victory')));await page.locator('#victory').waitFor({state:'visible'});
  assert.equal(await page.locator('#victory-title').innerText(),'你贏了');
  assert.equal(await page.evaluate(()=>document.activeElement.id),'victory-retry','retry is the default action');
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'true','victory pauses play');
  await page.screenshot({path:path.join(reports,'victory-desktop.png')});
  await page.locator('#victory-rooms').click();assert(await page.locator('#rooms').isVisible());assert.equal(await page.locator('#victory').isVisible(),false);
  await page.locator('#rooms-close').click();
  await page.evaluate(()=>dispatchEvent(new CustomEvent('phase-victory')));await page.locator('#victory-retry').click();
  await page.waitForFunction(()=>!document.getElementById('victory').open);
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SelectDemoRoom').at(-1).v),'1','retry restarts level1');
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'false','retry resumes play');

  await page.locator('#rooms-toggle').click();
  await page.evaluate(()=>{dispatchEvent(new Event('blur'));dispatchEvent(new Event('focus'));});
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'true','focus cannot resume behind the selector');
  await page.evaluate(()=>{document.getElementById('controls').showModal();document.getElementById('rooms').close();});
  await page.waitForFunction(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v==='true');
  assert(await page.locator('#controls').isVisible(),'closing the selector preserves another dialog pause');
  await page.locator('#controls-close').click();
  await page.setViewportSize({width:390,height:844});await page.locator('#rooms-toggle').click();
  assert(await page.evaluate(()=>document.getElementById('rooms').getBoundingClientRect().right<=innerWidth));
  await page.screenshot({path:path.join(reports,'rooms-mobile.png')});await page.locator('#rooms-close').click();
  await page.setViewportSize({width:1280,height:720});
  const cdp=await page.context().newCDPSession(page);
  for(let i=0;i<3;i++){
   await page.locator('#fullscreen-toggle').click();await page.waitForFunction(()=>!!document.fullscreenElement);
   await cdp.send('Emulation.setDeviceMetricsOverride',{width:636+i*20,height:827-i*20,deviceScaleFactor:1,mobile:false});
   await page.locator('#fullscreen-toggle').click();await page.waitForFunction(()=>!document.fullscreenElement);
   assert.equal(await page.locator('#fullscreen-toggle').getAttribute('aria-pressed'),'false');
  }
  await page.evaluate(()=>{window.__silent=true;const timeout=window.__timeout=window.setTimeout;window.setTimeout=(f,ms)=>timeout(f,ms===15000?30:ms);});
  await page.locator('#rooms-toggle').click();await page.locator('[data-room="2"]').click();
  await page.waitForFunction(()=>document.getElementById('rooms-status').textContent.includes('重新整理'));
  assert(await page.locator('[data-room="2"]').isDisabled(),'timeout cannot create overlapping uncorrelated bridge requests');
  const pauses=await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').length);
  await page.evaluate(()=>dispatchEvent(new CustomEvent('phase-map-result',{detail:{success:true}})));
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').length),pauses+1,'late completion reapplies the dialog pause');
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'true');
  assert(await page.locator('#rooms').isVisible(),'late callback cannot close a new dialog');
  assert(await page.locator('[data-room="2"]').isEnabled(),'late callback recovers selection');
  assert.equal(await page.locator('#rooms-status').innerText(),'','late callback clears the timeout');
  await page.evaluate(()=>{window.__silent=false;window.setTimeout=window.__timeout;});await page.locator('[data-room="3"]').click();
  await page.waitForFunction(()=>!document.getElementById('rooms').open);
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SelectDemoRoom').at(-1).v),'3','selection works after recovery');

  const gameJson='{"name":"in-game"}',gameDownload=page.waitForEvent('download');
  await page.evaluate(json=>dispatchEvent(new CustomEvent('phase-map-export',{detail:{json}})),gameJson);
  const gameFile=await gameDownload;assert.equal(gameFile.suggestedFilename(),'phase-custom-map.json');
  assert.equal(fs.readFileSync(await gameFile.path(),'utf8'),gameJson,'in-game export saves Unity JSON unchanged');
  const gameChooser=page.waitForEvent('filechooser');await page.evaluate(()=>dispatchEvent(new CustomEvent('phase-map-import')));
  await (await gameChooser).setFiles({name:'in-game.json',mimeType:'application/json',buffer:Buffer.from(gameJson)});
  await page.waitForFunction(()=>__calls.some(c=>c.m==='ImportDraft'));
  assert.deepEqual(await page.evaluate(()=>__calls.filter(c=>c.m==='ImportDraft')),[{o:'PHASE',m:'ImportDraft',v:gameJson}],'in-game import sends the file to Unity');
  assert.deepEqual(errors,[]);console.log('PASS: mock bridge selection and failure, victory dialog, dialog pause, mobile, repeated resized fullscreen, timeout recovery and in-game editor file bridge');
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
