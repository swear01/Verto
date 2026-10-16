(() => {
  if (!globalThis.WebGL2RenderingContext) return;
  const query = WebGL2RenderingContext.prototype.getInternalformatParameter;
  const snorm = new Set([0x8f94, 0x8f95, 0x8f97, 0x8f98, 0x8f99, 0x8f9b]);
  WebGL2RenderingContext.prototype.getInternalformatParameter = function (target, format, name) {
    // shortcut: Unity 6000.4 probes unsupported SNORM MSAA formats; remove after the engine/browser fix.
    if (this.canvas.id === 'unity-canvas' && target === this.RENDERBUFFER && name === this.SAMPLES && snorm.has(format))
      return new Int32Array(0);
    return query.call(this, target, format, name);
  };
})();
