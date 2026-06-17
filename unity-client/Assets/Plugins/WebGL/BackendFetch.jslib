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
  }
});
