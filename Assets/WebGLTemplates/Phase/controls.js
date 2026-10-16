'use strict';
function setupControls(instance, canvas) {
  const labels = ['向左', '向右', '向上', '向下', '跳躍', '衝刺', '攀抓'];
  const defaults = [['LeftArrow', 'A'], ['RightArrow', 'D'], ['UpArrow', 'W'], ['DownArrow', 'S'], ['Z', 'Space'], ['X'], ['C', 'LeftShift', 'RightShift']];
  const codeNames = { Space: 'Space', ArrowLeft: 'LeftArrow', ArrowRight: 'RightArrow', ArrowUp: 'UpArrow', ArrowDown: 'DownArrow', ShiftLeft: 'LeftShift', ShiftRight: 'RightShift', Comma: 'Comma', Period: 'Period', Slash: 'Slash', Semicolon: 'Semicolon', Quote: 'Quote', BracketLeft: 'LeftBracket', BracketRight: 'RightBracket', Backslash: 'Backslash', Backquote: 'Backquote', Minus: 'Minus', Equal: 'Equals', Enter: 'Enter', Backspace: 'Backspace' };
  const names = { LeftArrow: '←', RightArrow: '→', UpArrow: '↑', DownArrow: '↓', Space: 'Space', LeftShift: '左 Shift', RightShift: '右 Shift' };
  const storageKey = 'phase.controls.v1';
  const dialog = document.getElementById('controls');
  const status = document.getElementById('controls-status');
  const toggle = document.getElementById('controls-toggle');
  let overrides = labels.map(() => ''), pending = -1;
  const keyFor = code => /^Key[A-Z]$/.test(code) ? code.slice(3) : /^Digit[0-9]$/.test(code) ? code : codeNames[code];
  const allowed = new Set([...Object.values(codeNames), ...'ABCDEFGHIJKLMNOPQRSTUVWXYZ', ...Array.from({ length: 10 }, (_, i) => `Digit${i}`)]);
  const valid = value => {
    if (!Array.isArray(value) || value.length !== labels.length || !value.every(key => key === '' || allowed.has(key))) return false;
    const keys = value.flatMap((key, i) => key ? [key] : defaults[i]);
    return new Set(keys).size === keys.length;
  };
  try { const saved = JSON.parse(localStorage.getItem(storageKey)); if (valid(saved)) overrides = saved; } catch {}
  const send = () => instance.SendMessage('PHASE', 'ConfigureControls', JSON.stringify({ keys: overrides }));
  const pause = open => instance.SendMessage('PHASE', 'SetControlsOpen', String(open));
  const resets = [];
  const buttons = labels.map((label, i) => {
    const row = document.createElement('div'); row.className = 'binding';
    const text = document.createElement('span'); text.textContent = label;
    const button = document.createElement('button'); button.className = 'key'; button.dataset.action = i;
    button.addEventListener('click', () => { pending = i; render(); status.textContent = '按下新鍵，Esc 取消。'; });
    const reset = document.createElement('button'); reset.className = 'restore'; reset.dataset.reset = i; reset.textContent = '還原';
    reset.setAttribute('aria-label', `恢復${label}預設鍵位`); reset.title = `恢復${label}預設鍵位`;
    reset.addEventListener('click', () => {
      pending = -1; render();
      const candidate = [...overrides]; candidate[i] = '';
      if (!valid(candidate)) {
        const conflict = overrides.findIndex((key, j) => j !== i && (key ? [key] : defaults[j]).some(key => defaults[i].includes(key)));
        status.textContent = `預設鍵已用於${labels[conflict]}，請先修改該動作。`; return;
      }
      overrides = candidate; render(); save();
    });
    resets.push(reset);
    row.append(text, button, reset); document.getElementById('bindings').append(row); return button;
  });
  function render() {
    buttons.forEach((button, i) => {
      const keys = overrides[i] ? [overrides[i]] : defaults[i];
      button.textContent = pending === i ? '按下新鍵…' : keys.map(key => names[key] || key.replace('Digit', '')).join(' / ');
      button.setAttribute('aria-label', `修改${labels[i]}鍵，目前 ${keys.map(key => names[key] || key).join('、')}`);
      button.setAttribute('aria-pressed', String(pending === i));
      resets[i].disabled = overrides[i] === '';
    });
  }
  function save() {
    send();
    try { localStorage.setItem(storageKey, JSON.stringify(overrides)); status.textContent = ''; }
    catch { status.textContent = '目前瀏覽器無法儲存；本次遊戲仍可使用。'; }
  }
  toggle.addEventListener('click', () => { pause(true); pending = -1; status.textContent = ''; render(); dialog.showModal(); });
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
    if (event.code === 'Escape') return;
    if (event.code === 'Tab') { pending = -1; status.textContent = ''; render(); return; }
    event.preventDefault(); event.stopPropagation();
    if (event.repeat) return;
    const key = keyFor(event.code);
    if (!key || event.ctrlKey || event.altKey || event.metaKey) { status.textContent = '請使用字母、數字、方向鍵或一般按鍵。'; return; }
    const candidate = [...overrides]; candidate[pending] = key;
    if (!valid(candidate)) { status.textContent = '此鍵已用於其他動作，請換一個鍵。'; return; }
    overrides = candidate; pending = -1; render(); save();
  }, true);
  addEventListener('blur', () => pause(true));
  addEventListener('focus', () => pause(dialog.open));
  send(); render(); toggle.hidden = false;
}
