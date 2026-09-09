using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 캐릭터 기획 카탈로그. 소스: Resources/characters.json
    /// 이름·선호공양·엔딩기물·설명·혼잣말은 JSON, 스프라이트는 CharacterData SO.
    /// </summary>
    public static class CharacterCatalog
    {
        [Serializable]
        public class PreferredOffering
        {
            public string id;
            public string name;
        }

        [Serializable]
        public class Entry
        {
            public string id;
            public string displayName;
            public string startingStage;
            public PreferredOffering[] preferredOfferings;
            public string endingPropId;
            public string detailDescription;
            public string[] monologueLines;

            public bool TryParseId(out CharacterId characterId)
                => Enum.TryParse(id, ignoreCase: true, out characterId);

            public bool TryParseStage(out GrowthStage stage)
                => Enum.TryParse(startingStage, ignoreCase: true, out stage);

            public string FormatPreferredNames()
            {
                if (preferredOfferings == null || preferredOfferings.Length == 0)
                    return "";
                var sb = new StringBuilder();
                for (int i = 0; i < preferredOfferings.Length; i++)
                {
                    var p = preferredOfferings[i];
                    if (p == null) continue;
                    string label = !string.IsNullOrEmpty(p.name) ? p.name : p.id;
                    if (string.IsNullOrEmpty(label)) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(label);
                }
                return sb.ToString();
            }
        }

        [Serializable]
        class Root
        {
            public Entry[] characters;
        }

        static Dictionary<CharacterId, Entry> _byId;
        static OfferingData[] _offerings;
        static bool _loggedMissingAsset;

        public static void EnsureLoaded()
        {
            if (_byId != null) return;

            _byId = new Dictionary<CharacterId, Entry>();
            var text = Resources.Load<TextAsset>("characters");
            if (text == null)
            {
                Debug.LogError("[CharacterCatalog] Resources/characters.json 을 찾지 못했습니다.");
                return;
            }

            var root = JsonUtility.FromJson<Root>(text.text);
            if (root?.characters == null)
            {
                Debug.LogError("[CharacterCatalog] characters.json 파싱 실패.");
                return;
            }

            foreach (var entry in root.characters)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (!entry.TryParseId(out var cid))
                {
                    Debug.LogWarning($"[CharacterCatalog] 알 수 없는 id: {entry.id}");
                    continue;
                }
                _byId[cid] = entry;
            }
        }

        public static void SetOfferings(OfferingData[] offerings) => _offerings = offerings;

        public static bool TryGet(CharacterId id, out Entry entry)
        {
            EnsureLoaded();
            if (_byId != null && _byId.TryGetValue(id, out entry)) return true;
            entry = null;
            return false;
        }

        public static Entry Get(CharacterId id)
        {
            TryGet(id, out var entry);
            return entry;
        }

        public static void ApplyTo(CharacterData data)
        {
            if (data == null) return;
            EnsureLoaded();
            if (!TryGet(data.id, out var entry) || entry == null) return;

            if (!string.IsNullOrEmpty(entry.displayName))
                data.displayName = entry.displayName;

            if (entry.TryParseStage(out var stage))
                data.startingStage = stage;

            if (entry.detailDescription != null)
                data.detailDescription = entry.detailDescription;

            data.monologueLines = entry.monologueLines != null
                ? (string[])entry.monologueLines.Clone()
                : Array.Empty<string>();

            data.preferredOfferings = ResolveOfferings(entry.preferredOfferings);
        }

        public static OfferingData FindOffering(string offeringId)
        {
            if (string.IsNullOrEmpty(offeringId) || _offerings == null) return null;
            for (int i = 0; i < _offerings.Length; i++)
            {
                var o = _offerings[i];
                if (o == null) continue;
                if (string.Equals(o.offeringId, offeringId, StringComparison.OrdinalIgnoreCase))
                    return o;
            }
            return null;
        }

        static OfferingData[] ResolveOfferings(PreferredOffering[] prefs)
        {
            if (prefs == null || prefs.Length == 0) return Array.Empty<OfferingData>();
            if (_offerings == null || _offerings.Length == 0)
            {
                if (!_loggedMissingAsset)
                {
                    Debug.LogWarning("[CharacterCatalog] Offering 테이블이 비어 있습니다.");
                    _loggedMissingAsset = true;
                }
                return Array.Empty<OfferingData>();
            }

            var list = new List<OfferingData>(prefs.Length);
            foreach (var pref in prefs)
            {
                if (pref == null || string.IsNullOrEmpty(pref.id)) continue;
                var found = FindOffering(pref.id);
                if (found != null) list.Add(found);
                else
                    Debug.LogWarning($"[CharacterCatalog] 공양물 에셋 없음: {pref.id}");
            }
            return list.ToArray();
        }
    }
}
