using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yoegoe.Characters
{
    /// <summary>씬 안의 모든 PropSlot을 등록해두고, 걷기 목적지 후보를 골라주는 매니저.</summary>
    public class PropManager : MonoBehaviour
    {
        public static PropManager Instance { get; private set; }
        private readonly List<PropSlot> allProps = new List<PropSlot>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Register(PropSlot slot)
        {
            if (!allProps.Contains(slot)) allProps.Add(slot);
        }

        public void Unregister(PropSlot slot) => allProps.Remove(slot);

        /// <summary>
        /// 6-2 걷기 목적지 후보 선정: 비어있고, 직전 기물이 아니고, 다른 요괴의 엔딩 기물이 아닌 것 중 랜덤.
        /// 후보가 없으면 null (호출측에서 대기 후 재추첨).
        /// </summary>
        public PropSlot GetRandomAvailableProp(CharacterAgent requester, PropSlot exclude)
        {
            var candidates = allProps.Where(p =>
                p != null &&
                !p.IsOccupied &&
                !p.IsReserved &&
                p != exclude &&
                p.CanBeUsedBy(requester)
            ).ToList();

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }
    }
}
