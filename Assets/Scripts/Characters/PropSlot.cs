using System;
using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Cooking;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.UI;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 씬에 배치된 기물 하나.
    /// 공덕은 기물별 더미에 쌓이고, 탭으로 수거한다 (기획 7-1·7-2).
    /// 자원 기물(물·엽전·재료)은 주기마다 1개씩 보관에 쌓이고, 만창이면 생산·기력소모가 멈춘다 (Docs/00 §6-2).
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

        PropStorage.State storage;
        /// <summary>활터·약초밭: 보관 중인 재료(1개당 1칸, 뽑힌 순서).</summary>
        readonly List<int> pendingIngredients = new List<int>();

        public PropResourceType ResourceType => data != null ? data.resourceType : PropResourceType.None;
        public bool IsResourceProp =>
            ResourceType == PropResourceType.PurifiedWater || ResourceType == PropResourceType.Yeopjeon
            || ResourceType == PropResourceType.Hunt || ResourceType == PropResourceType.Gather;
        public int StoredResources => storage.Stored;
        public int ResourceCapacity => data != null ? PropStorage.Capacity(data.baseCapacity, level) : 0;
        public bool HasPendingResources => IsBuilt && IsResourceProp && storage.Stored > 0;
        /// <summary>탭 수거할 게 있는지 (공덕 더미 또는 자원 보관).</summary>
        public bool HasPendingCollectible => HasPendingMerit || HasPendingResources;
        public IReadOnlyList<int> PendingIngredients => pendingIngredients;

        /// <summary>만창 — 앉아 있어도 생산·기력소모 정지.</summary>
        public bool IsStorageHalted
        {
            get
            {
                if (!IsBuilt || data == null) return false;
                if (IsResourceProp) return PropStorage.IsHalted(storage, ResourceCapacity);
                if (ResourceType == PropResourceType.Merit)
                    return PendingMerit.ToDouble() >= MeritCapacity - 0.0001;
                return false;
            }
        }

        public double MeritCapacity => data == null ? double.PositiveInfinity
            : ProductionFormula.MeritCapacity(data.baseProductionPerMinute, level, data.levelGrowth, data.meritCapacityMinutes);

        private TextMesh pileLabel;
        private TextMesh lockLabel;
        private SpriteRenderer spriteRenderer;
        private Renderer meshRenderer;
        private Sprite builtSprite;
        private Sprite occupiedByOwnerSprite;
        private Color builtTint = Color.white;
        private static Font sharedPileFont;
        private CharacterAgent hiddenOccupantVisual;
        private int lastPileStage = -1;
        private string lastPileAmount;
        private int lastPileRounded = int.MinValue;
        private double lastPileDisplayKey = double.NaN;

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
            // 플레이어 드롭 등이 다른 요괴의 걷기 예약보다 우선
            if (IsReserved && ReservedBy != agent)
                ReservedBy = null;
            Occupant = agent;
            ReservedBy = null;
            RefreshOccupancyVisual();
            return true;
        }

        /// <summary>예약만 강제 해제 (드롭 착석용).</summary>
        public void ClearReservation()
        {
            ReservedBy = null;
        }

        /// <summary>점유 해제. 기절 중에는 호출하지 않는다. 더미는 기물에 남는다.</summary>
        public void Vacate(CharacterAgent agent)
        {
            if (Occupant == agent)
            {
                Occupant = null;
                RefreshOccupancyVisual();
            }
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
            RefreshOccupancyVisual();
        }

        /// <summary>세이브 복원용 강제 점유.</summary>
        public void ForceOccupyForSaveRestore(CharacterAgent agent)
        {
            if (!IsBuilt) return;
            Occupant = agent;
            ReservedBy = null;
            RefreshOccupancyVisual();
        }

        /// <summary>7-1: 머물기 중 생산분을 기물 더미에 적립. HUD 지갑으로는 바로 안 들어간다. 만창에서 멈춘다.</summary>
        public void AddToMeritPile(BigNumber amount)
        {
            if (!IsBuilt || amount.Mantissa == 0) return;
            PendingMerit += amount;
            double cap = MeritCapacity;
            if (!double.IsInfinity(cap) && PendingMerit.ToDouble() > cap)
                PendingMerit = (BigNumber)cap;
        }

        /// <summary>
        /// 앉은 요괴가 dt초 머무는 동안의 생산. 반환 = 실제로 일한 초(이만큼만 기력이 닳는다).
        /// 만창이면 0. meritPerMinute는 보정까지 끝난 공덕 분당 산출(공덕 기물만 사용).
        /// </summary>
        public float ProduceWhileStaying(float dt, double meritPerMinute)
        {
            if (!IsBuilt || data == null || dt <= 0f) return dt;
            switch (ResourceType)
            {
                case PropResourceType.Merit:
                {
                    if (meritPerMinute <= 0) return dt;
                    double cap = MeritCapacity;
                    double room = double.IsInfinity(cap) ? double.MaxValue : cap - PendingMerit.ToDouble();
                    if (room <= 0.0001) return 0f;
                    float worked = (float)Math.Min(dt, room / meritPerMinute * 60.0);
                    AddToMeritPile(meritPerMinute / 60.0 * worked);
                    return worked;
                }
                case PropResourceType.PurifiedWater:
                case PropResourceType.Yeopjeon:
                case PropResourceType.Hunt:
                case PropResourceType.Gather:
                {
                    var type = ResourceType;
                    return PropStorage.Advance(ref storage, data.cycleMinutes * 60f, ResourceCapacity,
                        PropStorage.OverflowChance(level), dt, () => UnityEngine.Random.value,
                        () =>
                        {
                            if (type == PropResourceType.Hunt || type == PropResourceType.Gather)
                                pendingIngredients.Add((int)PropCatalog.RollIngredient(type, UnityEngine.Random.value));
                        });
                }
                default:
                    return dt; // 화덕 등 — 산출 없음, 기력은 평소대로
            }
        }

        /// <summary>자원 기물 수거 지점 통지 (propSlot, 종류, 개수). UI가 구독해 연출.</summary>
        public static event Action<PropSlot, PropResourceType, int> ResourcesCollected;

        /// <summary>탭 수거 — 공덕 더미 또는 자원 보관을 지갑으로.</summary>
        public bool TryCollect()
        {
            if (HasPendingResources) return TryCollectResources();
            return TryCollectMerit();
        }

        bool TryCollectResources()
        {
            if (!HasPendingResources || GameEconomy.Instance == null) return false;
            var type = ResourceType;
            int n = PropStorage.TakeAll(ref storage);
            switch (type)
            {
                case PropResourceType.PurifiedWater: GameEconomy.Instance.AddPurifiedWater(n); break;
                case PropResourceType.Yeopjeon: GameEconomy.Instance.AddYeopjeon(n); break;
                default:
                    foreach (var ing in pendingIngredients)
                        GameEconomy.Instance.AddMaterial((CookingIngredientId)ing, 1);
                    break;
            }
            pendingIngredients.Clear();
            ForceRefreshPileLabel();
            ResourcesCollected?.Invoke(this, type, n);
            return true;
        }

        /// <summary>세이브용 자원 보관 스냅샷.</summary>
        public void CaptureStorage(out int stored, out float cycleProgress, out bool overflowJudged, out int[] ingredients)
        {
            stored = storage.Stored;
            cycleProgress = storage.CycleProgressSeconds;
            overflowJudged = storage.OverflowJudged;
            ingredients = pendingIngredients.ToArray();
        }

        /// <summary>세이브 로드용 자원 보관 복원.</summary>
        public void RestoreStorage(int stored, float cycleProgress, bool overflowJudged, int[] ingredients)
        {
            storage = new PropStorage.State
            {
                Stored = Mathf.Max(0, stored),
                CycleProgressSeconds = Mathf.Max(0f, cycleProgress),
                OverflowJudged = overflowJudged
            };
            pendingIngredients.Clear();
            if (ingredients != null) pendingIngredients.AddRange(ingredients);
        }

        /// <summary>
        /// 7-2: 기물 탭 수거. 더미를 비우고 플레이어 공덕(HUD)에 더한다.
        /// </summary>
        /// <summary>
        /// 기물 수거 연출 지점 통지. UI(GameHud)가 구독해서 실제 이펙트를 재생한다 —
        /// 이 클래스는 UI를 모른다.
        /// </summary>
        public static event System.Action<Vector3> MeritCollectedAtWorld;

        public bool TryCollectMerit()
        {
            if (!HasPendingMerit) return false;
            var collected = TakePendingMerit();
            GameEconomy.Instance.AddMerit(collected);
            MeritCollectedAtWorld?.Invoke(TopAnchorWorld(0.15f));
            return true;
        }

        /// <summary>기물 스프라이트 윗변 중앙 + pad (연출·드래그 마커 위치).</summary>
        public Vector3 TopAnchorWorld(float pad)
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
                return new Vector3(transform.position.x, spriteRenderer.bounds.max.y + pad, transform.position.z);
            return transform.position + Vector3.up * (0.25f + pad);
        }

        /// <summary>드래그 중인 요괴가 지금 바로 앉을 수 있는 기물인지 (금색 ▼ 마커).</summary>
        public bool CanSitNow(CharacterAgent agent) => IsBuilt && !IsOccupied && CanBeUsedBy(agent);

        /// <summary>더미만 비워 반환 (일괄 수거용 — HUD에 바로 넣지 않음).</summary>
        public BigNumber TakePendingMerit()
        {
            var collected = PendingMerit;
            PendingMerit = BigNumber.Zero;
            ForceRefreshPileLabel();
            return collected;
        }

        /// <summary>더미 UI를 즉시 갱신 (LateUpdate 대기 없이).</summary>
        public void ForceRefreshPileLabel() => RefreshPileLabel();

        /// <summary>
        /// TEMP: 공덕 수거 히트 = 더미(***·숫자) 라벨만. 기물 본체는 포함하지 않는다.
        /// </summary>
        public bool TryGetPileLabelHitScore(Vector3 world, float padding, out float score)
        {
            score = float.MaxValue;
            if (!HasPendingCollectible) return false;
            if (pileLabel == null || !pileLabel.gameObject.activeInHierarchy) return false;

            var mr = pileLabel.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Bounds b = mr.bounds;
                b.Expand(Mathf.Max(0f, padding));
                Vector3 p = world;
                p.z = b.center.z;
                if (!b.Contains(p)) return false;
                score = Vector2.Distance(b.center, p);
                return true;
            }

            // MeshRenderer 없을 때: LowerCenter 기준 대략 박스
            Vector3 c = pileLabel.transform.position;
            float halfW = 0.4f + Mathf.Max(0f, padding);
            float h = 0.85f + Mathf.Max(0f, padding);
            if (Mathf.Abs(world.x - c.x) > halfW) return false;
            if (world.y < c.y - padding || world.y > c.y + h) return false;
            score = Vector2.Distance(new Vector2(c.x, c.y + h * 0.5f), world);
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
            if (!IsBuilt || data == null) return 0;
            if (data.resourceType != PropResourceType.Merit) return 0;
            return data.baseProductionPerMinute * ProductionFormula.LevelMultiplier(level, data.levelGrowth);
        }

        public bool CanBeUsedBy(CharacterAgent agent)
        {
            if (!IsBuilt) return false;
            if (data == null || agent == null || agent.Data == null) return true;
            if (data.isEndingProp && data.owner != agent.Data.id) return false;
            return true;
        }

        /// <summary>다른 요괴의 엔딩 기물(앉을 수 없음 → 옆 배치·거절 연출용).</summary>
        public bool IsForbiddenEndingFor(CharacterAgent agent)
        {
            if (!IsBuilt || data == null || agent == null || agent.Data == null) return false;
            return data.isEndingProp && data.owner != agent.Data.id;
        }

        public string DisplayName => data != null && !string.IsNullOrEmpty(data.displayName)
            ? data.displayName
            : name;

        /// <summary>Main이 건립 시 쓸 스프라이트·틴트를 기억.</summary>
        public void SetBuiltAppearance(Sprite sprite, Color tint, Sprite occupiedByOwner = null)
        {
            builtSprite = sprite;
            builtTint = tint;
            occupiedByOwnerSprite = occupiedByOwner != null
                ? occupiedByOwner
                : data != null ? data.occupiedByOwnerSprite : null;
        }

        /// <summary>주인 전용 점유 아트가 있으면 기물 스프라이트를 바꾸고, 캐릭터 본체를 숨긴다.</summary>
        public void RefreshOccupancyVisual()
        {
            bool useOccupied = ShouldShowOwnerOccupationArt();

            if (spriteRenderer != null && IsBuilt)
            {
                Sprite next = useOccupied
                    ? (occupiedByOwnerSprite != null ? occupiedByOwnerSprite : builtSprite)
                    : builtSprite;
                if (next != null) spriteRenderer.sprite = next;
                spriteRenderer.color = Color.white;
                spriteRenderer.enabled = true;
            }

            // 이전 숨김 복구
            if (hiddenOccupantVisual != null && (!useOccupied || hiddenOccupantVisual != Occupant))
            {
                hiddenOccupantVisual.SetSpriteVisible(true);
                hiddenOccupantVisual = null;
            }

            if (useOccupied && Occupant != null)
            {
                Occupant.SetSpriteVisible(false);
                hiddenOccupantVisual = Occupant;
            }
        }

        private bool ShouldShowOwnerOccupationArt()
        {
            if (!IsOccupied || Occupant == null || Occupant.Data == null) return false;
            Sprite art = occupiedByOwnerSprite != null
                ? occupiedByOwnerSprite
                : (data != null ? data.occupiedByOwnerSprite : null);
            if (art == null) return false;
            if (data == null) return false;
            if (!data.hasUniqueEndingAnimation) return false;
            return data.isEndingProp && data.owner == Occupant.Data.id;
        }

        private void ApplyBuiltVisual()
        {
            if (lockLabel != null) lockLabel.gameObject.SetActive(false);

            if (meshRenderer != null && !(meshRenderer is SpriteRenderer))
            {
                meshRenderer.enabled = true;
                if (meshRenderer.material != null)
                    meshRenderer.material.color = builtTint;
            }

            RefreshOccupancyVisual();
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
        }

        private void RefreshLockVisual()
        {
            if (IsBuilt)
            {
                if (lockLabel != null && lockLabel.gameObject.activeSelf)
                    lockLabel.gameObject.SetActive(false);
                return;
            }
            EnsureLockLabel();
            if (!lockLabel.gameObject.activeSelf)
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
            lockLabel.fontSize = UiFonts.Size(42);
            lockLabel.color = new Color(0.9f, 0.85f, 0.7f, 1f);
            if (sharedPileFont == null)
                sharedPileFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (sharedPileFont != null) lockLabel.font = sharedPileFont;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 480;
        }

        private void RefreshPileLabel()
        {
            if (IsResourceProp)
            {
                RefreshResourceLabel();
                return;
            }

            int stage = GetPileStage();
            if (stage <= 0)
            {
                if (pileLabel != null && pileLabel.gameObject.activeSelf)
                    pileLabel.gameObject.SetActive(false);
                lastPileStage = 0;
                lastPileAmount = null;
                lastPileRounded = int.MinValue;
                lastPileDisplayKey = double.NaN;
                return;
            }

            EnsurePileLabel();
            if (!pileLabel.gameObject.activeSelf)
                pileLabel.gameObject.SetActive(true);

            // 표시 문자열이 실제로 바뀔 때만 TextMesh 갱신 (매 프레임 할당 → GC 스파이크 방지)
            double v = Math.Abs(PendingMerit.ToDouble());
            bool textChanged;
            if (v < 1000d)
            {
                int rounded = Mathf.RoundToInt((float)v);
                textChanged = stage != lastPileStage || rounded != lastPileRounded;
                if (textChanged)
                {
                    lastPileStage = stage;
                    lastPileRounded = rounded;
                    lastPileAmount = StarPrefix(stage) + rounded;
                    pileLabel.text = lastPileAmount;
                }
            }
            else
            {
                // 큰 수는 표시 단위가 바뀔 때만
                double key = Math.Round(PendingMerit.Mantissa, 2) * 1000 + PendingMerit.Exponent;
                textChanged = stage != lastPileStage || key != lastPileDisplayKey;
                if (textChanged)
                {
                    lastPileStage = stage;
                    lastPileDisplayKey = key;
                    lastPileAmount = StarPrefix(stage) + PendingMerit.ToDisplayString();
                    pileLabel.text = lastPileAmount;
                }
            }

            pileLabel.transform.position = GetPileLabelWorldPos();
            EnsurePileLabelSorting();
        }

        int lastResourceStored = -1;
        int lastResourceCapacity = -1;
        bool lastResourceHalted;

        /// <summary>자원 기물 라벨: "물 3/6" · 만창이면 붉게.</summary>
        void RefreshResourceLabel()
        {
            if (!HasPendingResources)
            {
                if (pileLabel != null && pileLabel.gameObject.activeSelf)
                    pileLabel.gameObject.SetActive(false);
                lastResourceStored = -1;
                return;
            }

            EnsurePileLabel();
            if (!pileLabel.gameObject.activeSelf)
                pileLabel.gameObject.SetActive(true);

            int stored = storage.Stored, cap = ResourceCapacity;
            bool halted = IsStorageHalted;
            if (stored != lastResourceStored || cap != lastResourceCapacity || halted != lastResourceHalted)
            {
                lastResourceStored = stored;
                lastResourceCapacity = cap;
                lastResourceHalted = halted;
                pileLabel.text = ResourceLabel(ResourceType) + " " + stored + "/" + cap;
                pileLabel.color = halted ? new Color(1f, 0.55f, 0.45f, 1f) : new Color(1f, 0.92f, 0.55f, 1f);
            }
            pileLabel.transform.position = GetPileLabelWorldPos();
            EnsurePileLabelSorting();
        }

        static string ResourceLabel(PropResourceType type)
        {
            switch (type)
            {
                case PropResourceType.PurifiedWater: return "물";
                case PropResourceType.Yeopjeon: return "엽전";
                default: return "재료";
            }
        }

        Vector3 GetPileLabelWorldPos()
        {
            float topY = transform.position.y + 0.85f;
            if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.sprite != null)
                topY = spriteRenderer.bounds.max.y;
            // 엔딩 점유 아트처럼 키가 큰 기물도 숫자게 스프라이트 위로 뜨게
            return new Vector3(transform.position.x, topY + 0.28f, transform.position.z);
        }

        void EnsurePileLabelSorting()
        {
            if (pileLabel == null) return;
            var mr = pileLabel.GetComponent<MeshRenderer>();
            if (mr == null) return;
            // 기물·캐릭터(수백대)보다 항상 앞에. TextMesh는 sortingOrder가 먹히도록 명시.
            mr.sortingOrder = 1200;
        }

        private static string StarPrefix(int stage)
        {
            switch (stage)
            {
                case 1: return "*\n";
                case 2: return "**\n";
                case 3: return "***\n";
                case 4: return "****\n";
                default: return "*****\n";
            }
        }

        private void EnsurePileLabel()
        {
            if (pileLabel != null) return;

            var go = new GameObject("MeritPile");
            // 부모 스케일(기물 propScale)에 숫자가 찌그러지지 않게 월드에 독립
            go.transform.SetParent(null, false);
            pileLabel = go.AddComponent<TextMesh>();
            pileLabel.anchor = TextAnchor.LowerCenter;
            pileLabel.alignment = TextAlignment.Center;
            pileLabel.characterSize = 0.08f;
            pileLabel.fontSize = UiFonts.Size(48);
            pileLabel.color = new Color(1f, 0.92f, 0.55f, 1f);
            if (sharedPileFont == null)
                sharedPileFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (sharedPileFont != null) pileLabel.font = sharedPileFont;
            EnsurePileLabelSorting();
        }

        private void OnDestroy()
        {
            if (pileLabel != null)
            {
                Destroy(pileLabel.gameObject);
                pileLabel = null;
            }
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
