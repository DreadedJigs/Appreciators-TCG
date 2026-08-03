mergeInto(LibraryManager.library, {
  AppreciatorsResumeWebAudio: function () {
    if (typeof window !== "undefined" && typeof window.APPRECIATORS_UNLOCK_AUDIO === "function") {
      window.APPRECIATORS_UNLOCK_AUDIO();
    }

    try {
      if (typeof WEBAudio !== "undefined" && WEBAudio.audioContext && WEBAudio.audioContext.state !== "running") {
        WEBAudio.audioContext.resume().then(function () {
          document.documentElement.dataset.appreciatorsAudio = WEBAudio.audioContext.state;
        });
      } else if (typeof WEBAudio !== "undefined" && WEBAudio.audioContext) {
        document.documentElement.dataset.appreciatorsAudio = WEBAudio.audioContext.state;
      }
    } catch (_) {}
  }
});
