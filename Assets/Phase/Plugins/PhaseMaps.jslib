mergeInto(LibraryManager.library, {
  PhaseMapResult: function (success, message) {
    window.dispatchEvent(new CustomEvent('phase-map-result', {
      detail: { success: success !== 0, message: UTF8ToString(message) }
    }));
  }
});
