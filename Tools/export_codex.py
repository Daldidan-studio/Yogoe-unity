#!/usr/bin/env python3
"""요리책(도감) 설명: 구글 시트 탭 codex ⇄ Assets/Resources/codex.json

시트 = 윷 말풍선 시트(Tools/yut_bubbles_sheets.config.json 의 sheet_id)의 탭 1개:
  codex  section, id, name, combo, description, note
         한 줄 = 도감 칸 1개 (재료 15 · 음식 36 · 공양물 24). 게임에 들어가는 건 description 뿐.
         id·name·combo 는 레시피(recipes 탭)에서 만든 참고용 — id 는 바꾸지 마세요.

사용법:
  python3 Tools/export_codex.py            # 시트 → codex.json   (npm run codex)
  python3 Tools/export_codex.py --csv      # Tools/sheets/codex.csv → codex.json
  python3 Tools/export_codex.py --to-csv   # codex.json + 레시피 표 → Tools/sheets/codex.csv
  python3 Tools/export_codex.py --push     # codex.json + 레시피 표 → CSV + 시트 탭 덮어쓰기 (npm run codex:push)
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_characters import load_config, push_tab, read_csv_text, write_csv  # noqa: E402
from export_yut_bubbles import fetch_sheet_csv  # noqa: E402
from recipes_data import INGREDIENTS as _INGREDIENTS, KIND_KO, combo_text, products  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
JSON_PATH = ROOT / "Assets" / "Resources" / "codex.json"
CSV_PATH = ROOT / "Tools" / "sheets" / "codex.csv"

TAB = "codex"
HEADERS = ["section", "id", "name", "combo", "description", "note"]

# 재료 칸 = CookingIngredientId 순서 + 특수 수집품 (CodexScreen 재료 칸 순서와 같다)
INGREDIENTS = _INGREDIENTS + [("GoldenRice", "황금쌀"), ("GoldenHoney", "황금꿀")]


def recipe_rows() -> list[dict]:
    """recipes.json(시트 recipes 탭) → 결과물별 한 줄 (조합 여러 개면 ' / ')."""
    return [{"section": KIND_KO[p["kind"]], "id": p["id"], "name": p["name"],
             "combo": " / ".join(combo_text(c) for c in p["combos"])} for p in products()]


def all_rows() -> list[dict]:
    rows = [{"section": "재료", "id": i, "name": n, "combo": ""} for i, n in INGREDIENTS]
    return rows + recipe_rows()


def rows_to_json(rows: list[dict]) -> tuple[dict, list[str]]:
    known = {r["id"] for r in all_rows()}
    errors, seen, entries = [], set(), []
    for r in rows:
        where = f"[{TAB}] {r['_row']}행"
        rid = r.get("id", "")
        if rid not in known:
            errors.append(f"{where}: 모르는 id '{rid}' (코드의 재료·레시피 id만)")
            continue
        if rid in seen:
            errors.append(f"{where}: id 중복 ({rid})")
            continue
        seen.add(rid)
        if r.get("description"):
            entries.append({"id": rid, "description": r["description"]})
    return {"entries": entries}, errors


def merged_rows() -> list[dict]:
    """레시피 표 순서대로, 설명은 codex.json 에서."""
    desc = {}
    if JSON_PATH.exists():
        for e in json.loads(JSON_PATH.read_text(encoding="utf-8")).get("entries", []):
            desc[e["id"]] = e.get("description", "")
    rows = all_rows()
    for r in rows:
        r["description"] = desc.get(r["id"], "")
    return rows


def main() -> int:
    ap = argparse.ArgumentParser(description="요리책 설명 시트 ⇄ codex.json")
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--csv", action="store_true")
    mode.add_argument("--to-csv", action="store_true")
    mode.add_argument("--push", action="store_true")
    args = ap.parse_args()
    config = load_config()

    if args.to_csv or args.push:
        rows = merged_rows()
        write_csv(CSV_PATH, HEADERS, rows)
        print(f"Wrote {CSV_PATH.relative_to(ROOT)} ({len(rows)}칸)")
        if args.push:
            if not config.get("write_url"):
                print("config 에 write_url 이 없습니다", file=sys.stderr)
                return 1
            push_tab(config, TAB, HEADERS, rows)
        return 0

    if args.csv:
        text = CSV_PATH.read_text(encoding="utf-8-sig")
    else:
        if not config.get("sheet_id"):
            print("config 에 sheet_id 가 없습니다.", file=sys.stderr)
            return 1
        text = fetch_sheet_csv(config["sheet_id"], TAB)
    data, errors = rows_to_json(read_csv_text(text, ["id"], TAB))
    if errors:
        print("시트 오류 — codex.json 을 쓰지 않았습니다:", file=sys.stderr)
        for e in errors:
            print("  " + e, file=sys.stderr)
        return 1
    JSON_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {JSON_PATH.relative_to(ROOT)} (설명 {len(data['entries'])}칸)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
