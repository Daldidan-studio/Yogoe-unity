#!/usr/bin/env python3
"""Google Sheets(또는 로컬 CSV) → Assets/Resources/Dialogue/{character}_tutorial.{lang}.json

사용법:
  python3 Tools/export_dialogue.py
  python3 Tools/export_dialogue.py --csv Tools/sheets/okto.csv --character okto
  python3 Tools/export_dialogue.py --sheet-id ID --character okto

config: Tools/dialogue_sheets.config.json
  {
    "sheet_id": "...",
    "characters": ["okto"],
    "locales": ["ko", "en", "zh"],
    "fallback_locale": "ko"
  }
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
OUT_DIR = ROOT / "Assets" / "Resources" / "Dialogue"
CONFIG_PATH = ROOT / "Tools" / "dialogue_sheets.config.json"
VERSION = "1.0"
DEFAULT_LOCALES = ["ko", "en", "zh"]
DEFAULT_FALLBACK = "ko"
LANG_COL_RE = re.compile(r"^(text|label)_([a-z]{2}(?:-[a-z]+)?)$", re.I)


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        return {}
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))


def fetch_sheet_csv(sheet_id: str, character: str) -> str:
    query = urllib.parse.urlencode({"tqx": "out:csv", "sheet": character})
    url = f"https://docs.google.com/spreadsheets/d/{sheet_id}/gviz/tq?{query}"
    req = urllib.request.Request(url, headers={"User-Agent": "YogoeDialogueExport/1.0"})
    with urllib.request.urlopen(req, timeout=30) as res:
        raw = res.read()
    text = raw.decode("utf-8-sig")
    if "<html" in text.lower():
        raise RuntimeError(
            f"시트를 CSV로 읽지 못했습니다. 공유를 '링크 있는 모든 사용자: 뷰어'로 바꾸거나 "
            f"탭 이름({character})을 확인하세요.\nURL: {url}"
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
            found.add(m.group(2).lower())
    # bare text/label → ko
    if "text" in fields or "label" in fields:
        found.add("ko")
    locales = []
    for loc in configured:
        locales.append(loc)
    for loc in sorted(found):
        if loc not in locales:
            locales.append(loc)
    return locales or [DEFAULT_FALLBACK]


def read_rows_from_csv_text(text: str, configured_locales: list[str]) -> tuple[list[dict], list[str]]:
    reader = csv.DictReader(io.StringIO(text))
    if not reader.fieldnames:
        raise RuntimeError("CSV 헤더가 없습니다.")

    fields = {h.strip() for h in reader.fieldnames if h}
    required = {"type", "id", "order", "speaker", "narration", "option_id"}
    missing = required - fields
    if missing:
        raise RuntimeError(f"헤더 누락: {sorted(missing)}")

    has_text = "text" in fields or any(f.startswith("text_") for f in fields)
    has_label = "label" in fields or any(f.startswith("label_") for f in fields)
    if not has_text:
        raise RuntimeError("헤더 누락: text 또는 text_ko 등 필요")
    if not has_label:
        raise RuntimeError("헤더 누락: label 또는 label_ko 등 필요")

    locales = detect_locales(fields, configured_locales)

    rows = []
    for i, row in enumerate(reader, start=2):
        cleaned = {k.strip(): (v.strip() if isinstance(v, str) else v) for k, v in row.items() if k}
        typ = (cleaned.get("type") or "").lower()
        if not typ:
            continue
        rid = cleaned.get("id") or ""
        if not rid:
            raise RuntimeError(f"{i}행: id가 비어 있습니다.")
        cleaned["_row"] = i
        cleaned["type"] = typ
        cleaned["id"] = rid

        texts = {}
        labels = {}
        for loc in locales:
            texts[loc] = cell(cleaned, f"text_{loc}", "text" if loc == "ko" else "")
            labels[loc] = cell(cleaned, f"label_{loc}", "label" if loc == "ko" else "")
        # bare columns without suffix count as ko
        if "text" in cleaned and not texts.get("ko"):
            texts["ko"] = cell(cleaned, "text")
        if "label" in cleaned and not labels.get("ko"):
            labels["ko"] = cell(cleaned, "label")

        cleaned["_texts"] = texts
        cleaned["_labels"] = labels
        rows.append(cleaned)

    return rows, locales


def to_bool(v) -> bool:
    if isinstance(v, bool):
        return v
    return str(v or "").strip().upper() in {"TRUE", "1", "Y", "YES"}


def to_order(v) -> int:
    try:
        return int(float(str(v).strip() or 0))
    except ValueError:
        return 0


def pick_text(row: dict, locale: str, fallback: str) -> str:
    texts = row.get("_texts") or {}
    return texts.get(locale) or texts.get(fallback) or ""


def pick_label(row: dict, locale: str, fallback: str) -> str:
    labels = row.get("_labels") or {}
    return labels.get(locale) or labels.get(fallback) or ""


def build_json(character: str, rows: list[dict], locale: str, fallback: str) -> dict:
    line_rows = [r for r in rows if r["type"] == "line"]
    choice_rows = [r for r in rows if r["type"] == "choice"]
    other = [r for r in rows if r["type"] not in {"line", "choice"}]
    if other:
        bad = other[0]
        raise RuntimeError(f"{bad['_row']}행: type은 line 또는 choice 여야 합니다 ({bad['type']})")

    section_order: list[str] = []
    section_map: dict[str, list] = {}
    for r in sorted(line_rows, key=lambda r: r["_row"]):
        sid = r["id"]
        if sid not in section_map:
            section_map[sid] = []
            section_order.append(sid)
        section_map[sid].append(r)

    for sid in section_order:
        section_map[sid].sort(key=lambda r: (to_order(r.get("order")), r["_row"]))

    sections = [
        {
            "id": sid,
            "lines": [
                {
                    "speaker": r.get("speaker") or "",
                    "text": pick_text(r, locale, fallback),
                    "narration": to_bool(r.get("narration")),
                }
                for r in section_map[sid]
            ],
        }
        for sid in section_order
    ]

    choice_order: list[str] = []
    choice_map: dict[str, list] = {}
    for r in sorted(choice_rows, key=lambda r: r["_row"]):
        cid = r["id"]
        if cid not in choice_map:
            choice_map[cid] = []
            choice_order.append(cid)
        choice_map[cid].append(r)

    for cid in choice_order:
        choice_map[cid].sort(key=lambda r: (to_order(r.get("order")), r["_row"]))

    choices = [
        {
            "id": cid,
            "options": [
                {
                    "id": r.get("option_id") or "",
                    "label": pick_label(r, locale, fallback),
                }
                for r in choice_map[cid]
            ],
        }
        for cid in choice_order
    ]

    return {
        "version": VERSION,
        "character": character,
        "locale": locale,
        "sections": sections,
        "choices": choices,
    }


def locale_has_content(rows: list[dict], locale: str) -> bool:
    for r in rows:
        texts = r.get("_texts") or {}
        labels = r.get("_labels") or {}
        if texts.get(locale) or labels.get(locale):
            return True
    return False


def write_json(character: str, locale: str, data: dict) -> Path:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    path = OUT_DIR / f"{character}_tutorial.{locale}.json"
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


def export_rows(character: str, rows: list[dict], locales: list[str], fallback: str) -> list[Path]:
    written = []
    # 항상 fallback(ko)은 출력. 다른 언어는 해당 컬럼에 내용이 있을 때만.
    for loc in locales:
        if loc != fallback and not locale_has_content(rows, loc):
            continue
        data = build_json(character, rows, loc, fallback)
        written.append(write_json(character, loc, data))
    return written


def export_from_csv_file(
    csv_path: Path,
    character: str | None,
    locales: list[str],
    fallback: str,
) -> list[Path]:
    character = character or csv_path.stem
    rows, detected = read_rows_from_csv_text(csv_path.read_text(encoding="utf-8-sig"), locales)
    return export_rows(character, rows, detected, fallback)


def export_from_sheet(
    sheet_id: str,
    character: str,
    locales: list[str],
    fallback: str,
) -> list[Path]:
    text = fetch_sheet_csv(sheet_id, character)
    rows, detected = read_rows_from_csv_text(text, locales)
    return export_rows(character, rows, detected, fallback)


def main() -> int:
    parser = argparse.ArgumentParser(description="Export dialogue into Assets/Resources/Dialogue")
    parser.add_argument("--csv", type=Path, help="로컬 CSV 경로 (캐릭터 탭 1개분)")
    parser.add_argument("--sheet-id", help="Google Spreadsheet ID")
    parser.add_argument("--character", help="탭/캐릭터 이름 (예: okto)")
    parser.add_argument("--all", action="store_true", help="config의 characters 전부 export")
    args = parser.parse_args()

    config = load_config()
    sheet_id = args.sheet_id or config.get("sheet_id")
    locales = list(config.get("locales") or DEFAULT_LOCALES)
    fallback = config.get("fallback_locale") or DEFAULT_FALLBACK
    if fallback not in locales:
        locales.insert(0, fallback)

    characters = []
    if args.character:
        characters = [args.character]
    elif args.all or config.get("characters"):
        characters = list(config.get("characters") or [])

    written: list[Path] = []

    if args.csv:
        written.extend(export_from_csv_file(args.csv, args.character, locales, fallback))
    elif sheet_id and characters:
        for c in characters:
            written.extend(export_from_sheet(sheet_id, c, locales, fallback))
    elif sheet_id and not characters:
        print("캐릭터(탭) 이름이 필요합니다. --character okto 또는 config.characters", file=sys.stderr)
        return 1
    else:
        print(
            "사용법 예:\n"
            "  python3 Tools/export_dialogue.py --csv Tools/sheets/okto.csv\n"
            "  python3 Tools/export_dialogue.py --sheet-id ID --character okto\n"
            "config에 sheet_id / characters 를 넣으면: python3 Tools/export_dialogue.py",
            file=sys.stderr,
        )
        return 1

    # 구버전 단일 파일 정리 안내성 삭제 (ko로 대체)
    for path in written:
        data = json.loads(path.read_text(encoding="utf-8"))
        n_lines = sum(len(s.get("lines", [])) for s in data.get("sections", []))
        n_opts = sum(len(c.get("options", [])) for c in data.get("choices", []))
        print(
            f"Wrote {path.relative_to(ROOT)}  "
            f"(locale={data.get('locale')}, sections={len(data.get('sections', []))}, "
            f"lines={n_lines}, choice_options={n_opts})"
        )

    # legacy okto_tutorial.json 제거
    for character in {p.name.split("_tutorial.")[0] for p in written}:
        legacy = OUT_DIR / f"{character}_tutorial.json"
        legacy_meta = OUT_DIR / f"{character}_tutorial.json.meta"
        if legacy.exists():
            legacy.unlink()
            print(f"Removed legacy {legacy.relative_to(ROOT)}")
        if legacy_meta.exists():
            legacy_meta.unlink()
            print(f"Removed legacy {legacy_meta.relative_to(ROOT)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
