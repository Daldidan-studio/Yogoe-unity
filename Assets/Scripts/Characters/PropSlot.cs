using System;
using UnityEngine;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 씬에 배치된 기물 하나.
    /// 공덕은 기물별 더미에 쌓이고, 탭으로 수거한다 (기획 7-1·7-2).
    /// </summary>
    [DisallowMultipleComponent]
    public class PropSlot : MonoBehaviour
    {
        public PropData data;
        [Min(1)] public int level = 1;

        public CharacterAgent Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;

        public CharacterAgent ReservedBy { get; private set; }
        public bool IsReserved => ReservedBy != null;

        /// <summary>아직 수거하지 않은 기물 공덕 더미 (7-2).</summary>
        public BigNumber PendingMerit { get; private set; } = BigNumber.Zero;
        public bool HasPendingMerit => PendingMerit.Mantissa != 0;

        private TextMesh pileLabel;
        private static Font sharedPileFont;

        private void OnEnable() => PropManager.Instance?.Register(this);
        private void OnDisable() => PropManager.Instance?.Unregister(this);

        private void LateUpdate() => RefreshPileLabel();

        public bool TryReserve(CharacterAgent agent)
        {
            if (IsOccupied) return false;
            if (IsReserved && ReservedBy != agent) return false;
            ReservedBy = agent;
            return true;
        }

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

        /// <summary>점유 해제. 주저앉기·기절 중에는 호출하지 않는다. 더미는 기물에 남는다.</summary>
        public void Vacate(CharacterAgent agent)
        {
            if (Occupant == agent) Occupant = null;
        }

        /// <summary>7-1: 머물기 중 생산분을 기물 더미에 적립. HUD 지갑으로는 바로 안 들어간다.</summary>
        public void AddToMeritPile(BigNumber amount)
        {
            if (amount.Mantissa == 0) return;
            PendingMerit += amount;
        }

        /// <summary>
        /// 7-2: 기물 탭 수거. 더미를 비우고 플레이어 공덕(HUD)에 더한다.
        /// </summary>
        public bool TryCollectMerit()
        {
            if (!HasPendingMerit) return false;
            var collected = PendingMerit;
            PendingMerit = BigNumber.Zero;
            GameEconomy.AddMerit(collected);
            return true;
        }

        /// <summary>
        /// 더미 표시 단계 0(빈) ~ 5.
        /// 경계 = 기물 분당 기본생산 × 1·3·10·20·30분 (7-2).
        /// </summary>
        public int GetPileStage()
        {
            if (!HasPendingMerit) return 0;
            double perMin = GetBaseProductionThisLevel();
            if (perMin <= 0) return 1;

            double pile = Math.Abs(PendingMerit.ToDouble());
            int[] minuteMarks = { 1, 3, 10, 20, 30 };
            int stage = 1;
            for (int i = 0; i < minuteMarks.Length; i++)
            {
                if (pile >= perMin * minuteMarks[i]) stage = i + 1;
                else break;
            }
            return stage;
        }

        public double GetBaseProductionThisLevel()
        {
            if (data == null) return 0;
            return data.baseProductionPerMinute * Math.Pow(1.1, level - 1);
        }

        public bool CanBeUsedBy(CharacterAgent agent)
        {
            if (data == null || agent == null || agent.Data == null) return true;
            if (data.isEndingProp && data.owner != agent.Data.id) return false;
            return true;
        }

        private void RefreshPileLabel()
        {
            int stage = GetPileStage();
            if (stage <= 0)
            {
                if (pileLabel != null) pileLabel.gameObject.SetActive(false);
                return;
            }

            EnsurePileLabel();
            pileLabel.gameObject.SetActive(true);
            // 단계만큼 ●, 옆에 더미 수치 (날아가는 연출 전 MVP)
            pileLabel.text = new string('*', stage) + "\n" + FormatPileAmount(PendingMerit);
            pileLabel.transform.position = transform.position + Vector3.up * 0.85f;
        }

        private static string FormatPileAmount(BigNumber amount)
        {
            double v = Math.Abs(amount.ToDouble());
            if (v < 1000) return Mathf.RoundToInt((float)v).ToString();
            // TextMesh 기본 폰트는 한글 ㄱ 단위가 깨질 수 있어 초반은 숫자, 이후는 지수 표기
            return amount.ToDisplayString();
        }

        private void EnsurePileLabel()
        {
            if (pileLabel != null) return;

            var go = new GameObject(name + "_MeritPile");
            pileLabel = go.AddComponent<TextMesh>();
            pileLabel.anchor = TextAnchor.LowerCenter;
            pileLabel.alignment = TextAlignment.Center;
            pileLabel.characterSize = 0.08f;
            pileLabel.fontSize = 48;
            pileLabel.color = new Color(1f, 0.92f, 0.55f, 1f);
            if (sharedPileFont == null)
                sharedPileFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (sharedPileFont != null) pileLabel.font = sharedPileFont;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 500;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = IsOccupied ? new Color(1f, 0.5f, 0f) : IsReserved ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
#endif
    }
}
