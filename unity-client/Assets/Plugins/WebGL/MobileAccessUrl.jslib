mergeInto(LibraryManager.library, {
  AppreciatorsGetMobileAccessUrl: function () {
    var url = "";

    if (typeof window !== "undefined") {
      url = window.APPRECIATORS_MOBILE_URL || window.location.href || "";
    }

    if (!url) {
      url = "http://127.0.0.1:8088/?mobile=1";
    }

    var lengthBytes = lengthBytesUTF8(url) + 1;
    var stringOnWasmHeap = _malloc(lengthBytes);
    stringToUTF8(url, stringOnWasmHeap, lengthBytes);
    return stringOnWasmHeap;
  }
});
