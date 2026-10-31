"""Extracts exact verse text from eBible.org chapter pages of the Bíblia Livre (BLIVRE, CC BY 4.0).

Download the chapters first (e.g. https://ebible.org/porbr2018/1CO06.htm) into a folder, then:

    python -I tools/verses/extract.py <folder> 1CO06:18 GAL05:22-23 ...

Prints one JSON object per reference: {"chapter": "1CO06", "verses": "18", "text": "..."}.
Text is copied verbatim (tags removed, entities decoded, whitespace collapsed); nothing is reworded.
"""
import html
import json
import re
import sys
from pathlib import Path

VERSE = re.compile(r'<span class="verse" id="V(\d+)">.*?</span>(.*?)(?=<span class="verse"|<div class="footnote|<ul class=.tnav|</div>\s*<div class=.chapterlabel|$)', re.S)


def verses(page):
    body = page.split('<div class="main">', 1)[-1]
    out = {}
    for number, raw in VERSE.findall(body):
        text = re.sub(r"<a [^>]*class=\"notemark\".*?</a>", "", raw, flags=re.S)
        text = html.unescape(re.sub(r"<[^>]+>", "", text))
        out[int(number)] = " ".join(text.split())
    return out


def main(folder, refs):
    sys.stdout.reconfigure(encoding="utf-8")  # -I ignores PYTHONIOENCODING
    for ref in refs:
        chapter, span = ref.split(":")
        first, _, last = span.partition("-")
        found = verses(Path(folder, f"{chapter}.htm").read_text(encoding="utf-8"))
        numbers = range(int(first), int(last or first) + 1)
        print(json.dumps({"chapter": chapter, "verses": span, "text": " ".join(found[n] for n in numbers)}, ensure_ascii=False))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
