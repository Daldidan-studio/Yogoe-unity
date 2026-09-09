using System.Collections;
using UnityEngine;
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
    /// </summary>
    public class CharacterAgent : MonoBehaviour
    {
        public CharacterData Data;
        public CharacterRuntimeStats Stats = new CharacterRuntimeStats();

        [Header("이동 (에셋 없어도 동작)")]
        public float moveSpeed = 2f;
        [SerializeField] private SpriteRenderer spriteRenderer; // 없어도 무방, 나중에 아트 연결용

        private const float PlayDurationSeconds = 5f * 60f; // 6-2 놀기
        private const float FaintThresholdSeconds = 12f * 60f * 60f;
        private const float WanderRetrySeconds = 30f;

        [Header("혼잣말 (6-4장)")]
        [Tooltip("혼잣말 말풍선에 쓸 한글 폰트. 비워두면 유니티 기본 폰트로 나와서 한글이 깨질 수 있음.")]
        public Font bubbleFont;
        private const float SeparationRadius = 0.55f;
        /// <summary>전진을 죽이지 않도록 이동 속도보다 낮게 유지.</summary>
        private const float SeparationSpeed = 1.2f;
        private const float FacingMoveThresholdSq = 0.00004f; // ~0.006m/frame @60fps
        private const float FacingAxisBias = 1.25f; // 축이 이만큼 우세할 때만 좌우↔상하 전환

        private static readonly System.Collections.Generic.List<CharacterAgent> ActiveAgents
            = new System.Collections.Generic.List<CharacterAgent>();

        /// <summary>지금 씬에 존재하는 모든 캐릭터 (슬롯바 UI 등에서 순회용). 절대 수정하지 말 것.</summary>
        public static System.Collections.Generic.IReadOnlyList<CharacterAgent> All => ActiveAgents;

        /// <summary>플레이어가 드래그 중이면 AI 틱을 멈춘다.</summary>
        public bool IsBeingDragged { get; private set; }

        /// <summary>넋·기절은 드래그 불가 (6-2).</summary>
        public bool CanBeDraggedByPlayer =>
            Stats.Stage != GrowthStage.Neok && Stats.State != ActionState.Fainted;

        const float DragLiftScale = 1.12f;
        const float DragBesideDistance = 0.85f;
        const float RefuseDurationSeconds = 3f;
        const float RefuseFlipInterval = 0.2f;
        Vector3 dragScaleBefore;
        private bool isRefusing;
        private float refuseTimer;
        private float refuseFlipTimer;

        private PropSlot currentProp;
        private PropSlot previousProp;
        private PropSlot destination;
        private bool isWandering;
        private float wanderTimer;
        private Vector3? wanderTarget; // isWandering 중 실제로 걸어갈 맵 안의 임시 목적지

        // ---------------- 걷기 애니메이션 (방향별 4프레임 스와핑) ----------------
        private enum FacingDir { Down, Up, Left, Right }
        private const float AnimFrameInterval = 0.15f; // 프레임당 재생 시간
        private Vector3 lastPosition;
        private FacingDir facing = FacingDir.Down;
        private int animFrame;
        private float animTimer;
        /// <summary>소환·세이브 복원이 Start 기본 스탯을 덮어쓰지 않게 막는다.</summary>
        private bool statsAppliedExternally;

        // 넋(도깨비불) 부유
        private Vector3 neokLogicalPos;
        private Vector3? neokDriftTarget;
        private float neokBobPhase;
        private const float NeokDriftSpeed = 0.7f;
        private const float NeokBobAmplitude = 0.14f;
        private const float NeokBobSpeed = 2.4f;
        private bool evolvingToHon;

        CharacterRequestState requests;
        public CharacterRequestState Requests => requests ?? (requests = new CharacterRequestState(this));
        public bool HasOfferingRequest => Requests.HasOfferingRequest;

        Coroutine tempSpeechRoutine;

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

        /// <summary>소환 직후 넋 상태로 고정 (Start보다 먼저 호출).</summary>
        public void ApplyFreshNeokSummon()
        {
            statsAppliedExternally = true;
            Stats.Stage = GrowthStage.Neok;
            Stats.Intimacy = 0f;
            Stats.Stamina = 0f;
            Stats.State = ActionState.Walking;
            Stats.StateTimer = 0f;
            neokLogicalPos = transform.position;
            neokDriftTarget = null;
            neokBobPhase = Random.Range(0f, Mathf.PI * 2f);
            lastPosition = transform.position;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            ApplyAnimationFrameImmediate();
        }

        private void ApplyAnimationFrameImmediate()
        {
            if (spriteRenderer == null || Data == null) return;
            bool wantsWalk = Stats.State == ActionState.Walking
                || Stats.State == ActionState.Playing;
            Sprite[] frames = ResolveAnimationFrames(wantsWalk, out bool flipX);
            if (frames != null && frames.Length > 0 && frames[0] != null)
                spriteRenderer.sprite = frames[0];
            spriteRenderer.flipX = flipX;
        }

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
            if (Yoegoe.UI.CeremonyGate.BlocksWorldInput)
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

        /// <summary>이보다 긴 dt는 이동·일반 틱에 쓰지 않는다 (프레임 스파이크 방지).</summary>
        private const float MaxContinuousMoveDelta = 0.25f;

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

        /// <summary>
        /// 복귀 catch-up 중 Walking이면 목적지만 보장한다.
        /// 위치 스냅은 하지 않는다 — 화면이 보이는 상태에서 기물로 순간이동하면 UX가 깨진다.
        /// </summary>
        private void EnsureWalkingDestination()
        {
            if (isWandering || destination == null)
                PickDestination();
        }

        private ArtScaleSettings _artScale;
        private ArtScaleSettings ArtScale =>
            _artScale != null ? _artScale : (_artScale = Resources.Load<ArtScaleSettings>("ArtScaleSettings"));

        /// <summary>Y 기준 sortingOrder — ArtScaleSettings.asset 의 characterSortBase / ySortMultiplier.</summary>
        private void UpdateSortingOrder()
        {
            if (spriteRenderer == null) return;
            var s = ArtScale;
            if (s == null) return;
            spriteRenderer.sortingOrder = s.SortOrderForCharacter(transform.position.y);
        }

        // ---------------- 혼잣말 (6-4장) ----------------
        // 걷기/놀기/머물기 중 랜덤 주기(30초~1분)로 머리 위 말풍선.
        // 단일 탭: 표시·순환·10초 연장. 더블탭: 상세화면 (말풍선은 더블탭 판정 후에만).

        private TextMesh bubbleTextMesh;
        private SpriteRenderer bubbleBg;
        private float monologueTimer;
        private bool monologueShowing;
        private int monologueIndex;
        private const float MonologueDisplaySeconds = 10f;
        private const float MonologueMinInterval = 30f;
        private const float MonologueMaxInterval = 60f;

        private bool CanShowMonologue =>
            Stats.State == ActionState.Walking
            || Stats.State == ActionState.Playing
            || Stats.State == ActionState.Staying;

        private void UpdateMonologue(float dt)
        {
            if (HasOfferingRequest || Requests.HasPropRequest) return;
            if (Data == null || Data.monologueLines == null || Data.monologueLines.Length == 0) return;

            if (monologueShowing && bubbleTextMesh != null)
            {
                float spriteTop = spriteRenderer != null ? spriteRenderer.bounds.extents.y : 0.3f;
                Vector3 bubblePos = transform.position + Vector3.up * (spriteTop + 0.55f);
                bubbleTextMesh.transform.position = bubblePos;
                if (bubbleBg != null) bubbleBg.transform.position = bubblePos;
            }

            if (monologueShowing)
            {
                monologueTimer -= dt;
                if (monologueTimer <= 0f) HideMonologue();
                return;
            }

            // 자동 팝업은 걷기/놀기/머물기에서만
            if (!CanShowMonologue) return;

            monologueTimer -= dt;
            if (monologueTimer <= 0f) ShowMonologue();
        }

        /// <summary>
        /// 단일 탭 확정 시(더블탭이 아님) MapPointerRouter가 호출.
        /// 넋: 통통·깜빡 리액션(보상 없음). 혼: 혼잣말.
        /// </summary>
        public void OnTapped()
        {
            if (Stats.Stage == GrowthStage.Neok)
            {
                PlayNeokTapReact();
                return;
            }

            if (!CanShowMonologue) return;
            if (Data == null || Data.monologueLines == null || Data.monologueLines.Length == 0) return;
            ShowMonologue();
        }

        Coroutine neokTapRoutine;

        /// <summary>9-2: 넋 탭 시 비언어 리액션 + 살짝 줌.</summary>
        void PlayNeokTapReact()
        {
            if (neokTapRoutine != null) StopCoroutine(neokTapRoutine);
            neokTapRoutine = StartCoroutine(NeokTapReactRoutine());
        }

        IEnumerator NeokTapReactRoutine()
        {
            var focus = Yoegoe.Debugging.MapCameraFocus.Instance;
            if (focus != null)
            {
                var cam = Camera.main;
                float ortho = cam != null ? cam.orthographicSize * 0.72f : 3.2f;
                focus.Focus(transform.position, ortho, 0.28f);
            }

            Vector3 baseScale = transform.localScale;
            var mr = GetComponent<MeshRenderer>();
            Color baseColor = Color.white;
            if (mr != null && mr.material != null)
            {
                if (mr.material.HasProperty("_BaseColor"))
                    baseColor = mr.material.GetColor("_BaseColor");
                else if (mr.material.HasProperty("_Color"))
                    baseColor = mr.material.color;
            }

            // 통통 2회 + 깜빡
            for (int i = 0; i < 2; i++)
            {
                float t = 0f;
                const float half = 0.12f;
                while (t < half)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Sin(Mathf.Clamp01(t / half) * Mathf.PI);
                    transform.localScale = baseScale * (1f + 0.22f * u);
                    SetMeshAlpha(mr, 0.55f + 0.45f * (1f - u));
                    yield return null;
                }
            }

            transform.localScale = baseScale;
            if (mr != null && mr.material != null)
            {
                if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", baseColor);
                if (mr.material.HasProperty("_Color")) mr.material.color = baseColor;
            }

            if (focus != null) focus.Restore(0.35f);
            neokTapRoutine = null;
        }

        private void ShowMonologue()
        {
            EnsureBubble();
            bubbleTextMesh.text = PickMonologueLine();
            bubbleTextMesh.gameObject.SetActive(true);
            bubbleBg.gameObject.SetActive(true);

            // 배경 판을 텍스트 실제 크기에 맞춰 다시 그림 (말풍선처럼 보이게).
            // 배경과 텍스트는 서로 형제 오브젝트라, 배경 스케일을 바꿔도 텍스트 크기엔 영향 없음.
            var renderer = bubbleTextMesh.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 1001; // 상태 점(1000)보다 위
            Bounds bounds = renderer.bounds;
            bubbleBg.transform.localScale = new Vector3(bounds.size.x + 0.3f, bounds.size.y + 0.18f, 1f);

            monologueShowing = true;
            monologueTimer = MonologueDisplaySeconds;
        }

        /// <summary>혼잣말1 → 2 → 3 … 순서로 순환.</summary>
        private string PickMonologueLine()
        {
            var lines = Data.monologueLines;
            if (lines.Length == 0) return string.Empty;
            if (monologueIndex < 0 || monologueIndex >= lines.Length) monologueIndex = 0;
            string line = lines[monologueIndex];
            monologueIndex = (monologueIndex + 1) % lines.Length;
            return line;
        }

        private void HideMonologue()
        {
            if (bubbleTextMesh != null) bubbleTextMesh.gameObject.SetActive(false);
            if (bubbleBg != null) bubbleBg.gameObject.SetActive(false);
            monologueShowing = false;
            monologueTimer = Random.Range(MonologueMinInterval, MonologueMaxInterval);
        }

        public void HideMonologueForRequest() => HideMonologue();

        /// <summary>요구 완료 등 짧은 대사.</summary>
        public void ShowTempSpeech(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            ShowTempSpeechSequence(new[] { line }, null);
        }

        /// <summary>대사를 순서대로 표시한 뒤 onComplete 호출. 요구→꾸러미 연출용.</summary>
        public void ShowTempSpeechSequence(string[] lines, System.Action onComplete)
        {
            if (lines == null || lines.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }
            if (tempSpeechRoutine != null) StopCoroutine(tempSpeechRoutine);
            tempSpeechRoutine = StartCoroutine(TempSpeechSequenceRoutine(lines, onComplete));
        }

        IEnumerator TempSpeechSequenceRoutine(string[] lines, System.Action onComplete)
        {
            const float secondsPerLine = 2.8f;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                yield return TempSpeechRoutine(line, secondsPerLine, hideAtEnd: true);
            }
            tempSpeechRoutine = null;
            onComplete?.Invoke();
        }

        IEnumerator TempSpeechRoutine(string line, float duration, bool hideAtEnd)
        {
            EnsureBubble();
            bubbleTextMesh.text = line;
            bubbleTextMesh.gameObject.SetActive(true);
            bubbleBg.gameObject.SetActive(true);
            var renderer = bubbleTextMesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 1001;
                Bounds bounds = renderer.bounds;
                bubbleBg.transform.localScale = new Vector3(bounds.size.x + 0.3f, bounds.size.y + 0.18f, 1f);
            }
            monologueShowing = true;
            monologueTimer = duration;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (bubbleTextMesh != null)
                {
                    float spriteTop = spriteRenderer != null ? spriteRenderer.bounds.extents.y : 0.3f;
                    Vector3 bubblePos = transform.position + Vector3.up * (spriteTop + 0.55f);
                    bubbleTextMesh.transform.position = bubblePos;
                    if (bubbleBg != null) bubbleBg.transform.position = bubblePos;
                }
                yield return null;
            }
            if (hideAtEnd) HideMonologue();
        }

        private static Sprite sharedBubbleSprite;

        /// <summary>말풍선 배경용 1색 스프라이트 (디버그 점 제거 후에도 말풍선이 씀).</summary>
        private static Sprite GetSharedDotSprite()
        {
            if (sharedBubbleSprite != null) return sharedBubbleSprite;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            sharedBubbleSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return sharedBubbleSprite;
        }

        private void EnsureBubble()
        {
            if (bubbleTextMesh != null) return;

            var bgGo = new GameObject(gameObject.name + "_BubbleBg");
            bubbleBg = bgGo.AddComponent<SpriteRenderer>();
            bubbleBg.sprite = GetSharedDotSprite();
            bubbleBg.color = new Color(1f, 1f, 0.96f, 0.92f);
            bubbleBg.sortingOrder = 1000;
            bgGo.SetActive(false);

            var textGo = new GameObject(gameObject.name + "_Bubble");
            bubbleTextMesh = textGo.AddComponent<TextMesh>();
            bubbleTextMesh.characterSize = 0.045f;
            bubbleTextMesh.fontSize = 48;
            bubbleTextMesh.anchor = TextAnchor.MiddleCenter;
            bubbleTextMesh.alignment = TextAlignment.Center;
            bubbleTextMesh.color = new Color(0.15f, 0.1f, 0.08f);
            if (bubbleFont != null)
            {
                bubbleTextMesh.font = bubbleFont;
                textGo.GetComponent<MeshRenderer>().material = bubbleFont.material;
            }
            textGo.GetComponent<MeshRenderer>().sortingOrder = 1001;
            textGo.SetActive(false);
        }

        /// <summary>
        /// 이번 프레임 이동·상태로 스프라이트를 재생한다.
        /// 걷기/놀기 이동: 시트 1~4행(walkDown/Left/Right/Up).
        /// 놀기 정지: idle. 머물기/주저앉기/기절: stay/slumped/fainted.
        /// </summary>
        private void UpdateWalkAnimation(float dt)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null || Data == null) return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            bool moved = delta.sqrMagnitude > FacingMoveThresholdSq;

            // 이동 중이거나, 목적지/방황 목표가 있으면 걷기 사이클(시트 1~4행)
            bool wantsWalkCycle =
                Stats.State == ActionState.Walking
                || (Stats.State == ActionState.Playing && (moved || wanderTarget.HasValue));

            if (moved)
                UpdateFacingFromDelta(delta);

            bool advanceFrames = wantsWalkCycle || Stats.State == ActionState.Staying
                || Stats.State == ActionState.Slumped
                || Stats.State == ActionState.Fainted
                || Stats.State == ActionState.Playing;

            Sprite[] frames = ResolveAnimationFrames(wantsWalkCycle, out bool flipX);
            if (frames == null || frames.Length == 0) return;

            if (advanceFrames)
            {
                float interval = wantsWalkCycle ? AnimFrameInterval : AnimFrameInterval * 2f;
                animTimer += dt;
                if (animTimer >= interval)
                {
                    animTimer -= interval;
                    animFrame = (animFrame + 1) % frames.Length;
                }
            }
            else
            {
                animFrame = 0;
                animTimer = 0f;
            }

            if (animFrame >= frames.Length)
                animFrame = 0;

            int idx = animFrame;
            if (frames[idx] != null)
                spriteRenderer.sprite = frames[idx];
            spriteRenderer.flipX = flipX;
        }

        private void UpdateFacingFromDelta(Vector3 delta)
        {
            float ax = Mathf.Abs(delta.x);
            float ay = Mathf.Abs(delta.y);
            // 분리 밀림 등으로 축이 매 프레임 바뀌면 스프라이트가 깜빡이듯 끊긴다.
            if (facing == FacingDir.Left || facing == FacingDir.Right)
            {
                if (ay > ax * FacingAxisBias)
                    facing = delta.y > 0f ? FacingDir.Up : FacingDir.Down;
                else if (ax >= ay)
                    facing = delta.x > 0f ? FacingDir.Right : FacingDir.Left;
            }
            else
            {
                if (ax > ay * FacingAxisBias)
                    facing = delta.x > 0f ? FacingDir.Right : FacingDir.Left;
                else if (ay >= ax)
                    facing = delta.y > 0f ? FacingDir.Up : FacingDir.Down;
            }
        }

        private Sprite[] ResolveAnimationFrames(bool wantsWalkCycle, out bool flipX)
        {
            flipX = false;
            if (wantsWalkCycle)
                return WalkFramesForFacing(out flipX);

            switch (Stats.State)
            {
                case ActionState.Staying:
                    if (HasFrames(Data.stay)) return Data.stay;
                    break;
                case ActionState.Playing:
                    if (HasFrames(Data.idle)) return Data.idle;
                    break;
                case ActionState.Slumped:
                    if (HasFrames(Data.slumped)) return Data.slumped;
                    break;
                case ActionState.Fainted:
                    if (HasFrames(Data.fainted)) return Data.fainted;
                    break;
            }

            if (HasFrames(Data.idle)) return Data.idle;
            return WalkFramesForFacing(out flipX);
        }

        private Sprite[] WalkFramesForFacing(out bool flipX)
        {
            flipX = false;
            Sprite[] frames;
            switch (facing)
            {
                case FacingDir.Up:
                    frames = Data.walkUp;
                    break;
                case FacingDir.Right:
                    frames = Data.walkRight;
                    break;
                case FacingDir.Left:
                    // 전용 Left가 없으면 Right를 좌우반전해서 사용
                    if (HasFrames(Data.walkLeft))
                    {
                        frames = Data.walkLeft;
                    }
                    else if (HasFrames(Data.walkRight))
                    {
                        flipX = true;
                        frames = Data.walkRight;
                    }
                    else
                    {
                        frames = Data.walkLeft;
                    }
                    break;
                default:
                    frames = Data.walkDown;
                    break;
            }

            if (HasFrames(frames)) return frames;
            if (HasFrames(Data.walkDown))
            {
                flipX = false;
                return Data.walkDown;
            }
            return frames;
        }

        private static bool HasFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return false;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) return true;
            return false;
        }

        // ---------------- Walking ----------------

        private void EnterWalking()
        {
            Stats.State = ActionState.Walking;
            Stats.StateTimer = 0f;
            animFrame = 0;
            animTimer = 0f;
            lastPosition = transform.position;
            PickDestination();
        }

        private void PickDestination()
        {
            // 이전에 찜해둔 목적지가 있으면(도착 못 하고 재추첨하는 경우) 먼저 예약 해제.
            if (destination != null) destination.ReleaseReservation(this);

            destination = PropManager.Instance != null
                ? PropManager.Instance.GetRandomAvailableProp(this, previousProp)
                : null;

            // 고르는 즉시 찜해둬서, 같은 프레임에 다른 캐릭터가 고를 때 후보에서 빠지게 한다
            // (다 같이 같은 기물로 몰려가는 문제 방지).
            if (destination != null) destination.TryReserve(this);

            isWandering = destination == null;
            wanderTimer = 0f;
            wanderTarget = null;
        }

        private void TickWalking(float dt)
        {
            if (isWandering)
            {
                // 6-2 방황/재추첨 — Docs/06_행동룰.md (후보없음·자리없음을 한 루프로 통합)
                wanderTimer += dt;
                if (wanderTimer >= WanderRetrySeconds) { PickDestination(); return; }

                if (wanderTarget == null || Vector3.Distance(transform.position, wanderTarget.Value) < 0.05f)
                {
                    wanderTarget = MapBounds.RandomPoint(transform.position.z);
                }
                transform.position = MapBounds.Clamp(Vector3.MoveTowards(transform.position, wanderTarget.Value, moveSpeed * dt));
                ResolveSeparation(dt);
                return;
            }

            if (destination == null) { PickDestination(); return; }

            Vector3 targetPos = destination.transform.position;
            transform.position = MapBounds.Clamp(Vector3.MoveTowards(transform.position, targetPos, moveSpeed * dt));
            ResolveSeparation(dt);

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

        /// <summary>
        /// 걷는 중인 캐릭터끼리 너무 가까워지면 서로 살짝 밀어낸다.
        /// 상대가 앉아 있는 경우만 피하고, 전진 방향 성분은 약하게 유지해 제자리 떨림을 막는다.
        /// </summary>
        private void ResolveSeparation(float dt)
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                var other = ActiveAgents[i];
                if (other == null || other == this) continue;
                if (other.Stats.Stage == GrowthStage.Neok) continue;
                // 앉아/기절한 상대는 밀되, 같이 걷는 상대만 상호 분리(앉아있는 요괴 자리는 건드리지 않음)
                if (other.Stats.State == ActionState.Fainted) continue;

                Vector3 diff = transform.position - other.transform.position;
                diff.z = 0f;
                float dist = diff.magnitude;
                if (dist > 0.0001f && dist < SeparationRadius)
                    push += diff.normalized * (SeparationRadius - dist);
            }

            if (push == Vector3.zero) return;

            // 한 프레임 밀림 상한 — moveSpeed 대비 과도하면 전진이 상쇄되어 끊겨 보인다.
            float maxPush = moveSpeed * 0.55f * dt;
            Vector3 step = push * SeparationSpeed * dt;
            if (step.sqrMagnitude > maxPush * maxPush)
                step = step.normalized * maxPush;

            transform.position = MapBounds.Clamp(transform.position + step);
        }

        // ---------------- Staying ----------------

        private void EnterStaying()
        {
            Stats.State = ActionState.Staying;
            Stats.StateTimer = 0f;
        }

        private void TickStaying(float dt)
        {
            // 긴 dt에서도 기력 고갈 전 구간만 생산 (OfflineSimulator와 동일).
            float left = dt;
            int guard = 0;
            while (left > 0.0001f && Stats.State == ActionState.Staying && guard++ < 8)
                left -= TickStayingSlice(left);
        }

        /// <summary>
        /// 머물기 한 구간. 소모한 초를 반환한다.
        /// 생산은 앉아 있는 동안 기력 0까지 계속(상한 없음). 5분·33분치 강제 종료 없음.
        /// </summary>
        private float TickStayingSlice(float dt)
        {
            const float drain = 1f / 20f; // 6-2: 1분당 3 = 20초당 1
            float timeToZero = Stats.Stamina > 0f ? Stats.Stamina / drain : 0f;
            float slice = Mathf.Min(dt, timeToZero);

            if (slice <= 0f)
            {
                Stats.Stamina = 0f;
                EnterSlumped();
                return 0.0001f;
            }

            float staminaBefore = Stats.Stamina;
            Stats.StateTimer += slice;
            Stats.Stamina -= slice * drain;
            if (Stats.Stamina < 0f) Stats.Stamina = 0f;
            Requests.NotifyStaminaDrain(staminaBefore, Stats.Stamina);

            if (currentProp != null)
            {
                double perMinute = currentProp.GetBaseProductionThisLevel()
                                    * GetIntimacyCorrection()
                                    * GetEndingPropCorrection();
                currentProp.AddToMeritPile(perMinute / 60.0 * slice);
            }

            if (Stats.Stamina <= 0f)
            {
                Stats.Stamina = 0f;
                EnterSlumped();
            }

            return slice;
        }

        /// <summary>7-1: 친밀도 보정 = 1 + 친밀도/100. 넋은 친밀도 없음 → ×1.</summary>
        private double GetIntimacyCorrection()
        {
            if (Stats.Stage == GrowthStage.Neok) return 1.0;
            return 1.0 + Stats.Intimacy / 100.0;
        }

        /// <summary>7-1: 주인이 자기 엔딩 기물에 앉으면 ×2 (MVP: 옥토끼–떡절구).</summary>
        private double GetEndingPropCorrection()
        {
            if (currentProp == null || currentProp.data == null || Data == null) return 1.0;
            bool ownEnding = currentProp.data.isEndingProp && currentProp.data.owner == Data.id;
            return ownEnding ? 2.0 : 1.0;
        }

        /// <summary>전용 점유 아트 표시 중에는 캐릭터 스프라이트를 숨긴다.</summary>
        public void SetSpriteVisible(bool visible)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.enabled = visible;
        }

        private void LeaveCurrentProp()
        {
            if (currentProp != null)
            {
                currentProp.Vacate(this);
                previousProp = currentProp; // 다음 목적지 선정 시 제외 대상
                currentProp = null;
            }
            SetSpriteVisible(true);
        }

        // ---------------- Playing (놀기, 6-2) ----------------

        /// <summary>플레이어 드래그 시작 — 예약/점유를 풀고 AI를 멈춘다. 살짝 키워 들어올린 느낌을 낸다.</summary>
        public void BeginPlayerDrag()
        {
            if (!CanBeDraggedByPlayer) return;
            IsBeingDragged = true;
            isRefusing = false;
            ClearWalkDestination();
            LeaveCurrentProp();
            lastPosition = transform.position;
            dragScaleBefore = transform.localScale;
            transform.localScale = dragScaleBefore * DragLiftScale;
        }

        public void SetDragWorldPosition(Vector3 world)
        {
            if (!IsBeingDragged) return;
            world.z = transform.position.z;
            transform.position = MapBounds.Clamp(world);
            lastPosition = transform.position; // 드롭 직후 가짜 이동량으로 방향이 튀지 않게
        }

        /// <summary>
        /// 드래그 종료.
        /// 빈 기물 → 즉시 착석. 타 엔딩 기물 → 옆에 두고 3초 도리도리 후 걷기.
        /// 점유된 기물 → 옆에 내려놓음. 그 외 → 놀기.
        /// 주저앉기 중이면 지친 상태를 유지한 채 배치.
        /// </summary>
        public void EndPlayerDrag(PropSlot dropProp)
        {
            if (!IsBeingDragged) return;
            IsBeingDragged = false;
            transform.localScale = dragScaleBefore.sqrMagnitude > 0.0001f ? dragScaleBefore : transform.localScale;
            lastPosition = transform.position;

            if (Stats.State == ActionState.Slumped)
            {
                SettleSlumpedAfterDrag(dropProp);
                return;
            }

            if (dropProp != null && dropProp.IsForbiddenEndingFor(this))
            {
                PlaceBesideProp(dropProp, startWalking: false);
                BeginRefuseShake();
                return;
            }

            if (dropProp != null && !dropProp.IsOccupied && TrySitOnProp(dropProp))
                return;

            if (dropProp != null && dropProp.IsOccupied && dropProp.CanBeUsedBy(this))
            {
                PlaceBesideProp(dropProp);
                return;
            }

            EnterPlaying();
        }

        /// <summary>기물 옆에 내려놓는다. startWalking이면 빈 기물 탐색으로 이어간다.</summary>
        void PlaceBesideProp(PropSlot prop, bool startWalking = true)
        {
            ClearWalkDestination();
            LeaveCurrentProp();
            isWandering = false;
            wanderTarget = null;

            Vector3 origin = prop.transform.position;
            Vector3 dir = Vector3.right;
            if (prop.Occupant != null)
            {
                Vector3 away = transform.position - prop.Occupant.transform.position;
                if (away.sqrMagnitude < 0.0001f)
                    away = Random.value < 0.5f ? Vector3.left : Vector3.right;
                dir = away.normalized;
            }
            else
            {
                Vector3 away = transform.position - origin;
                if (away.sqrMagnitude > 0.0001f)
                    dir = away.normalized;
                else
                    dir = Random.value < 0.5f ? Vector3.left : Vector3.right;
            }

            Vector3 beside = origin + dir * DragBesideDistance;
            beside.z = transform.position.z;
            transform.position = MapBounds.Clamp(beside);
            lastPosition = transform.position;
            if (startWalking) EnterWalking();
        }

        /// <summary>타 엔딩 기물 거절: 좌우 도리도리 후 걷기.</summary>
        void BeginRefuseShake()
        {
            isRefusing = true;
            refuseTimer = RefuseDurationSeconds;
            refuseFlipTimer = 0f;
            facing = FacingDir.Left;
            animFrame = 0;
            animTimer = 0f;
            ApplyRefuseFacingSprite();
        }

        void TickRefuse(float dt)
        {
            refuseTimer -= dt;
            refuseFlipTimer += dt;
            if (refuseFlipTimer >= RefuseFlipInterval)
            {
                refuseFlipTimer = 0f;
                facing = facing == FacingDir.Left ? FacingDir.Right : FacingDir.Left;
                ApplyRefuseFacingSprite();
            }

            if (refuseTimer > 0f) return;

            isRefusing = false;
            EnterWalking();
        }

        void ApplyRefuseFacingSprite()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null || Data == null) return;

            Sprite[] frames = WalkFramesForFacing(out bool flipX);
            if (frames == null || frames.Length == 0)
            {
                frames = Data.idle;
                flipX = facing == FacingDir.Left;
            }
            if (frames == null || frames.Length == 0) return;

            if (frames[0] != null)
                spriteRenderer.sprite = frames[0];
            spriteRenderer.flipX = flipX;
        }

        /// <summary>주저앉기 드래그 드롭: 상태·12시간 타이머 유지, 가능하면 기물 점유. 점유·타 엔딩이면 옆에.</summary>
        private void SettleSlumpedAfterDrag(PropSlot dropProp)
        {
            if (dropProp != null
                && dropProp.CanBeUsedBy(this)
                && !dropProp.IsOccupied
                && dropProp.TryOccupy(this))
            {
                currentProp = dropProp;
                var p = dropProp.transform.position;
                transform.position = new Vector3(p.x, p.y, transform.position.z);
                return;
            }

            if (dropProp != null && (dropProp.IsOccupied && dropProp.CanBeUsedBy(this) || dropProp.IsForbiddenEndingFor(this)))
            {
                Vector3 origin = dropProp.transform.position;
                Vector3 away = transform.position - (dropProp.Occupant != null
                    ? dropProp.Occupant.transform.position
                    : origin);
                if (away.sqrMagnitude < 0.0001f)
                    away = Random.value < 0.5f ? Vector3.left : Vector3.right;
                Vector3 beside = origin + away.normalized * DragBesideDistance;
                beside.z = transform.position.z;
                transform.position = MapBounds.Clamp(beside);
            }
            // 기물 아니면 드롭 좌표에 그대로 주저앉음 (State는 이미 Slumped)
        }

        /// <summary>
        /// 놀기: 기물 점유를 풀고 5분간 맵을 돌아다닌다. 기력 소모·생산 없음.
        /// 진입: 기물 아닌 곳 드래그 드롭.
        /// 종료: 5분 후 Walking(목표 타겟팅) → 기물. Docs/06_행동룰.md
        /// </summary>
        public void EnterPlaying()
        {
            if (Stats.Stage == GrowthStage.Neok) return;
            if (Stats.State == ActionState.Fainted) return;

            bool fromStay = Stats.State == ActionState.Staying;

            ClearWalkDestination();
            LeaveCurrentProp();
            isWandering = false;
            wanderTarget = null;
            Stats.State = ActionState.Playing;
            Stats.StateTimer = 0f;
            animFrame = 0;
            animTimer = 0f;
            lastPosition = transform.position;

            // 10장 기물 요구: 머물다 일어남(놀기 진입)
            if (fromStay) Requests.TryStartPropRequest();
        }

        private void TickPlaying(float dt)
        {
            Stats.StateTimer += dt;
            if (Stats.StateTimer >= PlayDurationSeconds)
            {
                EnterWalking();
                return;
            }

            // 목적지 없이 맵을 떠돈다. 가끔 한곳에 멈춰 쉬는 연출은 wander 도착 시 짧은 대기로 대체.
            if (wanderTarget == null || Vector3.Distance(transform.position, wanderTarget.Value) < 0.05f)
            {
                // 도착 후 1~3초 쉬는 느낌: 다음 타겟을 바로 안 고르고 타이머만 쓰려면 복잡해지므로
                // 랜덤 지점으로 계속 이동 (기력 소모 없음).
                wanderTarget = MapBounds.RandomPoint(transform.position.z);
            }
            transform.position = MapBounds.Clamp(
                Vector3.MoveTowards(transform.position, wanderTarget.Value, moveSpeed * dt));
            ResolveSeparation(dt);
        }

        /// <summary>
        /// 놀기·걷기 중 기물에 올려 앉히기. 성공 시 머물기(기력 0까지 생산)가 새로 시작된다.
        /// 이미 다른 요괴가 앉아 있거나 엔딩기물 제한이면 false.
        /// </summary>
        public bool TrySitOnProp(PropSlot prop)
        {
            if (prop == null || Stats.Stage == GrowthStage.Neok) return false;
            if (Stats.State == ActionState.Fainted) return false;
            if (Stats.State == ActionState.Slumped) return false; // 주저앉기는 SettleSlumpedAfterDrag

            ClearWalkDestination();
            LeaveCurrentProp();
            isWandering = false;
            wanderTarget = null;

            if (!prop.CanBeUsedBy(this)) return false;
            if (!prop.TryOccupy(this)) return false;

            currentProp = prop;
            var p = prop.transform.position;
            transform.position = MapBounds.Clamp(new Vector3(p.x, p.y, transform.position.z));
            EnterStaying();
            Requests.NotifySatOnProp(prop);
            return true;
        }

        private void ClearWalkDestination()
        {
            if (destination != null)
            {
                destination.ReleaseReservation(this);
                destination = null;
            }
            isWandering = false;
            wanderTarget = null;
        }

        // ---------------- Slumped / Fainted ----------------

        private void EnterSlumped()
        {
            // 6-2 기물 배정 규칙: 주저앉기·기절 중에는 현재 기물을 계속 점유한다 (Vacate 호출 안 함)
            Stats.State = ActionState.Slumped;
            Stats.StateTimer = 0f;
            Requests.ClearAll(); // 신규 요구 없음·진행 중 요구도 정리
        }

        private void TickSlumped(float dt)
        {
            TickSlumpedSlice(dt);
        }

        private float TickSlumpedSlice(float dt)
        {
            float timeToFaint = Mathf.Max(0f, FaintThresholdSeconds - Stats.StateTimer);
            float slice = Mathf.Min(dt, timeToFaint > 0f ? timeToFaint : dt);
            if (slice <= 0f) slice = dt;

            Stats.StateTimer += slice;
            if (Stats.StateTimer >= FaintThresholdSeconds)
            {
                Stats.State = ActionState.Fainted;
                Stats.StateTimer = 0f;
                Requests.ClearAll(); // 기절 시 요구 삭제 [확정]
            }
            return slice;
        }

        private float TickPlayingSlice(float dt)
        {
            float timeToEnd = Mathf.Max(0f, PlayDurationSeconds - Stats.StateTimer);
            float slice = Mathf.Min(dt, timeToEnd > 0f ? timeToEnd : dt);
            if (slice <= 0f) slice = dt;

            Stats.StateTimer += slice;
            if (Stats.StateTimer >= PlayDurationSeconds)
                EnterWalking();
            return slice;
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

        /// <summary>넋 → 혼. 친밀도 0부터. playFx면 줌인·변화 후 확인 창.</summary>
        public void EvolveToHon(bool playFx = true)
        {
            if (Stats.Stage != GrowthStage.Neok || evolvingToHon) return;

            Stats.Stage = GrowthStage.Hon;
            Stats.Intimacy = 0f;
            Stats.Stamina = Mathf.Max(Stats.Stamina, 100f);
            Stats.StateTimer = 0f;
            Requests.ClearAll();
            transform.position = MapBounds.Clamp(neokLogicalPos.sqrMagnitude > 0.0001f
                ? neokLogicalPos
                : transform.position);
            lastPosition = transform.position;

            if (playFx && isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                StartCoroutine(EvolveToHonFxRoutine());
            }
            else
            {
                CharacterSpawner.EnsureHonVisual(this);
                EnterWalking();
                ApplyAnimationFrameImmediate();
            }

            Debug.Log($"[CharacterAgent] {Data?.displayName ?? name} 넋→혼 진화");
        }

        private IEnumerator EvolveToHonFxRoutine()
        {
            evolvingToHon = true;
            Yoegoe.UI.CeremonyGate.Begin();

            var focus = Yoegoe.Debugging.MapCameraFocus.Instance;
            if (focus != null)
            {
                var cam = Camera.main;
                float ortho = cam != null ? Mathf.Max(2.2f, cam.orthographicSize * 0.55f) : 2.8f;
                focus.Focus(transform.position, ortho, 0.55f);
                yield return new WaitForSecondsRealtime(0.55f);
            }

            var meshRenderer = GetComponent<MeshRenderer>();
            Vector3 neokScale = transform.localScale;
            const float fadeOut = 0.45f;
            float t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / fadeOut);
                float e = u * u;
                transform.localScale = Vector3.Lerp(neokScale, Vector3.zero, e);
                SetMeshAlpha(meshRenderer, 1f - u);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            CharacterSpawner.EnsureHonVisual(this);

            var scaleSettings = ArtScaleSettings.GetOrDefault();
            Vector3 honScale = Vector3.one * scaleSettings.characterScale;
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = 0f;
                spriteRenderer.color = c;
            }
            transform.localScale = honScale * 0.7f;

            const float fadeIn = 0.65f;
            t = 0f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeIn));
                transform.localScale = Vector3.Lerp(honScale * 0.7f, honScale, u);
                if (spriteRenderer != null)
                {
                    var c = spriteRenderer.color;
                    c.a = u;
                    spriteRenderer.color = c;
                }
                UpdateSortingOrder();
                yield return null;
            }

            transform.localScale = honScale;
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = 1f;
                spriteRenderer.color = c;
            }

            ApplyAnimationFrameImmediate();

            string who = Data != null && !string.IsNullOrEmpty(Data.displayName)
                ? Data.displayName
                : name;
            string msg = who + "가 혼으로 진화했다.";

            bool confirmed = false;
            if (Yoegoe.UI.EvolutionConfirmPopup.Instance != null)
            {
                Yoegoe.UI.EvolutionConfirmPopup.Instance.Open(msg, () => confirmed = true);
                while (!confirmed) yield return null;
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.8f);
            }

            if (focus != null) focus.Restore(0.45f);
            yield return new WaitForSecondsRealtime(0.2f);

            EnterWalking();
            evolvingToHon = false;
            Yoegoe.UI.CeremonyGate.End();
            Yoegoe.Save.GameSaveBridge.SaveFromWorld();
        }

        private static void SetMeshAlpha(MeshRenderer meshRenderer, float alpha)
        {
            if (meshRenderer == null) return;
            var mat = meshRenderer.material;
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor"))
            {
                var c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
            if (mat.HasProperty("_Color"))
            {
                var c = mat.color;
                c.a = alpha;
                mat.color = c;
            }
        }

        /// <summary>넋 도깨비불: 맵을 천천히 떠돌며 위아래로 둥둥.</summary>
        private void TickNeokFloat(float dt)
        {
            if (neokLogicalPos.sqrMagnitude < 0.0001f && transform.position.sqrMagnitude > 0.0001f)
                neokLogicalPos = transform.position;

            if (!neokDriftTarget.HasValue
                || Vector2.Distance(neokLogicalPos, neokDriftTarget.Value) < 0.12f)
            {
                PickNeokDriftTarget();
            }

            if (neokDriftTarget.HasValue)
            {
                neokLogicalPos = Vector3.MoveTowards(
                    neokLogicalPos, neokDriftTarget.Value, NeokDriftSpeed * dt);
                neokLogicalPos = MapBounds.Clamp(neokLogicalPos);
            }

            neokBobPhase += dt * NeokBobSpeed;
            float bob = Mathf.Sin(neokBobPhase) * NeokBobAmplitude;
            transform.position = new Vector3(neokLogicalPos.x, neokLogicalPos.y + bob, neokLogicalPos.z);
            lastPosition = transform.position;
        }

        private void PickNeokDriftTarget()
        {
            Vector2 min = MapBounds.Min;
            Vector2 max = MapBounds.Max;
            // bounds 미설정 시 현재 근처만
            if (max.x - min.x < 0.5f || max.y - min.y < 0.5f)
            {
                neokDriftTarget = neokLogicalPos + (Vector3)(Random.insideUnitCircle * 1.2f);
                return;
            }

            neokDriftTarget = new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                neokLogicalPos.z);
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
