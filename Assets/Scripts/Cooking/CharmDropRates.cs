using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yoegoe.Cooking
{
    /// <summary>
    /// 윷 말 완주 보상 부적의 종류별 가중치 — 시트 charms 탭(Resources/charms.json, npm run charms).
    /// 가중치 합에 대한 비율이 확률 (예: 전부 1이면 6종 균등). 파일이 없거나 비면 6종 균등.
    /// </summary>
    public static class CharmDropRates
    {
        [Serializable] class Row { public string id; public float weight; }
        [Serializable] class File { public Row[] charms; }

        public const string ResourcePath = "charms";
        static readonly List<(CookingCharmType type, float weight)> table = new List<(CookingCharmType, float)>();
        static bool loaded;

        /// <summary>완주 보상 6종 (None 제외).</summary>
        public static readonly CookingCharmType[] All =
        {
            CookingCharmType.PlusFive, CookingCharmType.Diagonal, CookingCharmType.Clairvoyance,
            CookingCharmType.Recycle, CookingCharmType.Double, CookingCharmType.Cancel,
        };

        public static IReadOnlyList<(CookingCharmType type, float weight)> Table
        {
            get { Ensure(); return table; }
        }

        static void Ensure()
        {
            if (loaded) return;
            LoadFromJson(Resources.Load<TextAsset>(ResourcePath)?.text);
        }

        /// <summary>charms.json 내용으로 다시 채운다 (테스트·핫리로드). 모르는 id·음수는 건너뛴다.</summary>
        public static void LoadFromJson(string json)
        {
            loaded = true;
            table.Clear();
            var file = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<File>(json);
            if (file?.charms != null)
            {
                foreach (var r in file.charms)
                {
                    if (r == null || r.weight <= 0f) continue;
                    if (!Enum.TryParse(r.id, true, out CookingCharmType t) || t == CookingCharmType.None) continue;
                    table.Add((t, r.weight));
                }
            }
            if (table.Count == 0)
                foreach (var t in All) table.Add((t, 1f));
        }

        /// <summary>가중치대로 하나. random01 = [0,1).</summary>
        public static CookingCharmType Roll(float random01)
        {
            Ensure();
            float total = 0f;
            foreach (var (_, w) in table) total += w;
            float pick = Mathf.Clamp01(random01) * total;
            foreach (var (t, w) in table)
            {
                if (pick < w) return t;
                pick -= w;
            }
            return table[table.Count - 1].type;
        }
    }
}
