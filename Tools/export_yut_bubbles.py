#!/usr/bin/env python3
"""Google Sheets(또는 로컬 CSV) → Assets/Resources/Yut/yut_bubbles.{locale}.json

사용법:
  python3 Tools/export_yut_bubbles.py
  python3 Tools/export_yut_bubbles.py --csv Tools/sheets/yut_bubbles.csv
  python3 Tools/export_yut_bubbles.py --sheet-id ID --tab yut_bubbles

config: Tools/yut_bubbles_sheets.config.json
  {
    "sheet_id": "...",
    "tab": "yut_bubbles",
    "locales": ["ko", "en", "zh"],
    "fallback_locale": "ko"
  }

시트 헤더: id, note(선택), text 또는 text_ko / text_en / ...
"""

from __future__ import annotations

import argparse
import csv
import io
import json
import re
import sys
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "Assets" / "Resources" / "Yut"
CONFIG_PATH = ROOT / "Tools" / "yut_bubbles_sheets.config.json"
VERSION = "1.0"
DEFAULT_LOCALES = ["ko", "en", "zh"]
DEFAULT_FALLBACK = "ko"
DEFAULT_TAB = "yut_bubbles"
LANG_COL_RE = re.compile(r"^text_([a-z]{2}(?:-[a-z]+)?)$", re.I)

# Catalog Ids 와 동일 — export 시 누락 경고용
KNOWN_IDS = [
    "rabbit.yut",
    "rabbit.mo",
    "rabbit.baekdo",
    "rabbit.steps",
    "candidate.treasure",
    "candidate.offering",
    "candidate.coin",
    "candidate.capture",
    "candidate.stack",
    "candidate.finish",
    "event.captured",
    "event.revived",
    "event.opponent_caught",
    "opponent.throw",
]


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        return {}
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))


def fetch_sheet_csv(sheet_id: str, tab: str) -> str:
    query = urllib.parse.urlencode({"tqx": "out:csv", "sheet": tab})
    url = f"https://docs.google.com/spreadsheets/d/{sheet_id}/gviz/tq?{query}"
    req = urllib.request.Request(url, headers={"User-Agent": "YogoeYutBubblesExport/1.0"})
    with urllib.request.urlopen(req, timeout=30) as res:
        raw = res.read()
    text = raw.decode("utf-8-sig")
    if "<html" in text.lower():
        raise RuntimeError(
            f"시트를 CSV로 읽지 못했습니다. 공유를 '링크 있는 모든 사용자: 뷰어'로 바꾸거나 "
            f"탭 이름({tab})을 확인하세요.\nURL: {url}"
        )
    return text


def cell(row: dict, *keys: str) -> str:
    for k in keys:
        v = row.get(k)
        if v is None:
            continue
        s = str(v).strip()
        if s:
            return s
    return ""


def detect_locales(fields: set[str], configured: list[str]) -> list[str]:
    found = set()
    for f in fields:
        m = LANG_COL_RE.match(f.strip())
        if m:
            found.add(m.group(1).lower())
    if "text" in fields:
        found.add("ko")
    locales: list[str] = []
    for loc in configured:
        locales.append(loc)
    for loc in sorted(found):
        if loc not in locales:
            locales.append(loc)
    return locales or [DEFAULT_FALLBACK]


def read_rows(text: str, configured_locales: list[str]) -> tuple[list[dict], list[str]]:
    reader = csv.DictReader(io.StringIO(text))
    if not reader.fieldnames:
        raise RuntimeError("CSV 헤더가 없습니다.")

    fields = {h.strip() for h in reader.fieldnames if h}
    if "id" not in fields:
        raise RuntimeError("헤더 누락: id")
    has_text = "text" in fields or any(LANG_COL_RE.match(f) for f in fields)
    if not has_text:
        raise RuntimeError("헤더 누락: text 또는 text_ko 등 필요")

    locales = detect_locales(fields, configured_locales)
    rows: list[dict] = []
    for i, row in enumerate(reader, start=2):
        cleaned = {k.strip(): (v.strip() if isinstance(v, str) else v) for k, v in row.items() if k}
        rid = (cleaned.get("id") or "").strip()
        if not rid:
            continue
        texts: dict[str, str] = {}
        for loc in locales:
            texts[loc] = cell(cleaned, f"text_{loc}", "text" if loc == "ko" else "")
        if "text" in cleaned and not texts.get("ko"):
            texts["ko"] = cell(cleaned, "text")
        cleaned["_row"] = i
        cleaned["id"] = rid
        cleaned["_texts"] = texts
        rows.append(cleaned)
    return rows, locales


