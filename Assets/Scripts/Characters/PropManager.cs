using System.Collections.Generic;
using UnityEngine;

namespace Yoegoe.Characters
{
    /// <summary>씬 안의 모든 PropSlot을 등록해두고, 걷기 목적지 후보를 골라주는 매니저.</summary>
    public class PropManager : MonoBehaviour
    {
        public static PropManager Instance { get; private set; }
        private readonly List<PropSlot> allProps = new List<PropSlot>();

        /// <summary>등록된 기물 목록 (HUD 등에서 FindObjectsByType 대신 사용).</summary>
        public List<PropSlot> All => allProps;

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
            var candidates = new List<PropSlot>();
            for (int i = 0; i < allProps.Count; i++)
            {
                var p = allProps[i];
                if (p == null) continue;
                if (!p.IsBuilt) continue;
                if (p.IsOccupied) continue;
                if (p.IsReserved) continue;
                if (p == exclude) continue;
                if (!p.CanBeUsedBy(requester)) continue;
                candidates.Add(p);
            }

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>드래그 드롭용: worldPos가 기물 스프라이트 bounds 안(또는 maxRadius 이내)인 가장 가까운 기물.
        /// maxRadius=0이면 PNG 크기(스프라이트 AABB) 안에 있을 때만 매칭.</summary>
        public PropSlot FindNearestDropTarget(CharacterAgent requester, Vector3 worldPos, float maxRadius)
        {
            PropSlot best = null;
            // maxRadius=0일 때도 "아직 미선택"과 구분되도록 시작값을 크게 둔 뒤, 조건은 d <= maxRadius로 검사
            float bestDist = float.MaxValue;
            float limit = Mathf.Max(0f, maxRadius);
            foreach (var p in allProps)
            {
                if (p == null) continue;
                if (!p.IsBuilt) continue;
                if (!p.CanBeUsedBy(requester)) continue;
                if (p.IsOccupied) continue;

                float d = DistanceToPropSurface(p, worldPos);
                if (d <= limit && d < bestDist)
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
                float d = DistanceToPropSurface(p, worldPos);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>bounds 표면까지 거리(안이면 0). 큰 기물 가장자리 드롭도 잡힘.</summary>
        private static float DistanceToPropSurface(PropSlot prop, Vector3 worldPos)
        {
            Bounds b = GetPropBounds(prop);
            Vector3 p = worldPos;
            p.z = b.center.z;
            if (b.Contains(p)) return 0f;
            Vector3 closest = b.ClosestPoint(p);
            return Vector2.Distance(closest, p);
        }

        private static Bounds GetPropBounds(PropSlot prop)
        {
            var sr = prop.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.bounds;
            var r = prop.GetComponentInChildren<Renderer>();
            if (r != null) return r.bounds;
            return new Bounds(prop.transform.position, Vector3.one * 0.8f);
        }
    }
}
