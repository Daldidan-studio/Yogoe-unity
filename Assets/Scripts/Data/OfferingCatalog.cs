using System;
using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Cooking;

namespace Yoegoe.Data
{
    /// <summary>
    /// 게임 전체 음식·공양물 목록 — 공양간 레시피(시트 recipes)마다 런타임 OfferingData를 만들어 채운다
    /// (에셋은 그림만 빌려 쓴다, 물은 에셋 그대로).
    /// 요리로 만든 것도 인벤·상세 급여·선호 판정에 그대로 쓰이게 하기 위함.
    ///
    /// <see cref="RandomPool"/> = 3차 공양물 24종 — 상점 진열·윷 보물상자·선물꾸러미가 여기서 뽑는다.
    /// </summary>
    public static class OfferingCatalog
    {
        static readonly List<OfferingData> all = new List<OfferingData>();
        static readonly List<OfferingData> pool = new List<OfferingData>();
        static readonly Dictionary<string, OfferingData> byId =
            new Dictionary<string, OfferingData>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<OfferingData> All => all;
        public static IReadOnlyList<OfferingData> RandomPool => pool;

        /// <summary>
        /// 카탈로그를 (다시) 만든다. 반환값 = All 배열.
        /// - 요리: 레시피(시트 recipes)마다 런타임 항목 — 이름·종류는 항상 레시피를 따른다.
        ///   같은 id의 에셋(Assets/Data/Offerings)이 있으면 그림(icon)만 빌려 쓴다.
        /// - 물: 물 에셋 그대로.
        /// - 레시피에 없는 옛 에셋은 넣지 않는다.
        /// </summary>
        public static OfferingData[] Build(OfferingData[] assets)
        {
            all.Clear();
            pool.Clear();
            byId.Clear();

            var iconById = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (assets != null)
            {
                foreach (var o in assets)
                {
                    if (o == null || string.IsNullOrEmpty(o.offeringId)) continue;
                    if (o.IsWater)
                    {
                        if (byId.ContainsKey(o.offeringId)) continue;
                        byId[o.offeringId] = o;
                        all.Add(o);
                    }
                    else if (o.icon != null)
                        iconById[o.offeringId] = o.icon;
                }
            }

            foreach (var r in CookingRecipeCatalog.Recipes)
            {
                if (!byId.TryGetValue(r.Id, out var o))
                {
                    o = ScriptableObject.CreateInstance<OfferingData>();
                    o.name = "Runtime_" + r.Id;
                    o.hideFlags = HideFlags.DontSave;
                    o.offeringId = r.Id;
                    o.displayName = r.DisplayName;
                    o.kind = r.Kind == CookingResultKind.Food ? OfferingKind.Food : OfferingKind.General;
                    iconById.TryGetValue(r.Id, out o.icon);
                    byId[r.Id] = o;
                    all.Add(o);
                }
                if (r.Kind == CookingResultKind.Offering && !pool.Contains(o))
                    pool.Add(o);
                if (r.Kind == CookingResultKind.Food) AddGoldenVariant(o);
            }

            return all.ToArray();
        }

        public const string GoldenSuffix = "_golden";

        /// <summary>음식 id → 황금음식 id ("bap" → "bap_golden").</summary>
        public static string GoldenIdOf(string foodId) => foodId + GoldenSuffix;

        public static OfferingData FindGolden(string foodId) =>
            string.IsNullOrEmpty(foodId) ? null : Find(GoldenIdOf(foodId));

        static void AddGoldenVariant(OfferingData food)
        {
            string id = GoldenIdOf(food.offeringId);
            if (byId.ContainsKey(id)) return;
            var g = ScriptableObject.CreateInstance<OfferingData>();
            g.name = "Runtime_" + id;
            g.hideFlags = HideFlags.DontSave;
            g.offeringId = id;
            g.displayName = "황금 " + food.displayName;
            g.kind = OfferingKind.Food;
            g.icon = food.icon;
            g.staminaGain = food.staminaGain;
            g.golden = true;
            g.baseOfferingId = food.offeringId;
            byId[id] = g;
            all.Add(g);
        }

        public static OfferingData Find(string offeringId)
        {
            if (string.IsNullOrEmpty(offeringId)) return null;
            return byId.TryGetValue(offeringId, out var o) ? o : null;
        }
    }
}
