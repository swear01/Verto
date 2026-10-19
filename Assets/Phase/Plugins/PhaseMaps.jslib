mergeInto(LibraryManager.library, {
  PhaseMapResult: function (success, message) {
    window.dispatchEvent(new CustomEvent('phase-map-result', {
      detail: { success: success !== 0, message: UTF8ToString(message) }
    }));
  },
  // The page saves this JSON as a file; it answers an import with SendMessage('PHASE', 'ImportDraft', json).
  PhaseExportMap: function (json) {
    window.dispatchEvent(new CustomEvent('phase-map-export', { detail: { json: UTF8ToString(json) } }));
  },
  PhaseImportMap: function () {
    window.dispatchEvent(new CustomEvent('phase-map-import'));
  }
});
