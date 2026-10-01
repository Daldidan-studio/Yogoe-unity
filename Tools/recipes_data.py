"""공양간 레시피 공용 데이터 — Assets/Resources/recipes.json (정본: 시트 recipes 탭, npm run recipes).

다른 도구(export_characters 선호 검증 등)는 여기서 레시피를 읽는다.
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RECIPES_JSON = ROOT / "Assets" / "Resources" / "recipes.json"

# CookingIngredientId 순서 (enum 이름, 한글 이름)
INGREDIENTS = [
    ("Water", "물"), ("Chili", "고추"), ("Rice", "쌀"), ("RedBean", "팥"), ("Fruit", "과실"),
    ("Namul", "산나물"), ("Herb", "약재"), ("Honey", "꿀"), ("Boar", "멧돼지고기"), ("Bird", "새고기"),
    ("Fish", "물고기"), ("Egg", "새알"), ("Oil", "기름"),
]
ING_KO = dict(INGREDIENTS)
ING_ORDER = [i for i, _ in INGREDIENTS]
KO_TO_ING = {ko: i for i, ko in INGREDIENTS}

KIND_KO = {"Food": "음식", "Offering": "공양물"}
KO_TO_KIND = {v: k for k, v in KIND_KO.items()}
KIND_INGREDIENTS = {"Food": 2, "Offering": 3}  # 19장: 음식 = 재료 2, 공양물 = 재료 3


def load_recipes() -> list[dict]:
    return json.loads(RECIPES_JSON.read_text(encoding="utf-8")).get("recipes", [])


def products() -> list[dict]:
    """결과물 id 순서대로 (같은 id의 조합은 combos 에 모음)."""
    order, info = [], {}
    for r in load_recipes():
        pid = r["id"]
        if pid not in info:
            order.append(pid)
            info[pid] = {"id": pid, "name": r["name"], "kind": r["kind"], "combos": []}
        info[pid]["combos"].append(sorted(r["ingredients"], key=ING_ORDER.index))
    return [info[p] for p in order]


def combo_text(ingredients: list[str]) -> str:
    return " + ".join(ING_KO[i] for i in sorted(ingredients, key=ING_ORDER.index))
