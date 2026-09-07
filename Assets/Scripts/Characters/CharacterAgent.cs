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

        [Header("디버그: 기력 소모 배속 (테스트용, 확인 끝나면 1로 되돌릴 것)")]
        [Tooltip("1 = 기획서 그대로(1분당 3, 20초당 1). 8이면 8배 빨리 줄어서 2.5초당 1점 — " +
                 "눈으로 바로 확인하기 위한 임시값. 확인 끝나면 1로 되돌려주세요.")]
        public float staminaDrainMultiplier = 8f;

        [Header("혼잣말 (6-4장)")]
        [Tooltip("혼잣말 말풍선에 쓸 한글 폰트. 비워두면 유니티 기본 폰트로 나와서 한글이 깨질 수 있음.")]
        public Font bubbleFont;
        private const float SeparationRadius = 0.6f; // 이보다 가까워지면 서로 밀어냄 (안 겹치게)
        private const float SeparationSpeed = 3f;

        private static readonly System.Collections.Generic.List<CharacterAgent> ActiveAgents
            = new System.Collections.Generic.List<CharacterAgent>();

        /// <summary>지금 씬에 존재하는 모든 캐릭터 (슬롯바 UI 등에서 순회용). 절대 수정하지 말 것.</summary>
        public static System.Collections.Generic.IReadOnlyList<CharacterAgent> All => ActiveAgents;

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

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            lastPosition = transform.position;
        }

        private void OnEnable() => ActiveAgents.Add(this);
        private void OnDisable()
        {
            ActiveAgents.Remove(this);
            if (stateDot != null) Destroy(stateDot.gameObject);
            if (bubbleBg != null) Destroy(bubbleBg.gameObject);
            if (bubbleTextMesh != null) Destroy(bubbleTextMesh.gameObject);
        }

        // ---------------- 상태 디버그 표시 (에디터 Gizmo는 WebGL 빌드에선 안 보여서 따로 만듦) ----------------
        private SpriteRenderer stateDot;
        private static Sprite sharedDotSprite;

        private static Sprite GetSharedDotSprite()
        {
            if (sharedDotSprite != null) return sharedDotSprite;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            sharedDotSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return sharedDotSprite;
        }

        /// <summary>
        /// 지금 뭘 하고 있는지(걷기=초록/머물기=파랑/주저앉기=노랑/기절=빨강)를 캐릭터 머리 위에
        /// 작은 점으로 항상 표시한다. Scene 뷰 Gizmo와 달리 실제 빌드(WebGL 포함)에서도 보여서,
        /// "멈춰 보이는 게 버그인지 원래 일하는 중(머물기)인지" 눈으로 바로 구분할 수 있게 하기 위함.
        /// </summary>
        private void UpdateStateDot()
        {
            if (stateDot == null)
            {
                var dotGO = new GameObject(gameObject.name + "_StateDot");
                stateDot = dotGO.AddComponent<SpriteRenderer>();
                stateDot.sprite = GetSharedDotSprite();
                stateDot.sortingOrder = 1000;
                dotGO.transform.localScale = Vector3.one * 0.12f;
            }

            float spriteTop = spriteRenderer != null ? spriteRenderer.bounds.extents.y : 0.3f;
            stateDot.transform.position = transform.position + Vector3.up * (spriteTop + 0.15f);
            stateDot.color = Stats.State switch
            {
                ActionState.Walking => Color.green,
                ActionState.Staying => new Color(0.25f, 0.55f, 1f), // 파랑 = 기물에서 일하는 중 (정상)
                ActionState.Slumped => Color.yellow,
                ActionState.Fainted => Color.red,
                _ => Color.white
            };
        }

        private void Start()
        {
            // Data 초기화는 Awake가 아니라 Start에서 한다: 코드로 캐릭터를 생성할 때
            // AddComponent<CharacterAgent>() 직후에 Data를 대입하는 패턴(TestSceneBootstrap 등)이
            // 흔한데, AddComponent가 Awake를 즉시 실행시키기 때문에 Awake 시점엔 Data가 아직
            // null이라 초기화가 안 먹는 버그가 있었다. Start는 모든 오브젝트의 Awake가 끝난
            // 다음 프레임 이전에 실행되므로 이 시점엔 Data가 확실히 채워져 있다.
            if (Data != null)
            {
                Stats.Stage = Data.startingStage;
                Stats.Intimacy = Data.startingIntimacy;
                Stats.Stamina = Data.startingStamina;
            }

            if (Stats.Stage != GrowthStage.Neok) EnterWalking();

            // 로딩 직후 모든 캐릭터가 동시에 혼잣말을 시작하지 않도록 첫 대사까지 약간의 랜덤 지연을 둔다.
            monologueTimer = Random.Range(2f, MonologueMinInterval);
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

            UpdateWalkAnimation(dt);
            UpdateStateDot();
            if (Stats.State != ActionState.Fainted) UpdateMonologue(dt);
        }

        // ---------------- 혼잣말 (6-4장, 05_기획_미확정사항.md 9번) ----------------
        // 걷기/머물기 중 랜덤 주기(30초~1분)로 머리 위 말풍선이 뜬다 (유지 시간 약 10초).
        // 캐릭터를 탭해도 즉시 뜨고, 이미 떠 있는 상태에서 또 탭하면 내용이 바뀌고 유지 시간이 10초로 다시 연장된다.
        // (탭하면 상세화면으로 이동하는 것은 "요구" 말풍선 쪽 규칙이라 여기선 적용 안 함 — 09번 문서 참고)

        private TextMesh bubbleTextMesh;
        private SpriteRenderer bubbleBg;
        private float monologueTimer;
        private bool monologueShowing;
        private string lastMonologueLine;
        private const float MonologueDisplaySeconds = 10f;
        private const float MonologueMinInterval = 30f;
        private const float MonologueMaxInterval = 60f;

        private void UpdateMonologue(float dt)
        {
            if (Data == null || Data.monologueLines == null || Data.monologueLines.Length == 0) return;

            if (monologueTimer <= 0f)
            {
                monologueTimer = 0f; // 음수로 계속 내려가지 않게
                if (monologueShowing) HideMonologue();
                else ShowMonologue();
            }
            monologueTimer -= dt;

            if (monologueShowing && bubbleTextMesh != null)
            {
                float spriteTop = spriteRenderer != null ? spriteRenderer.bounds.extents.y : 0.3f;
                Vector3 bubblePos = transform.position + Vector3.up * (spriteTop + 0.55f);
                bubbleTextMesh.transform.position = bubblePos;
                if (bubbleBg != null) bubbleBg.transform.position = bubblePos;
            }
        }

        /// <summary>
        /// 캐릭터를 탭했을 때 CharacterTapRouter가 호출한다. 안 떠 있으면 새로 띄우고,
        /// 이미 떠 있으면 문구를 바꾸고 유지 시간을 다시 10초로 연장한다 (기획 09번 규칙).
        /// </summary>
        public void OnTapped()
        {
            if (Stats.State == ActionState.Fainted) return;
            if (Data == null || Data.monologueLines == null || Data.monologueLines.Length == 0) return;
            ShowMonologue();
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

        /// <summary>같은 문구가 연달아 나오지 않도록, 대사가 2개 이상이면 직전과 다른 것을 고른다.</summary>
        private string PickMonologueLine()
        {
            var lines = Data.monologueLines;
            if (lines.Length <= 1) return lines[0];
            string line;
            do { line = lines[Random.Range(0, lines.Length)]; } while (line == lastMonologueLine);
            lastMonologueLine = line;
            return line;
        }

        private void HideMonologue()
        {
            if (bubbleTextMesh != null) bubbleTextMesh.gameObject.SetActive(false);
            if (bubbleBg != null) bubbleBg.gameObject.SetActive(false);
            monologueShowing = false;
            monologueTimer = Random.Range(MonologueMinInterval, MonologueMaxInterval);
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
            bubbleTextMesh.characterSize = 0.08f;
            bubbleTextMesh.fontSize = 64;
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
        /// 이번 프레임 실제 이동량(transform.position 변화)으로 방향을 판단해
        /// Data의 방향별 4프레임 배열을 순환 재생한다. 멈춰있으면(대기·머무르기·기절 등) 0번 프레임으로 정지.
        /// spriteRenderer나 Data가 없으면 조용히 아무것도 안 함(아트 없이도 기존처럼 동작).
        /// </summary>
        private void UpdateWalkAnimation(float dt)
        {
            if (spriteRenderer == null || Data == null) return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            bool isMoving = delta.sqrMagnitude > 0.0000001f;

            if (isMoving)
            {
                facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? (delta.x > 0f ? FacingDir.Right : FacingDir.Left)
                    : (delta.y > 0f ? FacingDir.Up : FacingDir.Down);

                animTimer += dt;
                if (animTimer >= AnimFrameInterval)
                {
                    animTimer -= AnimFrameInterval;
                    animFrame = (animFrame + 1) % 4;
                }
            }
            else
            {
                animFrame = 0;
                animTimer = 0f;
            }

            Sprite[] frames = facing switch
            {
                FacingDir.Down => Data.walkDown,
                FacingDir.Up => Data.walkUp,
                FacingDir.Left => Data.walkLeft,
                FacingDir.Right => Data.walkRight,
                _ => Data.walkDown
            };

            if (frames != null && frames.Length > 0)
            {
                int idx = Mathf.Clamp(animFrame, 0, frames.Length - 1);
                if (frames[idx] != null) spriteRenderer.sprite = frames[idx];
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
                // 6-2 "후보가 없으면 30초 걷고 재추첨" / "자리가 없으면 걷거나 길바닥에 앉아 쉰다"를
                // 하나의 대기-후-재추첨 루프로 단순화 (문서 표현이 두 케이스를 명확히 구분 안 해서 통합함).
                // [버그 수정] 예전엔 이 분기에서 타이머만 세고 실제로는 제자리에 가만히 서 있었다
                // ("돌아다녀야하는데 다 같이 멈춰있다" 버그의 일부). 맵(MapBounds) 안의 랜덤한 지점을
                // 목적지로 삼아 실제로 걸어다니게 하고, 도착하면 다음 랜덤 지점을 또 고른다.
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
        /// 걷는 중인 캐릭터끼리 너무 가까워지면(스프라이트가 겹쳐 보일 정도) 서로 밀어낸다.
        /// 자리 잡고 일하는 중(Staying 등)인 캐릭터의 위치는 여기서 건드리지 않는다 — 이 함수는
        /// "이번에 움직이고 있는 나"의 위치만 보정하고, 상대방 위치는 그대로 둔다.
        /// </summary>
        private void ResolveSeparation(float dt)
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                var other = ActiveAgents[i];
                if (other == null || other == this) continue;

                Vector3 diff = transform.position - other.transform.position;
                diff.z = 0f;
                float dist = diff.magnitude;
                if (dist > 0.0001f && dist < SeparationRadius)
                {
                    push += diff.normalized * (SeparationRadius - dist);
                }
            }

            if (push != Vector3.zero)
            {
                transform.position = MapBounds.Clamp(transform.position + push * SeparationSpeed * dt);
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
            Stats.Stamina -= dt / 20f * staminaDrainMultiplier; // 6-2: 1분당 3 소모 = 20초당 1 (디버그 배속 적용)

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
