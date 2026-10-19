'use strict';
function setupMaps(instance, gameCanvas) {
  const model = PhaseMapModel, colors = { 7:'#e8e8e8', 1:'#ff8f91', 2:'#79c9ff', 4:'#78d892', 5:'#f1dc81', 3:'#d7a3ed', 6:'#85e4dc' };
  const rooms = document.getElementById('rooms'), editor = document.getElementById('map-editor'), victory = document.getElementById('victory');
  const timeout = '載入逾時，請重新整理後再試。', requests = '[data-room], #map-play, #victory-retry';
  const status = document.getElementById('map-status'), roomStatus = document.getElementById('rooms-status');
  const board = document.getElementById('map-board'), context = board.getContext('2d');
  const storageKey = 'phase.map.v1', undo = [], redo = [];
  let map = model.empty(), tool = 'block', mask = 7, selected = { x:3, y:19 }, stroke = null, previous = null, pending = null, stalled = false;
  for (let x = 0; x < model.columns; x++) model.paint(map, x, 21, 'block', 7);
  let notice = '';
  try { const saved = localStorage.getItem(storageKey); if (saved) map = model.parse(saved); }
  catch { notice = '無法讀取草稿，請匯入備份 JSON。'; }
  const snapshot = () => JSON.stringify(map);
  const pause = () => instance.SendMessage('PHASE','SetControlsOpen',String(document.hidden || !document.hasFocus() || window.phaseAnyDialogOpen()));
  function open(dialog) { if (!dialog.open) dialog.showModal(); pause(); }
  function closed() { if (!window.phaseAnyDialogOpen()) gameCanvas.focus(); pause(); }
  function save() {
    try { localStorage.setItem(storageKey, JSON.stringify(model.room(map))); status.textContent = ''; }
    catch { status.textContent = '無法儲存草稿；本次編輯仍保留，請匯出 JSON 備份。'; }
  }
  function changed(before) {
    if (before === snapshot()) return;
    undo.push(before); if (undo.length > 80) undo.shift(); redo.length = 0; save(); render();
  }
  function history(from, to) {
    if (!from.length || pending) return;
    to.push(snapshot()); map = JSON.parse(from.pop()); save(); render();
  }
  function render() {
    context.fillStyle = '#101a2b'; context.fillRect(0,0,640,352);
    map.cells.forEach((tile,i) => {
      if (!tile) return;
      const x = i % 40 * 16, y = Math.floor(i / 40) * 16;
      context.fillStyle = colors[tile.mask];
      if (tile.tool === 'block') {
        context.globalAlpha = .4; context.fillRect(x+1,y+1,14,14); context.globalAlpha = 1;
        context.fillRect(x+1,y+1,14,3);
      } else {
        context.save(); context.translate(x+8,y+8); context.rotate([0,Math.PI,-Math.PI/2,Math.PI/2][Number(tile.tool.slice(-1))]);
        context.beginPath(); context.moveTo(-7,7); context.lineTo(0,-7); context.lineTo(7,7); context.fill(); context.restore();
      }
    });
    context.strokeStyle = '#26374c'; context.lineWidth = 2; context.beginPath();
    for (let x=0;x<=640;x+=16) { context.moveTo(x,0);context.lineTo(x,352); }
    for (let y=0;y<=352;y+=16) { context.moveTo(0,y);context.lineTo(640,y); } context.stroke();
    const sx=map.spawnX*2, sy=map.spawnY*2;
    context.fillStyle='#eadfc5';context.fillRect(sx+3,sy+2,10,8);context.fillRect(sx+5,sy+10,6,11);
    context.fillStyle='#101a2b';context.fillRect(sx+5,sy+4,2,3);context.fillRect(sx+10,sy+4,2,3);
    context.strokeStyle='#eadfc5';context.lineWidth=2;context.strokeRect(selected.x*16+1,selected.y*16+1,14,14);
    board.setAttribute('aria-label',`地圖格線，第 ${selected.x+1} 欄、第 ${selected.y+1} 列。方向鍵移動，Space 繪製。`);
    document.getElementById('map-coordinate').textContent=`${selected.x * 8}, ${selected.y * 8}`;
    document.getElementById('map-undo').disabled=!undo.length || !!pending;
    document.getElementById('map-redo').disabled=!redo.length || !!pending;
    document.querySelectorAll('[data-map-tool]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.mapTool===tool)));
    document.querySelectorAll('[data-map-mask]').forEach(b=>b.setAttribute('aria-pressed',String(Number(b.dataset.mapMask)===mask)));
  }
  function apply(x,y) {
    const before=snapshot();
    if (!model.paint(map,x,y,tool,mask)) return;
    try { model.validateSpawn(map); status.textContent=''; }
    catch(error) { map=JSON.parse(before);status.textContent=error.message; }
  }
  function position(event) {
    const rect=board.getBoundingClientRect();
    return {x:Math.max(0,Math.min(39,Math.floor((event.clientX-rect.left)/rect.width*40))),y:Math.max(0,Math.min(21,Math.floor((event.clientY-rect.top)/rect.height*22)))};
  }
  function drawTo(point) {
    const from=previous || point, dx=point.x-from.x, dy=point.y-from.y, steps=Math.max(Math.abs(dx),Math.abs(dy));
    for(let n=0;n<=steps;n++) apply(Math.round(from.x+dx*(steps?n/steps:0)),Math.round(from.y+dy*(steps?n/steps:0)));
    previous=point;selected=point;render();
  }
  board.addEventListener('pointerdown',event=>{
    if(pending || event.button!==0) return;
    event.preventDefault();board.focus();board.setPointerCapture(event.pointerId);stroke=snapshot();previous=null;drawTo(position(event));
  });
  board.addEventListener('pointermove',event=>{ if(stroke!==null) drawTo(position(event)); });
  function endStroke() { if(stroke===null)return; const before=stroke;stroke=null;previous=null;changed(before); }
  board.addEventListener('pointerup',endStroke);board.addEventListener('pointercancel',endStroke);board.addEventListener('lostpointercapture',endStroke);
  document.querySelectorAll('[data-map-tool]').forEach(b=>b.addEventListener('click',()=>{tool=b.dataset.mapTool;render();}));
  document.querySelectorAll('[data-map-mask]').forEach(b=>b.addEventListener('click',()=>{mask=Number(b.dataset.mapMask);render();}));
  document.getElementById('map-undo').addEventListener('click',()=>history(undo,redo));
  document.getElementById('map-redo').addEventListener('click',()=>history(redo,undo));
  function openRooms() { roomStatus.textContent=stalled?timeout:'';open(rooms); }
  document.getElementById('rooms-toggle').addEventListener('click',openRooms);
  document.getElementById('rooms-close').addEventListener('click',()=>rooms.close());
  document.getElementById('map-open').addEventListener('click',()=>{open(editor);rooms.close();status.textContent=stalled?timeout:notice;notice='';render();board.focus();});
  document.getElementById('map-close').addEventListener('click',()=>editor.close());
  for(const dialog of [rooms,editor,victory]) { dialog.addEventListener('close',closed);dialog.addEventListener('cancel',event=>{if(pending)event.preventDefault();}); }
  function request(method,value,dialog) {
    if(pending || stalled)return;
    dialog.querySelector('[role="status"]').textContent='載入中…';
    pending={dialog,timer:setTimeout(()=>{stalled=true;finish(false,timeout);},15000)};
    for(const b of dialog.querySelectorAll('button')) b.disabled=true;
    try {instance.SendMessage('PHASE',method,value);} catch {finish(false,'無法載入，請再試一次。');}
  }
  function finish(success,message) {
    if(!pending) {
      // A late answer to the timed-out request means Unity is responsive again; no other request can be outstanding.
      if(stalled) { stalled=false; for(const b of document.querySelectorAll(requests)) b.disabled=false; for(const p of document.querySelectorAll('dialog [role="status"]')) if(p.textContent===timeout) p.textContent=''; pause(); }
      return;
    }
    const {dialog,timer}=pending;clearTimeout(timer);pending=null;
    for(const b of dialog.querySelectorAll('button')) b.disabled=false;
    render();
    for(const b of document.querySelectorAll(requests)) b.disabled=stalled;
    if(success) {dialog.close();closed();}
    else dialog.querySelector('[role="status"]').textContent=message || '無法載入，請檢查地圖。';
  }
  addEventListener('phase-map-result',event=>finish(event.detail?.success===true,event.detail?.message));
  addEventListener('phase-victory',()=>{victory.querySelector('[role="status"]').textContent='';open(victory);});
  document.getElementById('victory-retry').addEventListener('click',()=>request('SelectDemoRoom','1',victory));
  document.getElementById('victory-rooms').addEventListener('click',()=>{openRooms();victory.close();});
  document.querySelectorAll('[data-room]').forEach(b=>b.addEventListener('click',()=>request('SelectDemoRoom',b.dataset.room,rooms)));
  document.getElementById('map-play').addEventListener('click',()=>{
    try {const room=model.room(map);if(!room.blocks.length)throw Error('地圖至少需要一格地形。');const json=JSON.stringify(room);model.parse(json);request('LoadCustomMap',json,editor);}
    catch(error){status.textContent=error.message;}
  });
  function download(json) {
    const url=URL.createObjectURL(new Blob([json],{type:'application/json'})),a=document.createElement('a');
    a.href=url;a.download='phase-custom-map.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  document.getElementById('map-export').addEventListener('click',()=>{
    try {const json=JSON.stringify(model.room(map),null,2);model.parse(json);download(json);}
    catch(error){status.textContent=error.message;}
  });
  // The in-game editor's export and import icons; Unity validates the file and flashes on failure.
  addEventListener('phase-map-export',event=>download(event.detail.json));
  addEventListener('phase-map-import',()=>{
    const input=document.createElement('input');input.type='file';input.accept='application/json,.json';
    input.addEventListener('change',async()=>{
      const file=input.files[0];if(!file)return;
      instance.SendMessage('PHASE','ImportDraft',file.size>800000?'':await file.text());
    });
    input.click();
  });
  document.getElementById('map-import').addEventListener('click',()=>document.getElementById('map-file').click());
  document.getElementById('map-file').addEventListener('change',async event=>{
    const file=event.target.files[0];event.target.value='';if(!file)return;
    try {
      if(file.size>800000)throw Error('地圖檔案過大。');
      const imported=model.parse(await file.text()),before=snapshot();map=imported;changed(before);
    } catch(error){status.textContent=error.message;}
  });
  addEventListener('keydown',event=>{
    if(!rooms.open&&!editor.open&&!victory.open)return;
    event.stopImmediatePropagation();
    if(pending){event.preventDefault();return;}
    if(event.target===board) {
      const move={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]}[event.code];
      if(move){event.preventDefault();selected={x:Math.max(0,Math.min(39,selected.x+move[0])),y:Math.max(0,Math.min(21,selected.y+move[1]))};render();}
      else if(event.code==='Space'){event.preventDefault();const before=snapshot();apply(selected.x,selected.y);changed(before);render();}
      else if((event.ctrlKey||event.metaKey)&&event.code==='KeyZ'){event.preventDefault();history(event.shiftKey?redo:undo,event.shiftKey?undo:redo);}
    } else if((event.code==='Enter'||event.code==='Space')&&event.target.tagName==='BUTTON') {event.preventDefault();if(!event.repeat)event.target.click();}
  },true);
  document.getElementById('rooms-toggle').hidden=false;render();
}
