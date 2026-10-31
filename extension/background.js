// No way? Yahweh! — service worker.
// Domain blocking itself is done by the static declarativeNetRequest ruleset and works
// without the native host. This worker only talks to the host to (a) report how many blocks
// happened — never which site — and (b) receive extra domains from the user's list.

importScripts("host-status.js");

const HOST = "com.nowayyahweh.host";
const EXTRA_REDIRECT_RULE = 100;
const EXTRA_BLOCK_RULE = 101;
const SYNC_ALARM = "sync";
const RESUME_ALARM = "resume";
const KEYWORD_RULESET = "keyword"; // tools/build-rules.mjs

const brands = navigator.userAgentData?.brands?.map((b) => b.brand) ?? [];
const browser = brands.includes("Microsoft Edge") ? "edge" : brands.includes("Brave") ? "brave" : "chrome";

function sendToHost(message) {
  return new Promise((resolve, reject) => {
    chrome.runtime.sendNativeMessage(HOST, message, (reply) => {
      if (chrome.runtime.lastError) return reject(new Error(chrome.runtime.lastError.message));
      if (!reply || reply.type === "error") return reject(new Error(reply?.error ?? "resposta vazia"));
      resolve(reply);
    });
  });
}

async function applyExtraDomains(domains) {
  const addRules = domains.length
    ? [
        { id: EXTRA_REDIRECT_RULE, priority: 2, action: { type: "redirect", redirect: { extensionPath: "/blocked.html?r=list" } },
          condition: { requestDomains: domains, resourceTypes: ["main_frame"] } },
        { id: EXTRA_BLOCK_RULE, priority: 2, action: { type: "block" },
          condition: { requestDomains: domains, excludedResourceTypes: ["main_frame"] } },
      ]
    : [];
  await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds: [EXTRA_REDIRECT_RULE, EXTRA_BLOCK_RULE], addRules });
}

// Block counts are kept in storage until the host acknowledges them, because the
// worker can be stopped at any time and the host may not be installed yet.
// All read-modify-write of the counter goes through this queue.
let queue = Promise.resolve();
const serialized = (fn) => (queue = queue.then(fn, fn));

const addPendingBlock = () => serialized(async () => {
  const { pendingBlocks = 0 } = await chrome.storage.local.get("pendingBlocks");
  await chrome.storage.local.set({ pendingBlocks: pendingBlocks + 1 });
});

const flushPendingBlocks = () => serialized(async () => {
  const { pendingBlocks = 0 } = await chrome.storage.local.get("pendingBlocks");
  if (!pendingBlocks) return;
  await sendToHost({ type: "blocked", count: pendingBlocks });
  await chrome.storage.local.set({ pendingBlocks: 0 });
});

// A pause (granted by the app after the reflection and challenge) switches off only the
// hostname-keyword rule and the user's extra domains; the known adult sites stay blocked.
// It ends on its own at pausedUntil, even if the host is gone by then.
async function applyPause(pausedUntil) {
  const paused = pausedUntil > Date.now();
  await chrome.declarativeNetRequest.updateEnabledRulesets(
    paused ? { disableRulesetIds: [KEYWORD_RULESET] } : { enableRulesetIds: [KEYWORD_RULESET] });
  await chrome.storage.local.set({ pausedUntil: paused ? pausedUntil : 0 });
  if (paused) chrome.alarms.create(RESUME_ALARM, { when: pausedUntil });
  else chrome.alarms.clear(RESUME_ALARM);
  return paused;
}

async function sync() {
  try {
    const state = await sendToHost({ type: "hello", browser, version: chrome.runtime.getManifest().version });
    const paused = await applyPause(state.pausedUntil ?? 0);
    await applyExtraDomains(paused ? [] : state.extraDomains);
    await chrome.storage.local.set({ hostConnected: true, hostError: null });
    NwyHostStatus.show(true);
    await flushPendingBlocks();
  } catch (e) {
    // Host missing or failing: blocking keeps working from the static rules, and an expired
    // pause still ends locally.
    const { pausedUntil = 0 } = await chrome.storage.local.get("pausedUntil");
    if (pausedUntil && pausedUntil <= Date.now()) await applyPause(0);
    // The browser's exact message (e.g. "host not found") is what a support check needs.
    await chrome.storage.local.set({ hostConnected: false, hostError: `${new Date().toISOString()} ${e.message}` });
    NwyHostStatus.show(false);
    console.warn("Host nativo indisponível:", e.message);
  }
}

chrome.runtime.onInstalled.addListener(() => {
  // Every minute, so a pause or "resume now" in the app reaches the browser quickly.
  chrome.alarms.create(SYNC_ALARM, { periodInMinutes: 1 });
  sync();
});
chrome.runtime.onStartup.addListener(sync);
chrome.alarms.onAlarm.addListener(async (a) => {
  if (a.name === RESUME_ALARM) await applyPause(0);
  if (a.name === SYNC_ALARM || a.name === RESUME_ALARM) sync();
});

chrome.runtime.onMessage.addListener((message, sender) => {
  if (sender.id !== chrome.runtime.id) return;
  if (message?.type === "blocked")
    addPendingBlock().then(flushPendingBlocks).catch((e) => console.warn("Contador pendente:", e.message));
  else if (message?.type === "sync") sync(); // from extension pages (and tests)
});
