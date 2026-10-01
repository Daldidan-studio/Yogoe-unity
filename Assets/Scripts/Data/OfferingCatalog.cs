using System;
using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Cooking;

namespace Yoegoe.Data
{
    /// <summary>
    /// 게임 전체 음식·공양물 목록. 에셋(Assets/Data/Offerings)이 있으면 에셋, 없으면
    /// 공양간 레시피(음식 36·공양물 24)마다 런타임 OfferingData를 만들어 채운다.
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

        /// <summary>에셋 목록 + 레시피 결과물로 카탈로그를 (다시) 만든다. 반환값 = All 배열.</summary>
        public static OfferingData[] Build(OfferingData[] assets)
        {
            all.Clear();
            pool.Clear();
            byId.Clear();

            if (assets != null)
            {
                foreach (var o in assets)
                {
                    if (o == null || string.IsNullOrEmpty(o.offeringId) || byId.ContainsKey(o.offeringId)) continue;
                    byId[o.offeringId] = o;
                    all.Add(o);
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
