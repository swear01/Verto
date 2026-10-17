'use strict';
const PhaseMapModel = (() => {
  const columns = 40, rows = 22, cell = 8;
  const integer = (n, min, max) => Number.isInteger(n) && n >= min && n <= max && n % cell === 0;
  const masks = n => Number.isInteger(n) && n >= 1 && n <= 7;
  function empty() {
    return { name: '自訂地圖', cells: Array(columns * rows).fill(null), spawnX: 24, spawnY: 152 };
  }
  function validateSpawn(map) {
    if (!integer(map.spawnX, 0, 312) || !integer(map.spawnY, 0, 160)) throw Error('出生點必須在地圖內，並對齊格線。');
    for (let y = Math.floor(map.spawnY / cell); y <= Math.floor((map.spawnY + 10) / cell); y++) {
      const tile = map.cells[y * columns + map.spawnX / cell];
      if (tile && (tile.mask & 1)) throw Error('出生點與紅色狀態下的地板或尖刺重疊。');
    }
    return map;
  }
  function parse(text) {
    if (typeof text !== 'string' || text.length > 200000) throw Error('地圖 JSON 不可超過 200,000 字元。');
    let room;
    try { room = JSON.parse(text); } catch { throw Error('JSON 格式無效。'); }
    if (!room || Array.isArray(room) || typeof room !== 'object' || Object.keys(room).some(k => !['name','blocks','spikes','spawnX','spawnY'].includes(k))) throw Error('只支援地板、尖刺與出生點。');
    if (typeof room.name !== 'string' || room.name.length > 64 || !Array.isArray(room.blocks) || !Array.isArray(room.spikes) || room.blocks.length + room.spikes.length > columns * rows) throw Error('地圖名稱或物件清單無效。');
    const map = empty(); map.name = room.name; map.spawnX = room.spawnX; map.spawnY = room.spawnY;
    function place(item, spike) {
      if (!item || typeof item !== 'object' || Array.isArray(item) || !masks(item.phaseMask)) throw Error('每個物件都需要有效的顏色遮罩。');
      if (spike && (!Number.isInteger(item.direction) || item.direction < 0 || item.direction > 3)) throw Error('尖刺方向無效。');
      if (!spike && item.phase !== -1) throw Error('地板 phase 必須為 -1；顏色使用 phaseMask。');
      const w = spike ? (item.direction < 2 ? item.length : cell) : item.w;
      const h = spike ? (item.direction < 2 ? cell : item.length) : item.h;
      if (!integer(item.x, 0, 312) || !integer(item.y, 0, 168) || !integer(w, 8, 320) || !integer(h, 8, 176) || item.x + w > 320 || item.y + h > 176) throw Error('物件必須對齊 8 px 格線，並位於 320 × 176 範圍內。');
      for (let y = item.y / cell; y < (item.y + h) / cell; y++) for (let x = item.x / cell; x < (item.x + w) / cell; x++) {
        const i = y * columns + x;
        if (map.cells[i]) throw Error('地板與尖刺不可占用同一格。');
        map.cells[i] = { tool: spike ? `spike${item.direction}` : 'block', mask: item.phaseMask };
      }
    }
    room.blocks.forEach(b => place(b, false)); room.spikes.forEach(s => place(s, true));
    return validateSpawn(map);
  }
  function room(map) {
    validateSpawn(map);
    const result = { name: map.name, blocks: [], spikes: [], spawnX: map.spawnX, spawnY: map.spawnY };
    map.cells.forEach((tile, i) => {
      if (!tile) return;
      const x = i % columns * cell, y = Math.floor(i / columns) * cell;
      if (tile.tool === 'block') result.blocks.push({ x, y, w: cell, h: cell, phase: -1, phaseMask: tile.mask });
      else result.spikes.push({ x, y, length: cell, direction: Number(tile.tool.slice(-1)), phaseMask: tile.mask });
    });
    return result;
  }
  function paint(map, x, y, tool, mask) {
    if (!Number.isInteger(x) || !Number.isInteger(y) || x < 0 || x >= columns || y < 0 || y >= rows) return false;
    if (tool === 'spawn') {
      if (y > 20 || map.spawnX === x * cell && map.spawnY === y * cell) return false;
      map.spawnX = x * cell; map.spawnY = y * cell; return true;
    }
    if (!['block','spike0','spike1','spike2','spike3','erase'].includes(tool) || !masks(mask)) return false;
    const tile = tool === 'erase' ? null : { tool, mask }, i = y * columns + x;
    if (JSON.stringify(map.cells[i]) === JSON.stringify(tile)) return false;
    map.cells[i] = tile; return true;
  }
  return { columns, rows, cell, empty, parse, room, paint, validateSpawn };
})();
if (typeof module !== 'undefined') module.exports = PhaseMapModel;
