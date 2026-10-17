'use strict';
function setupControls(instance, canvas) {
  const labels = ['向左', '向右', '向上', '向下', '跳躍', '衝刺', '攀抓'];
  const defaults = ['LeftArrow', 'A', 'RightArrow', 'D', 'UpArrow', 'W', 'DownArrow', 'S', 'Z', 'Space', 'X', '', 'C', 'Shift'];
  const codeNames = { Space: 'Space', ArrowLeft: 'LeftArrow', ArrowRight: 'RightArrow', ArrowUp: 'UpArrow', ArrowDown: 'DownArrow', ShiftLeft: 'Shift', ShiftRight: 'Shift', Comma: 'Comma', Period: 'Period', Slash: 'Slash', Semicolon: 'Semicolon', Quote: 'Quote', BracketLeft: 'LeftBracket', BracketRight: 'RightBracket', Backslash: 'Backslash', Backquote: 'Backquote', Minus: 'Minus', Equal: 'Equals', Enter: 'Enter', Backspace: 'Backspace' };
  const names = { LeftArrow: '←', RightArrow: '→', UpArrow: '↑', DownArrow: '↓', Space: 'Space', Shift: 'Shift' };
  const storageKey = 'phase.controls.v2';
  const dialog = document.getElementById('controls');
  const status = document.getElementById('controls-status');
  const toggle = document.getElementById('controls-toggle');
  let keys = [...defaults], pending = -1, loadNotice = '';
  const keyFor = code => /^Key[A-Z]$/.test(code) ? code.slice(3) : /^Digit[0-9]$/.test(code) ? code : codeNames[code];
  const allowed = new Set([...Object.values(codeNames), ...'ABCDEFGHIJKLMNOPQRSTUVWXYZ', ...Array.from({ length: 10 }, (_, i) => `Digit${i}`)]);
  const valid = value => Array.isArray(value) && value.length === defaults.length &&
    value.every(key => key === '' || allowed.has(key)) && labels.every((_, i) => value[i * 2] || value[i * 2 + 1]) &&
    new Set(value.filter(Boolean)).size === value.filter(Boolean).length;
  try {
    const saved = localStorage.getItem(storageKey);
    if (saved !== null) {
      const value = JSON.parse(saved);
      if (valid(value)) keys = value;
      else loadNotice = '儲存的鍵位無效，已使用預設。';
    } else {
      const legacy = JSON.parse(localStorage.getItem('phase.controls.v1'));
      if (legacy !== null) {
        const normalize = key => key === 'LeftShift' || key === 'RightShift' ? 'Shift' : key;
        const migrated = Array.isArray(legacy) && legacy.length === labels.length && legacy.every(key => typeof key === 'string' && (key === '' || allowed.has(normalize(key))))
          ? legacy.flatMap((key, i) => key ? [normalize(key), ''] : defaults.slice(i * 2, i * 2 + 2)) : null;
        if (valid(migrated)) keys = migrated;
        else loadNotice = '舊鍵位有衝突或無效，已使用預設；請重新設定。';
      }
    }
  } catch { loadNotice = '無法讀取儲存的鍵位，已使用預設。'; }
  const send = () => instance.SendMessage('PHASE', 'ConfigureControls', JSON.stringify({ keys }));
  const pause = open => instance.SendMessage('PHASE', 'SetControlsOpen', String(open));
  const buttons = [], resets = [];
  labels.forEach((label, i) => {
    const row = document.createElement('div'); row.className = 'binding';
    const text = document.createElement('span'); text.textContent = label; row.append(text);
    for (let slot = 0; slot < 2; slot++) {
      const index = i * 2 + slot;
      const button = document.createElement('button'); button.className = 'key'; button.dataset.action = i; button.dataset.slot = slot;
      button.addEventListener('click', () => { pending = index; render(); status.textContent = '按下新鍵；Esc 清除此格，Tab 取消。'; });
      buttons.push(button); row.append(button);
    }
    const reset = document.createElement('button'); reset.className = 'restore'; reset.dataset.reset = i; reset.textContent = '還原';
    reset.setAttribute('aria-label', `恢復${label}預設鍵位`); reset.title = `恢復${label}預設鍵位`;
    reset.addEventListener('click', () => {
      pending = -1; render();
      const candidate = [...keys]; candidate.splice(i * 2, 2, ...defaults.slice(i * 2, i * 2 + 2));
      if (!valid(candidate)) {
        const conflict = labels.findIndex((_, j) => j !== i && keys.slice(j * 2, j * 2 + 2).some(key => key && defaults.slice(i * 2, i * 2 + 2).includes(key)));
        status.textContent = `預設鍵已用於${labels[conflict]}，請先修改該動作。`; return;
      }
      keys = candidate; render(); save();
    });
    resets.push(reset); row.append(reset); document.getElementById('bindings').append(row);
  });
  function render() {
    buttons.forEach((button, index) => {
      const label = labels[Math.floor(index / 2)], key = keys[index], display = names[key] || key.replace('Digit', '') || '未設定';
      button.textContent = pending === index ? '按下新鍵…' : display;
      button.setAttribute('aria-label', `修改${label}第${index % 2 + 1}組鍵，目前 ${display}`);
      button.setAttribute('aria-pressed', String(pending === index));
    });
    resets.forEach((reset, i) => { reset.disabled = keys[i * 2] === defaults[i * 2] && keys[i * 2 + 1] === defaults[i * 2 + 1]; });
  }
  function save() {
    send(); loadNotice = '';
    try { localStorage.setItem(storageKey, JSON.stringify(keys)); status.textContent = ''; }
    catch { status.textContent = '目前瀏覽器無法儲存；本次遊戲仍可使用。'; }
  }
  toggle.addEventListener('click', () => { pause(true); pending = -1; status.textContent = loadNotice; render(); dialog.showModal(); });
  document.getElementById('controls-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('cancel', event => { if (pending >= 0) { event.preventDefault(); pending = -1; status.textContent = ''; render(); } });
  dialog.addEventListener('close', () => { pending = -1; pause(false); canvas.focus(); });
  addEventListener('keydown', event => {
    const activate = () => {
      if ((event.code === 'Enter' || event.code === 'Space') && event.target.tagName === 'BUTTON') {
        event.preventDefault(); if (!event.repeat) event.target.click();
      }
    };
    if (!dialog.open) { if (event.target !== canvas || event.code === 'Tab') event.stopImmediatePropagation(); activate(); return; }
    event.stopImmediatePropagation();
    if (pending < 0) { activate(); return; }
    if (event.code === 'Tab') { pending = -1; status.textContent = ''; render(); return; }
    event.preventDefault(); event.stopPropagation();
    if (event.repeat) return;
    const key = event.code === 'Escape' ? '' : keyFor(event.code);
    if (key === undefined || event.ctrlKey || event.altKey || event.metaKey) { status.textContent = '請使用字母、數字、方向鍵或一般按鍵。'; return; }
    const candidate = [...keys]; candidate[pending] = key;
    if (!valid(candidate)) { status.textContent = key ? '此鍵已設定，請換一個鍵。' : '每個動作至少保留一個鍵。'; return; }
    keys = candidate; pending = -1; render(); save();
  }, true);
  addEventListener('blur', () => pause(true));
  addEventListener('focus', () => pause(dialog.open));
  send(); render(); toggle.hidden = false;
}
