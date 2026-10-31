// node --test tests/extension
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { readDomainList, normalizeDomain } from "../../tools/domains.mjs";
import { buildDomainRules, buildKeywordRules, HOST_KEYWORD_REGEX } from "../../tools/build-rules.mjs";

const p = (rel) => fileURLToPath(new URL(`../../${rel}`, import.meta.url));
const blocked = readDomainList(p("blocklist/domains.txt"));
const allowed = readDomainList(p("blocklist/allow.txt"));
const domainRules = JSON.parse(readFileSync(p("extension/rules/adult_domains.json"), "utf8"));
const keywordRules = JSON.parse(readFileSync(p("extension/rules/keyword.json"), "utf8"));
const rules = [...domainRules, ...keywordRules];
const manifest = JSON.parse(readFileSync(p("extension/manifest.json"), "utf8"));

test("generated rulesets are up to date with the lists", () => {
  assert.deepEqual(domainRules, buildDomainRules(blocked), "rode: node tools/build-rules.mjs");
  assert.deepEqual(keywordRules, buildKeywordRules(allowed), "rode: node tools/build-rules.mjs");
});

test("manifest loads the generated ruleset", () => {
  const res = manifest.declarative_net_request.rule_resources;
  assert.deepEqual(res.map((r) => [r.id, r.path, r.enabled]), [
    ["adult_domains", "rules/adult_domains.json", true],
    ["keyword", "rules/keyword.json", true],
  ]);
});

test("a pause can only switch off the keyword ruleset; known sites live in the other one", () => {
  assert.match(readFileSync(p("extension/background.js"), "utf8"), /const KEYWORD_RULESET = "keyword";/);
  assert.ok(domainRules.every((r) => r.condition.requestDomains === undefined || r.condition.requestDomains === blocked || r.condition.requestDomains.length === blocked.length));
  assert.ok(keywordRules.every((r) => !r.condition.requestDomains?.some((d) => blocked.includes(d))));
});

test("private windows can show the explanation page", () => {
  // "spanning" forbids extension pages in incognito tabs, so the redirect would end in ERR_BLOCKED_BY_CLIENT.
  assert.equal(manifest.incognito, "split");
});

test("domains required by the spec are listed", () => {
  for (const d of ["pornhub.com", "xvideos.com", "xnxx.com", "nhentai.net", "xhamster.com", "redtube.com"]) {
    assert.ok(blocked.includes(d), d);
  }
});

test("lists are disjoint and contain no subdomain of another entry", () => {
  assert.equal(allowed.filter((d) => blocked.includes(d)).length, 0);
  const redundant = blocked.filter((d) => blocked.some((o) => o !== d && d.endsWith("." + o)));
  assert.deepEqual(redundant, [], "subdomínios já são cobertos pelo domínio pai");
});

test("rule ids are unique and every navigation rule redirects to the explanation page", () => {
  for (const set of [domainRules, keywordRules]) assert.equal(new Set(set.map((r) => r.id)).size, set.length);
  for (const r of rules.filter((r) => r.action.type === "redirect")) {
    assert.match(r.action.redirect.extensionPath, /^\/blocked\.html\?r=(list|keyword)$/);
    assert.deepEqual(r.condition.resourceTypes, ["main_frame"]);
  }
});

test("every rule states its resource types (default would silently skip main_frame)", () => {
  for (const r of rules) assert.ok(r.condition.resourceTypes || r.condition.excludedResourceTypes, `regra ${r.id}`);
});

test("common sites are never in the block list", () => {
  for (const d of ["google.com", "youtube.com", "wikipedia.org", "instagram.com", "x.com", "reddit.com", "bing.com"]) {
    assert.ok(!blocked.some((b) => d === b || d.endsWith("." + b)), d);
  }
});

test("hostname keyword rule matches the host, never the path or query", () => {
  const re = new RegExp(HOST_KEYWORD_REGEX);
  assert.ok(re.test("https://free-porn-tube.example/"));
  assert.ok(re.test("https://hentaisite.example:8080/x"));
  assert.ok(re.test("http://x.pornsite.example"));
  assert.ok(!re.test("https://www.google.com/search?q=porn"));
  assert.ok(!re.test("https://en.wikipedia.org/wiki/Pornography_addiction"));
  assert.ok(!re.test("https://example.com/#porn"));
});

test("normalizeDomain", () => {
  assert.equal(normalizeDomain(" WWW.Example.com "), "example.com");
  assert.equal(normalizeDomain("https://example.com"), null);
  assert.equal(normalizeDomain("example.com/a"), null);
  assert.equal(normalizeDomain("localhost"), null);
});
