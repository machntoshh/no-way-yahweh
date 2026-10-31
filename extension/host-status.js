// Makes a missing native host visible with a "!" badge and tooltip on the toolbar icon.
// Site blocking keeps working without the host; only the counters and the user's extra
// domains depend on it.
globalThis.NwyHostStatus = (() => {
  let current = null;

  function show(connected) {
    if (connected === current) return;
    current = connected;
    chrome.action.setBadgeText({ text: connected ? "" : "!" });
    chrome.action.setBadgeBackgroundColor({ color: "#E3B16B" });
    chrome.action.setTitle({
      title: connected
        ? "No way? Yahweh! — protegido"
        : "No way? Yahweh! — serviço local não encontrado. Abra o app No way? Yahweh! " +
          "(ou use \"Reparar integração\"). Os sites continuam bloqueados; só os domínios extras e a contagem dependem dele.",
    });
  }

  return { show };
})();
