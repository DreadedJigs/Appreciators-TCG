mergeInto(LibraryManager.library, {
  AppreciatorsRequestWalletConnection: function (gameObjectNamePtr) {
    var target = UTF8ToString(gameObjectNamePtr);
    var send = function (method, value) {
      if (typeof SendMessage === "function") SendMessage(target, method, value || "");
    };

    (async function () {
      try {
        if (typeof window === "undefined" || !window.ethereum || !window.ethereum.request) {
          throw new Error("No injected EVM wallet was found. Open this page in MetaMask Mobile or install a browser wallet.");
        }

        var chainId = "0x8173";
        try {
          await window.ethereum.request({ method: "wallet_switchEthereumChain", params: [{ chainId: chainId }] });
        } catch (switchError) {
          if (switchError && switchError.code === 4902) {
            await window.ethereum.request({
              method: "wallet_addEthereumChain",
              params: [{
                chainId: chainId,
                chainName: "ApeChain",
                nativeCurrency: { name: "APE", symbol: "APE", decimals: 18 },
                rpcUrls: ["https://rpc.apechain.com/http"],
                blockExplorerUrls: ["https://apescan.io/"]
              }]
            });
          } else {
            throw switchError;
          }
        }

        var accounts = await window.ethereum.request({ method: "eth_requestAccounts" });
        if (!accounts || !accounts.length) throw new Error("The wallet returned no account.");
        send("OnInjectedWalletConnected", String(accounts[0]));
      } catch (error) {
        send("OnInjectedWalletError", error && error.message ? error.message : String(error));
      }
    })();
  },

  AppreciatorsSignWalletMessage: function (gameObjectNamePtr, walletAddressPtr, messagePtr) {
    var target = UTF8ToString(gameObjectNamePtr);
    var walletAddress = UTF8ToString(walletAddressPtr);
    var message = UTF8ToString(messagePtr);
    var send = function (method, value) {
      if (typeof SendMessage === "function") SendMessage(target, method, value || "");
    };

    (async function () {
      try {
        if (typeof window === "undefined" || !window.ethereum || !window.ethereum.request) {
          throw new Error("The connected wallet provider is no longer available.");
        }
        var signature = await window.ethereum.request({
          method: "personal_sign",
          params: [message, walletAddress]
        });
        send("OnInjectedWalletSignature", String(signature));
      } catch (error) {
        send("OnInjectedWalletError", error && error.message ? error.message : String(error));
      }
    })();
  },

  AppreciatorsPasteText: function (gameObjectNamePtr, successMethodPtr, errorMethodPtr) {
    var target = UTF8ToString(gameObjectNamePtr);
    var successMethod = UTF8ToString(successMethodPtr);
    var errorMethod = UTF8ToString(errorMethodPtr);
    var send = function (method, value) {
      if (typeof SendMessage === "function") SendMessage(target, method, value || "");
    };

    (async function () {
      try {
        if (!navigator.clipboard || !navigator.clipboard.readText) {
          throw new Error("Clipboard access is not available in this browser.");
        }
        var value = await navigator.clipboard.readText();
        send(successMethod, String(value || ""));
      } catch (error) {
        send(errorMethod, error && error.message ? error.message : String(error));
      }
    })();
  }
});
