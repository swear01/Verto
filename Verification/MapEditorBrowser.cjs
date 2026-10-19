const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../Assets/WebGLTemplates/Phase');
const reports=path.resolve(__dirname,'../TestResults/MapEditor');fs.mkdirSync(reports,{recursive:true});
const mock=`<script>window.__calls=[];window.__fail=false;window.createUnityInstance=async()=>({SendMessage:(o,m,v)=>{__calls.push({o,m,v});if(!window.__silent&&(m==='SelectDemoRoom'||m==='LoadCustomMap'))setTimeout(()=>dispatchEvent(new CustomEvent('phase-map-result',{detail:{success:!__fail,message:__fail?'測試：地圖無效':''}})),30);}});</script>`;
let html=fs.readFileSync(path.join(root,'index.html'),'utf8').replace(/<script src="Build\/.*?<\/script>/,'').replace(/\{\{\{.*?\}\}\}/g,'"mock"').replace('</head>',mock+'</head>');
const server=http.createServer((req,res)=>{
 const file=decodeURIComponent(req.url.split('?')[0]);
 if(file==='/'){res.setHeader('Content-Type','text/html');res.end(html);return;}
 const name=path.basename(file);if(!['controls.js','maps.js','map-model.js','webgl-compat.js'].includes(name)){res.writeHead(404);res.end();return;}
 res.setHeader('Content-Type','text/javascript');res.end(fs.readFileSync(path.join(root,name)));
});
(async()=>{
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));let browser;
 try{
  browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1280,height:720},hasTouch:true});
  const errors=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.locator('#rooms-toggle').waitFor({state:'visible'});
  await page.locator('#rooms-toggle').click();await page.locator('[data-room="5"]').click();await page.waitForFunction(()=>!document.getElementById('rooms').open);
  assert(await page.evaluate(()=>__calls.some(c=>c.m==='SelectDemoRoom'&&c.v==='5')));
  assert.equal(await page.locator('[data-room]').count(),12,'selector includes crystals and level12');
  assert.deepEqual(await page.locator('[data-room]').allInnerTexts(),Array.from({length:12},(_,i)=>String(i+1)),'levels are numbered1..12');
  for(const room of ['9','10','11','12']) {
    await page.locator('#rooms-toggle').click();await page.locator(`[data-room="${room}"]`).click();
    await page.waitForFunction(()=>!document.getElementById('rooms').open);
    assert(await page.evaluate(value=>__calls.some(c=>c.m==='SelectDemoRoom'&&c.v===value),room));
  }
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

  await page.locator('#rooms-toggle').click();await page.locator('#map-open').click();assert(await page.locator('#map-editor').isVisible());
  const board=page.locator('#map-board');await board.focus();
  await page.keyboard.press('ArrowRight');await page.keyboard.press('ArrowRight');await page.keyboard.press('Space');
  assert(await page.locator('#map-undo').isEnabled());
  await page.locator('#map-undo').click();assert(await page.locator('#map-redo').isEnabled());
  assert.equal(await page.evaluate(()=>JSON.parse(localStorage.getItem('phase.map.v1')).blocks.some(b=>b.x===40&&b.y===152)),false,'undo removes keyboard stroke');
  await page.locator('#map-redo').click();
  assert(await page.evaluate(()=>JSON.parse(localStorage.getItem('phase.map.v1')).blocks.some(b=>b.x===40&&b.y===152)),'redo restores keyboard stroke');
  await page.locator('[data-map-tool="spike0"]').click();await page.locator('[data-map-mask="5"]').click();
  const rect=await board.boundingBox();
  await page.mouse.move(rect.x+rect.width*10.5/40,rect.y+rect.height*10.5/22);await page.mouse.down();
  await page.mouse.move(rect.x+rect.width*14.5/40,rect.y+rect.height*10.5/22,{steps:4});await page.mouse.up();
  const download=page.waitForEvent('download');await page.locator('#map-export').click();const file=await download;
  assert.equal(file.suggestedFilename(),'phase-custom-map.json');const filename=path.join(reports,'roundtrip.json');await file.saveAs(filename);
  const map=JSON.parse(fs.readFileSync(filename));assert.equal(map.spikes.length,5);assert.equal(map.spikes[0].phaseMask,5);
  assert(map.blocks.some(b=>b.x===40&&b.y===152),'keyboard paint reaches selected cell');
  await page.locator('#map-file').setInputFiles({name:'invalid.json',mimeType:'application/json',buffer:Buffer.from('{')});await page.waitForFunction(()=>document.getElementById('map-status').textContent.includes('JSON'));
  await page.locator('#map-file').setInputFiles(filename);
  const empty={name:'empty',blocks:[],spikes:[],spawnX:24,spawnY:152};
  await page.locator('#map-file').setInputFiles({name:'empty.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(empty))});
  const callsBefore=await page.evaluate(()=>__calls.filter(c=>c.m==='LoadCustomMap').length);
  await page.locator('#map-play').click();assert.match(await page.locator('#map-status').innerText(),/至少需要一格地形/);
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='LoadCustomMap').length),callsBefore,'empty draft stays local');
  await page.locator('#map-file').setInputFiles(filename);
  await page.evaluate(()=>__fail=true);await page.locator('#map-play').click();await page.waitForFunction(()=>document.getElementById('map-status').textContent==='測試：地圖無效');assert(await page.locator('#map-editor').isVisible());
  await page.evaluate(()=>__fail=false);await page.locator('#map-play').click();await page.waitForFunction(()=>!document.getElementById('map-editor').open);
  const sent=await page.evaluate(()=>__calls.filter(c=>c.m==='LoadCustomMap').at(-1));assert.deepEqual(JSON.parse(sent.v),map);
  await page.reload();await page.locator('#rooms-toggle').click();await page.locator('#map-open').click();
  const persisted=await page.evaluate(()=>JSON.parse(localStorage.getItem('phase.map.v1')));assert.deepEqual(persisted,map,'draft survives reload and room selection');
  await page.screenshot({path:path.join(reports,'editor-desktop.png')});
  await page.setViewportSize({width:390,height:844});const touch=await board.boundingBox();await page.touchscreen.tap(touch.x+touch.width*20.5/40,touch.y+touch.height*12.5/22);assert(await page.locator('#map-undo').isEnabled(),'touch paints a cell');await page.screenshot({path:path.join(reports,'editor-mobile.png')});
  assert(await page.evaluate(()=>document.getElementById('map-editor').getBoundingClientRect().right<=innerWidth));
  await page.evaluate(()=>{dispatchEvent(new Event('blur'));dispatchEvent(new Event('focus'));});
  assert.equal(await page.evaluate(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v),'true','focus cannot resume behind map dialog');
  await page.evaluate(()=>{window.__setItem=Storage.prototype.setItem;Storage.prototype.setItem=function(){throw new DOMException('blocked','SecurityError');};});
  await board.focus();await page.keyboard.press('ArrowRight');await page.keyboard.press('Space');
  assert.match(await page.locator('#map-status').innerText(),/本次編輯仍保留/);
  await page.evaluate(()=>{Storage.prototype.setItem=window.__setItem;document.getElementById('controls').showModal();document.getElementById('map-editor').close();});
  await page.waitForFunction(()=>__calls.filter(c=>c.m==='SetControlsOpen').at(-1).v==='true');
  assert(await page.locator('#controls').isVisible(),'closing map preserves another dialog pause');
  await page.locator('#controls-close').click();
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
  await page.locator('#rooms-toggle').click();await page.locator('#map-open').click();assert(await page.locator('#map-play').isEnabled());
  const gameJson='{"name":"in-game"}',gameDownload=page.waitForEvent('download');
  await page.evaluate(json=>dispatchEvent(new CustomEvent('phase-map-export',{detail:{json}})),gameJson);
  const gameFile=await gameDownload;assert.equal(gameFile.suggestedFilename(),'phase-custom-map.json');
  assert.equal(fs.readFileSync(await gameFile.path(),'utf8'),gameJson,'in-game export saves Unity JSON unchanged');
  const gameChooser=page.waitForEvent('filechooser');await page.evaluate(()=>dispatchEvent(new CustomEvent('phase-map-import')));
  await (await gameChooser).setFiles({name:'in-game.json',mimeType:'application/json',buffer:Buffer.from(gameJson)});
  await page.waitForFunction(()=>__calls.some(c=>c.m==='ImportDraft'));
  assert.deepEqual(await page.evaluate(()=>__calls.filter(c=>c.m==='ImportDraft')),[{o:'PHASE',m:'ImportDraft',v:gameJson}],'in-game import sends the file to Unity');
  assert.deepEqual(errors,[]);console.log('PASS: in-game editor file bridge, mock bridge selection, victory dialog, timeout recovery, painter keyboard/drag, history, JSON, failure retention, persistence, mobile and repeated resized fullscreen enter/exit');
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
