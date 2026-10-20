const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const target = process.argv[2] || pathToFileURL(path.resolve(__dirname, '../Documentation/wiki/index.html')).href;
  const output = path.resolve(__dirname, '../TestResults/Wiki');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  try {
    for (const [name, width, height] of [['desktop', 1440, 1000], ['mobile', 390, 844]]) {
      const page = await browser.newPage({ viewport: { width, height }, reducedMotion: 'reduce' });
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
      page.on('requestfailed', request => errors.push(`${request.url()}: ${request.failure().errorText}`));
      await page.goto(target, { waitUntil: 'networkidle' });
      await page.keyboard.press('Tab');
      assert.equal(await page.locator(':focus').getAttribute('href'), '#main', `${name}: missing keyboard skip link`);
      await page.keyboard.press('Enter');
      assert.equal(await page.locator(':focus').getAttribute('id'), 'main', `${name}: skip link does not focus the document`);
      if (width < 1024) assert.ok(await page.evaluate(() => document.querySelector('h1').getBoundingClientRect().top >= document.querySelector('.masthead').getBoundingClientRect().bottom), `${name}: skip link hides the title`);
      assert.equal(await page.locator('article.room').count(), 11, `${name}: missing room guide`);
      assert.equal(await page.locator('article.room details:not([open])').count(), 11, `${name}: room solutions must start collapsed`);
      const initialOpen = await page.locator('details').evaluateAll(details => details.map(detail => detail.open));
      await page.locator('details').evaluateAll(details => details.forEach(detail => { detail.open = true; }));
      await page.locator('img').evaluateAll(images => images.forEach(image => { image.loading = 'eager'; }));
      await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0));
      await page.locator('img').evaluateAll(images => Promise.all(images.map(image => image.decode())));
      await page.locator('details').evaluateAll((details, values) => details.forEach((detail, i) => { detail.open = values[i]; }), initialOpen);
      assert.match(await page.locator('html').getAttribute('lang'), /^zh(-Hant|-TW)/);
      assert.equal(await page.locator('h1').count(), 1);
      const checks = await page.evaluate(() => ({
        brokenAnchors: [...document.querySelectorAll('a[href^="#"]')].map(a => a.getAttribute('href')).filter(href => href.length > 1 && !document.getElementById(decodeURIComponent(href.slice(1)))),
        brokenImages: [...document.images].filter(i => !i.complete || !i.naturalWidth || !i.alt.trim()).map(i => i.getAttribute('src')),
        overflowing: document.documentElement.scrollWidth > innerWidth + 1,
        text: document.body.innerText,
        images: document.images.length,
      }));
      assert.deepEqual(checks.brokenAnchors, [], `${name}: broken anchors`);
      assert.deepEqual(checks.brokenImages, [], `${name}: broken images or missing alt text`);
      assert.equal(checks.overflowing, false, `${name}: document overflows`);
      assert.ok(checks.images >= 12, `${name}: missing scene images`);
      for (const word of ['Wick', '攀爬', '衝刺', '雷射', '尖刺', 'PhaseDemo1.1', 'DemoRooms.json', 'phaseMask', 'phase.editor.v1', 'Paint Grid terrain', '匯出 JSON', '七色水晶', 'Prefab', 'PhaseCrystal']) assert.ok(checks.text.includes(word), `${name}: missing ${word}`);
      await page.evaluate(() => scrollTo({ top: 0, behavior: 'instant' }));
      await page.waitForFunction(() => !document.querySelector('.toc a[aria-current]'));
      await page.screenshot({ path: path.join(output, `${name}-top.png`) });
      await page.screenshot({ path: path.join(output, `${name}-full.png`), fullPage: true });
      const disclosures = page.locator('details');
      for (let i = 0; i < await disclosures.count(); i++) {
        const detail = disclosures.nth(i);
        if (await detail.getAttribute('open') === null) {
          await detail.locator('summary').click();
          assert.notEqual(await detail.getAttribute('open'), null, `${name}: disclosure does not open`);
          await detail.locator('summary').click();
        }
      }
      const headings = page.locator('main h2');
      for (let i = 0; i < await headings.count(); i++) {
        const section = await headings.nth(i).evaluate(heading => {
          heading.scrollIntoView({ block: 'start', behavior: 'instant' });
          return heading.closest('section').id;
        });
        await page.waitForFunction(id => document.querySelector('.toc a[aria-current]')?.hash === `#${id}`, section);
        await page.screenshot({ path: path.join(output, `${name}-section-${i + 1}.png`) });
      }
      assert.deepEqual(errors, [], `${name}: browser errors`);
      await page.close();
      console.log(`${name}: images, links, content, disclosures and layout passed`);
    }
    const page = await browser.newPage({ javaScriptEnabled: false, viewport: { width: 390, height: 844 } });
    await page.goto(target);
    assert.ok((await page.locator('main').innerText()).includes('DemoRooms.json'), 'Core documentation requires JavaScript');
    await page.close();
    console.log('No-JavaScript reading passed');
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
