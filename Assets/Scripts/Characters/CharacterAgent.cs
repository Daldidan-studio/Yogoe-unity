using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 캐릭터 행동 상태머신 (기획서 6장): 걷기 → 머물기(생산) → [기력 0] → 주저앉기 → [12시간] → 기절.
    ///
    /// 에셋(스프라이트) 없이도 완전히 동작한다 — 이동은 Transform.position만 사용하고,
    /// SpriteRenderer는 있으면 참조만 해두는 정도. 씬에는 빈 GameObject에 이 스크립트 붙이고
    /// 위치만 잡아두면 테스트 가능 (Scene 뷰에서 Gizmo 색으로 상태 확인: 초록=걷기/파랑=머물기/
    /// 노랑=주저앉기/빨강=기절).
    ///
    /// 넋 단계는 이 상태머신을 타지 않음 (9장: 소환된 넋은 화면에 뜨는 고정 도깨비불로 취급하고
    /// 정화수로만 진화 게이지를 채운다는 가정 — DESIGN_DECISIONS_NEEDED.md 4번 참고, 확정 아님).
    /// </summary>
    public class CharacterAgent : MonoBehaviour
    {
        public CharacterData Data;
        public CharacterRuntimeStats Stats = new CharacterRuntimeStats();

        [Header("이동 (에셋 없어도 동작)")]
        public float moveSpeed = 2f;
        [SerializeField] private SpriteRenderer spriteRenderer; // 없어도 무방, 나중에 아트 연결용

        private const float StayDurationSeconds = 5f * 60f;
        private const float FaintThresholdSeconds = 12f * 60f * 60f;
        private const float WanderRetrySeconds = 30f;

        private PropSlot currentProp;
        private PropSlot previousProp;
        private PropSlot destination;
        private bool isWandering;
        private float wanderTimer;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (Data != null)
            {
                Stats.Stage = Data.startingStage;
                Stats.Intimacy = Data.startingIntimacy;
                Stats.Stamina = Data.startingStamina;
            }
        }

        private void Start()
        {
            if (Stats.Stage != GrowthStage.Neok) EnterWalking();
        }

        private void Update()
        {
            if (Stats.Stage == GrowthStage.Neok) return; // 넋은 상태머신 대상 아님

            float dt = Time.deltaTime;
            switch (Stats.State)
            {
                case ActionState.Walking: TickWalking(dt); break;
                case ActionState.Staying: TickStaying(dt); break;
                case ActionState.Slumped: TickSlumped(dt); break;
                case ActionState.Fainted: /* 외부(공양)에서만 깨어남 */ break;
            }
        }

        // ---------------- Walking ----------------

        private void EnterWalking()
        {
            Stats.State = ActionState.Walking;
            Stats.StateTimer = 0f;
            PickDestination();
        }

        private void PickDestination()
        {
            destination = PropManager.Instance != null
                ? PropManager.Instance.GetRandomAvailableProp(this, previousProp)
                : null;

            isWandering = destination == null;
            wanderTimer = 0f;
        }

        private void TickWalking(float dt)
        {
            if (isWandering)
            {
                // 6-2 "후보가 없으면 30초 걷고 재추첨" / "자리가 없으면 걷거나 길바닥에 앉아 쉰다"를
                // 하나의 대기-후-재추첨 루프로 단순화 (문서 표현이 두 케이스를 명확히 구분 안 해서 통합함)
                wanderTimer += dt;
                if (wanderTimer >= WanderRetrySeconds) PickDestination();
                return;
            }

            if (destination == null) { PickDestination(); return; }

            Vector3 targetPos = destination.transform.position;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * dt);

            if (Vector3.Distance(transform.position, targetPos) < 0.05f)
            {
                if (destination.TryOccupy(this))
                {
                    currentProp = destination;
                    destination = null;
                    EnterStaying();
                }
                else
                {
                    // 6-2 "밀린 쪽은 되돌지 않고 직진해 다음 기물로" — 실제 경로 기하가 없어서
                    // 다음 후보를 즉시 재선정하는 것으로 단순화
                    PickDestination();
                }
            }
        }

        // ---------------- Staying ----------------

        private void EnterStaying()
        {
            Stats.State = ActionState.Staying;
            Stats.StateTimer = 0f;
        }

        private void TickStaying(float dt)
        {
            Stats.StateTimer += dt;
            Stats.Stamina -= dt / 20f; // 6-2: 1분당 3 소모 = 20초당 1

            if (Stats.Stamina <= 0f)
            {
                Stats.Stamina = 0f;
                EnterSlumped();
                return;
            }

            if (currentProp != null)
            {
                double perMinute = currentProp.GetBaseProductionThisLevel()
                                    * GetIntimacyCorrection()
                                    * GetEndingPropCorrection();
                GameEconomy.AddMerit(perMinute / 60.0 * dt);
            }

            if (Stats.StateTimer >= StayDurationSeconds)
            {
                LeaveCurrentProp();
                EnterWalking();
            }
        }

        /// <summary>7-1: 친밀도 보정 = 1 + 친밀도/100.</summary>
        private double GetIntimacyCorrection() => 1.0 + Stats.Intimacy / 100.0;

        /// <summary>7-1: 주인이 자기 엔딩 기물에 앉으면 x2.</summary>
        private double GetEndingPropCorrection()
        {
            if (currentProp == null || currentProp.data == null) return 1.0;
            bool ownEnding = currentProp.data.isEndingProp && currentProp.data.owner == Data.id;
            return ownEnding ? 2.0 : 1.0;
        }

        private void LeaveCurrentProp()
        {
            if (currentProp != null)
            {
                currentProp.Vacate(this);
                previousProp = currentProp; // 다음 목적지 선정 시 제외 대상
                currentProp = null;
            }
        }

        // ---------------- Slumped / Fainted ----------------

        private void EnterSlumped()
        {
            // 6-2 기물 배정 규칙: 주저앉기·기절 중에는 현재 기물을 계속 점유한다 (Vacate 호출 안 함)
            Stats.State = ActionState.Slumped;
            Stats.StateTimer = 0f;
        }

        private void TickSlumped(float dt)
        {
            Stats.StateTimer += dt;
            if (Stats.StateTimer >= FaintThresholdSeconds)
            {
                Stats.State = ActionState.Fainted;
                Stats.StateTimer = 0f;
            }
        }

        // ---------------- 외부 API (공양 시스템에서 호출) ----------------

        /// <summary>
        /// 공양 처리 (5-3/5-4). 공양물 종류별 수치 계산은 공양 시스템 쪽에서 하고 여기엔 최종값만 넘긴다.
        /// 기절 상태는 상세화면에서만 호출하도록 UI에서 막을 것 (효과 자체는 동일하게 처리함).
        /// </summary>
        public void ReceiveOffering(int staminaGain, float intimacyGain)
        {
            Stats.Stamina = Mathf.Min(100f, Stats.Stamina + staminaGain);

            if (Stats.Stage != GrowthStage.Neok)
                Stats.Intimacy = Mathf.Min(100f, Stats.Intimacy + intimacyGain);

            if (Stats.State == ActionState.Slumped || Stats.State == ActionState.Fainted)
            {
                LeaveCurrentProp();
                EnterWalking();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color c;
            switch (Stats.State)
            {
                case ActionState.Walking: c = Color.green; break;
                case ActionState.Staying: c = Color.blue; break;
                case ActionState.Slumped: c = Color.yellow; break;
                case ActionState.Fainted: c = Color.red; break;
                default: c = Color.white; break;
            }
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.5f, 0.2f);
        }
#endif
    }
}
