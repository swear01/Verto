const { chromium } = require('playwright');
const { PNG } = require('pngjs');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');

const remote = /^https?:/.test(process.argv[2] || '') ? process.argv[2] : null;
const root = path.resolve(remote ? 'Builds/Web' : (process.argv[2] || 'Builds/Web'));
const reports = path.resolve(__dirname, '../TestResults/Web');
fs.mkdirSync(reports, { recursive: true });
if (!remote) assert(fs.existsSync(path.join(root, 'index.html')), 'Web output is missing');
const server = http.createServer((request, response) => {
  let file;
  try { file = path.resolve(root, '.' + decodeURIComponent(new URL(request.url, 'http://localhost').pathname)); }
  catch { response.writeHead(400).end(); return; }
  if (file === root) file = path.join(root, 'index.html');
  if (!file.startsWith(root + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
    response.writeHead(404).end(); return;
  }
  const mime = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm' };
  response.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).on('error', () => response.destroy()).pipe(response);
});

const cloakColors=Array.from({length:9},(_,i)=>[234,223,197].map((v,k)=>Math.round(v+([255,91,94][k]-v)*i/8)));

function character(buffer) {
  const image = PNG.sync.read(buffer);
  let xSum = 0, ySum = 0, count = 0, minX = image.width, maxX = 0, minY = image.height, maxY = 0;
  for (let y = 0; y < image.height; y++) for (let x = 0; x < image.width; x++) {
    // Toolbar icons share the cloak color in full-viewport captures.
    if (y < 80 && x > image.width - 140) continue;
    const i = (y * image.width + x) * 4;
    if (image.data[i]>=232 && cloakColors.some(rgb=>rgb.every((v,k)=>Math.abs(image.data[i+k]-v)<4))) {
      xSum += x; ySum += y; count++;
      minX = Math.min(minX,x); maxX = Math.max(maxX,x); minY = Math.min(minY,y); maxY = Math.max(maxY,y);
    }
  }
  return count ? { x: xSum / count, y: ySum / count, width: maxX-minX+1, height: maxY-minY+1 } : null;
}

