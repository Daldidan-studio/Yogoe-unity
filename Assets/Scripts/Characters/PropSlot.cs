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
    /// 미건립(자물쇠)은 걷기·생산·점유 대상이 아니다 (기획 8장).
    /// </summary>
    [DisallowMultipleComponent]
    public class PropSlot : MonoBehaviour
    {
        public PropData data;
        [Min(1)] public int level = 1;

        /// <summary>건립 여부. prebuilt면 시작 true, 자물쇠는 구매 후 true.</summary>
        public bool IsBuilt { get; private set; }

        public CharacterAgent Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;

        public CharacterAgent ReservedBy { get; private set; }
        public bool IsReserved => ReservedBy != null;

        /// <summary>아직 수거하지 않은 기물 공덕 더미 (7-2).</summary>
        public BigNumber PendingMerit { get; private set; } = BigNumber.Zero;
        public bool HasPendingMerit => IsBuilt && PendingMerit.Mantissa != 0;

        private TextMesh pileLabel;
        private TextMesh lockLabel;
        private SpriteRenderer spriteRenderer;
        private Renderer meshRenderer;
        private Sprite builtSprite;
        private Color builtTint = Color.white;
        private static Font sharedPileFont;

        public event Action<PropSlot> OnBuilt;
        public event Action<PropSlot> OnLevelUp;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            meshRenderer = GetComponent<Renderer>();
        }

        private void OnEnable() => PropManager.Instance?.Register(this);
        private void OnDisable() => PropManager.Instance?.Unregister(this);

        private void LateUpdate()
        {
            RefreshPileLabel();
            RefreshLockVisual();
        }

        /// <summary>Main 스폰 직후 호출. prebuilt면 즉시 건립.</summary>
        public void ConfigureBuiltState(bool built)
        {
            IsBuilt = built;
            if (built)
                ApplyBuiltVisual();
            else
                ApplyLockVisual();
        }

        public void Build()
        {
            if (IsBuilt) return;
            IsBuilt = true;
            level = Math.Max(1, level);
            ApplyBuiltVisual();
            OnBuilt?.Invoke(this);
        }

        public void NotifyLevelUp() => OnLevelUp?.Invoke(this);

        public bool TryReserve(CharacterAgent agent)
        {
            if (!IsBuilt) return false;
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
            if (!IsBuilt) return false;
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

        /// <summary>세이브 로드용. 더미만 덮어쓴다.</summary>
        public void SetPendingMeritFromSave(BigNumber amount)
        {
            PendingMerit = amount;
        }

        /// <summary>세이브 복원용 건립/레벨.</summary>
        public void ApplySaveBuiltState(bool built, int savedLevel)
        {
            level = Mathf.Max(1, savedLevel);
            if (built && !IsBuilt)
                Build();
            else if (!built && IsBuilt)
            {
                IsBuilt = false;
                ApplyLockVisual();
            }
            else if (built)
                ApplyBuiltVisual();
        }

        /// <summary>세이브 복원 전 점유만 비운다 (더미는 유지).</summary>
        public void ClearOccupantForSaveRestore()
        {
            Occupant = null;
            ReservedBy = null;
        }

        /// <summary>세이브 복원용 강제 점유.</summary>
        public void ForceOccupyForSaveRestore(CharacterAgent agent)
        {
            if (!IsBuilt) return;
            Occupant = agent;
            ReservedBy = null;
        }

        /// <summary>7-1: 머물기 중 생산분을 기물 더미에 적립. HUD 지갑으로는 바로 안 들어간다.</summary>
        public void AddToMeritPile(BigNumber amount)
        {
            if (!IsBuilt || amount.Mantissa == 0) return;
            PendingMerit += amount;
        }

        /// <summary>
        /// 7-2: 기물 탭 수거. 더미를 비우고 플레이어 공덕(HUD)에 더한다.
        /// </summary>
        public bool TryCollectMerit()
        {
            if (!HasPendingMerit) return false;
            var collected = TakePendingMerit();
            GameEconomy.AddMerit(collected);
            return true;
        }

        /// <summary>더미만 비워 반환 (일괄 수거용 — HUD에 바로 넣지 않음).</summary>
        public BigNumber TakePendingMerit()
        {
            var collected = PendingMerit;
            PendingMerit = BigNumber.Zero;
            return collected;
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
            if (!IsBuilt || data == null) return 0;
            return data.baseProductionPerMinute * Math.Pow(1.1, level - 1);
        }

        public bool CanBeUsedBy(CharacterAgent agent)
        {
            if (!IsBuilt) return false;
            if (data == null || agent == null || agent.Data == null) return true;
            if (data.isEndingProp && data.owner != agent.Data.id) return false;
            return true;
        }

        public string DisplayName => data != null && !string.IsNullOrEmpty(data.displayName)
            ? data.displayName
            : name;

        /// <summary>Main이 건립 시 쓸 스프라이트·틴트를 기억.</summary>
        public void SetBuiltAppearance(Sprite sprite, Color tint)
        {
            builtSprite = sprite;
            builtTint = tint;
        }

        private void ApplyBuiltVisual()
        {
            if (lockLabel != null) lockLabel.gameObject.SetActive(false);

            if (spriteRenderer != null)
            {
                if (builtSprite != null) spriteRenderer.sprite = builtSprite;
                spriteRenderer.color = Color.white;
                spriteRenderer.enabled = true;
            }
            if (meshRenderer != null && !(meshRenderer is SpriteRenderer))
            {
                meshRenderer.enabled = true;
                if (meshRenderer.material != null)
                    meshRenderer.material.color = builtTint;
            }
        }

        private void ApplyLockVisual()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.35f, 0.35f, 0.4f, 0.55f);
                if (builtSprite != null) spriteRenderer.sprite = builtSprite;
            }
            if (meshRenderer != null && !(meshRenderer is SpriteRenderer))
            {
                meshRenderer.enabled = true;
                if (meshRenderer.material != null)
                    meshRenderer.material.color = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            }
            EnsureLockLabel();
            lockLabel.gameObject.SetActive(true);
            lockLabel.text = "자물쇠";
            lockLabel.transform.position = transform.position + Vector3.up * 0.55f;
        }

        private void RefreshLockVisual()
        {
            if (IsBuilt)
            {
                if (lockLabel != null) lockLabel.gameObject.SetActive(false);
                return;
            }
            EnsureLockLabel();
            lockLabel.gameObject.SetActive(true);
            lockLabel.transform.position = transform.position + Vector3.up * 0.55f;
        }

        private void EnsureLockLabel()
        {
            if (lockLabel != null) return;
            var go = new GameObject(name + "_Lock");
            lockLabel = go.AddComponent<TextMesh>();
            lockLabel.anchor = TextAnchor.MiddleCenter;
            lockLabel.alignment = TextAlignment.Center;
            lockLabel.characterSize = 0.07f;
            lockLabel.fontSize = 42;
            lockLabel.color = new Color(0.9f, 0.85f, 0.7f, 1f);
            if (sharedPileFont == null)
                sharedPileFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (sharedPileFont != null) lockLabel.font = sharedPileFont;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 480;
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
            pileLabel.text = new string('*', stage) + "\n" + FormatPileAmount(PendingMerit);
            pileLabel.transform.position = transform.position + Vector3.up * 0.85f;
        }

        private static string FormatPileAmount(BigNumber amount)
        {
            double v = Math.Abs(amount.ToDouble());
            if (v < 1000) return Mathf.RoundToInt((float)v).ToString();
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
            Gizmos.color = !IsBuilt ? Color.gray
                : IsOccupied ? new Color(1f, 0.5f, 0f)
                : IsReserved ? Color.yellow
                : Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
#endif
    }
}
