'use strict';
function setupMaps(instance, gameCanvas) {
  // Only the focused game canvas sends keys to Unity; play pauses while the page is in the background.
  const pause = paused => instance.SendMessage('PHASE','SetControlsOpen',String(paused));
  addEventListener('blur',()=>pause(true));addEventListener('focus',()=>pause(document.hidden));
  document.addEventListener('visibilitychange',()=>pause(document.hidden || !document.hasFocus()));
  addEventListener('keydown',event=>{if(event.target!==gameCanvas||event.code==='Tab')event.stopImmediatePropagation();},true);
  // Right-click erases in the in-game editor, so the canvas never opens the browser menu.
  gameCanvas.addEventListener('contextmenu',event=>event.preventDefault());
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
}
