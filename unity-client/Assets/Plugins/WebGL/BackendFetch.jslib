mergeInto(LibraryManager.library, {
  AppreciatorsFetchGet: function (urlPtr, gameObjectPtr, successMethodPtr, errorMethodPtr) {
    var url = UTF8ToString(urlPtr);
    var gameObject = UTF8ToString(gameObjectPtr);
    var successMethod = UTF8ToString(successMethodPtr);
    var errorMethod = UTF8ToString(errorMethodPtr);

    fetch(url, {
      method: "GET",
      mode: "cors",
      cache: "no-store"
    })
      .then(function (response) {
        return response.text().then(function (text) {
          if (response.ok) {
            SendMessage(gameObject, successMethod, text);
            return;
          }

          SendMessage(gameObject, errorMethod, text || response.statusText || ("HTTP " + response.status));
        });
      })
      .catch(function (error) {
        SendMessage(gameObject, errorMethod, error && error.message ? error.message : String(error));
      });
  },

  AppreciatorsCopyText: function (textPtr, gameObjectPtr, successMethodPtr, errorMethodPtr) {
    var text = UTF8ToString(textPtr);
    var gameObject = UTF8ToString(gameObjectPtr);
    var successMethod = UTF8ToString(successMethodPtr);
    var errorMethod = UTF8ToString(errorMethodPtr);

    function fallbackCopy(value) {
      var textarea = document.createElement("textarea");
      textarea.value = value;
      textarea.setAttribute("readonly", "readonly");
      textarea.style.position = "fixed";
      textarea.style.left = "-9999px";
      textarea.style.top = "0";
      document.body.appendChild(textarea);
      textarea.focus();
      textarea.select();

      var copied = false;
      try {
        copied = document.execCommand("copy");
      } catch (error) {
        copied = false;
      }

      document.body.removeChild(textarea);
      return copied;
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text)
        .then(function () {
          SendMessage(gameObject, successMethod, "copied");
        })
        .catch(function (error) {
          if (fallbackCopy(text)) {
            SendMessage(gameObject, successMethod, "copied");
            return;
          }

          SendMessage(gameObject, errorMethod, error && error.message ? error.message : "Clipboard permission denied.");
        });
      return;
    }

    if (fallbackCopy(text)) {
      SendMessage(gameObject, successMethod, "copied");
      return;
    }

    SendMessage(gameObject, errorMethod, "Clipboard API unavailable.");
  }
});
