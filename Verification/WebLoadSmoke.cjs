// Loads a Web build (a folder or an http(s) URL), starts level1 from the title and walks right, with a clean console.
const { chromium } = require('playwright');
const { PNG } = require('pngjs');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');

const remote = /^https?:/.test(process.argv[2] || '') ? process.argv[2] : null;
const root = remote ? null : path.resolve(process.argv[2] || 'Builds/Web');
const reports = path.resolve(__dirname, '../TestResults/Web');
fs.mkdirSync(reports, { recursive: true });
const server = remote ? null : http.createServer((request, response) => {
  let file, stat;
  try {
    file = path.resolve(root, '.' + decodeURIComponent(new URL(request.url, 'http://localhost').pathname));
    if (file === root) file = path.join(root, 'index.html');
    if (!file.startsWith(root + path.sep) || !(stat = fs.statSync(file)).isFile()) throw new Error('not a served file');
  } catch { response.writeHead(404).end(); return; }
  const mime = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm' };
  response.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  response.setHeader('Content-Length', stat.size);
  fs.createReadStream(file).on('error', () => response.destroy()).pipe(response);
});

// Pixels in the player's body colors (palette c, d, b and B in PhasePixels.Sprite); nothing else in the game uses them.
const body = ['3b2a78', '261a52', '4a3a92', '2c2160'].map(hex => [0, 2, 4].map(i => parseInt(hex.slice(i, i + 2), 16)));
function playerPixels(buffer) {
  const image = PNG.sync.read(buffer), pixels = new Map();
  for (let i = 0; i < image.data.length; i += 4)
    if (body.some(rgb => rgb.every((v, k) => Math.abs(image.data[i + k] - v) < 4))) pixels.set(i / 4, (i / 4) % image.width);
  return pixels;
}
const centroidX = pixels => [...pixels.values()].reduce((a, b) => a + b) / pixels.size;

(async () => {
  let browser;
  const result = { errors: [], warnings: [], failedRequests: [] };
  try {
    if (!remote) assert(fs.existsSync(path.join(root, 'index.html')), 'Web output is missing');
    if (!remote) await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    browser = await chromium.launch({ channel: 'chromium', headless: true });
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 });
    page.on('console', message => {
      if (message.type() === 'error') result.errors.push(message.text());
      if (message.type() === 'warning') result.warnings.push(message.text());
    });
    page.on('pageerror', error => result.errors.push(String(error)));
    page.on('requestfailed', request => result.failedRequests.push(`${request.url()} ${request.failure()?.errorText}`));
    await page.goto(remote || `http://127.0.0.1:${server.address().port}/`);
    await page.locator('#loading').waitFor({ state: 'hidden', timeout: 120000 });
    // The template hides #loading on failure too and shows the reason in #error.
    assert.equal(await page.locator('#error').isVisible(), false, `The Web build failed to load: ${await page.locator('#error').textContent()}`);
    const canvas = page.locator('#unity-canvas');
    await canvas.focus();
    // A dropped key leaves the title up, so wait for the level-load result. Enter confirms whatever jump is bound to.
    await page.evaluate(() => { window.__levelStarted = false; addEventListener('phase-map-result', () => window.__levelStarted = true, { once: true }); });
    // A press right after a screen opens is ignored until keys are released; retry only after a full wait.
    for (let attempt = 0; attempt < 3 && !await page.evaluate(() => window.__levelStarted); attempt++) {
      await page.keyboard.press('Enter', { delay: 80 });
      for (let n = 0; n < 30 && !await page.evaluate(() => window.__levelStarted); n++) await page.waitForTimeout(100);
    }
    assert(await page.evaluate(() => window.__levelStarted), 'The title starts level1');
    await page.waitForTimeout(1000);
    const start = playerPixels(await canvas.screenshot());
    assert(start.size, 'Level1 shows the player');
    // A short walk stays on the spawn ledge.
    await page.keyboard.down('ArrowRight'); await page.waitForTimeout(250); await page.keyboard.up('ArrowRight');
    await page.waitForTimeout(300);
    const shot = await canvas.screenshot(), walked = playerPixels(shot);
    fs.writeFileSync(path.join(reports, 'load-smoke.png'), shot);
    assert(walked.size, 'Level1 still shows the player after the walk');
    result.moved = centroidX(walked) - centroidX(start);
    assert(result.moved > 8, `Holding right moves the player (moved ${result.moved}px)`);
    assert.deepEqual(result.errors, [], 'Console errors');
    assert.deepEqual(result.failedRequests, [], 'Failed requests');
    assert.deepEqual(result.warnings, [], 'Console warnings');
    result.passed = true;
  } catch (error) {
    result.passed = false;
    result.failure = String(error.stack || error);
    process.exitCode = 1;
  } finally {
    fs.writeFileSync(path.join(reports, 'load-smoke.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result));
    await browser?.close();
    if (!remote) await new Promise(resolve => server.close(resolve));
  }
})();
