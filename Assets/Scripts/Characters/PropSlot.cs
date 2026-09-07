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

        private void OnEnable() => PropManager.Instance?.Register(this);
        private void OnDisable() => PropManager.Instance?.Unregister(this);

        public bool TryOccupy(CharacterAgent agent)
        {
            if (IsOccupied) return false;
            Occupant = agent;
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
            if (data == null) return true;
            if (data.isEndingProp && data.owner != agent.Data.id) return false;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = IsOccupied ? new Color(1f, 0.5f, 0f) : Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
#endif
    }
}
