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
  let xSum = 0, ySum = 0, count = 0;
  for (let y = 0; y < image.height; y++) for (let x = 0; x < image.width; x++) {
    const i = (y * image.width + x) * 4;
    if (Math.abs(image.data[i] - 234) < 4 && Math.abs(image.data[i + 1] - 223) < 4 && Math.abs(image.data[i + 2] - 197) < 4) {
      xSum += x; ySum += y; count++;
    }
  }
  return count ? { x: xSum / count, y: ySum / count } : null;
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
    const page = await browser.newPage({ viewport: { width: 1100, height: 800 }, deviceScaleFactor: 1 });
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
      result.initial = await capture('initial');
      if (result.initial) break;
      await page.waitForTimeout(250);
    }
    assert(result.initial, 'Player pixels did not render');
    await page.keyboard.down('ArrowRight');
    await page.waitForTimeout(180);
    await page.keyboard.up('ArrowRight');
    await page.waitForTimeout(70);
    result.moved = await capture('moved');
    assert(result.moved && result.moved.x > result.initial.x + 12, 'Right input did not move the player');
    await page.keyboard.down('Space');
    await page.waitForTimeout(90);
    await page.keyboard.up('Space');
    result.jumped = await capture('jumped');
    assert(result.jumped && result.jumped.y < result.moved.y - 12, 'Space did not jump');
    await page.keyboard.down('ArrowRight');
    await page.keyboard.press('x');
    await page.waitForTimeout(100);
    await page.keyboard.up('ArrowRight');
    result.dashed = await capture('dashed');
    assert(result.dashed && result.dashed.x > result.jumped.x + 30, 'X did not dash');
    await page.keyboard.down('ArrowRight');
    await page.keyboard.down('ArrowUp');
    await page.keyboard.down('c');
    const wallAt = (buffer, x) => {
      const image = PNG.sync.read(buffer);
      const scale = image.width / 320;
      const i = (Math.floor((x < 160 ? 96 : 80) * scale) * image.width + Math.floor(x * scale)) * 4;
      return Array.from(image.data.subarray(i, i + 3));
    };
    const isWall = (frame, x) => wallAt(frame, x).every((value, i) => Math.abs(value - [169, 184, 197][i]) <= 2);
    let crossed = false;
    for (let i = 0; i < 65; i++) {
      await page.waitForTimeout(180);
      const frame = await canvas.screenshot();
      const player = character(frame);
      result.edgeApproach = { player, leftWall: wallAt(frame, 82), rightWall: wallAt(frame, 242) };
      if (i === 64) fs.writeFileSync(path.join(reports, 'edge-forward-final.png'), frame);
      if (player && isWall(frame, 82)) {
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
      if (player && player.x > 820 && isWall(frame, 242)) {
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
    await page.setViewportSize({ width: 800, height: 600 });
    await page.waitForTimeout(200);
    const bounds = await canvas.boundingBox();
    assert.equal(bounds.width, 640);
    assert.equal(bounds.height, 360);
    result.integerResize = true;
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(200);
    const narrow = await canvas.boundingBox();
    assert.equal(narrow.width, 320);
    assert.equal(narrow.height, 180);
    await page.screenshot({ path: path.join(reports, 'narrow-browser.png') });
    result.narrowLayout = true;
    assert.deepEqual(result.errors, [], 'Browser errors');
    assert.deepEqual(result.warnings, [], 'Browser warnings');
    assert.deepEqual(result.failedRequests, [], 'Failed network requests');
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
    process.exitCode = 1;
  } finally {
    fs.writeFileSync(path.join(reports, 'browser.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result, null, 2));
    if (browser) await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
})();
