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
        /// 6-2 걷기 목적지 후보 선정.
        /// 룰 기준: Docs/06_행동룰.md
        /// 비어있고, 직전 기물이 아니고, 다른 요괴의 엔딩 기물이 아닌 것 중 랜덤.
        /// 후보가 없으면 null (호출측에서 30초 방황 후 재추첨).
        /// </summary>
        public PropSlot GetRandomAvailableProp(CharacterAgent requester, PropSlot exclude)
        {
            var candidates = allProps.Where(p =>
                p != null &&
                p.IsBuilt &&
                !p.IsOccupied &&
                !p.IsReserved &&
                p != exclude &&
                p.CanBeUsedBy(requester)
            ).ToList();

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>드래그 드롭용: worldPos 근처에서 앉힐 수 있는 가장 가까운 기물.</summary>
        public PropSlot FindNearestDropTarget(CharacterAgent requester, Vector3 worldPos, float maxRadius)
        {
            PropSlot best = null;
            float bestDist = maxRadius;
            foreach (var p in allProps)
            {
                if (p == null) continue;
                if (!p.IsBuilt) continue;
                if (!p.CanBeUsedBy(requester)) continue;
                if (p.IsOccupied) continue;
                // 예약만 된 자리(다른 요괴가 오는 중)는 앉히지 않음
                if (p.IsReserved && p.ReservedBy != requester) continue;

                float d = DistanceToProp(p, worldPos);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>탭/수거용: worldPos에 가장 가까운 기물.</summary>
        public PropSlot FindNearestProp(Vector3 worldPos, float maxRadius)
        {
            PropSlot best = null;
            float bestDist = maxRadius;
            foreach (var p in allProps)
            {
                if (p == null) continue;
                float d = DistanceToProp(p, worldPos);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>스프라이트/메시 bounds 중심 기준 거리 (큐브 절구도 잡히게).</summary>
        private static float DistanceToProp(PropSlot prop, Vector3 worldPos)
        {
            Vector3 center = prop.transform.position;
            var sr = prop.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                center = sr.bounds.center;
            else
            {
                var r = prop.GetComponentInChildren<Renderer>();
                if (r != null) center = r.bounds.center;
            }
            return Vector2.Distance(center, worldPos);
        }
    }
}
