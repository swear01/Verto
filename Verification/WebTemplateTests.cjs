const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
let calls = 0;
class WebGL2RenderingContext {
  constructor(id = 'unity-canvas') { this.canvas = { id }; this.RENDERBUFFER = 0x8d41; this.SAMPLES = 0x80a9; }
  getInternalformatParameter(target, format, name) {
    calls++;
    if (target !== this.RENDERBUFFER || name !== this.SAMPLES) throw new Error('invalid query');
    return new Int32Array([4, 2]);
  }
}
const source = fs.readFileSync(path.join(__dirname, '../Assets/WebGLTemplates/Phase/webgl-compat.js'), 'utf8');
vm.runInNewContext(source, { WebGL2RenderingContext, Int32Array });
const gl = new WebGL2RenderingContext();
for (const format of [0x8f94, 0x8f95, 0x8f97, 0x8f98, 0x8f99, 0x8f9b])
  assert.deepEqual([...gl.getInternalformatParameter(gl.RENDERBUFFER, format, gl.SAMPLES)], []);
assert.equal(calls, 0);
assert.deepEqual([...gl.getInternalformatParameter(gl.RENDERBUFFER, 0x8058, gl.SAMPLES)], [4, 2]);
assert.deepEqual([...new WebGL2RenderingContext('other').getInternalformatParameter(gl.RENDERBUFFER, 0x8f94, gl.SAMPLES)], [4, 2]);
assert.throws(() => gl.getInternalformatParameter(0, 0x8f94, gl.SAMPLES), /invalid query/);
assert.throws(() => gl.getInternalformatParameter(gl.RENDERBUFFER, 0x8f94, 0), /invalid query/);
vm.runInNewContext(source, {});
console.log('PASS: SNORM sample guard preserves ordinary queries, other canvases and invalid-query errors');