def build_json(rows: list[dict], locale: str, fallback: str) -> dict:
    lines = []
    seen: set[str] = set()
    for r in rows:
        rid = r["id"]
        if rid in seen:
            raise RuntimeError(f"{r['_row']}행: id 중복 ({rid})")
        seen.add(rid)
        texts = r.get("_texts") or {}
        text = texts.get(locale) or texts.get(fallback) or ""
        lines.append({"id": rid, "text": text})

    missing = [k for k in KNOWN_IDS if k not in seen]
    unknown = [r["id"] for r in rows if r["id"] not in KNOWN_IDS]
    if missing:
        print(f"경고: 알려진 id 누락: {', '.join(missing)}", file=sys.stderr)
    if unknown:
        print(f"경고: Catalog에 없는 id (무시되지 않음, JSON에 포함): {', '.join(unknown)}", file=sys.stderr)

    return {
        "version": VERSION,
        "locale": locale,
        "lines": lines,
    }


def locale_has_content(rows: list[dict], locale: str) -> bool:
    for r in rows:
        texts = r.get("_texts") or {}
        if texts.get(locale):
            return True
    return False


def write_json(locale: str, data: dict) -> Path:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    path = OUT_DIR / f"yut_bubbles.{locale}.json"
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


def export_rows(rows: list[dict], locales: list[str], fallback: str) -> list[Path]:
    written = []
    for loc in locales:
        if loc != fallback and not locale_has_content(rows, loc):
            continue
        data = build_json(rows, loc, fallback)
        written.append(write_json(loc, data))
    return written


def main() -> int:
    parser = argparse.ArgumentParser(description="Export yut bubble lines into Assets/Resources/Yut")
    parser.add_argument("--csv", type=Path, help="로컬 CSV 경로")
    parser.add_argument("--sheet-id", help="Google Spreadsheet ID")
    parser.add_argument("--tab", help="시트 탭 이름 (기본 yut_bubbles)")
    args = parser.parse_args()

    config = load_config()
    sheet_id = args.sheet_id or config.get("sheet_id") or ""
    tab = args.tab or config.get("tab") or DEFAULT_TAB
    locales = list(config.get("locales") or DEFAULT_LOCALES)
    fallback = config.get("fallback_locale") or DEFAULT_FALLBACK
    if fallback not in locales:
        locales.insert(0, fallback)

    if args.csv:
        text = args.csv.read_text(encoding="utf-8-sig")
    elif sheet_id:
        text = fetch_sheet_csv(sheet_id, tab)
    else:
        default_csv = ROOT / "Tools" / "sheets" / "yut_bubbles.csv"
        if default_csv.exists():
            text = default_csv.read_text(encoding="utf-8-sig")
            print(f"sheet_id 없음 — {default_csv.relative_to(ROOT)} 사용")
        else:
            print(
                "사용법:\n"
                "  python3 Tools/export_yut_bubbles.py --csv Tools/sheets/yut_bubbles.csv\n"
                "  python3 Tools/export_yut_bubbles.py --sheet-id ID --tab yut_bubbles\n"
                "config에 sheet_id를 넣으면: python3 Tools/export_yut_bubbles.py",
                file=sys.stderr,
            )
            return 1

    rows, detected = read_rows(text, locales)
    if not rows:
        print("행이 없습니다.", file=sys.stderr)
        return 1

    written = export_rows(rows, detected, fallback)
    for path in written:
        data = json.loads(path.read_text(encoding="utf-8"))
        print(
            f"Wrote {path.relative_to(ROOT)}  "
            f"(locale={data.get('locale')}, lines={len(data.get('lines', []))})"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
