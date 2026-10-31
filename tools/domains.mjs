// Parsing and validation of the domain list files (blocklist/*.txt).
// Shared by the rule generator and the tests so both read the list the same way.
import { readFileSync } from "node:fs";

const DOMAIN = /^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+(?:[a-z]{2,63}|xn--[a-z0-9-]{1,59})$/;

/** Lowercases, drops a leading "www." and returns null when the value is not a bare domain. */
export function normalizeDomain(value) {
  const d = value.trim().toLowerCase().replace(/^www\./, "");
  return DOMAIN.test(d) ? d : null;
}

/** Reads a list file; throws on any line that is not a valid bare domain. */
export function readDomainList(path) {
  const domains = new Set();
  readFileSync(path, "utf8").split(/\r?\n/).forEach((raw, i) => {
    const line = raw.trim();
    if (!line || line.startsWith("#")) return;
    const d = normalizeDomain(line);
    if (!d) throw new Error(`${path}:${i + 1}: domínio inválido "${line}"`);
    domains.add(d);
  });
  return [...domains].sort();
}
