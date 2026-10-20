'use strict';
function setupMaps(instance, gameCanvas) {
  const rooms = document.getElementById('rooms'), victory = document.getElementById('victory');
  const timeout = '載入逾時，請重新整理後再試。', requests = '[data-room], #victory-retry';
  const roomStatus = document.getElementById('rooms-status');
  let pending = null, stalled = false;
  const pause = () => instance.SendMessage('PHASE','SetControlsOpen',String(document.hidden || !document.hasFocus() || window.phaseAnyDialogOpen()));
  function open(dialog) { if (!dialog.open) dialog.showModal(); pause(); }
  function closed() { if (!window.phaseAnyDialogOpen()) gameCanvas.focus(); pause(); }
  function openRooms() { roomStatus.textContent=stalled?timeout:'';open(rooms); }
  document.getElementById('rooms-toggle').addEventListener('click',openRooms);
  document.getElementById('rooms-close').addEventListener('click',()=>rooms.close());
  for(const dialog of [rooms,victory]) { dialog.addEventListener('close',closed);dialog.addEventListener('cancel',event=>{if(pending)event.preventDefault();}); }
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
    for(const b of document.querySelectorAll(requests)) b.disabled=stalled;
    if(success) {dialog.close();closed();}
    else dialog.querySelector('[role="status"]').textContent=message || '無法載入，請再試一次。';
  }
  addEventListener('phase-map-result',event=>finish(event.detail?.success===true,event.detail?.message));
  addEventListener('phase-victory',()=>{victory.querySelector('[role="status"]').textContent='';open(victory);});
  document.getElementById('victory-retry').addEventListener('click',()=>request('SelectDemoRoom','1',victory));
  document.getElementById('victory-rooms').addEventListener('click',()=>{openRooms();victory.close();});
  document.querySelectorAll('[data-room]').forEach(b=>b.addEventListener('click',()=>request('SelectDemoRoom',b.dataset.room,rooms)));
  // The in-game editor's export and import icons; Unity validates the file and flashes on failure.
  addEventListener('phase-map-export',event=>{
    const url=URL.createObjectURL(new Blob([event.detail.json],{type:'application/json'})),a=document.createElement('a');
    a.href=url;a.download='phase-custom-map.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  });
  addEventListener('phase-map-import',()=>{
    const input=document.createElement('input');input.type='file';input.accept='application/json,.json';
    input.addEventListener('change',async()=>{
      const file=input.files[0];if(!file)return;
      instance.SendMessage('PHASE','ImportDraft',file.size>800000?'':await file.text());
    });
    input.click();
  });
  addEventListener('keydown',event=>{
    if(!rooms.open&&!victory.open)return;
    event.stopImmediatePropagation();
    if(pending){event.preventDefault();return;}
    if((event.code==='Enter'||event.code==='Space')&&event.target.tagName==='BUTTON') {event.preventDefault();if(!event.repeat)event.target.click();}
  },true);
  document.getElementById('rooms-toggle').hidden=false;
}
