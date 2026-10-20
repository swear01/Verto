const { chromium } = require('playwright');
const { PNG } = require('pngjs');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');

const calibrationRoom=Number((process.argv.find(a=>a.startsWith('--room='))||'').slice(7))||0;
assert(Number.isInteger(calibrationRoom)&&calibrationRoom>=0&&calibrationRoom<=11,'Calibration room must be1..11');
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
    if (y < 80 && x > image.width - 192) continue;
    const i = (y * image.width + x) * 4;
    if (image.data[i]>=232 && cloakColors.some(rgb=>rgb.every((v,k)=>Math.abs(image.data[i+k]-v)<4))) {
      xSum += x; ySum += y; count++;
      minX = Math.min(minX,x); maxX = Math.max(maxX,x); minY = Math.min(minY,y); maxY = Math.max(maxY,y);
    }
  }
  if (!count) return null;
  const player = { x: xSum / count, y: ySum / count, width: maxX-minX+1, height: maxY-minY+1 };
  const eyes = [[255,143,145],[121,201,255],[120,216,146]], matches=[0,0,0];
  for(let y=Math.max(0,Math.floor(player.y-40));y<=Math.min(image.height-1,Math.ceil(player.y));y++)
    for(let x=Math.max(0,Math.floor(player.x-24));x<=Math.min(image.width-1,Math.ceil(player.x+24));x++) {
      const i=(y*image.width+x)*4;
      eyes.forEach((rgb,phase)=>{if(rgb.every((v,k)=>Math.abs(image.data[i+k]-v)<4))matches[phase]++;});
    }
  player.phase=Math.max(...matches)>1?matches.indexOf(Math.max(...matches)):null;
  return player;
}

const pixelProbe=new PNG({width:320,height:180});
for(const [x,y,rgb] of [[100,100,[234,223,197]],[100,90,[121,201,255]],[101,90,[121,201,255]],[310,20,[234,223,197]]]) {
  const i=(y*pixelProbe.width+x)*4;pixelProbe.data.set([...rgb,255],i);
}
assert.deepEqual(character(PNG.sync.write(pixelProbe)),{x:100,y:100,width:1,height:1,phase:1},'Eye phase detection excludes toolbar pixels');

