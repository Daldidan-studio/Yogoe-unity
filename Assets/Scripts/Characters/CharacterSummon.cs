using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 요괴 소환 (기획 9장). UI와 분리된 규칙·스폰만 담당.
    /// MVP: 빈 슬롯 → 향 3 → 고라니 고정. 소환 즉시 친밀도 0·기력 1.
    /// </summary>
    public static class CharacterSummon
    {
        public const int HyangCost = 3;
        public static readonly Vector3 DefaultGoraniSpawn = new Vector3(0f, -0.2f, 0f);
        public static readonly Color GoraniPlaceholderColor = new Color(0.45f, 0.85f, 1f, 1f);

        public static bool IsPresent(CharacterId id)
        {
            for (int i = 0; i < CharacterAgent.All.Count; i++)
            {
                var a = CharacterAgent.All[i];
                if (a != null && a.Data != null && a.Data.id == id) return true;
            }
            return false;
        }

        public static bool CanSummonGorani() =>
            !IsPresent(CharacterId.Gorani) && GameEconomy.Instance.Hyang >= HyangCost;

        /// <summary>Resources/Characters/Gorani 또는 인자로 넘긴 에셋의 런타임 사본(characters.json 적용).</summary>
        public static CharacterData ResolveGoraniData(CharacterData overrideData = null)
        {
            var data = overrideData != null
                ? overrideData
                : Resources.Load<CharacterData>("Characters/Gorani");
            return CharacterCatalog.RuntimeCopy(data);
        }

        /// <summary>향을 소모하고 고라니를 기본 위치에 스폰. 연출 없이 쓸 때.</summary>
        public static CharacterAgent TrySummonGorani(CharacterData goraniData, Font bubbleFont)
        {
            if (IsPresent(CharacterId.Gorani)) return null;
            if (!GameEconomy.Instance.TrySpendHyang(HyangCost)) return null;

            var agent = SpawnGorani(goraniData, bubbleFont, DefaultGoraniSpawn);
            if (agent == null)
            {
                GameEconomy.Instance.AddHyang(HyangCost);
                return null;
            }

            agent.ApplyFreshSummon();
            return agent;
        }

        /// <summary>비용 없이 스폰 (소환 연출·세이브 복원용). 호출측에서 향 소모.</summary>
        public static CharacterAgent SpawnGorani(CharacterData goraniData, Font bubbleFont, Vector3 pos)
        {
            if (IsPresent(CharacterId.Gorani)) return null;

            var data = ResolveGoraniData(goraniData);
            if (data == null)
            {
                Debug.LogError("[CharacterSummon] Gorani CharacterData가 없습니다. Resources/Characters/Gorani 를 확인하세요.");
                return null;
            }

            return CharacterSpawner.Spawn(data, pos, GoraniPlaceholderColor, bubbleFont);
        }

        /// <summary>세이브에만 있고 월드에 없는 고라니를 스폰 (향 소모 없음).</summary>
        public static CharacterAgent SpawnGoraniForSaveRestore(CharacterData goraniData, Font bubbleFont, Vector3 pos)
        {
            if (IsPresent(CharacterId.Gorani)) return Find(CharacterId.Gorani);

            var agent = SpawnGorani(goraniData, bubbleFont, pos);
            if (agent != null) agent.MarkStatsAppliedExternally();
            return agent;
        }

        public static CharacterAgent Find(CharacterId id)
        {
            for (int i = 0; i < CharacterAgent.All.Count; i++)
            {
                var a = CharacterAgent.All[i];
                if (a != null && a.Data != null && a.Data.id == id) return a;
            }
            return null;
        }
    }
}
