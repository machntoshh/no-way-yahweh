// Explanation page shown instead of a blocked site. Verses come from verses.json
// (Bíblia Livre, CC BY 4.0 — built by tools/verses/build.mjs).

const REASONS = {
  list: "Este site foi bloqueado porque está na lista de sites com conteúdo sexual explícito.",
  keyword: "Este endereço foi bloqueado porque o próprio nome do site indica conteúdo sexual explícito.",
};

const reason = new URLSearchParams(location.search).get("r");
if (REASONS[reason]) document.getElementById("reason").textContent = REASONS[reason];

fetch(chrome.runtime.getURL("verses.json"))
  .then((r) => r.json())
  .then(({ attribution, verses }) => {
    const verse = verses[Math.floor(Math.random() * verses.length)];
    document.getElementById("verse-text").textContent = verse.text;
    document.getElementById("verse-ref").textContent = `${verse.reference} (BLIVRE)`;
    document.getElementById("reflection").textContent = verse.reflection;
    document.getElementById("attribution").textContent = attribution;
  });

document.getElementById("back").addEventListener("click", () => {
  if (history.length > 1) history.back();
  else location.href = "about:blank";
});

chrome.runtime.sendMessage({ type: "blocked" }).catch(() => {});