(async () => {
  let browser;
  const result = { errors: [], warnings: [], failedRequests: [], rooms: [], cacheResponses: [], cacheReads: [], screenshotMs: [], checkpoints: [] };
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
    async function capture(options) {const started=performance.now();const buffer=await canvas.screenshot(options);result.screenshotMs.push(Math.round(performance.now()-started));return buffer;}
    const loaded = () => page.locator('#loading').waitFor({state:'hidden',timeout:120000});
    // The build opens on the title, which draws no player; jump starts level1.
    const begin = async () => {
      await loaded(); await canvas.click();
      fs.writeFileSync(path.join(reports,'demo-title.png'),await capture());
      return startLevel();
    };
    // Title letters can look like the player, so wait for the level-load result instead. Enter confirms whatever jump is bound to.
    const startLevel = async () => {
      await page.evaluate(()=>{window.__levelStarted=false;addEventListener('phase-map-result',()=>window.__levelStarted=true,{once:true});});
      // A press right after a screen opens is ignored until keys are released; retry only after a full wait.
      for (let attempt=0;attempt<3&&!await page.evaluate(()=>window.__levelStarted);attempt++) {
        await page.keyboard.press('Enter',{delay:80});
        for (let n=0;n<30&&!await page.evaluate(()=>window.__levelStarted);n++) await page.waitForTimeout(100);
      }
      if(!await page.evaluate(()=>window.__levelStarted))throw Error('Title did not start level1');
      for (let n=0;n<20;n++) { const player=character(await capture()); if(player) return player; await page.waitForTimeout(100); }
      throw Error('Player pixels did not render');
    };
    const observe = async () => character(await capture());
    // In-game menus read key edges per frame, so each tap is held and released for a few frames.
    async function tap(key, times=1) { for(let n=0;n<times;n++){await page.keyboard.press(key,{delay:60});await page.waitForTimeout(60);} }
    // The victory banner's first letter strokes, drawn in the light ink at the center of the frame.
    async function victoryShown(buffer) {
      const top=Math.floor(await canvas.evaluate(c=>c.height)/2)-22;
      return [[137,8],[137,9],[136,10],[135,11]].every(([x,y])=>pixelMatches(buffer,x,top+y,[234,223,197]));
    }
    async function visible(label, predicate, seconds=6) {
      const deadline=Date.now()+seconds*1000;let last=null,buffer;
      while(Date.now()<deadline) {
        buffer=await capture();last=character(buffer);
        if(last&&predicate(last)){fs.writeFileSync(path.join(reports,`demo-${label}.png`),buffer);result.checkpoints.push({label,player:last});return last;}
        await page.waitForTimeout(16);
      }
      if(buffer)fs.writeFileSync(path.join(reports,`demo-${label}-failed.png`),buffer);
      throw Error(`${label}: visible checkpoint not reached: ${JSON.stringify(last)}`);
    }
    async function walkUntil(label,predicate) {
      const deadline=Date.now()+25000;let buffer,current;
      while(Date.now()<deadline) {
        buffer=await capture();current=character(buffer);
        if(current&&predicate(current)){fs.writeFileSync(path.join(reports,`demo-${label}.png`),buffer);result.checkpoints.push({label,player:current});return current;}
        await page.keyboard.press('ArrowRight',{delay:40});await page.waitForTimeout(100);
      }
      if(buffer)fs.writeFileSync(path.join(reports,`demo-${label}-failed.png`),buffer);
      throw Error(`${label}: walking checkpoint not reached: ${JSON.stringify(current)}`);
    }
    async function stableLanding(label,phase,min,max,height) {
      let previous=null,stable=0;
      return visible(label,p=>{
        const settled=p.phase===phase&&p.x/4>=min&&p.x/4<=max&&Math.abs(p.y/4-height)<2;
        stable=settled&&previous!==null&&Math.abs(p.y-previous)<2?stable+1:0;previous=p.y;
        return stable>=2;
      });
    }
    function pixelMatches(buffer,x,y,rgb) {
      const image=PNG.sync.read(buffer),scale=image.width/320;
      const i=(Math.floor((y+.5)*scale)*image.width+Math.floor((x+.5)*scale))*4;
      return rgb.every((value,k)=>Math.abs(image.data[i+k]-value)<4);
    }
    async function inputSegment(keys,ms) {
      for(const key of keys)await page.keyboard.down(key);
      try{await page.waitForTimeout(ms);}
      finally{for(const key of keys)await page.keyboard.up(key);}
    }
    const blueLanding=(label,min,max,height)=>stableLanding(label,1,min,max,height);
    await page.goto(url); await begin();
    assert.equal(await page.title(),'PHASE');
    assert.equal(await page.locator('body').innerText(),'');
    const rooms=['DemoRooms','CrystalRooms'].flatMap(course=>JSON.parse(fs.readFileSync(path.join(__dirname,`../Assets/Phase/${course}.json`))).rooms);
    await page.evaluate(()=>{window.__nativeMapResults=[];addEventListener('phase-map-result',event=>window.__nativeMapResults.push(event.detail));});
    if(calibrationRoom>1)await selectRoom(calibrationRoom);
    for(let index=calibrationRoom?calibrationRoom-1:0;index<(calibrationRoom||rooms.length);index++) {
      const room=rooms[index];
      if(index>=8 && (index===8 || calibrationRoom)) {
        await page.clock.pauseAt(await page.evaluate(()=>Date.now()+100));
        result.crystalRouteClock='Controlled browser time; production physics at 1/60s';
      }
      const start=character(await capture({path:path.join(reports,`demo-${index+1}-start.png`)}));
      assert(start && Math.abs(start.x/4-room.spawnX-4)<1,`Room ${index+1} checkpoint spawn`);
      const artOffset=start.y/4-room.spawnY,exitY=room.goal.y-11+artOffset;
      let held=new Set();
      if(index===0) {
        // Synchronize each staircase jump to its visible phase and settled landing.
        let approach=await observe();
        for(let n=0;approach.x/4<50&&n<12;n++) {
          await page.keyboard.press('ArrowRight',{delay:70});await page.waitForTimeout(100);approach=await observe();
        }
        assert(approach.x/4>=50&&approach.x/4<62,'Room1 jump starts before the first gap');
        for(const [phase,left,top] of [[1,80,144],[2,128,128],[0,176,112]]) {
          await page.keyboard.down('ArrowRight');await page.keyboard.down('z');await page.waitForTimeout(450);
          await page.keyboard.up('z');await page.keyboard.up('ArrowRight');
          await stableLanding(`1-stair-${phase}`,phase,left+4,left+35,top-11+artOffset);
        }
      }
      if(index===4) {
        // The low ceiling requires one blue jump, a settled landing, then a horizontal dash.
        const blueY=room.goal.y-11+(start.y/4-room.spawnY);
        await walkUntil('5-before-jump',p=>p.x/4>=59&&p.x/4<66);
        await page.keyboard.down('ArrowRight');await page.keyboard.down('z');await page.waitForTimeout(400);
        await page.keyboard.up('z');await page.keyboard.up('ArrowRight');
        await capture({path:path.join(reports,'demo-5-jump-released.png')});
        await blueLanding('5-first-blue-landing',88,124,blueY);
        await walkUntil('5-before-dash',p=>p.phase===1&&p.x/4>=114&&p.x/4<130);
        await page.keyboard.down('ArrowRight');await page.keyboard.down('x');await page.waitForTimeout(200);
        await page.keyboard.up('x');await page.keyboard.up('ArrowRight');
        await blueLanding('5-second-blue-landing',168,222,blueY);
      }
      if(index===7) {
        await page.keyboard.press('z',{delay:600});
        await stableLanding('8-blue-spawn',1,room.spawnX+3,room.spawnX+6,160-11+artOffset);
        await walkUntil('8-at-gear',p=>p.phase===1&&p.x/4>=67&&p.x/4<70);
        let lifted=false;
        for(let attempt=0;attempt<6;attempt++) {
          await inputSegment(['ArrowRight','ArrowUp','c'],attempt?200:920);
          const first=await capture();
          if(pixelMatches(first,74,80,[160,160,160])) {
            const second=await capture();
            if(pixelMatches(second,74,80,[160,160,160])) {
              fs.writeFileSync(path.join(reports,'demo-8-gear-top-stop.png'),second);lifted=true;break;
            }
          }
        }
        assert(lifted,'Room8 visible rack cap reaches its24px top stop');
        await page.keyboard.press('ArrowDown',{delay:600});
        await stableLanding('8-floor-after-gear',1,66,70,160-11+artOffset);
        await walkUntil('8-before-bridge-dash',p=>p.phase===1&&p.x/4>=82&&p.x/4<88);
        await inputSegment(['ArrowRight','x'],200);await page.waitForTimeout(400);
        await stableLanding('8-blue-bridge',1,128,156,160-11+artOffset);
        assert(pixelMatches(await capture(),180,144,[240,240,240]),'First dash brings the white ferry to the near station');
        await walkUntil('8-before-ferry-jump',p=>p.phase===1&&p.x/4>=149&&p.x/4<157);
        await inputSegment(['ArrowRight','z'],266);
        await stableLanding('8-green-near-ferry',2,176,204,144-11+artOffset);
        await inputSegment(['ArrowRight','x'],200);await page.waitForTimeout(400);
        await stableLanding('8-green-far-ferry',2,248,275,144-11+artOffset);
        await walkUntil('8-before-up-dash',p=>p.phase===2&&p.x/4>=263&&p.x/4<272);
        await inputSegment(['ArrowUp','x'],200);
        await inputSegment(['ArrowRight'],300);
        await stableLanding('8-green-exit-platform',2,280,304,96-11+artOffset);
      }
      for(const step of index===0||index===4||index===7?[]:room.route.slice(0,-1)) {
        const started=performance.now(), next=new Set();
        if(step.x)next.add(step.x<0?'ArrowLeft':'ArrowRight');
        if(step.y)next.add(step.y<0?'ArrowUp':'ArrowDown');
        for(const [flag,key] of [['jump','z'],['dash','x'],['grab','c']])if(step[flag])next.add(key);
        await Promise.all([...held].filter(k=>!next.has(k)).map(k=>page.keyboard.up(k)));
        await Promise.all([...next].filter(k=>!held.has(k)).map(k=>page.keyboard.down(k)));
        held=next;
        if(index>=8)await page.clock.runFor(Math.round(step.frames*1000/60));
        else await page.waitForTimeout(Math.max(0,step.frames*1000/60-(performance.now()-started)));
      }
      await Promise.all([...held].map(k=>page.keyboard.up(k)));
      const last=room.route.at(-1);
      assert(last.x===1&&!last.y&&!last.jump&&!last.dash&&!last.grab,'Route ends with a horizontal landing approach');
      if(index>=8) {
        await page.keyboard.down('ArrowRight');
        const deadline=Date.now()+12000;
        let reached=false;
        while(Date.now()<deadline) {
          await page.clock.runFor(32);
          const player=await observe();
          if(player&&player.phase===room.goal.phase&&player.x/4>=room.goal.x+3&&Math.abs(player.y/4-exitY)<2){reached=true;break;}
        }
        await page.keyboard.up('ArrowRight');
        assert(reached,`Crystal room ${index+1} reaches its visible landing`);
      } else {
      // Release well before the scene edge; screenshots can span several live frames.
      await walkUntil(`${index+1}-exit-approach`,p=>p.x/4>=room.goal.x-24&&Math.abs(p.y/4-exitY)<2);
      await page.waitForTimeout(120);
      let atExit=false;
      for(let attempt=0;attempt<16;attempt++) {
        const player=await observe();
        if(player&&player.x/4>=room.goal.x+3&&Math.abs(player.y/4-exitY)<2){atExit=true;break;}
        await page.keyboard.press('ArrowRight',{delay:60});await page.waitForTimeout(100);
      }
      assert(atExit,`Room ${index+1} controlled approach reaches exit before transition`);
      await stableLanding(`${index+1}-exit-landing`,room.goal.phase,room.goal.x+2,320,exitY);
      }
      const end=character(await capture({path:path.join(reports,`demo-${index+1}-end.png`)}));
      assert(end && end.x/4>=room.goal.x-2,`Room ${index+1} route reaches exit landing: ${JSON.stringify(end)}`);
      assert(Math.abs(end.y/4-exitY)<2,`Room ${index+1} exit height`);
      result.rooms.push({room:index+1,start,end});console.log(`Passed visible route ${index+1}`);
      if(index<rooms.length&&!calibrationRoom) {
        const travelDeadline=Date.now()+5000;
        let arrived=false;
        while(Date.now()<travelDeadline) {
          // Release before the slide ends so the destination checkpoint stays still.
          if(index>=8) { await page.keyboard.down('ArrowRight');await page.clock.runFor(32);await page.keyboard.up('ArrowRight'); }
          else { await page.keyboard.press('ArrowRight',{delay:80});await page.waitForTimeout(280); }
          const player=await observe();
          if(player&&player.x/4<room.goal.x-12){arrived=true;break;}
        }
        assert(arrived,`Room ${index+1} right edge loads the next checkpoint`);
        if(index>=8)await page.clock.runFor(350);else await page.waitForTimeout(350);
      }
    }
    if(calibrationRoom){assert.deepEqual(result.errors,[]);assert.deepEqual(result.warnings,[]);assert.deepEqual(result.failedRequests,[]);result.calibrationRoom=calibrationRoom;result.passed=true;return;}
    await page.clock.resume();
    result.checkpointTransitions=rooms.length;console.log(`Passed ${rooms.length} routes and ${rooms.length} transitions; checking native room selection and custom map`);

    async function nativeResult(before) {
      await page.waitForFunction(n=>window.__nativeMapResults.length>n,before);
      const response=await page.evaluate(()=>window.__nativeMapResults.at(-1));assert.equal(response.success,true,response.message);
      return response;
    }
    // Escape pauses; the grid icon opens the 4×3 level select at the current level.
    async function selectRoom(number, expected=rooms[number-1]) {
      const before=await page.evaluate(()=>window.__nativeMapResults.length);
      await tap('Escape');await tap('ArrowRight');await tap('Enter');
      await tap('ArrowUp',2);await tap('ArrowLeft',3);
      await tap('ArrowRight',(number-1)%4);await tap('ArrowDown',Math.floor((number-1)/4));
      if(number===12)fs.writeFileSync(path.join(reports,'demo-level-select.png'),await capture());
      await tap('Enter');
      const response=await nativeResult(before);
      const player=await visible(`select-${number}`,p=>Math.abs(p.x/4-expected.spawnX-4)<2&&p.phase===0);
      return {room:number,response,player};
    }
    result.selectedRooms=[];for(let number=1;number<=rooms.length;number++)result.selectedRooms.push(await selectRoom(number));
    const handmade=await selectRoom(12,{spawnX:24});result.selectedRooms.push(handmade);
    await page.keyboard.press('z',{delay:80});
    // A mover sits 5px above the redesigned spawn, so the jump shows as its phase change rather than height.
    const handmadeJump=await observe();assert(handmadeJump.phase===1,'Room12 accepts native jump');
    await page.keyboard.down('ArrowRight');await page.keyboard.press('x',{delay:80});await page.keyboard.up('ArrowRight');
    const handmadeDash=await observe();assert(handmadeDash.x>handmadeJump.x+40,'Room12 accepts native dash');
    await capture({path:path.join(reports,'room-12-dash.png')});
    result.handmade={start:handmade.player,jumped:handmadeJump,dashed:handmadeDash};
    await selectRoom(12,{spawnX:24});
    await page.clock.pauseAt(await page.evaluate(()=>Date.now()+100));
    // Align input edges to fixed simulation steps using the measured browser frame cadence; the pause menu holds play meanwhile.
    await page.keyboard.down('Escape');await page.clock.runFor(50);await page.keyboard.up('Escape');
    await page.evaluate(()=>{window.__routeRaf=[];function tick(t){__routeRaf.push(t);if(__routeRaf.length<4)requestAnimationFrame(tick);}requestAnimationFrame(tick);});
    await page.clock.runFor(100);
    const routeRaf=await page.evaluate(()=>__routeRaf[1]-__routeRaf[0]);
    assert(routeRaf>0&&Number.isFinite(routeRaf));
    await page.keyboard.down('Escape');await page.clock.runFor(50);await page.keyboard.up('Escape');await page.clock.runFor(routeRaf);
    let routeKeys=new Set(),routeFrames=0,lastRaf=0;
    result.level12Steps=[];
    const route12=JSON.parse(fs.readFileSync(path.join(__dirname,'Room12Route.json')));
    for(const step of route12) {
      const next=new Set();
      if(step.x)next.add(step.x<0?'ArrowLeft':'ArrowRight');
      if(step.y)next.add(step.y<0?'ArrowUp':'ArrowDown');
      for(const [flag,key]of[['jump','z'],['dash','x'],['grab','c']])if(step[flag])next.add(key);
      for(const key of routeKeys)if(!next.has(key))await page.keyboard.up(key);
      for(const key of next)if(!routeKeys.has(key))await page.keyboard.down(key);
      routeKeys=next;
      routeFrames+=step.frames;
      const targetRaf=Math.ceil(routeFrames*1000/60/routeRaf-1e-7);
      await page.clock.runFor((targetRaf-lastRaf)*routeRaf);lastRaf=targetRaf;
      result.level12Steps.push({frames:routeFrames,player:await observe()});
    }
    for(const key of routeKeys)await page.keyboard.up(key);
    for(let n=0;n<20&&!await victoryShown(await capture());n++) {
      await page.keyboard.down('ArrowRight');await page.clock.runFor(100);await page.keyboard.up('ArrowRight');
    }
    assert(await victoryShown(await capture({path:path.join(reports,'demo-12-victory.png')})),'Full level12 keyboard route reaches the victory banner');
    await page.setViewportSize({width:390,height:844});await page.clock.runFor(100);
    assert(await victoryShown(await capture({path:path.join(reports,'demo-12-victory-portrait.png')})),'Victory banner stays centered in portrait');
    await page.setViewportSize({width:1280,height:720});await page.clock.resume();
    // The victory grid icon opens level select; Enter on its first tile replays level1.
    const beforeReplay=await page.evaluate(()=>window.__nativeMapResults.length);
    await tap('ArrowRight');await tap('Enter');await tap('Enter');await nativeResult(beforeReplay);
    result.victory={route:true,rafInterval:routeRaf,frames:route12.reduce((n,s)=>n+s.frames,0),restarted:await visible('victory-replay',p=>Math.abs(p.x/4-28)<2&&p.phase===0)};
    result.backtracking=[];
    for(const [number,previous] of [[2,1],[9,8],[12,11]]) {
      await selectRoom(number,number===12?{spawnX:24}:rooms[number-1]);
      await page.keyboard.press('ArrowLeft',{delay:450});
      const returned=await visible(`back-${number}-to-${previous}`,p=>p.x/4>=280);
      await page.waitForTimeout(250);
      await page.keyboard.press('ArrowRight',{delay:550});
      const resumed=await visible(`forward-${previous}-to-${number}`,p=>p.x/4<80);
      result.backtracking.push({from:number,to:previous,returned,resumed});
    }
    await selectRoom(1);
    await page.reload(); await begin();
    for(const [width,height] of [[320,407],[800,600],[2560,1080],[844,390],[390,844]]) {
      await page.setViewportSize({width,height}); await page.waitForTimeout(250);
      const box=await canvas.boundingBox();assert.deepEqual([box.x,box.y,box.width,box.height],[0,0,width,height]);
      assert.deepEqual(await canvas.evaluate(c=>[c.width,c.height]),[Math.max(320,Math.round(180*width/height)),Math.max(180,Math.ceil(320*height/width))]);
      assert(character(await capture()),'Player remains visible after resize');
      await page.screenshot({path:path.join(reports,`demo-viewport-${width}.png`)});
    }
    await page.setViewportSize({width:1280,height:720}); await page.waitForTimeout(250);
    console.log('Passed viewport checks; checking key settings');
    // Pause freezes play; the keyboard icon opens key settings (arrows move, Enter captures, Backspace clears).
    await page.waitForTimeout(150);const paused=character(await capture());
    await tap('Escape');await page.keyboard.press('x',{delay:180});await page.waitForTimeout(60);
    await tap('ArrowRight',2);await tap('Enter');
    await tap('ArrowDown');await tap('Enter');await tap('l');
    await tap('ArrowDown',3);await tap('Enter');await tap('j');await tap('ArrowRight');await tap('Backspace');
    fs.writeFileSync(path.join(reports,'demo-keys.png'),await capture());
    await tap('Escape');await tap('Escape');await page.waitForTimeout(150);
    const before=character(await capture());
    assert(paused&&before&&Math.abs(before.x-paused.x)<1&&Math.abs(before.y-paused.y)<1,'Pause and key settings hold player movement');
    await page.keyboard.press('l',{delay:100});
    const moved=character(await capture());assert(moved.x>before.x+12,'Custom move reaches Unity');
    await page.keyboard.press('d',{delay:100});
    const other=character(await capture());assert(other.x>moved.x+12,'Other movement slot survives');
    await page.keyboard.press('j',{delay:90});
    const jumped=character(await capture());assert(jumped.y<other.y-12,'Custom jump reaches Unity');
    await page.reload();await startLevel();await page.waitForTimeout(400);
    let start=await observe();await page.keyboard.press('Space',{delay:90});
    assert(Math.abs((await observe()).y-start.y)<1,'Cleared jump slot persists');
    await page.keyboard.press('l',{delay:100});
    assert((await observe()).x>start.x+12,'Saved move binding persists');
    await tap('Escape');await tap('ArrowRight',2);await tap('Enter');await tap('ArrowDown',7);await tap('Enter');await tap('Escape');await tap('Escape');
    await page.waitForTimeout(400);start=await observe();await page.keyboard.press('z',{delay:90});
    assert((await observe()).y<start.y-12,'Restore brings back the default jump');
    console.log('Passed key settings; checking fullscreen');
    await page.locator('#fullscreen-toggle').click();
    await page.waitForFunction(()=>document.fullscreenElement===document.documentElement);
    await page.waitForFunction(()=>document.getElementById('fullscreen-toggle').getAttribute('aria-label')==='退出全螢幕');
    await page.screenshot({path:path.join(reports,'demo-fullscreen.png')});
    await page.locator('#fullscreen-toggle').click();await page.waitForFunction(()=>!document.fullscreenElement);
    await page.waitForTimeout(150);const afterFullscreen=await observe();
    await page.keyboard.press('ArrowRight',{delay:120});const resumed=await observe();
    assert(resumed.x>afterFullscreen.x+12,'Exiting fullscreen restores native movement without another canvas click');
    console.log('Passed fullscreen and resumed input; checking in-game editor files');
    // The editor toolbar's second row: 7 colors, import, export, play, back; 7px icons at y=18 in a 320px-wide frame.
    async function editorIcon(slot) {
      const box=await canvas.boundingBox(),scale=box.width/320,x=6+slot*11+(slot>=7?4:0)+(slot>=9?4:0)+3.5;
      await page.mouse.move(box.x+x*scale,box.y+21.5*scale);await page.waitForTimeout(100);
      await page.mouse.down();await page.waitForTimeout(80);await page.mouse.up();
    }
    await page.reload();await loaded();await canvas.click();
    await tap('ArrowRight',2);await page.keyboard.press('z',{delay:80});await page.waitForTimeout(1500);
    const editorMap={name:'Unity editor import',blocks:[{x:0,y:168,w:320,h:8,phase:-1,phaseMask:7}],spikes:[],
      lasers:[{x:160,y:96,direction:1,phaseMask:2}],crystals:[{x:80,y:120,kind:1,phaseMask:7}],spawnX:24,spawnY:152};
    const editorChooser=page.waitForEvent('filechooser');await editorIcon(7);
    await (await editorChooser).setFiles({name:'phase-editor-map.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(editorMap))});
    await page.waitForTimeout(500);fs.writeFileSync(path.join(reports,'demo-editor-import.png'),await capture());
    const editorDownload=page.waitForEvent('download');await editorIcon(8);
    const editorExport=JSON.parse(fs.readFileSync(await (await editorDownload).path(),'utf8'));
    assert.equal((await editorDownload).suggestedFilename(),'phase-custom-map.json');
    assert.deepEqual([editorExport.blocks.length,editorExport.spikes,editorExport.lasers,editorExport.crystals,editorExport.spawnX,editorExport.spawnY],
      [40,[],editorMap.lasers,editorMap.crystals,24,152],'In-game editor imports and exports lasers and crystals');
    await page.waitForTimeout(500);await page.reload();await loaded();await canvas.click();
    await tap('ArrowRight',2);await page.keyboard.press('z',{delay:80});await page.waitForTimeout(1500);
    const reloadedDownload=page.waitForEvent('download');await editorIcon(8);
    assert.deepEqual(JSON.parse(fs.readFileSync(await (await reloadedDownload).path(),'utf8')),editorExport,'In-game editor draft persists across reload');
    // The play icon runs the draft natively as a custom map; the toolbar is hidden while it plays.
    await editorIcon(9);
    const customStart=await visible('custom-map-loaded',p=>Math.abs(p.x/4-28)<2&&p.phase===0);
    await page.keyboard.press('ArrowRight',{delay:150});const customMoved=await observe();assert(customMoved.x>customStart.x+20,'Custom map accepts native movement');
    await page.keyboard.press('z',{delay:90});const customJumped=await observe();assert(customJumped.y<customMoved.y-12&&customJumped.phase===1,'Custom map accepts native jump/phase');
    result.editorFiles={imported:editorMap.name,exported:editorExport,persisted:true};
    result.customMap={start:customStart,moved:customMoved,jumped:customJumped};
    console.log('Passed in-game editor files and custom-map play; checking load failure');
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
