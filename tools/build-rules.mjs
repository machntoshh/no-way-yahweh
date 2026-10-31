// Generates the declarativeNetRequest static rulesets from blocklist/domains.txt and
// blocklist/allow.txt:
//   extension/rules/adult_domains.json — the known adult sites, always on
//   extension/rules/keyword.json       — hostname-keyword rule + its allow list; the only
//                                         ruleset a pause switches off (background.js)
//   node tools/build-rules.mjs
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { readDomainList } from "./domains.mjs";

const root = new URL("../", import.meta.url);
const path = (p) => fileURLToPath(new URL(p, root));

// Hostname contains one of these words (e.g. "free-porn-site.example").
export const HOST_KEYWORD_REGEX = "^[a-z]+://[^/?#]*(porn|hentai)[^/?#]*([/?#:]|$)";

const redirect = (reason) => ({ type: "redirect", redirect: { extensionPath: `/blocked.html?r=${reason}` } });

// Priorities (compared across all rulesets of the extension): allow list (3) > known
// domains (2) > hostname keyword (1), so a listed site always reports the more precise
// reason and allowed sites are never caught by the keyword.
export function buildDomainRules(blocked) {
  return [
    // Navigating to a listed domain (or any subdomain, or via redirect) shows the explanation page.
    { id: 1, priority: 2, action: redirect("list"), condition: { requestDomains: blocked, resourceTypes: ["main_frame"] } },
    // Content from listed domains embedded in other sites (images, video, iframes, scripts...) is dropped.
    { id: 2, priority: 2, action: { type: "block" }, condition: { requestDomains: blocked, excludedResourceTypes: ["main_frame"] } },
  ];
}

export function buildKeywordRules(allowed) {
  const rules = [
    // Unknown adult sites whose hostname gives them away.
    { id: 1, priority: 1, action: redirect("keyword"), condition: { regexFilter: HOST_KEYWORD_REGEX, resourceTypes: ["main_frame"] } },
  ];
  if (allowed.length) {
    // Without resourceTypes a rule skips main_frame, so the type must be explicit here.
    rules.push({ id: 2, priority: 3, action: { type: "allow" }, condition: { requestDomains: allowed, resourceTypes: ["main_frame"] } });
  }
  return rules;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const blocked = readDomainList(path("blocklist/domains.txt"));
  const allowed = readDomainList(path("blocklist/allow.txt"));
  const overlap = allowed.filter((d) => blocked.includes(d));
  if (overlap.length) throw new Error(`Domínio na lista de bloqueio e de permissão: ${overlap.join(", ")}`);
  const json = (rules) => JSON.stringify(rules, null, 2) + "\n";
  writeFileSync(path("extension/rules/adult_domains.json"), json(buildDomainRules(blocked)));
  writeFileSync(path("extension/rules/keyword.json"), json(buildKeywordRules(allowed)));
  console.log(`${blocked.length} domínios bloqueados, ${allowed.length} permitidos.`);
}
