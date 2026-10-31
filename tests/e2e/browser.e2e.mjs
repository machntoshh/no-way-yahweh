// End-to-end test in a real browser (Edge or Brave): loads the unpacked extension, registers the
// built native host against a temporary data folder, and checks real navigations.
//   dotnet build && node tests/e2e/browser.e2e.mjs edge|brave
// Registry: points the browser's HKCU ...\NativeMessagingHosts\com.nowayyahweh.host key at a
// temporary manifest for the duration of the run and restores the previous value afterwards.
// No content is fetched from blocked sites: the rules redirect before any request is sent.
import { spawn, execFileSync } from "node:child_process";
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import assert from "node:assert/strict";

const root = fileURLToPath(new URL("../../", import.meta.url));
const BROWSERS = {
  edge: { exe: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", key: "HKCU\\Software\\Microsoft\\Edge" },
  brave: { exe: "C:\\Program Files\\BraveSoftware\\Brave-Browser\\Application\\brave.exe", key: process.env.NWY_BRAVE_KEY ?? "HKCU\\Software\\BraveSoftware\\Brave-Browser" },
};
const NAME = process.argv[2] ?? "edge";
const BROWSER = BROWSERS[NAME];
if (!BROWSER) { console.error(`Uso: node tests/e2e/browser.e2e.mjs ${Object.keys(BROWSERS).join("|")}`); process.exit(1); }
const HOST_EXE = join(root, "artifacts", "bin", "Debug", "NoWayYahweh.Host.exe");
const REG_KEY = `${BROWSER.key}\\NativeMessagingHosts\\com.nowayyahweh.host`;
const EXT_ID = "olipaknkbpadbdcdaaehfnpolcdeieee";
const PORT = 9333;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// --- local test site: a page with embedded images and a redirect to a listed domain
const site = createServer((req, res) => {
  if (req.url === "/redirect") return res.writeHead(302, { Location: "https://xhamster.com/" }).end();
  if (req.url === "/ok.svg") return res.writeHead(200, { "Content-Type": "image/svg+xml" }).end('<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"/>');
  res.writeHead(200, { "Content-Type": "text/html" }).end(`<!doctype html><title>teste</title>
    <img id="bad" src="https://www.pornhub.com/favicon.ico"><img id="good" src="/ok.svg">`);
}).listen(0, "127.0.0.1");
await new Promise((r) => site.once("listening", r));
const local = `http://127.0.0.1:${site.address().port}`;

// --- isolated data folder + host registration
const data = mkdtempSync(join(tmpdir(), "nwy-e2e-data-"));
const profile = mkdtempSync(join(tmpdir(), "nwy-e2e-profile-"));
writeFileSync(join(data, "extra-domains.txt"), "example.org\n");
const manifestPath = join(data, "host.json");
writeFileSync(manifestPath, JSON.stringify({ name: "com.nowayyahweh.host", description: "e2e", path: HOST_EXE, type: "stdio", allowed_origins: [`chrome-extension://${EXT_ID}/`] }));

// A browser left over from an earlier run would answer on the port with old rules.
const portBusy = await fetch(`http://127.0.0.1:${PORT}/json/version`).then(() => true, () => false);
if (portBusy) { console.error(`Porta ${PORT} já em uso — feche o navegador de teste anterior.`); process.exit(1); }

let previous = null;
try { previous = execFileSync("reg", ["query", REG_KEY, "/ve"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).match(/REG_SZ\s+(.+)/)?.[1]?.trim() ?? null; } catch {}
execFileSync("reg", ["add", REG_KEY, "/ve", "/d", manifestPath, "/f"]);

spawn(BROWSER.exe, [
  "--headless=new", `--user-data-dir=${profile}`, `--remote-debugging-port=${PORT}`,
  `--load-extension=${join(root, "extension")}`, "--no-first-run", "--no-default-browser-check", "about:blank",
], { env: { ...process.env, NOWAYYAHWEH_DATA_DIR: data }, stdio: "ignore" });

// --- minimal CDP client
async function cdp(wsUrl) {
  const ws = new WebSocket(wsUrl);
  await new Promise((r, j) => { ws.onopen = r; ws.onerror = j; });
  let id = 0; const pending = new Map();
  ws.onmessage = (e) => { const m = JSON.parse(e.data); pending.get(m.id)?.(m); pending.delete(m.id); };
  const send = (method, params = {}) => new Promise((r) => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
  return { send, close: () => ws.close() };
}

const results = [];
async function check(name, fn) {
  try { await fn(); results.push(["ok", name]); }
  catch (e) { results.push(["FALHOU", `${name}: ${e.message}`]); }
}

try {
  let version;
  for (let i = 0; i < 50 && !version; i++) {
    try { version = await (await fetch(`http://127.0.0.1:${PORT}/json/version`)).json(); } catch { await sleep(200); }
  }
  assert.ok(version, `${NAME} não abriu a porta de depuração`);

  // Wait until the extension's worker has synced with the host (dynamic rule for example.org).
  const browser = await cdp(version.webSocketDebuggerUrl);
  let synced = false;
  for (let i = 0; i < 50 && !synced; i++) {
    try { synced = Object.keys(JSON.parse(readFileSync(join(data, "state.json"), "utf8")).BrowsersLastSeen).includes(NAME); } catch {}
    if (!synced) await sleep(200);
  }

  const { result: { targetId } } = await browser.send("Target.createTarget", { url: "about:blank" });
  const targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
  const page = await cdp(targets.find((t) => t.id === targetId).webSocketDebuggerUrl);
  await page.send("Page.enable");
  const visit = async (url) => {
    await page.send("Page.navigate", { url });
    await sleep(1500);
    return (await page.send("Runtime.evaluate", { expression: "location.href" })).result.result.value;
  };
  const blockedPage = `chrome-extension://${EXT_ID}/blocked.html`;

  await check(`host recebeu hello identificado como "${NAME}"`, () => assert.ok(synced));
  await check("domínio da lista é bloqueado", async () => assert.equal(await visit("https://www.pornhub.com/"), `${blockedPage}?r=list`));
  await check("subdomínio é bloqueado", async () => assert.equal(await visit("https://pt.xvideos.com/"), `${blockedPage}?r=list`));
  await check("domínio sem www é bloqueado", async () => assert.equal(await visit("https://nhentai.net/g/1"), `${blockedPage}?r=list`));
  await check("redirecionamento para domínio da lista é bloqueado", async () => assert.equal(await visit(`${local}/redirect`), `${blockedPage}?r=list`));
  await check("palavra-chave no nome do site é bloqueada", async () => assert.equal(await visit("https://free-porn-videos.example/"), `${blockedPage}?r=keyword`));
  await check("domínio extra do usuário é bloqueado (host → regra dinâmica)", async () => assert.equal(await visit("https://example.org/"), `${blockedPage}?r=list`));
  await check("site de ajuda da lista de permissão não é pego pela palavra-chave", async () =>
    assert.ok(!(await visit("https://www.yourbrainonporn.com/")).startsWith(blockedPage)));
  await check("site comum não é bloqueado", async () => assert.equal(await visit("https://example.com/"), "https://example.com/"));
  await check("palavra na busca não bloqueia", async () => assert.equal(await visit(`${local}/?q=porn`), `${local}/?q=porn`));
  await check("imagem embutida de domínio da lista é bloqueada; imagem comum carrega", async () => {
    await visit(`${local}/`);
    const { result } = await page.send("Runtime.evaluate", { expression: "[bad.complete && bad.naturalWidth, good.complete && good.naturalWidth]", returnByValue: true });
    assert.deepEqual(result.result.value, [0, 4]);
  });
  await check("imagens de sites comuns não são alteradas (sem filtro de imagens)", async () => {
    const { result } = await page.send("Runtime.evaluate", { expression: "[getComputedStyle(good).filter, good.hasAttribute('data-nwy')]", returnByValue: true });
    assert.deepEqual(result.result.value, ["none", false]);
  });
  await check("página de bloqueio mostra versículo da Bíblia Livre com atribuição", async () => {
    await visit("https://www.pornhub.com/");
    const { result } = await page.send("Runtime.evaluate", { expression: "[document.getElementById('verse-text').textContent, document.getElementById('verse-ref').textContent, document.getElementById('attribution').textContent]", returnByValue: true });
    const [text, ref, credit] = result.result.value;
    assert.ok(text.length > 20, "sem versículo");
    assert.match(ref, /\(BLIVRE\)$/);
    assert.match(credit, /CC BY 4\.0/);
  });

  // --- Fase 3: pause granted by the app (written to state.json as the app does) and resume.
  const setPause = async (until) => {
    const file = join(data, "state.json");
    const state = JSON.parse(readFileSync(file, "utf8"));
    state.PausedUntil = until;
    writeFileSync(file, JSON.stringify(state));
    await visit(`${blockedPage}?r=list`); // an extension page can ask the worker to sync now
    await page.send("Runtime.evaluate", { expression: `chrome.runtime.sendMessage({ type: "sync" })` });
    await sleep(1500);
  };
  await setPause(new Date(Date.now() + 10 * 60_000).toISOString());
  await check("pausa libera a regra de palavra-chave", async () => {
    const url = await visit("https://another-porn-site.example/");
    assert.ok(!url.startsWith(blockedPage), url);
  });
  await check("pausa libera os domínios extras", async () => assert.equal(await visit("https://example.org/"), "https://example.org/"));
  await check("durante a pausa, sites conhecidos continuam bloqueados", async () =>
    assert.equal(await visit("https://www.pornhub.com/"), `${blockedPage}?r=list`));
  await setPause(null);
  await check("reativar volta a bloquear palavra-chave e domínios extras", async () => {
    assert.equal(await visit("https://free-porn-videos.example/"), `${blockedPage}?r=keyword`);
    assert.equal(await visit("https://example.org/"), `${blockedPage}?r=list`);
  });

  await sleep(1500);
  await check("bloqueios contados no host, sem gravar URLs", () => {
    const raw = readFileSync(join(data, "state.json"), "utf8");
    assert.ok(JSON.parse(raw).BlockCount >= 6, `BlockCount=${JSON.parse(raw).BlockCount}`);
    assert.ok(!/pornhub|xvideos|example\.org|127\.0\.0\.1/.test(raw), "state.json contém endereço");
  });

  page.close();
  await browser.send("Browser.close");
  browser.close();
} catch (e) {
  results.push(["FALHOU", e.message]);
} finally {
  // The browser detaches child processes from the launcher, so a tree kill is not enough:
  // stop every process started with this test profile.
  const ps = `Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*${profile}*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }`;
  try { execFileSync("powershell", ["-NoProfile", "-Command", ps], { stdio: "ignore" }); } catch {}
  site.close();
  if (previous) execFileSync("reg", ["add", REG_KEY, "/ve", "/d", previous, "/f"]);
  else execFileSync("reg", ["delete", REG_KEY, "/f"]);
  await sleep(1000);
  for (const d of [profile, data]) {
    try { rmSync(d, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 }); } catch { console.warn("Não removido:", d); }
  }
}

for (const [s, n] of results) console.log(`${s.padEnd(6)} ${n}`);
process.exit(results.some(([s]) => s !== "ok") ? 1 : 0);
