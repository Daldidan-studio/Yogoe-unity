using UnityEngine;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Save;

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
    /// 정화수로만 기력을 채운다 — 기력 100 도달 시 혼으로 진화, Docs/05 4항 확정).
    ///
    /// 구현은 관심사별로 여러 파일에 나뉜 partial class다 (이 파일은 라이프사이클/공개 API만):
    /// <see cref="CharacterAgent"/>.Movement.cs(걷기/머물기/놀기/주저앉기 상태머신),
    /// .Drag.cs(플레이어 드래그), .Animation.cs(스프라이트/방향), .Dialogue.cs(혼잣말·임시 대사),
    /// .Evolution.cs(넋 부유·넋→혼 진화).
    /// </summary>
    public partial class CharacterAgent : MonoBehaviour
    {
        public CharacterData Data;
        public CharacterRuntimeStats Stats = new CharacterRuntimeStats();

        [Header("이동 (에셋 없어도 동작)")]
        public float moveSpeed = 1.5f;
        [SerializeField] private SpriteRenderer spriteRenderer; // 없어도 무방, 나중에 아트 연결용

        private static readonly System.Collections.Generic.List<CharacterAgent> ActiveAgents
            = new System.Collections.Generic.List<CharacterAgent>();

        /// <summary>지금 씬에 존재하는 모든 캐릭터 (슬롯바 UI 등에서 순회용). 절대 수정하지 말 것.</summary>
        public static System.Collections.Generic.IReadOnlyList<CharacterAgent> All => ActiveAgents;

        /// <summary>플레이어가 드래그 중이면 AI 틱을 멈춘다.</summary>
        public bool IsBeingDragged { get; private set; }

        /// <summary>넋·기절은 드래그 불가 (6-2).</summary>
        public bool CanBeDraggedByPlayer =>
            Stats.Stage != GrowthStage.Neok && Stats.State != ActionState.Fainted;

        /// <summary>소환·세이브 복원이 Start 기본 스탯을 덮어쓰지 않게 막는다.</summary>
        private bool statsAppliedExternally;

        CharacterRequestState requests;
        public CharacterRequestState Requests => requests ?? (requests = new CharacterRequestState(this));
        public bool HasOfferingRequest => Requests.HasOfferingRequest;

        /// <summary>이보다 긴 dt는 이동·일반 틱에 쓰지 않는다 (프레임 스파이크 방지).</summary>
        private const float MaxContinuousMoveDelta = 0.25f;

        private ArtScaleSettings _artScale;
        private ArtScaleSettings ArtScale =>
            _artScale != null ? _artScale : (_artScale = Resources.Load<ArtScaleSettings>("ArtScaleSettings"));

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            lastPosition = transform.position;
        }

        private void OnEnable() => ActiveAgents.Add(this);
        private void OnDisable()
        {
            ActiveAgents.Remove(this);
            if (bubbleBg != null) Destroy(bubbleBg.gameObject);
            if (bubbleTextMesh != null) Destroy(bubbleTextMesh.gameObject);
            requests?.DestroyVisuals();
        }

        private void Start()
        {
            // Data 초기화는 Awake가 아니라 Start에서 한다: 코드로 캐릭터를 생성할 때
            // AddComponent<CharacterAgent>() 직후에 Data를 대입하는 패턴(Main 등)이
            // 흔한데, AddComponent가 Awake를 즉시 실행시키기 때문에 Awake 시점엔 Data가 아직
            // null이라 초기화가 안 먹는 버그가 있었다. Start는 모든 오브젝트의 Awake가 끝난
            // 다음 프레임 이전에 실행되므로 이 시점엔 Data가 확실히 채워져 있다.
            if (!statsAppliedExternally && Data != null)
                ApplyDefaultStatsFromData();

            if (Stats.Stage != GrowthStage.Neok) EnterWalking();

            // 로딩 직후 모든 캐릭터가 동시에 혼잣말을 시작하지 않도록 첫 대사까지 약간의 랜덤 지연을 둔다.
            monologueTimer = Random.Range(2f, MonologueMinInterval);

            // AddComponent 직후 Data가 늦게 붙는 패턴 대비 — 첫 프레임부터 walk/idle 스프라이트 적용
            lastPosition = transform.position;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            ApplyAnimationFrameImmediate();
        }

        private void ApplyDefaultStatsFromData()
        {
            Stats.Stage = Data.startingStage;
            if (Stats.Stage == GrowthStage.Neok)
            {
                // 넋: 정화수로만 기력 충전 → 100이면 혼. 시작은 기력 0·친밀도 미사용.
                Stats.Intimacy = 0f;
                Stats.Stamina = 0f;
            }
            else
            {
                var start = StartingStateSettings.Get();
                Stats.Intimacy = start.startingIntimacy;
                Stats.Stamina = start.startingStamina;
            }
        }

        /// <summary>세이브 복원·소환 직후 Start가 스탯을 리셋하지 않도록 표시.</summary>
        public void MarkStatsAppliedExternally() => statsAppliedExternally = true;

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxContinuousMoveDelta);
            Requests.Tick(dt);

            if (evolvingToHon)
            {
                UpdateSortingOrder();
                return;
            }

            // 소환·진화 연출 중 맵 AI/부유 정지
            if (CeremonyGate.BlocksWorldInput)
            {
                UpdateSortingOrder();
                return;
            }

            if (Stats.Stage == GrowthStage.Neok)
            {
                // 기력 100이면 정화수가 없어도 진화 (이전에 다 먹인 채 멈춘 경우 포함)
                if (Stats.Stamina >= 100f - 0.001f)
                {
                    EvolveToHon();
                    return;
                }

                if (!IsBeingDragged)
                    TickNeokFloat(dt);
                UpdateSortingOrder();
                return;
            }

            if (IsBeingDragged)
            {
                UpdateSortingOrder();
                return;
            }

            if (isRefusing)
            {
                TickRefuse(dt);
                UpdateSortingOrder();
                return;
            }

            // 긴 공백 정산은 Main의 벽시계 CatchUpAll이 담당한다.
            // deltaTime에 의존하면 WebGL 탭 복귀 시 스파이크가 안 오거나, 오면 이동이 텔레포트한다.

            switch (Stats.State)
            {
                case ActionState.Walking: TickWalking(dt); break;
                case ActionState.Staying: TickStaying(dt); break;
                case ActionState.Slumped: TickSlumped(dt); break;
                case ActionState.Fainted: /* 외부(공양)에서만 깨어남 */ break;
                case ActionState.Playing: TickPlaying(dt); break;
            }

            UpdateWalkAnimation(dt);
            UpdateSortingOrder();
            if (Stats.State != ActionState.Fainted) UpdateMonologue(dt);
        }

        /// <summary>
        /// 탭/앱이 다시 살아났을 때 벽시계로 잰 공백을 전 캐릭터에 반영한다.
        /// 기력·공덕·상태만 따라잡고, 걷기는 위치를 유지한 채 복귀 후 정상 속도로 이어간다.
        /// </summary>
        public static void CatchUpAll(float seconds)
        {
            if (seconds <= 0.001f) return;
            float capped = Mathf.Min(seconds, OfflineSimulator.MaxOfflineSeconds);
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                var agent = ActiveAgents[i];
                if (agent != null) agent.CatchUpWallClock(capped);
            }
        }

        private void CatchUpWallClock(float remaining)
        {
            if (Stats.Stage == GrowthStage.Neok) return;

            int guard = 0;
            while (remaining > 0.0001f && guard++ < 256)
            {
                switch (Stats.State)
                {
                    case ActionState.Staying:
                        remaining -= TickStayingSlice(remaining);
                        break;
                    case ActionState.Slumped:
                        remaining -= TickSlumpedSlice(remaining);
                        break;
                    case ActionState.Playing:
                        remaining -= TickPlayingSlice(remaining);
                        break;
                    case ActionState.Walking:
                        // 오프라인과 동일: 걷는 동안 생산 없음. 순간이동하지 않고 남은 공백은 폐기.
                        EnsureWalkingDestination();
                        return;
                    case ActionState.Fainted:
                        return;
                    default:
                        return;
                }
            }
        }

        /// <summary>Y 기준 sortingOrder — ArtScaleSettings.asset 의 characterSortBase / ySortMultiplier.</summary>
        private void UpdateSortingOrder()
        {
            if (spriteRenderer == null) return;
            var s = ArtScale;
            if (s == null) return;
            spriteRenderer.sortingOrder = s.SortOrderForCharacter(transform.position.y);
        }

        // ---------------- 세이브 복원 ----------------

        /// <summary>오프라인 시뮬 결과를 월드에 붙일 때 호출. 점유 기물이 있으면 강제 앉힌다.</summary>
        public void ApplySaveSnapshot(
            GrowthStage stage,
            float intimacy,
            float stamina,
            ActionState state,
            float stateTimer,
            Vector3 worldPos,
            PropSlot occupyProp)
        {
            statsAppliedExternally = true;
            ClearWalkDestination();
            if (currentProp != null)
            {
                currentProp.Vacate(this);
                currentProp = null;
            }

            Stats.Stage = stage;
            Stats.Intimacy = intimacy;
            Stats.Stamina = stamina;
            Stats.State = state;
            Stats.StateTimer = stateTimer;
            transform.position = MapBounds.Clamp(worldPos);
            lastPosition = transform.position;
            neokLogicalPos = transform.position;
            neokDriftTarget = null;

            if (Stats.Stage == GrowthStage.Hon)
                CharacterSpawner.EnsureHonVisual(this);
            else if (Stats.Stage == GrowthStage.Neok && Stats.Stamina >= 100f - 0.001f)
                EvolveToHon(playFx: false);

            if (occupyProp != null
                && (state == ActionState.Staying
                    || state == ActionState.Slumped
                    || state == ActionState.Fainted))
            {
                occupyProp.ForceOccupyForSaveRestore(this);
                currentProp = occupyProp;
                transform.position = MapBounds.Clamp(new Vector3(
                    occupyProp.transform.position.x,
                    occupyProp.transform.position.y,
                    transform.position.z));
                lastPosition = transform.position;
            }

            ApplyAnimationFrameImmediate();
        }

        // ---------------- 외부 API (공양 시스템에서 호출) ----------------

        /// <summary>
        /// 공양 처리 (5-3/5-4). 공양물 종류별 수치 계산은 공양 시스템 쪽에서 하고 여기엔 최종값만 넘긴다.
        /// 넋은 정화수만 기력을 채우며, 기력 100 도달 시 즉시 혼으로 진화 (Docs/05 4항).
        /// </summary>
        public void ReceiveOffering(int staminaGain, float intimacyGain, OfferingKind kind = OfferingKind.General)
        {
            if (Stats.Stage == GrowthStage.Neok && kind != OfferingKind.PurifiedWater)
                return;

            Stats.Stamina = Mathf.Min(100f, Stats.Stamina + Mathf.Max(0, staminaGain));

            if (Stats.Stage != GrowthStage.Neok)
                Stats.Intimacy = Mathf.Min(100f, Stats.Intimacy + intimacyGain);

            if (Stats.Stage == GrowthStage.Neok && Stats.Stamina >= 100f - 0.001f)
            {
                EvolveToHon();
                return;
            }

            if (Stats.State == ActionState.Slumped || Stats.State == ActionState.Fainted)
            {
                LeaveCurrentProp();
                EnterWalking();
            }
        }

        public void BindSpriteRenderer(SpriteRenderer sr)
        {
            spriteRenderer = sr;
        }

        /// <summary>공양이 아닌 경로(윷놀이 말 이동 등)로 친밀도만 올릴 때 사용. 넋은 친밀도가 없어 무시.</summary>
        public void AddIntimacy(float amount)
        {
            if (Stats.Stage == GrowthStage.Neok) return;
            Stats.Intimacy = Mathf.Min(100f, Stats.Intimacy + amount);
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
                case ActionState.Playing: c = new Color(1f, 0.45f, 0.85f); break;
                default: c = Color.white; break;
            }
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.5f, 0.2f);
        }
#endif
    }
}
