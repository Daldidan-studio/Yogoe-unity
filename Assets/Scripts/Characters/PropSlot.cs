using System;
using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 씬에 배치된 기물 하나. 에셋(스프라이트) 없이도 빈 GameObject에 이 컴포넌트만 붙이면
    /// 상태머신 테스트가 가능하도록 만듦 — Gizmo로 점유 여부만 색으로 표시.
    /// </summary>
    [DisallowMultipleComponent]
    public class PropSlot : MonoBehaviour
    {
        public PropData data;
        [Min(1)] public int level = 1;

        public CharacterAgent Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;

        // [버그 수정] 여러 캐릭터가 같은 프레임(Start())에 동시에 목적지를 고르면, 그 순간엔
        // 아무도 아직 도착 전이라 IsOccupied가 전부 false라서 다들 같은 기물을 후보로 보고
        // 우연히 같은 곳을 찍어 "다같이 몰려다니는" 것처럼 보이는 문제가 있었다. "찜"(예약) 개념을
        // 따로 둬서, 목적지로 고르는 즉시(도착 전이라도) 후보 풀에서 빠지도록 한다.
        public CharacterAgent ReservedBy { get; private set; }
        public bool IsReserved => ReservedBy != null;

        private void OnEnable() => PropManager.Instance?.Register(this);
        private void OnDisable() => PropManager.Instance?.Unregister(this);

        /// <summary>목적지로 고른 즉시 호출 — 도착 전이라도 다른 캐릭터의 후보 풀에서 제외시킨다.</summary>
        public bool TryReserve(CharacterAgent agent)
        {
            if (IsOccupied) return false;
            if (IsReserved && ReservedBy != agent) return false;
            ReservedBy = agent;
            return true;
        }

        /// <summary>목적지를 포기(재추첨/타임아웃 등)할 때 예약 해제.</summary>
        public void ReleaseReservation(CharacterAgent agent)
        {
            if (ReservedBy == agent) ReservedBy = null;
        }

        public bool TryOccupy(CharacterAgent agent)
        {
            if (IsOccupied) return false;
            if (IsReserved && ReservedBy != agent) return false;
            Occupant = agent;
            ReservedBy = null;
            return true;
        }

        /// <summary>점유 해제. 주저앉기/기절 중에는 호출하지 않는다 (6-2: 그 상태에서는 계속 점유).</summary>
        public void Vacate(CharacterAgent agent)
        {
            if (Occupant == agent) Occupant = null;
        }

        /// <summary>7-1 공식의 기물 레벨 항목만 (기본생산 * 1.1^(L-1)). 친밀도/엔딩기물 보정은 CharacterAgent에서 곱함.</summary>
        public double GetBaseProductionThisLevel()
        {
            if (data == null) return 0;
            return data.baseProductionPerMinute * Math.Pow(1.1, level - 1);
        }

        /// <summary>6-2 걷기 후보 필터: 다른 요괴의 엔딩 기물은 후보에서 제외.</summary>
        public bool CanBeUsedBy(CharacterAgent agent)
        {
            if (data == null || agent == null || agent.Data == null) return true;
            if (data.isEndingProp && data.owner != agent.Data.id) return false;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 주황=점유중, 노랑=예약(오는 중), 하늘색=비어있음
            Gizmos.color = IsOccupied ? new Color(1f, 0.5f, 0f) : IsReserved ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
#endif
    }
}
