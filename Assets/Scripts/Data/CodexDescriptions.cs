using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 요리책 칸 설명 — 시트 codex 탭(Resources/codex.json, npm run codex). 기획자가 채운다.
    /// id = 재료(Water·Rice… GoldenRice·GoldenHoney) 또는 레시피 결과물 id(bap, yukjeon…).
    /// </summary>
    public static class CodexDescriptions
    {
        [Serializable] class Entry { public string id; public string description; }
        [Serializable] class Root { public Entry[] entries; }

        static Dictionary<string, string> map;

        public static string Get(string id)
        {
            Ensure();
            return !string.IsNullOrEmpty(id) && map.TryGetValue(id, out var d) ? d : "";
        }

        /// <summary>테스트·핫리로드용.</summary>
        public static void LoadFromJson(string json)
        {
            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json)) return;
            var root = JsonUtility.FromJson<Root>(json);
            if (root?.entries == null) return;
            foreach (var e in root.entries)
                if (e != null && !string.IsNullOrEmpty(e.id)) map[e.id] = e.description ?? "";
        }

        static void Ensure()
        {
            if (map != null) return;
            LoadFromJson(Resources.Load<TextAsset>("codex")?.text);
        }
    }
}
