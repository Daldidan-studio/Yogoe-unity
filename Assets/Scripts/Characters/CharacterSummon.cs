using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 요괴 소환 (기획 9장). UI와 분리된 규칙·스폰만 담당.
    /// - 3번째 빈 슬롯 → 향 3 → 고라니 고정
    /// - 4번째 잠긴 슬롯 → 엽전 99로 열기 → 향 3 → 구미호 확정
    /// 소환 즉시 친밀도 0·기력 1.
    /// </summary>
    public static class CharacterSummon
    {
        public const int HyangCost = 3;
        /// <summary>4번째 잠긴 슬롯을 여는 엽전.</summary>
        public const int LockedSlotYeopjeonCost = 99;

        public static readonly Vector3 DefaultSpawn = new Vector3(0f, -0.2f, 0f);
        public static readonly Color PlaceholderColor = new Color(0.45f, 0.85f, 1f, 1f);

        // 예전 이름 (윷 화면 등)
        public static Vector3 DefaultGoraniSpawn => DefaultSpawn;
        public static Color GoraniPlaceholderColor => PlaceholderColor;

        /// <summary>4번째 슬롯을 엽전으로 열었는지 (세이브).</summary>
        public static bool LockedSlotUnlocked { get; private set; }

        public static void ResetFromSave(bool lockedSlotUnlocked) => LockedSlotUnlocked = lockedSlotUnlocked;

        /// <summary>엽전 99로 4번째 슬롯 열기. 이미 열렸거나 엽전 부족이면 false.</summary>
        public static bool TryUnlockLockedSlot(GameEconomy economy = null)
        {
            var eco = economy != null ? economy : GameEconomy.Instance;
            if (LockedSlotUnlocked || eco == null) return false;
            if (!eco.TrySpendYeopjeon(LockedSlotYeopjeonCost)) return false;
            LockedSlotUnlocked = true;
            return true;
        }

        // ---------------- 소환 대상 데이터 ----------------

        static readonly Dictionary<CharacterId, CharacterData> registered = new Dictionary<CharacterId, CharacterData>();

        /// <summary>Main이 씬에 연결된 에셋(goraniData·gumihoData)을 등록. 없으면 Resources/Characters/{id}.</summary>
        public static void RegisterData(CharacterId id, CharacterData data)
        {
            if (data != null) registered[id] = data;
        }

        /// <summary>소환 대상 에셋의 런타임 사본(characters.json 적용).</summary>
        public static CharacterData ResolveData(CharacterId id, CharacterData overrideData = null)
        {
            var data = overrideData;
            if (data == null) registered.TryGetValue(id, out data);
            if (data == null) data = Resources.Load<CharacterData>("Characters/" + id);
            return CharacterCatalog.RuntimeCopy(data);
        }

        // ---------------- 규칙 ----------------

        public static bool IsPresent(CharacterId id) => Find(id) != null;

        /// <summary>지금 소환 슬롯이 열려 있는 대상인지: 고라니는 항상, 구미호는 4번째 슬롯을 연 뒤.</summary>
        public static bool HasOpenSlotFor(CharacterId id) =>
            id == CharacterId.Gorani || (id == CharacterId.Gumiho && LockedSlotUnlocked);

        public static bool CanSummon(CharacterId id) =>
            HasOpenSlotFor(id) && !IsPresent(id)
            && GameEconomy.Instance != null && GameEconomy.Instance.Hyang >= HyangCost;

        /// <summary>향을 소모하고 기본 위치에 스폰. 연출 없이 쓸 때.</summary>
        public static CharacterAgent TrySummon(CharacterId id, Font bubbleFont, CharacterData overrideData = null)
        {
            if (!CanSummon(id)) return null;
            if (!GameEconomy.Instance.TrySpendHyang(HyangCost)) return null;

            var agent = Spawn(id, bubbleFont, DefaultSpawn, overrideData);
            if (agent == null)
            {
                GameEconomy.Instance.AddHyang(HyangCost);
                return null;
            }
            agent.ApplyFreshSummon();
            return agent;
        }

        /// <summary>비용 없이 스폰 (소환 연출·세이브 복원용). 호출측에서 향 소모.</summary>
        public static CharacterAgent Spawn(CharacterId id, Font bubbleFont, Vector3 pos, CharacterData overrideData = null)
        {
            if (IsPresent(id)) return null;
            var data = ResolveData(id, overrideData);
            if (data == null)
            {
                Debug.LogError($"[CharacterSummon] {id} CharacterData가 없습니다. Main에 연결하거나 Resources/Characters/{id} 를 두세요.");
                return null;
            }
            return CharacterSpawner.Spawn(data, pos, PlaceholderColor, bubbleFont);
        }

        /// <summary>세이브에만 있고 월드에 없는 소환 요괴를 스폰 (향 소모 없음).</summary>
        public static CharacterAgent SpawnForSaveRestore(CharacterId id, Font bubbleFont, Vector3 pos)
        {
            var existing = Find(id);
            if (existing != null) return existing;
            var agent = Spawn(id, bubbleFont, pos);
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

        // ---------------- 고라니 전용 (예전 호출부 호환) ----------------

        public static bool CanSummonGorani() => CanSummon(CharacterId.Gorani);

        public static CharacterData ResolveGoraniData(CharacterData overrideData = null) =>
            ResolveData(CharacterId.Gorani, overrideData);

        public static CharacterAgent TrySummonGorani(CharacterData goraniData, Font bubbleFont) =>
            TrySummon(CharacterId.Gorani, bubbleFont, goraniData);

        public static CharacterAgent SpawnGorani(CharacterData goraniData, Font bubbleFont, Vector3 pos) =>
            Spawn(CharacterId.Gorani, bubbleFont, pos, goraniData);
    }
}