(async () => {
  let browser;
  const result = { errors: [], warnings: [], failedRequests: [], rooms: [], cacheResponses: [], cacheReads: [] };
  try {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const url = remote || `http://127.0.0.1:${server.address().port}/`;
    browser = await chromium.launch({ channel: 'chromium', headless: true });
    result.browser = browser.version();
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 });
    page.setDefaultTimeout(30000);
    await page.clock.install();
    const statuses=new WeakMap();
    page.on('console', message => {
      if (message.type() === 'error') result.errors.push(message.text());
      if (message.type() === 'warning') result.warnings.push(message.text());
      if (/successfully revalidated and served from the browser cache/.test(message.text())) result.cacheReads.push(message.text());
    });
    page.on('pageerror', error => result.errors.push(String(error)));
    page.on('response', response => {
      statuses.set(response.request(),response.status());
      if (response.status() === 304 && /\.data\.unityweb/.test(response.url())) result.cacheResponses.push(response.url());
    });
    page.on('requestfailed', request => result.failedRequests.push({url:request.url(),error:request.failure()?.errorText,status:statuses.get(request),conditional:Boolean(request.headers()['if-none-match'])}));
    const canvas = page.locator('#unity-canvas');
    const ready = async () => {
      await page.locator('#loading').waitFor({state:'hidden',timeout:120000});
      for (let n=0;n<40;n++) {
        const player=character(await canvas.screenshot());
        if(player) return player;
        await page.waitForTimeout(100);
      }
      throw Error('Player pixels did not render');
    };
    await page.goto(url); await ready(); await canvas.click();
    assert.equal(await page.title(),'PHASE');
    assert.equal(await page.locator('body').innerText(),'');
    await page.clock.pauseAt(await page.evaluate(()=>Date.now()+100));
    result.routeClock='Controlled browser time; production physics at 1/60s';
    const rooms=['DemoRooms','CrystalRooms'].flatMap(course=>JSON.parse(fs.readFileSync(path.join(__dirname,`../Assets/Phase/${course}.json`))).rooms);
    for(let index=0;index<rooms.length;index++) {
      const room=rooms[index];
      const start=character(await canvas.screenshot({path:path.join(reports,`demo-${index+1}-start.png`)}));
      assert(start && Math.abs(start.x/4-room.spawnX-4)<1,`Room ${index+1} checkpoint spawn`);
      let held=new Set();
      for(const step of room.route.slice(0,-1)) {
        const next=new Set();
        if(step.x)next.add(step.x<0?'ArrowLeft':'ArrowRight');
        if(step.y)next.add(step.y<0?'ArrowUp':'ArrowDown');
        for(const [flag,key] of [['jump','z'],['dash','x'],['grab','c']])if(step[flag])next.add(key);
        await Promise.all([...held].filter(k=>!next.has(k)).map(k=>page.keyboard.up(k)));
        await Promise.all([...next].filter(k=>!held.has(k)).map(k=>page.keyboard.down(k)));
        held=next;
        await page.clock.runFor(Math.round(step.frames*1000/60));
      }
      await Promise.all([...held].map(k=>page.keyboard.up(k)));
      const last=room.route.at(-1);
      assert(last.x===1&&!last.y&&!last.jump&&!last.dash&&!last.grab,'Route ends with a horizontal landing approach');
      await page.keyboard.down('ArrowRight');
      const deadline=Date.now()+5000;
      while(Date.now()<deadline) {
        await page.clock.runFor(32);
        const player=character(await canvas.screenshot());
        if(player&&player.x/4>=room.goal.x+3&&Math.abs(player.y/4-(room.goal.y-5))<5)break;
      }
      await page.keyboard.up('ArrowRight');
      const end=character(await canvas.screenshot({path:path.join(reports,`demo-${index+1}-end.png`)}));
      assert(end && end.x/4>=room.goal.x-2,`Room ${index+1} route reaches exit landing: ${JSON.stringify(end)}`);
      assert(Math.abs(end.y/4-(room.goal.y-5))<5,`Room ${index+1} exit height`);
      result.rooms.push({room:index+1,start,end});
      if(index<rooms.length-1) {
        await page.keyboard.down('ArrowRight');
        const travelDeadline=Date.now()+5000;
        let arrived=false;
        while(Date.now()<travelDeadline) {
          await page.clock.runFor(32);
          const player=character(await canvas.screenshot());
          if(player&&player.x/4<room.goal.x-12){arrived=true;break;}
        }
        await page.keyboard.up('ArrowRight');
        assert(arrived,`Room ${index+1} right edge loads the next checkpoint`);
        await page.clock.runFor(350);
      }
    }
    result.checkpointTransitions=rooms.length-1;console.log(`Passed ${rooms.length} routes and ${result.checkpointTransitions} transitions`);
    await page.clock.resume();
    await page.reload(); await ready(); await canvas.click();
    for(const [width,height] of [[320,407],[800,600],[2560,1080],[844,390],[390,844]]) {
      await page.setViewportSize({width,height}); await page.waitForTimeout(250);
      const box=await canvas.boundingBox();assert.deepEqual([box.x,box.y,box.width,box.height],[0,0,width,height]);
      assert.deepEqual(await canvas.evaluate(c=>[c.width,c.height]),[Math.max(320,Math.round(180*width/height)),Math.max(180,Math.ceil(320*height/width))]);
      assert(character(await canvas.screenshot()),'Player remains visible after resize');
      await page.screenshot({path:path.join(reports,`demo-viewport-${width}.png`)});
    }
    await page.setViewportSize({width:1280,height:720}); await page.waitForTimeout(250);
    console.log('Passed viewport checks; checking controls');
    await page.locator('#controls-toggle').click();
    const binding=(action,slot=0)=>page.locator(`#bindings button[data-action="${action}"][data-slot="${slot}"]`);
    const reset=action=>page.locator(`#bindings button[data-reset="${action}"]`);
    assert.equal(await page.locator('#bindings button[data-slot]').count(),14);
    await page.waitForTimeout(150);
    const paused=character(await canvas.screenshot());await page.keyboard.press('ArrowRight',{delay:180});
    const still=character(await canvas.screenshot());
    assert(paused&&still&&Math.abs(still.x-paused.x)<1&&Math.abs(still.y-paused.y)<1,'Settings pause player movement');
    await binding(1).click();await page.keyboard.press('l');
    assert.equal(await binding(1,1).innerText(),'D','Other movement slot survives');
    await binding(4).click();await page.keyboard.press('j');
    await binding(4,1).click();await page.keyboard.press('Escape');
    assert.equal(await binding(4,1).innerText(),'未設定');
    assert(await page.locator('#controls').isVisible());
    await page.locator('#controls-close').click();await page.waitForTimeout(150);
    const before=character(await canvas.screenshot());
    await page.keyboard.press('l',{delay:100});
    const moved=character(await canvas.screenshot());assert(moved.x>before.x+12,'Custom move reaches Unity');
    await page.keyboard.press('j',{delay:90});
    const jumped=character(await canvas.screenshot());assert(jumped.y<moved.y-12,'Custom jump reaches Unity');
    await page.reload();await ready();await page.locator('#controls-toggle').click();
    assert.equal(await binding(1).innerText(),'L');assert.equal(await binding(4).innerText(),'J');
    assert.equal(await binding(4,1).innerText(),'未設定','Cleared slot persists');
    await reset(4).click();assert.equal(await binding(4).innerText(),'Z');
    assert.equal(await binding(1).innerText(),'L','Per-action restore preserves another action');
    await reset(1).click();await page.locator('#controls-close').click();
    console.log('Passed controls; checking fullscreen');
    await page.locator('#fullscreen-toggle').click();
    await page.waitForFunction(()=>document.fullscreenElement===document.documentElement);
    await page.waitForFunction(()=>document.getElementById('fullscreen-toggle').getAttribute('aria-label')==='退出全螢幕');
    await page.screenshot({path:path.join(reports,'demo-fullscreen.png')});
    await page.locator('#fullscreen-toggle').click();await page.waitForFunction(()=>!document.fullscreenElement);
    console.log('Passed fullscreen; checking load failure');
    const failedPage=await browser.newPage();
    await failedPage.route('**/Build/*.data.unityweb',route=>route.abort());await failedPage.goto(url);
    await failedPage.locator('#error').waitFor({state:'visible',timeout:30000});
    assert.equal(await failedPage.locator('#loading').isVisible(),false);
    result.loadFailureVisible=true;
    const unexpected=result.failedRequests.filter(r=>!(remote&&r.error==='net::ERR_ABORTED'&&r.status===304&&r.conditional&&/\/Build\/[^/]+\.data\.unityweb$/.test(r.url)));
    assert.deepEqual(unexpected,[],'Unexpected request failures');
    assert.equal(result.cacheReads.length,result.failedRequests.length-unexpected.length,'Each aborted conditional 304 requires successful cache readback');
    assert.deepEqual(result.errors,[]);assert.deepEqual(result.warnings,[]);
    result.passed=true;
  } catch(error) {
    result.passed=false;result.failure=String(error).slice(0,2000);result.stack=error.stack?.slice(0,4000);process.exitCode=1;
  } finally {
    fs.writeFileSync(path.join(reports,'demo-browser.json'),JSON.stringify(result,null,2));
    console.log(JSON.stringify(result,null,2));
    if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));
  }
})();
