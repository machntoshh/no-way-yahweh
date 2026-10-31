// Privacy and secret scan of every file that would be published (git-tracked or, before the first
// commit, not ignored). Run it before every commit/push:
//   node tools/privacy-scan.mjs
// Your own identifiers (name, user name, e-mail…) must NOT be written in this public file. Pass them
// only through the environment, e.g. in PowerShell:
//   $env:PRIVACY_PATTERNS = 'meunome|meuusuario|meu@email'; node tools/privacy-scan.mjs
// Exit code 1 when anything is found.
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";

const files = execFileSync("git", ["ls-files", "--cached", "--others", "--exclude-standard"], { encoding: "utf8" })
  .split("\n").filter(Boolean)
  // licenses/ holds third-party notices copied verbatim (their authors' e-mails must stay, by license).
  .filter((f) => !f.startsWith("licenses/"));

const checks = [
  ["chave privada", /-----BEGIN [A-Z ]*PRIVATE KEY-----/],
  ["token GitHub", /\b(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}/],
  ["chave AWS", /\bAKIA[0-9A-Z]{16}\b/],
  ["chave de API (sk-)", /\bsk-[A-Za-z0-9_-]{20,}/],
  ["token Slack", /\bxox[abpr]-[A-Za-z0-9-]{10,}/],
  ["chave Google", /\bAIza[0-9A-Za-z_-]{35}\b/],
  ["senha em atribuição", /\b(password|passwd|pwd|senha)\s*[:=]\s*["'][^"'\s]{4,}["']/i],
  // A real Windows profile path (generic placeholders like <usuario> or %USERPROFILE% are fine).
  ["caminho de perfil do Windows", /[A-Za-z]:[\\/]+Users[\\/]+(?!<|%|Public\b|Default\b)[A-Za-z0-9._-]+/i],
  ["e-mail", /\b[A-Za-z0-9._%+-]+@(?!users\.noreply\.github\.com\b|example\.(com|org)\b)[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b/],
];
if (process.env.PRIVACY_PATTERNS) checks.push(["padrão pessoal (PRIVACY_PATTERNS)", new RegExp(process.env.PRIVACY_PATTERNS, "i")]);

const binary = /\.(png|ico|jpg|jpeg|gif|exe|dll|onnx|zip)$/i;
let findings = 0;
for (const file of files) {
  let text;
  try { text = readFileSync(file, binary.test(file) ? "latin1" : "utf8"); } catch { continue; }
  text.split(/\r?\n/).forEach((line, i) => {
    if (line.includes("privacy-scan: test value")) return; // deliberate fake values in tests
    for (const [name, re] of checks) {
      if (!re.test(line)) continue;
      findings++;
      console.log(`${file}:${i + 1}: ${name}`);
    }
  });
}
console.log(findings ? `\n${findings} achado(s) em ${files.length} arquivo(s).` : `Nenhum achado em ${files.length} arquivo(s).`);
process.exit(findings ? 1 : 0);
