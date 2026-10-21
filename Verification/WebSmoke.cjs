const { chromium } = require('playwright');
const { PNG } = require('pngjs');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');

const root = path.resolve(process.argv[2] || 'Builds/Web');
const reports = path.resolve(__dirname, '../TestResults/Web');
fs.mkdirSync(reports, { recursive: true });
assert(fs.existsSync(path.join(root, 'index.html')), 'Web output is missing');
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

function dashCharge(buffer, player) {
  const image=PNG.sync.read(buffer), scale=image.width/320;
  const count={ready:0,spent:0};
  for(let y=Math.max(0,Math.floor(player.y-16*scale));y<Math.min(image.height,player.y+4*scale);y++)
    for(let x=Math.max(0,Math.floor(player.x-10*scale));x<Math.min(image.width,player.x+10*scale);x++) {
      const i=(y*image.width+x)*4;
      for(const [key,rgb] of [['ready',[255,178,77]],['spent',[185,101,50]]])
        if(rgb.every((v,k)=>Math.abs(image.data[i+k]-v)<=2))count[key]++;
    }
  return {ready:count.ready/(scale*scale),spent:count.spent/(scale*scale)};
}

(async () => {
  let browser;
  const result = { errors: [], warnings: [], failedRequests: [] };
  try {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const url = `http://127.0.0.1:${server.address().port}/`;
    assert.equal((await fetch(url + '..%2Fpackage.json')).status, 404);
    browser = await chromium.launch({ channel: 'chromium', headless: true });
    result.browser = browser.version();
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 });
    page.on('console', message => {
      if (message.type() === 'error') result.errors.push(message.text());
      if (message.type() === 'warning') result.warnings.push(message.text());
    });
    page.on('pageerror', error => result.errors.push(String(error)));
    page.on('requestfailed', request => result.failedRequests.push(request.url()));
    await page.goto(url);
    const canvas = page.locator('#unity-canvas');
    await page.locator('#loading').waitFor({ state: 'hidden', timeout: 120000 });
    assert.equal(await page.title(), 'Verto');
    assert.equal(await page.locator('body').innerText(), '');
    await canvas.click();
    const capture = async name => character(await canvas.screenshot({ path: path.join(reports, `${name}.png`) }));
    for (let i = 0; i < 40; i++) {
      const frame=await canvas.screenshot({path:path.join(reports,`startup-${i}.png`)});
      result.initial=character(frame);result.startupFrames=i+1;
      if (result.initial) { fs.writeFileSync(path.join(reports,'initial.png'),frame);break; }
      await page.waitForTimeout(250);
    }
    assert(result.initial, 'Player pixels did not render');
    const neutralFrame=PNG.sync.read(await canvas.screenshot());
    result.neutralSolids={};
    for(const [name,x,y,rgb] of [['floor',24,160,[232,232,232]],['wall',242,80,[232,232,232]],['mover',164,144,[240,240,240]],['moverTexture',162,146,[120,120,120]],['route',201,147,[66,75,87]]]) {
      const scale=neutralFrame.width/320,i=(Math.floor((y+.5)*scale)*neutralFrame.width+Math.floor((x+.5)*scale))*4;
      const actual=Array.from(neutralFrame.data.subarray(i,i+3));result.neutralSolids[name]=actual;
      assert(actual.every((v,k)=>Math.abs(v-rgb[k])<=2),`${name} must use the neutral white/gray palette`);
    }
    result.chargeReady=dashCharge(await canvas.screenshot(),result.initial);
    assert(result.chargeReady.ready>24,'Available dash must color the whole hood');
    await page.screenshot({ path: path.join(reports, 'desktop-browser.png') });
    await page.keyboard.down('ArrowRight');
    await page.waitForTimeout(180);
    await page.keyboard.up('ArrowRight');
    await page.waitForTimeout(70);
    result.moved = await capture('moved');
    assert(result.moved && result.moved.x > result.initial.x + 12, 'Right input did not move the player');
    await page.keyboard.down('Space');
    await page.waitForTimeout(90);
    result.jumped = await capture('jumped');
    assert(result.jumped && result.jumped.y < result.moved.y - 12, 'Space did not jump');
    await page.keyboard.down('ArrowRight');
    await page.keyboard.press('x');
    for(let i=0;i<20;i++) {
      const buffer=await canvas.screenshot(), player=character(buffer);
      if(player) {
        result.dashed=player;result.chargeSpent=dashCharge(buffer,player);
        if(player.x>result.jumped.x+30 && result.chargeSpent.spent>24 && result.chargeSpent.ready===0) {
          fs.writeFileSync(path.join(reports,'dashed.png'),buffer);break;
        }
      }
      await page.waitForTimeout(10);
    }
    await page.keyboard.up('ArrowRight');await page.keyboard.up('Space');
    assert(result.dashed && result.dashed.x > result.jumped.x + 30, 'X did not dash');
    assert(result.chargeSpent.spent>24 && result.chargeSpent.ready===0,'Used air dash must show a dark orange hood');
    await page.waitForTimeout(650);
    const landed=await capture('dash-refilled');
    result.chargeRefilled=dashCharge(await canvas.screenshot(),landed);
    assert(result.chargeRefilled.ready>24 && result.chargeRefilled.spent===0,'Landing must restore the amber hood');
    result.dashChargeReadable=true;
    await page.keyboard.down('ArrowRight');
    await page.keyboard.down('ArrowUp');
    await page.keyboard.down('c');
    const wallAt = (buffer, x, viewWidth = 320) => {
      const image = PNG.sync.read(buffer);
      const scale = image.width / viewWidth;
      const viewHeight = Math.max(180,Math.ceil(320*image.height/image.width));
      const originY = Math.trunc((180-viewHeight)/2);
      const i = (Math.floor(((x < 160 ? 96 : 80)-originY+.5)*image.height/viewHeight) * image.width + Math.floor((x + .5) * scale)) * 4;
      return Array.from(image.data.subarray(i, i + 3));
    };
    const isWall = (frame, x, viewWidth) => wallAt(frame, x, viewWidth).every((value, i) => Math.abs(value - [232, 232, 232][i]) <= 2);
    let crossed = false;
    for (let i = 0; i < 65; i++) {
      await page.waitForTimeout(180);
      const frame = await canvas.screenshot();
      const player = character(frame);
      result.edgeApproach = { player, leftWall: wallAt(frame, 82), rightWall: wallAt(frame, 242) };
      if (i === 64) fs.writeFileSync(path.join(reports, 'edge-forward-final.png'), frame);
      if (player && player.x < PNG.sync.read(frame).width * .2 && isWall(frame, 82)) {
        result.nextRoom = player;
        fs.writeFileSync(path.join(reports, 'next-room.png'), frame);
        crossed = true;
        break;
      }
    }
    await page.keyboard.up('ArrowRight');
    await page.keyboard.up('ArrowUp');
    await page.keyboard.up('c');
    assert(crossed, 'Right screen edge did not load the second room');
    await page.waitForTimeout(150);
    assert(isWall(await canvas.screenshot(), 82), 'Arrival bounced back');
    await page.keyboard.down('ArrowLeft');
    crossed = false;
    for (let i = 0; i < 30; i++) {
      await page.waitForTimeout(100);
      const frame = await canvas.screenshot();
      const player = character(frame);
      if (player && player.x > PNG.sync.read(frame).width * .85 && isWall(frame, 242)) {
        result.returnedRoom = player;
        fs.writeFileSync(path.join(reports, 'returned-room.png'), frame);
        crossed = true;
        break;
      }
    }
    await page.keyboard.up('ArrowLeft');
    assert(crossed, 'Left screen edge did not return to the first room');
    result.edgeTransitions = true;
    await page.screenshot({ path: path.join(reports, 'browser.png') });
    for (const [width,height,name] of [[320,407,'minimum-round-trip-browser'],[800,600,'square-browser'],[2560,1080,'ultrawide-browser'],[844,390,'landscape-browser'],[390,844,'narrow-browser']]) {
      await page.setViewportSize({width,height}); await page.waitForTimeout(300);
      const bounds=await canvas.boundingBox();
      assert.deepEqual([bounds.x,bounds.y,bounds.width,bounds.height],[0,0,width,height], 'Canvas must fill the viewport');
      assert.deepEqual(await canvas.evaluate(c=>[c.width,c.height]),[Math.max(320,Math.round(180*width/height)),Math.max(180,Math.ceil(320*height/width))]);
      const frame=await canvas.screenshot(); const image=PNG.sync.read(frame);
      for (const [x,y] of [[8,8],[width-9,height-9]]) {
        const i=(y*image.width+x)*4;
        assert.notDeepEqual(Array.from(image.data.subarray(i,i+3)),[16,26,43],'Game pixels must replace page margins');
      }
      await page.screenshot({path:path.join(reports,name+'.png')});
    }
    await page.keyboard.press('ArrowLeft',{delay:800}); await page.waitForTimeout(100);
    const portrait=await capture('portrait-minimum-view');
    assert(portrait,'Portrait must keep the player visible');
    assert(portrait.width<24 && portrait.height<24,'Portrait must not magnify the player by cropping the room');
    assert(portrait.y>844*.5 && portrait.y<844*.65,'Tall view must center the playable area');
    const centered=PNG.sync.read(await canvas.screenshot());
    const top=(20*centered.width+20)*4, bottom=((centered.height-20)*centered.width+20)*4;
    assert.notDeepEqual(Array.from(centered.data.subarray(top,top+3)),Array.from(centered.data.subarray(bottom,bottom+3)),'The bottom must extend stone instead of copying the sky');
    assert(isWall(await canvas.screenshot(),242),'Portrait must show the far wall of the original room');
    result.centeredPlayArea=true; result.minimumView=true; result.adaptiveViewport=true; result.narrowLayout=true;
    await page.setViewportSize({width:2560,height:1080}); await page.waitForTimeout(300);
    await page.keyboard.press('ArrowRight',{delay:1600}); await page.waitForTimeout(100);
    const extended=await capture('wide-extended-floor');
    assert(extended && extended.x>2560*320/427 && extended.y>1080*.75,'Wide floor must be playable beyond the original room');
    await page.keyboard.down('ArrowRight');
    crossed=false;
    for(let i=0;i<30;i++) {
      await page.waitForTimeout(100); const frame=await canvas.screenshot(); const player=character(frame);
      if(player && player.x<2560*.2 && isWall(frame,82,427)){crossed=true;break;}
    }
    await page.keyboard.up('ArrowRight'); assert(crossed,'Wide right edge did not load the next room');
    await page.keyboard.down('ArrowLeft'); crossed=false;
    for(let i=0;i<30;i++) {
      await page.waitForTimeout(100); const frame=await canvas.screenshot(); const player=character(frame);
      if(player && player.x>2560*.85 && isWall(frame,242,427)){crossed=true;break;}
    }
    await page.keyboard.up('ArrowLeft'); assert(crossed,'Wide return must arrive at the extended edge');
    result.wideRoomTravel=true;
    assert.deepEqual(result.errors, [], 'Browser errors');
    assert.deepEqual(result.warnings, [], 'Browser warnings');
    assert.deepEqual(result.failedRequests, [], 'Failed network requests');
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.waitForTimeout(250);
    await page.keyboard.press('ArrowLeft', { delay: 1000 });
    await page.keyboard.down('ArrowLeft'); await page.keyboard.down('c');
    await page.keyboard.press('Space', { delay: 200 });
    await page.keyboard.up('ArrowLeft'); await page.waitForTimeout(350);
    const fatigue = buffer => {
      const image=PNG.sync.read(buffer), player=character(buffer), scale=image.width/320;
      assert(player,'Fatigue must remain visible on the character');
      const counts=Array(9).fill(0);
      for(let y=Math.max(80,Math.floor(player.y-8*scale));y<Math.min(image.height,player.y+8*scale);y++)
        for(let x=Math.max(0,Math.floor(player.x-8*scale));x<Math.min(image.width,player.x+8*scale);x++) {
          const i=(y*image.width+x)*4;
          if(image.data[i]<232)continue;
          const level=cloakColors.findIndex(rgb=>rgb.every((v,k)=>Math.abs(image.data[i+k]-v)<=2));
          if(level>=0)counts[level]++;
        }
      const level=counts.indexOf(Math.max(...counts));
      return {player,level,color:cloakColors[level]};
    };
    result.staminaHealthy=fatigue(await canvas.screenshot());assert.equal(result.staminaHealthy.level,0,'Healthy grab keeps the original cloak');
    fs.writeFileSync(path.join(reports,'stamina-healthy.png'),await canvas.screenshot());
    const deadline=Date.now()+12000;
    while(Date.now()<deadline) {
      await page.waitForTimeout(200);const frame=await canvas.screenshot(),value=fatigue(frame);
      if(!result.staminaEarly&&value.level>=1&&value.level<=4) {result.staminaEarly=value;fs.writeFileSync(path.join(reports,'stamina-early.png'),frame);}
      if(value.level>=7) {result.staminaCritical=value;fs.writeFileSync(path.join(reports,'stamina-critical.png'),frame);break;}
    }
    assert(result.staminaEarly&&result.staminaCritical,'Cloak must gradually turn red while grabbing');
    await page.setViewportSize({width:390,height:844});await page.waitForTimeout(150);
    assert(fatigue(await canvas.screenshot()).level>=5,'Portrait retains the character warning');
    await page.screenshot({path:path.join(reports,'stamina-narrow.png')});
    await page.setViewportSize({width:1280,height:720});
    const slipDeadline=Date.now()+4000;
    while(Date.now()<slipDeadline) {
      const frame=await canvas.screenshot(),value=fatigue(frame);
      if(value.level>=6&&value.player.y>result.staminaHealthy.player.y+12) {result.exhaustedSlideStart=value;break;}
      await page.waitForTimeout(80);
    }
    assert(result.exhaustedSlideStart,'Exhaustion must release the grip and slide down');
    const slideAt=Date.now();await page.waitForTimeout(200);
    result.exhaustedSlideEnd=fatigue(await canvas.screenshot());
    result.exhaustedSlideSeconds=(Date.now()-slideAt)/1000;
    const nativeDrop=(result.exhaustedSlideEnd.player.y-result.exhaustedSlideStart.player.y)/4;
    assert(nativeDrop>=2&&nativeDrop<=45*result.exhaustedSlideSeconds+2,'Held exhausted grab must slide slowly');
    await page.keyboard.up('c');await page.waitForTimeout(900);
    assert.equal(fatigue(await canvas.screenshot()).level,0,'Landing restores the cloak');
    result.staminaWarning=true;result.slowExhaustedSlide=true;
    await page.setViewportSize({ width: 1280, height: 720 });
    assert.deepEqual(result.errors, []); assert.deepEqual(result.warnings, []); assert.deepEqual(result.failedRequests, []);
    await page.locator('#fullscreen-toggle').click();
    await page.waitForFunction(() => document.fullscreenElement === document.documentElement);
    await page.waitForTimeout(250);
    assert.equal(await page.locator('#fullscreen-toggle').getAttribute('aria-label'), '退出全螢幕');
    const native=await canvas.evaluate(c=>[c.width,c.height]);
    const size=await page.evaluate(()=>[innerWidth,innerHeight]);
    assert.deepEqual(native,[Math.max(320,Math.round(180*size[0]/size[1])),Math.max(180,Math.ceil(320*size[1]/size[0]))],'Fullscreen keeps adaptive native pixels');
    const full = await canvas.boundingBox();
    const viewport = await page.evaluate(() => [innerWidth, innerHeight]);
    assert.deepEqual([full.x,full.y,full.width,full.height],[0,0,...viewport],'Fullscreen must fill both axes');
    await page.screenshot({ path: path.join(reports, 'fullscreen.png') });
    await page.locator('#fullscreen-toggle').click();
    await page.waitForFunction(() => !document.fullscreenElement && document.getElementById('fullscreen-toggle').getAttribute('aria-label') === '進入全螢幕');
    assert.equal(await page.locator('#fullscreen-toggle').getAttribute('aria-label'), '進入全螢幕');
    await page.evaluate(() => { document.documentElement.requestFullscreen = () => Promise.reject(new Error('denied')); });
    await page.locator('#fullscreen-toggle').click();
    await page.locator('#display-error').waitFor({ state: 'visible' });
    assert.match(await page.locator('#display-error').innerText(), /獨立分頁/);
    await page.screenshot({ path: path.join(reports, 'fullscreen-denied.png') });
    result.fullscreen = true;
    const failedPage = await browser.newPage();
    await failedPage.route('**/Build/*.data.unityweb', route => route.abort());
    await failedPage.goto(url);
    await failedPage.locator('#error').waitFor({ state: 'visible', timeout: 30000 });
    assert.equal(await failedPage.locator('#loading').isVisible(), false);
    await failedPage.screenshot({ path: path.join(reports, 'load-failure.png') });
    result.loadFailureVisible = true;
    result.passed = true;
  } catch (error) {
    result.passed = false;
    result.failure = String(error);
    result.stack = error.stack;
    process.exitCode = 1;
  } finally {
    fs.writeFileSync(path.join(reports, 'browser.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result, null, 2));
    if (browser) await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
})();
