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

function character(buffer) {
  const image = PNG.sync.read(buffer);
  let xSum = 0, ySum = 0, count = 0, minX = image.width, maxX = 0, minY = image.height, maxY = 0;
  for (let y = 0; y < image.height; y++) for (let x = 0; x < image.width; x++) {
    // Toolbar icons share the cloak color in full-viewport captures.
    if (y < 80 && x > image.width - 140) continue;
    const i = (y * image.width + x) * 4;
    if (Math.abs(image.data[i] - 234) < 4 && Math.abs(image.data[i + 1] - 223) < 4 && Math.abs(image.data[i + 2] - 197) < 4) {
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
    assert.equal(await page.title(), 'PHASE');
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
    const isWall = (frame, x, viewWidth) => wallAt(frame, x, viewWidth).every((value, i) => Math.abs(value - [169, 184, 197][i]) <= 2);
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
    await page.reload();
    await page.locator('#controls-toggle').waitFor({ state: 'visible', timeout: 120000 });
    await canvas.click();
    for (let i = 0; i < 40 && !(await capture('controls-initial')); i++) await page.waitForTimeout(250);
    await page.keyboard.press('Tab');
    assert.equal(await page.locator('#fullscreen-toggle').evaluate(button => button === document.activeElement), true);
    await page.keyboard.press('Tab');
    assert.equal(await page.locator('#controls-toggle').evaluate(button => button === document.activeElement), true);
    await page.keyboard.press('Enter');
    const dialog = page.locator('#controls');
    const binding = (i, slot = 0) => page.locator(`#bindings button[data-action="${i}"][data-slot="${slot}"]`);
    const reset = i => page.locator(`#bindings button[data-reset="${i}"]`);
    assert.equal(await page.locator('#bindings button[data-slot]').count(), 14);
    assert.equal(await binding(4).innerText(), 'Z');
    assert.equal(await binding(4, 1).innerText(), 'Space');
    assert.equal(await binding(5, 1).innerText(), '未設定');
    assert.equal(await binding(6, 1).innerText(), 'Shift');
    const paused = await canvas.screenshot();
    await page.keyboard.press('ArrowRight', { delay: 180 });
    assert.deepEqual(await canvas.screenshot(), paused, 'Settings did not pause the game');
    await binding(1).click(); await page.keyboard.press('l');
    await binding(4).click(); await page.keyboard.press('l');
    assert.match(await page.locator('#controls-status').innerText(), /此鍵已設定/);
    await page.keyboard.press('Escape');
    assert(await dialog.isVisible(), 'Escape clears a slot without closing settings');
    assert.equal(await binding(4).innerText(), '未設定');
    assert.equal(await binding(4, 1).innerText(), 'Space', 'Clearing first slot must preserve second');
    await binding(4).click(); await page.keyboard.press('j');
    await binding(5).click(); await page.keyboard.press('k');
    await binding(6).click(); await page.keyboard.press('h');
    await binding(6, 1).click(); await page.keyboard.press('ShiftRight');
    assert.equal(await binding(6, 1).innerText(), 'Shift');
    await binding(5, 1).click(); await page.keyboard.press('ShiftLeft');
    assert.match(await page.locator('#controls-status').innerText(), /此鍵已設定/);
    await page.keyboard.press('Escape');
    await binding(5, 1).click(); await page.keyboard.press('v');
    assert.equal(await binding(5).innerText(), 'K');
    await binding(5, 1).click(); await page.keyboard.press('Escape');
    assert.equal(await binding(5, 1).innerText(), '未設定');
    await binding(5).click(); await page.keyboard.press('Escape');
    assert.match(await page.locator('#controls-status').innerText(), /至少保留一個鍵/);
    assert(await dialog.isVisible(), 'Blocked clear cannot dismiss settings');
    assert.equal(await binding(5).getAttribute('aria-pressed'), 'true');
    await page.keyboard.press('Tab');
    assert.equal(await binding(5).innerText(), 'K', 'Cancel preserves the last key');
    await binding(1, 1).click(); await page.keyboard.press('Tab');
    assert.equal(await binding(1, 1).getAttribute('aria-pressed'), 'false');
    assert.equal(await binding(1, 1).innerText(), 'D');
    await page.screenshot({ path: path.join(reports, 'controls-desktop.png') });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(250);
    await page.screenshot({ path: path.join(reports, 'controls-narrow.png') });
    const panel = await dialog.boundingBox();
    assert(panel.x >= 0 && panel.x + panel.width <= 390, 'Settings overflow narrow viewport');
    await page.setViewportSize({ width: 320, height: 568 });
    const small = await dialog.boundingBox();
    assert(small.x >= 0 && small.x + small.width <= 320, 'Settings overflow 320px viewport');
    for (const button of await page.locator('#bindings button').all()) {
      const box = await button.boundingBox(); assert(box.height >= 44, 'Binding touch target too short');
    }
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.locator('#controls-close').click();
    await page.waitForTimeout(150);
    const start = await capture('controls-start');
    await page.keyboard.press('ArrowRight', { delay: 150 });
    const oldKey = await capture('controls-old-key');
    assert(Math.abs(oldKey.x - start.x) < 4, 'Replaced movement alias still works');
    await page.keyboard.press('d', { delay: 150 });
    assert((await capture('controls-secondary-moved')).x > start.x + 12, 'Retained secondary movement key failed');
    await page.locator('#controls-toggle').click();
    await binding(1, 1).click(); await page.keyboard.press('p');
    assert.equal(await binding(1).innerText(), 'L');
    await page.locator('#controls-close').click(); await page.waitForTimeout(150);
    const secondStart = await capture('controls-secondary-before');
    await page.keyboard.press('d', { delay: 150 });
    assert(Math.abs((await capture('controls-secondary-old')).x - secondStart.x) < 4, 'Replaced secondary still works');
    await page.keyboard.press('p', { delay: 150 });
    assert((await capture('controls-secondary-new')).x > secondStart.x + 12, 'New secondary movement key failed');
    await page.keyboard.press('l', { delay: 180 });
    await page.waitForTimeout(70);
    const customMove = await capture('controls-moved');
    assert(customMove.x > start.x + 12, 'Custom right key did not move player');
    await page.keyboard.press('j', { delay: 90 });
    const customJump = await capture('controls-jumped');
    assert(customJump.y < customMove.y - 12, 'Custom jump key did not jump');
    await page.keyboard.down('l'); await page.keyboard.press('k');
    await page.waitForTimeout(100); await page.keyboard.up('l');
    const customDash = await capture('controls-dashed');
    assert(customDash.x > customJump.x + 30, 'Custom dash key did not dash');
    await page.reload();
    await page.locator('#controls-toggle').waitFor({ state: 'visible', timeout: 120000 });
    await page.locator('#controls-toggle').click();
    assert.equal(await binding(1).innerText(), 'L');
    assert.equal(await binding(1, 1).innerText(), 'P');
    assert.equal(await binding(4, 1).innerText(), 'Space');
    assert.equal(await binding(6, 1).innerText(), 'Shift');
    assert.equal(await binding(4).innerText(), 'J');
    assert.equal(await binding(5).innerText(), 'K');
    assert.equal(await binding(5, 1).innerText(), '未設定', 'Esc clear must persist');
    await page.locator('#controls-close').click();
    await page.waitForTimeout(200);
    for (let i = 0; i < 40 && !(await capture('controls-reloaded')); i++) await page.waitForTimeout(250);
    const reloaded = await capture('controls-reloaded');
    await page.keyboard.press('l', { delay: 180 });
    assert((await capture('controls-reload-moved')).x > reloaded.x + 12, 'Reload did not apply saved bindings to Unity');
    const beforeSpace = await capture('controls-secondary-jump-before');
    await page.keyboard.press('Space', { delay: 90 });
    assert((await capture('controls-secondary-jump')).y < beforeSpace.y - 12, 'Secondary jump key did not survive reload');
    await page.locator('#controls-toggle').click();
    await binding(5).click(); await page.keyboard.press('z');
    await reset(4).click();
    assert.match(await page.locator('#controls-status').innerText(), /預設鍵已用於衝刺/);
    assert.equal(await binding(4).innerText(), 'J', 'Conflicting restore must preserve the old binding');
    assert.equal(await binding(5).innerText(), 'Z', 'Restore cannot change a different action');
    await binding(5).click(); await page.keyboard.press('k');
    await binding(4).click(); await reset(4).click();
    assert.equal(await binding(4).innerText(), 'Z');
    assert.equal(await binding(4, 1).innerText(), 'Space');
    assert(await reset(4).isDisabled(), 'Default action has no restore work');
    assert.equal(await binding(1).innerText(), 'L');
    assert.equal(await binding(5).innerText(), 'K');
    assert.equal(await binding(6).innerText(), 'H');
    assert(await reset(1).isEnabled());
    await page.reload();
    await page.locator('#controls-toggle').waitFor({ state: 'visible', timeout: 120000 });
    await canvas.click();
    let restoreReady;
    for (let i = 0; i < 40; i++) { restoreReady = await capture('controls-restore-ready'); if (restoreReady) break; await page.waitForTimeout(250); }
    assert(restoreReady, 'Player pixels missing after individual restore reload');
    await page.locator('#controls-toggle').click();
    assert.equal(await binding(4).innerText(), 'Z', 'Individual restore must persist');
    assert.equal(await binding(1).innerText(), 'L', 'Other overrides must persist');
    assert.equal(await binding(5).innerText(), 'K');
    assert.equal(await binding(6).innerText(), 'H');
    await page.keyboard.press('Escape');
    await page.waitForTimeout(200);
    const resetBefore = await capture('controls-reset-before');
    await page.keyboard.press('Space', { delay: 90 });
    assert((await capture('controls-reset-jumped')).y < resetBefore.y - 12, 'Reset did not restore default jump');
    await page.evaluate(() => localStorage.setItem('phase.controls.v2', '["L", "L"]'));
    await page.reload();
    await page.locator('#controls-toggle').waitFor({ state: 'visible', timeout: 120000 });
    await page.locator('#controls-toggle').click();
    assert.equal(await binding(1).innerText(), '→', 'Invalid saved controls must fall back to defaults');
    await page.evaluate(() => { window.savedSetItem = Storage.prototype.setItem; Storage.prototype.setItem = () => { throw new Error('storage blocked'); }; });
    await binding(4).click(); await page.keyboard.press('j');
    assert.match(await page.locator('#controls-status').innerText(), /無法儲存/);
    await page.locator('#controls-close').click();
    await page.waitForTimeout(200);
    for (let i = 0; i < 40 && !(await capture('controls-storage-before')); i++) await page.waitForTimeout(250);
    const storageBefore = await capture('controls-storage-before');
    await page.keyboard.press('j', { delay: 90 });
    assert((await capture('controls-storage-jumped')).y < storageBefore.y - 12, 'Storage failure must preserve session bindings');
    await page.evaluate(() => { Storage.prototype.setItem = window.savedSetItem; });
    await page.locator('#controls-toggle').click(); await reset(4).click(); await page.locator('#controls-close').click();
    await page.evaluate(() => {
      localStorage.removeItem('phase.controls.v2');
      localStorage.setItem('phase.controls.v1', JSON.stringify(['', 'L', '', '', 'J', 'K', 'RightShift']));
    });
    await page.reload();
    await page.locator('#controls-toggle').waitFor({ state: 'visible', timeout: 120000 });
    await page.locator('#controls-toggle').click();
    assert.equal(await binding(1).innerText(), 'L');
    assert.equal(await binding(1, 1).innerText(), '未設定');
    assert.equal(await binding(0, 1).innerText(), 'A');
    assert.equal(await binding(6).innerText(), 'Shift');
    assert.equal(await binding(6, 1).innerText(), '未設定');
    const legacy = await page.evaluate(() => localStorage.getItem('phase.controls.v1'));
    await binding(1, 1).click(); await page.keyboard.press('p');
    assert.equal(await page.evaluate(() => localStorage.getItem('phase.controls.v1')), legacy, 'Legacy settings must remain recoverable');
    assert.deepEqual(await page.evaluate(() => JSON.parse(localStorage.getItem('phase.controls.v2')).slice(2, 4)), ['L', 'P']);
    await page.locator('#controls-close').click(); await page.waitForTimeout(200);
    for (let i = 0; i < 40 && !(await capture('controls-migrated')); i++) await page.waitForTimeout(250);
    const migrated = await capture('controls-migrated');
    await page.keyboard.press('l', { delay: 180 });
    assert((await capture('controls-migrated-moved')).x > migrated.x + 12, 'Migrated binding did not reach Unity');
    result.escapeClearsSlot = true;
    result.twoKeySlots = true;
    result.legacyMigration = true;
    result.perActionReset = true;
    result.customControls = true;
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
    await page.locator('#controls-toggle').click(); assert(await dialog.isVisible());
    await page.locator('#controls-close').click();
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
