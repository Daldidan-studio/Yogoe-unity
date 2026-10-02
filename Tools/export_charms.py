#!/usr/bin/env python3
"""윷 완주 보상 부적 확률: 구글 시트 탭 charms ⇄ Assets/Resources/charms.json

시트 = 윷 말풍선 시트(Tools/yut_bubbles_sheets.config.json 의 sheet_id)의 탭 1개:
  charms  id, name, weight, note
          한 줄 = 부적 1종. 말 1개가 완주할 때 weight 비율로 1개가 나온다 (예: 전부 1이면 6종 균등).
          weight 0 = 안 나옴. id 는 코드와 연결 — 바꾸지 마세요.

사용법:
  python3 Tools/export_charms.py            # 시트 → charms.json   (npm run charms)
  python3 Tools/export_charms.py --csv      # Tools/sheets/charms.csv → charms.json
  python3 Tools/export_charms.py --to-csv   # charms.json → Tools/sheets/charms.csv
  python3 Tools/export_charms.py --push     # charms.json → CSV + 시트 탭 덮어쓰기 (npm run charms:push)
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_characters import load_config, push_tab, read_csv_text, write_csv  # noqa: E402
from export_yut_bubbles import fetch_sheet_csv  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
TAB = "charms"
HEADERS = ["id", "name", "weight", "note"]
JSON_PATH = ROOT / "Assets" / "Resources" / "charms.json"
CSV_PATH = ROOT / "Tools" / "sheets" / "charms.csv"
# CookingCharmType (None 제외) — CharmDropRates.All 과 같은 순서
CHARMS = [("PlusFive", "+5초"), ("Diagonal", "대각선"), ("Clairvoyance", "천리안"),
          ("Recycle", "회수"), ("Double", "몰빵"), ("Cancel", "나가리")]


def rows_to_json(rows: list[dict]) -> tuple[dict, list[str]]:
    known = dict(CHARMS)
    errors, seen, out = [], set(), []
    for r in rows:
        where = f"[{TAB}] {r['_row']}행"
        cid = r.get("id", "")
        if cid not in known:
            errors.append(f"{where}: 모르는 id '{cid}' (쓸 수 있는 id: {', '.join(known)})")
            continue
        if cid in seen:
            errors.append(f"{where}: id 중복 ({cid})")
            continue
        seen.add(cid)
        try:
            w = float(r.get("weight", "") or "0")
        except ValueError:
            errors.append(f"{where}: weight 는 숫자 ('{r.get('weight')}')")
            continue
        if w < 0:
            errors.append(f"{where}: weight 는 0 이상")
            continue
        out.append({"id": cid, "name": known[cid], "weight": w})
    if not any(c["weight"] > 0 for c in out):
        errors.append(f"[{TAB}] weight 가 0보다 큰 부적이 하나는 있어야 함")
    return {"charms": out}, errors


def json_rows() -> list[dict]:
    w = {}
    if JSON_PATH.exists():
        for c in json.loads(JSON_PATH.read_text(encoding="utf-8")).get("charms", []):
            w[c["id"]] = c.get("weight", 0)
    def fmt(v):
        return str(int(v)) if float(v).is_integer() else str(v)
    return [{"id": i, "name": n, "weight": fmt(w.get(i, 1))} for i, n in CHARMS]


def main() -> int:
    ap = argparse.ArgumentParser(description="완주 부적 확률 시트 ⇄ charms.json")
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--csv", action="store_true")
    mode.add_argument("--to-csv", action="store_true")
    mode.add_argument("--push", action="store_true")
    args = ap.parse_args()
    config = load_config()

    if args.to_csv or args.push:
        rows = json_rows()
        write_csv(CSV_PATH, HEADERS, rows)
        print(f"Wrote {CSV_PATH.relative_to(ROOT)} ({len(rows)}종)")
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
    data, errors = rows_to_json(read_csv_text(text, ["id", "weight"], TAB))
    if errors:
        print("시트 오류 — charms.json 을 쓰지 않았습니다:", file=sys.stderr)
        for e in errors:
            print("  " + e, file=sys.stderr)
        return 1
    JSON_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    total = sum(c["weight"] for c in data["charms"])
    print(f"Wrote {JSON_PATH.relative_to(ROOT)} — " + ", ".join(
        f"{c['name']} {c['weight'] / total * 100:.0f}%" for c in data["charms"] if c["weight"] > 0))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
