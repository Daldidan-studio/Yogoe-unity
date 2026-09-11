using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>
    /// 메인 HUD (상단 재화 바 + 하단 슬롯바).
    /// Prefab/씬에 셸(Canvas·TopBar·버튼·SlotBar 루트)이 있으면 그대로 쓰고,
    /// 없으면 런타임에 조립한다. 슬롯 칩은 항상 코드로 갱신.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }

        // 색·글자 크기: Resources/UiStyleSettings.asset
        UiStyleSettings Style => UiStyleSettings.Get();
        UiStyleSettings.Colors C => Style.colors;

        [Header("주입 (Main)")]
        public Font font;
        public Sprite purifiedWaterIcon;
        public DetailScreen detailScreen;

        [Header("셸 (Prefab/씬 — 비어 있으면 Play 시 코드 조립)")]
        [SerializeField] Canvas hudCanvas;
        [SerializeField] Text meritText;
        [SerializeField] RectTransform meritTextRt;
        [SerializeField] Text yeopjeonText;
        [SerializeField] Text hyangText;
        [SerializeField] Text purifiedWaterText;
        [SerializeField] Text yutTokenText;
        [SerializeField] Button yutTokenPlusButton;
        [SerializeField] Button tempResetButton;
        [SerializeField] Button tempClearCacheButton;
        [SerializeField] Button shopButton;
        [SerializeField] Button yutButton;
        [SerializeField] Transform slotBarRoot;
        [SerializeField] GameObject upgradeButtonRoot;
        [SerializeField] RectTransform upgradeButtonRt;
        [SerializeField] Image upgradeIconImage;
        [SerializeField] Text upgradeNameText;
        [SerializeField] Text upgradeCostText;

        /// <summary>Prefab/씬에 HUD 셸이 이미 연결돼 있는지.</summary>
        public bool HasPrefabShell =>
            hudCanvas != null && slotBarRoot != null && meritText != null;

        private bool upgradeHoldActive;
        private float upgradeHoldTimer;
        private const float UpgradeHoldInitialDelay = 0.35f;
        private const float UpgradeHoldInterval = 0.12f;

        private readonly List<SlotChip> slotChips = new List<SlotChip>();

        private class SlotChip
        {
            public CharacterAgent Agent;
            public bool IsSummonSlot;
            public Text NameText;
            public Text StatusTagText;
            public GameObject StatusTagRoot;
            public Image StatusTagBg;
            public Image StaminaFill;
            public RectTransform StaminaFillRt;
            public GameObject BatchButtonRoot;
            public Text BatchButtonLabel;
            public string LastNameLabel;
            public GrowthStage LastNameStage = (GrowthStage)(-1);
            public string LastDisplayName;
            public ActionState LastStatusState = (ActionState)(-1);
            public bool LastBatchVisible;
            public string LastBatchLabel;
            public BigNumber LastBatchAmount;
            public bool HasLastBatchAmount;
            public float LastStaminaRatio = -1f;
            public ActionState LastStaminaState = (ActionState)(-1);
        }

        private BigNumber lastMeritShown;
        private bool hasLastMeritShown;
        /// <summary>일괄 수거 시 HUD 공덕 숫자 카운트업.</summary>
        private bool meritCounting;
        private BigNumber meritAnimFrom;
        private BigNumber meritAnimTo;
        private BigNumber meritAnimShown;
        private float meritAnimElapsed;
        // MeritCollectFx: 첫 꽃잎 도착 ≈ Scatter(0.4)+Hover(0.28), 마지막까지 ≈ Stagger*17+Fly(0.55)
        private const float MeritCountDelay = 0.68f;
        private const float MeritCountDuration = 1.35f;
        private int lastYeopjeon = int.MinValue;
        private int lastHyang = int.MinValue;
        private int lastPurifiedWater = int.MinValue;
        private int lastYutToken = int.MinValue;
        private int lastYutTokenMax = int.MinValue;
        private string lastUpgradeName;
        private BigNumber lastUpgradeCostValue;
        private bool hasLastUpgradeCost;
        private bool lastUpgradeVisible;
        private PropSlot lastUpgradeTarget;
        private bool lastUpgradeCanAfford;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            MapPointerRouter.CharacterDetailRequested += HandleCharacterDetailRequested;
            PropSlot.MeritCollectedAtWorld += PlayMeritCollectFxFromWorld;
        }

        private void OnDisable()
        {
            MapPointerRouter.CharacterDetailRequested -= HandleCharacterDetailRequested;
            PropSlot.MeritCollectedAtWorld -= PlayMeritCollectFxFromWorld;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleCharacterDetailRequested(CharacterAgent agent, string highlight)
        {
            if (detailScreen == null) return;
            detailScreen.Open(agent, highlight);
        }

        private void Start()
        {
            EnsureHudShell();
            EnsureYutTokenPlusButton();
            WireRuntimeListeners();
            RefreshCurrencies();
            RefreshUpgradeButton();
        }

        /// <summary>
        /// Prefab 셸이 있으면 유지, 없으면 코드로 조립.
        /// 에디터 Bake도 이 경로를 쓴다.
        /// </summary>
        public void EnsureHudShell()
        {
            if (HasPrefabShell) return;
            BuildCanvas();
        }

        /// <summary>재화 칩 4개 + [+] 버튼이 한 줄에 다 들어가는 데 필요한 TopBar 최소 폭.</summary>
        const float MinTopBarWidth = 750f;

        /// <summary>
        /// 윷 토큰 칩 옆 [+] 버튼 — BuildTopBar가 만든 최신 셸엔 이미 있지만, HasPrefabShell이라
        /// 통째로 건너뛰는 예전 Bake본에도 이름으로 찾아 붙여서 항상 나타나게 한다.
        /// </summary>
        void EnsureYutTokenPlusButton()
        {
            if (yutTokenPlusButton == null)
            {
                var row = hudCanvas != null ? hudCanvas.transform.Find("TopBar/CurrencyRow") : null;
                if (row != null)
                {
                    var existing = row.Find("YutTokenPlus");
                    yutTokenPlusButton = existing != null
                        ? existing.GetComponent<Button>()
                        : CreateYutTokenPlusButton(row);
                }
            }

            // 예전 Bake본은 재화 칩 4개 기준 폭(690)으로 굳어 있어서, [+] 버튼이 추가된 뒤로는
            // 오른쪽 칩(윷 토큰 등)이 잘려 보인다 — 최소 폭만 보장(더 넓게 손댔으면 안 줄임).
            var topBarRt = hudCanvas != null ? hudCanvas.transform.Find("TopBar") as RectTransform : null;
            if (topBarRt != null && topBarRt.sizeDelta.x < MinTopBarWidth)
                topBarRt.sizeDelta = new Vector2(MinTopBarWidth, topBarRt.sizeDelta.y);
        }

        /// <summary>공덕 수거 연출 타겟(상단 공덕 텍스트)으로 꽃잎 Gather.</summary>
        public void PlayMeritCollectFx(RectTransform from)
        {
            if (hudCanvas == null || meritTextRt == null || from == null) return;
            MeritCollectFx.Play(hudCanvas, from, meritTextRt, font);
        }

        public void PlayMeritCollectFxFromWorld(Vector3 worldPos)
        {
            if (hudCanvas == null || meritTextRt == null) return;
            MeritCollectFx.PlayFromWorld(hudCanvas, worldPos, meritTextRt);
        }

        private void Update()
        {
            RefreshCurrencies();
            RefreshSlotBar();
            RefreshUpgradeButton();
            TickUpgradeHold();
        }

        private void RefreshCurrencies()
        {
            if (meritText != null)
            {
                if (meritCounting)
                    TickMeritCountUp();
                else
                {
                    var merit = GameEconomy.Instance.MeritPile;
                    if (!hasLastMeritShown || !merit.Equals(lastMeritShown))
                    {
                        hasLastMeritShown = true;
                        lastMeritShown = merit;
                        meritText.text = "공덕 " + merit.ToDisplayString();
                    }
                }
            }
            if (yeopjeonText != null && GameEconomy.Instance.Yeopjeon != lastYeopjeon)
            {
                lastYeopjeon = GameEconomy.Instance.Yeopjeon;
                yeopjeonText.text = "엽전 " + lastYeopjeon;
            }
            if (hyangText != null && GameEconomy.Instance.Hyang != lastHyang)
            {
                lastHyang = GameEconomy.Instance.Hyang;
                hyangText.text = "향 " + lastHyang;
            }
            if (purifiedWaterText != null && GameEconomy.Instance.PurifiedWater != lastPurifiedWater)
            {
                lastPurifiedWater = GameEconomy.Instance.PurifiedWater;
                purifiedWaterText.text = "정화수 " + lastPurifiedWater;
            }
            if (yutTokenText != null
                && (GameEconomy.Instance.YutToken != lastYutToken || GameEconomy.Instance.YutTokenMax != lastYutTokenMax))
            {
                lastYutToken = GameEconomy.Instance.YutToken;
                lastYutTokenMax = GameEconomy.Instance.YutTokenMax;
                yutTokenText.text = "윷 " + lastYutToken + "/" + lastYutTokenMax;
            }
        }

        private void RefreshSlotBar()
        {
            bool wantSummonSlot = !CharacterSummon.IsPresent(CharacterId.Gorani);
            int expected = CharacterAgent.All.Count + (wantSummonSlot ? 1 : 0);
            bool mismatched = slotChips.Count != expected;
            if (!mismatched)
            {
                int agentIdx = 0;
                for (int i = 0; i < slotChips.Count; i++)
                {
                    var chip = slotChips[i];
                    if (chip.IsSummonSlot)
                    {
                        if (!wantSummonSlot) { mismatched = true; break; }
                        continue;
                    }
                    if (agentIdx >= CharacterAgent.All.Count
                        || chip.Agent != CharacterAgent.All[agentIdx])
                    {
                        mismatched = true;
                        break;
                    }
                    agentIdx++;
                }
                if (!mismatched && agentIdx != CharacterAgent.All.Count) mismatched = true;
            }
            if (mismatched) RebuildSlotBar();

            foreach (var chip in slotChips)
            {
                if (chip.IsSummonSlot || chip.Agent == null) continue;
                string stageLabel = chip.Agent.Stats.Stage == GrowthStage.Neok ? "넋" : "혼";
                string name = chip.Agent.Data != null ? chip.Agent.Data.displayName : "?";
                if (chip.NameText != null
                    && (chip.LastDisplayName != name || chip.LastNameStage != chip.Agent.Stats.Stage))
                {
                    chip.LastDisplayName = name;
                    chip.LastNameStage = chip.Agent.Stats.Stage;
                    chip.LastNameLabel = name + " · " + stageLabel;
                    chip.NameText.text = chip.LastNameLabel;
                }
                if (chip.StaminaFill != null && chip.StaminaFillRt != null)
                {
                    var state = chip.Agent.Stats.State;
                    bool alert = state == ActionState.Slumped || state == ActionState.Fainted;
                    float ratio = alert ? 1f : Mathf.Clamp01(chip.Agent.Stats.Stamina / 100f);

                    bool flash = state == ActionState.Slumped || state == ActionState.Fainted;
                    bool ratioChanged = Mathf.Abs(ratio - chip.LastStaminaRatio) > 0.002f;
                    bool stateChanged = state != chip.LastStaminaState;
                    if (ratioChanged || stateChanged || flash)
                    {
                        chip.LastStaminaRatio = ratio;
                        chip.LastStaminaState = state;

                        var parentRt = chip.StaminaFillRt.parent as RectTransform;
                        float parentW = parentRt != null ? parentRt.rect.width : 130f;
                        if (parentW < 1f) parentW = 130f;

                        chip.StaminaFillRt.anchorMin = new Vector2(0f, 0f);
                        chip.StaminaFillRt.anchorMax = new Vector2(0f, 1f);
                        chip.StaminaFillRt.pivot = new Vector2(0f, 0.5f);
                        chip.StaminaFillRt.anchoredPosition = Vector2.zero;
                        chip.StaminaFillRt.sizeDelta = new Vector2(parentW * ratio, 0f);
                        chip.StaminaFillRt.localScale = Vector3.one;
                        chip.StaminaFill.color = CharacterStatusPresentation.ForStaminaBar(
                            state, Time.unscaledTime);
                    }
                }

                RefreshStatusTag(chip);
                RefreshBatchButton(chip);
            }
        }

        private static void RefreshStatusTag(SlotChip chip)
        {
            if (chip.StatusTagRoot == null || chip.StatusTagText == null) return;

            var state = chip.Agent.Stats.State;
            if (state == chip.LastStatusState) return;
            chip.LastStatusState = state;

            var badge = CharacterStatusPresentation.ForSlot(state);
            chip.StatusTagRoot.SetActive(badge.Visible);
            if (!badge.Visible) return;

            chip.StatusTagText.text = badge.Label;
            if (chip.StatusTagBg != null) chip.StatusTagBg.color = badge.Color;
        }

        /// <summary>7-2: 옥토끼 슬롯 위 앱 재시작 일괄 수거.</summary>
        private static void RefreshBatchButton(SlotChip chip)
        {
            if (chip.BatchButtonRoot == null || chip.BatchButtonLabel == null) return;
            bool show = GameEconomy.Instance.HasPendingBatchMerit;
            if (show != chip.LastBatchVisible)
            {
                chip.LastBatchVisible = show;
                chip.BatchButtonRoot.SetActive(show);
            }
            if (!show) return;

            var amount = GameEconomy.Instance.PendingBatchMerit;
            if (chip.HasLastBatchAmount && amount.Equals(chip.LastBatchAmount)) return;
            chip.HasLastBatchAmount = true;
            chip.LastBatchAmount = amount;
            chip.LastBatchLabel = "일괄 수거\n" + amount.ToDisplayString() + "\n(광고×3)";
            chip.BatchButtonLabel.text = chip.LastBatchLabel;
        }

        private void RebuildSlotBar()
        {
            foreach (Transform child in slotBarRoot) Destroy(child.gameObject);
            slotChips.Clear();

            foreach (var agent in CharacterAgent.All)
            {
                var chipGO = new GameObject("Slot_" + (agent.Data != null ? agent.Data.displayName : "?"));
                var chipRt = SetupRect(chipGO, slotBarRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 100));
                var chipLayout = chipGO.AddComponent<LayoutElement>();
                chipLayout.preferredWidth = 160;
                chipLayout.preferredHeight = 100;

                var bg = chipGO.AddComponent<Image>();
                bg.color = C.hudSlotChip;

                var capturedAgent = agent;
                var button = chipGO.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() =>
                {
                    if (detailScreen != null) detailScreen.Open(capturedAgent);
                });

                var tagGO = new GameObject("StatusTag");
                SetupRect(tagGO, chipRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0f),
                    new Vector2(0, 4), new Vector2(72, 22));
                var tagBg = tagGO.AddComponent<Image>();
                tagBg.color = C.hudStatusTag;
                var tagTextGO = new GameObject("Label");
                SetupRect(tagTextGO, tagGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                var tagText = tagTextGO.AddComponent<Text>();
                tagText.font = font;
                tagText.fontSize = UiFonts.Small;
                tagText.alignment = TextAnchor.MiddleCenter;
                tagText.color = Color.white;
                tagText.text = "일하는";
                tagText.raycastTarget = false;
                tagGO.SetActive(false);

                GameObject batchRoot = null;
                Text batchLabel = null;
                // 기획 7-2: 옥토끼 슬롯 위에만 앱 재시작 일괄 수거
                if (agent.Data != null && agent.Data.id == CharacterId.Rabbit)
                {
                    batchRoot = new GameObject("BatchCollect");
                    SetupRect(batchRoot, chipRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0f),
                        new Vector2(0, 30), new Vector2(148, 56));
                    var batchBg = batchRoot.AddComponent<Image>();
                    batchBg.color = C.hudBatchCollect;
                    var batchBtn = batchRoot.AddComponent<Button>();
                    batchBtn.targetGraphic = batchBg;
                    var batchRt = batchRoot.GetComponent<RectTransform>();
                    batchBtn.onClick.AddListener(() => OnBatchCollectClicked(batchRt));
                    var batchLabelGO = new GameObject("Label");
                    SetupRect(batchLabelGO, batchRoot.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                        Vector2.zero, Vector2.zero);
                    batchLabel = batchLabelGO.AddComponent<Text>();
                    batchLabel.font = font;
                    batchLabel.fontSize = UiFonts.Caption;
                    batchLabel.alignment = TextAnchor.MiddleCenter;
                    batchLabel.color = Color.white;
                    batchLabel.raycastTarget = false;
                    batchLabel.text = "일괄 수거";
                    batchRoot.SetActive(false);
                }

                var nameGO = new GameObject("Name");
                SetupRect(nameGO, chipRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
                    new Vector2(0, -6), new Vector2(-10, 26));
                var nameText = nameGO.AddComponent<Text>();
                nameText.font = font;
                nameText.fontSize = UiFonts.HudSlotName;
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.color = Color.white;
                nameText.raycastTarget = false;

                var barBgGO = new GameObject("StaminaBarBg");
                SetupRect(barBgGO, chipRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 10), new Vector2(130, 14));
                var barBg = barBgGO.AddComponent<Image>();
                barBg.color = C.hudStaminaTrack;

                var barFillGO = new GameObject("StaminaBarFill");
                var barFillRt = SetupRect(barFillGO, barBgGO.transform, Vector2.zero, Vector2.one,
                    new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                var barFill = barFillGO.AddComponent<Image>();
                barFill.color = C.hudStaminaFill;
                barFill.raycastTarget = false;

                slotChips.Add(new SlotChip
                {
                    Agent = agent,
                    IsSummonSlot = false,
                    NameText = nameText,
                    StatusTagText = tagText,
                    StatusTagRoot = tagGO,
                    StatusTagBg = tagBg,
                    StaminaFill = barFill,
                    StaminaFillRt = barFillRt,
                    BatchButtonRoot = batchRoot,
                    BatchButtonLabel = batchLabel
                });
            }

            if (!CharacterSummon.IsPresent(CharacterId.Gorani))
                AddEmptySummonSlot();
        }

        private void AddEmptySummonSlot()
        {
            var chipGO = new GameObject("Slot_Summon");
            var chipRt = SetupRect(chipGO, slotBarRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 100));
            var chipLayout = chipGO.AddComponent<LayoutElement>();
            chipLayout.preferredWidth = 160;
            chipLayout.preferredHeight = 100;

            var bg = chipGO.AddComponent<Image>();
            bg.color = C.hudSummonChip;

            var button = chipGO.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() =>
            {
                if (SummonPopup.Instance != null) SummonPopup.Instance.Open();
            });

            var nameGO = new GameObject("Name");
            SetupRect(nameGO, chipRt, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var nameText = nameGO.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = UiFonts.HudSummonName;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = C.hudSummonName;
            nameText.raycastTarget = false;
            nameText.text = "+ 소환";

            slotChips.Add(new SlotChip
            {
                Agent = null,
                IsSummonSlot = true,
                NameText = nameText
            });
        }

        private void OnBatchCollectClicked(RectTransform from)
        {
            // 콜드스타트 Sweep 이후 다시 쌓인 더미도 함께 수거해 기물 위 숫자가 남기지 않는다.
            GameSaveBridge.SweepPropPilesIntoBatch();
            if (!GameEconomy.Instance.HasPendingBatchMerit) return;
            if (BatchCollectPopup.Instance != null)
                BatchCollectPopup.Instance.Open(from);
            else
                ClaimBatchWithoutPopup(from);
        }

        /// <summary>팝업 없을 때 폴백 (1배).</summary>
        void ClaimBatchWithoutPopup(RectTransform from)
        {
            var before = GameEconomy.Instance.MeritPile;
            if (!GameEconomy.Instance.TryClaimBatchMerit(1)) return;
            var after = GameEconomy.Instance.MeritPile;
            GameSaveBridge.SaveFromWorld();
            BeginMeritCountUp(before, after);
            if (from != null) PlayMeritCollectFx(from);
        }

        /// <summary>공덕 HUD 숫자를 from→to로 단계적으로 올린다 (일괄 수거 연출).</summary>
        public void BeginMeritCountUpPublic(BigNumber from, BigNumber to) => BeginMeritCountUp(from, to);

        private void BeginMeritCountUp(BigNumber from, BigNumber to)
        {
            if (meritText == null) return;
            if (from.Equals(to)) return;

            // 이미 카운트 중이면 현재 표시값에서 이어서 목표만 갱신
            if (meritCounting)
                meritAnimFrom = meritAnimShown;
            else
                meritAnimFrom = from;

            meritAnimTo = to;
            meritAnimShown = meritAnimFrom;
            meritAnimElapsed = 0f;
            meritCounting = true;
            hasLastMeritShown = true;
            lastMeritShown = meritAnimFrom;
            meritText.text = "공덕 " + meritAnimFrom.ToDisplayString();
        }

        private void TickMeritCountUp()
        {
            // 실제 지갑이 연출 목표와 어긋나면(소비·추가 수거) 즉시 실제값으로 맞춤
            var real = GameEconomy.Instance.MeritPile;
            if (!real.Equals(meritAnimTo))
            {
                // 목표가 더 커진 경우(연출 중 추가 수거)는 이어서 카운트
                if (real > meritAnimTo)
                {
                    meritAnimFrom = meritAnimShown;
                    meritAnimTo = real;
                    meritAnimElapsed = MeritCountDelay; // 딜레이 없이 바로 이어서
                }
                else
                {
                    EndMeritCountUp(real);
                    return;
                }
            }

            meritAnimElapsed += Time.unscaledDeltaTime;
            if (meritAnimElapsed < MeritCountDelay)
            {
                // 꽃잎이 흩날리는 동안은 시작값 유지
                if (!meritAnimShown.Equals(meritAnimFrom))
                {
                    meritAnimShown = meritAnimFrom;
                    lastMeritShown = meritAnimShown;
                    meritText.text = "공덕 " + meritAnimShown.ToDisplayString();
                }
                return;
            }

            float u = Mathf.Clamp01((meritAnimElapsed - MeritCountDelay) / MeritCountDuration);
            // easeOutCubic — 초반에 빠르게 오르다 끝에서 감속
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            var shown = meritAnimFrom + (meritAnimTo - meritAnimFrom) * e;

            if (!shown.Equals(meritAnimShown))
            {
                meritAnimShown = shown;
                lastMeritShown = shown;
                meritText.text = "공덕 " + shown.ToDisplayString();
            }

            if (u >= 1f)
                EndMeritCountUp(meritAnimTo);
        }

        private void EndMeritCountUp(BigNumber final)
        {
            meritCounting = false;
            meritAnimShown = final;
            lastMeritShown = final;
            hasLastMeritShown = true;
            if (meritText != null)
                meritText.text = "공덕 " + final.ToDisplayString();
        }

        // ---------------- 우하단 업그레이드 (8장) ----------------

        private void RefreshUpgradeButton()
        {
            if (upgradeButtonRoot == null) return;

            var target = PropEconomy.FindCheapestUpgradeTarget();
            if (target == null)
            {
                if (lastUpgradeVisible)
                {
                    lastUpgradeVisible = false;
                    upgradeButtonRoot.SetActive(false);
                }
                lastUpgradeTarget = null;
                upgradeHoldActive = false;
                return;
            }

            var cost = PropEconomy.GetUpgradeCost(target);
            bool canAfford = GameEconomy.Instance.MeritPile >= cost;
            string name = target.DisplayName;
            bool changed = !lastUpgradeVisible
                || target != lastUpgradeTarget
                || name != lastUpgradeName
                || !hasLastUpgradeCost
                || !cost.Equals(lastUpgradeCostValue)
                || canAfford != lastUpgradeCanAfford;

            if (!lastUpgradeVisible)
            {
                lastUpgradeVisible = true;
                upgradeButtonRoot.SetActive(true);
            }

            if (changed)
            {
                lastUpgradeTarget = target;
                lastUpgradeName = name;
                lastUpgradeCostValue = cost;
                hasLastUpgradeCost = true;
                lastUpgradeCanAfford = canAfford;

                if (upgradeNameText != null)
                    upgradeNameText.text = name;
                if (upgradeCostText != null)
                {
                    upgradeCostText.text = cost.ToDisplayString();
                    upgradeCostText.color = canAfford
                        ? C.hudUpgradeCost
                        : new Color(0.75f, 0.4f, 0.35f);
                }

                if (upgradeIconImage != null)
                {
                    var icon = target.data != null ? target.data.icon : null;
                    upgradeIconImage.sprite = icon;
                    upgradeIconImage.enabled = icon != null;
                    if (icon == null)
                        upgradeIconImage.color = new Color(0.55f, 0.45f, 0.3f, 1f);
                    else
                        upgradeIconImage.color = Color.white;
                }
            }
        }

        private void TickUpgradeHold()
        {
            if (!upgradeHoldActive) return;
            upgradeHoldTimer -= Time.unscaledDeltaTime;
            if (upgradeHoldTimer > 0f) return;
            if (!TryPerformUpgrade())
            {
                upgradeHoldActive = false;
                return;
            }
            upgradeHoldTimer = UpgradeHoldInterval;
        }

        private void OnUpgradePointerDown()
        {
            upgradeHoldActive = true;
            upgradeHoldTimer = UpgradeHoldInitialDelay;
            TryPerformUpgrade();
        }

        private void OnUpgradePointerUp()
        {
            upgradeHoldActive = false;
        }

        private bool TryPerformUpgrade()
        {
            var upgraded = PropEconomy.TryUpgradeCheapest();
            if (upgraded == null) return false;

            PropUpgradeFx.SpawnWorld(upgraded.transform.position, "+1 급", font);
            if (upgradeButtonRt != null)
                PropUpgradeFx.SpawnUi(upgradeButtonRt, upgraded.DisplayName + " +1급", font);
            GameSaveBridge.SaveFromWorld();
            RefreshUpgradeButton();
            return true;
        }

        // ---------------- 빌드 ----------------

        void WireRuntimeListeners()
        {
            if (shopButton != null)
            {
                shopButton.onClick.RemoveAllListeners();
                shopButton.onClick.AddListener(() =>
                {
                    if (ShopScreen.Instance != null) ShopScreen.Instance.Open();
                });
            }

            if (yutButton != null)
            {
                yutButton.onClick.RemoveAllListeners();
                yutButton.onClick.AddListener(() =>
                {
                    if (YutScreen.Instance != null) YutScreen.Instance.Open();
                });
            }

            if (yutTokenPlusButton != null)
            {
                yutTokenPlusButton.onClick.RemoveAllListeners();
                yutTokenPlusButton.onClick.AddListener(() =>
                {
                    if (YutTokenShopPopup.Instance != null) YutTokenShopPopup.Instance.Open();
                });
            }

            WireTempDebugButtons();
            WireUpgradeHoldTriggers();
        }

        /// <summary>
        /// "초기화"/"캐시 날리기" 버튼은 Button.onClick.AddListener를 코드로만 붙이는데, 이건
        /// UnityEvent의 "런타임 전용" 리스너라 프리팹으로 구우면 리스너가 통째로 날아간다
        /// (Bake 당시 BuildTempDebugButtons가 실행되며 붙인 리스너는 저장되지 않음).
        /// HasPrefabShell이라 BuildCanvas 자체를 건너뛰는 예전 Bake본에서도 버튼은 남아있으니,
        /// 이름으로 찾아 매번 다시 연결해서 "버튼이 안 먹히는" 문제를 없앤다.
        /// </summary>
        void WireTempDebugButtons()
        {
            if (hudCanvas == null) return;

            if (tempResetButton == null)
                tempResetButton = hudCanvas.transform.Find("TempResetButton")?.GetComponent<Button>();
            if (tempClearCacheButton == null)
                tempClearCacheButton = hudCanvas.transform.Find("TempClearCacheButton")?.GetComponent<Button>();

            if (tempResetButton != null)
            {
                tempResetButton.onClick.RemoveAllListeners();
                tempResetButton.onClick.AddListener(OnTempResetClicked);
            }

            if (tempClearCacheButton != null)
            {
                tempClearCacheButton.onClick.RemoveAllListeners();
                tempClearCacheButton.onClick.AddListener(OnClearCacheClicked);
            }
        }

        void WireUpgradeHoldTriggers()
        {
            if (upgradeButtonRoot == null) return;

            var trigger = upgradeButtonRoot.GetComponent<EventTrigger>();
            if (trigger == null) trigger = upgradeButtonRoot.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => OnUpgradePointerDown());
            trigger.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => OnUpgradePointerUp());
            trigger.triggers.Add(up);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => OnUpgradePointerUp());
            trigger.triggers.Add(exit);
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("Canvas_HUD");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            hudCanvas = canvas;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            BuildTopBar(canvasGO.transform);
            BuildTempDebugButtons(canvasGO.transform);
            BuildSlotBar(canvasGO.transform);
            BuildUpgradeButton(canvasGO.transform);
        }

        /// <summary>임시 디버그: 초기화(세이브) + 캐시 날리기(브라우저 캐시/IndexedDB).</summary>
        private void BuildTempDebugButtons(Transform canvasTf)
        {
            // 우상단 세로 스택: 상점(-24,-24,h64) → 윷놀이(-24,-100,h64) → 초기화 → 캐시 (간격 12)
            const float x = -24f;
            const float w = 120f;
            const float h = 48f;
            const float gap = 12f;
            const float shopBottom = -100f - 64f; // 윷놀이 버튼 하단 y
            float resetY = shopBottom - gap;
            float cacheY = resetY - h - gap;

            BuildTempDebugButton(canvasTf, "TempResetButton", "초기화",
                new Vector2(x, resetY), new Vector2(w, h),
                new Color(0.55f, 0.18f, 0.16f, 0.92f), OnTempResetClicked);

            BuildTempDebugButton(canvasTf, "TempClearCacheButton", "캐시 날리기",
                new Vector2(x, cacheY), new Vector2(w, h),
                new Color(0.35f, 0.22f, 0.45f, 0.92f), OnClearCacheClicked);
        }

        void BuildTempDebugButton(Transform canvasTf, string name, string label,
            Vector2 anchoredPos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            SetupRect(go, canvasTf, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                anchoredPos, size);
            var bg = go.AddComponent<Image>();
            bg.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);

            var labelGO = new GameObject("Label");
            SetupRect(labelGO, go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var text = labelGO.AddComponent<Text>();
            text.font = font;
            text.fontSize = UiFonts.HudSlotName;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;
        }

        private static void OnTempResetClicked()
        {
            GameSaveService.DeleteSave();
            ReloadActiveScene();
        }

        private static void OnClearCacheClicked()
        {
            GameSaveService.ClearAllLocalData();
#if UNITY_WEBGL && !UNITY_EDITOR
            YogoeClearBrowserCacheAndReload();
#else
            ReloadActiveScene();
#endif
        }

        static void ReloadActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void YogoeClearBrowserCacheAndReload();
#endif

        private void BuildUpgradeButton(Transform canvasTf)
        {
            upgradeButtonRoot = new GameObject("UpgradeButton");
            upgradeButtonRt = SetupRect(upgradeButtonRoot, canvasTf,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-28, 200), new Vector2(200, 96));
            var bg = upgradeButtonRoot.AddComponent<Image>();
            bg.color = C.hudUpgradeButton;

            var btn = upgradeButtonRoot.AddComponent<Button>();
            btn.targetGraphic = bg;

            var iconGO = new GameObject("Icon");
            SetupRect(iconGO, upgradeButtonRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(12, 0), new Vector2(56, 56));
            upgradeIconImage = iconGO.AddComponent<Image>();
            upgradeIconImage.preserveAspect = true;
            upgradeIconImage.raycastTarget = false;

            var nameGO = new GameObject("Name");
            SetupRect(nameGO, upgradeButtonRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(20, -8), new Vector2(-70, 28));
            upgradeNameText = nameGO.AddComponent<Text>();
            upgradeNameText.font = font;
            upgradeNameText.fontSize = UiFonts.HudSlotName;
            upgradeNameText.alignment = TextAnchor.MiddleLeft;
            upgradeNameText.color = Color.white;
            upgradeNameText.raycastTarget = false;

            var costGO = new GameObject("Cost");
            SetupRect(costGO, upgradeButtonRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(20, 10), new Vector2(-70, 32));
            upgradeCostText = costGO.AddComponent<Text>();
            upgradeCostText.font = font;
            upgradeCostText.fontSize = UiFonts.Emphasis;
            upgradeCostText.alignment = TextAnchor.MiddleLeft;
            upgradeCostText.color = C.hudUpgradeCost;
            upgradeCostText.raycastTarget = false;

            upgradeButtonRoot.SetActive(false);
        }

        private void BuildTopBar(Transform canvasTf)
        {
            var topGO = new GameObject("TopBar");
            var topRt = SetupRect(topGO, canvasTf, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(24, -24), new Vector2(MinTopBarWidth, 140));
            var topBg = topGO.AddComponent<Image>();
            topBg.color = C.hudTopBar;
            topBg.raycastTarget = false;
            var topLayout = topGO.AddComponent<VerticalLayoutGroup>();
            topLayout.padding = new RectOffset(16, 16, 10, 10);
            topLayout.spacing = 6;
            topLayout.childAlignment = TextAnchor.UpperLeft;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = false;
            topLayout.childForceExpandWidth = true;

            var meritGO = new GameObject("MeritText");
            meritGO.AddComponent<LayoutElement>().preferredHeight = 48;
            meritGO.transform.SetParent(topRt, false);
            meritTextRt = meritGO.GetComponent<RectTransform>();
            if (meritTextRt == null) meritTextRt = meritGO.AddComponent<RectTransform>();
            meritText = meritGO.AddComponent<Text>();
            meritText.font = font;
            meritText.fontSize = UiFonts.HudMerit;
            meritText.color = C.textCream;
            meritText.alignment = TextAnchor.MiddleLeft;
            meritText.raycastTarget = false;

            var rowGO = new GameObject("CurrencyRow");
            rowGO.transform.SetParent(topRt, false);
            rowGO.AddComponent<LayoutElement>().preferredHeight = 44;
            var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;

            // 4종 재화가 말로만 구별돼서 헷갈린다는 피드백 — 재화별 색 아이콘 + 색 배경 칩으로 구분.
            yeopjeonText = CreateCurrencyChip(rowGO.transform, "엽전 0", C.currencyYeopjeon);
            hyangText = CreateCurrencyChip(rowGO.transform, "향 0", C.currencyHyang);
            purifiedWaterText = CreateCurrencyChip(rowGO.transform, "정화수 0", C.currencyPurifiedWater, purifiedWaterIcon);
            yutTokenText = CreateCurrencyChip(rowGO.transform, "윷 0/0", C.currencyYutToken);
            yutTokenPlusButton = CreateYutTokenPlusButton(rowGO.transform);

            // 우상단 상점 버튼
            var shopBtnGO = new GameObject("ShopButton");
            var shopRt = SetupRect(shopBtnGO, canvasTf, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-24, -24), new Vector2(120, 64));
            var shopImg = shopBtnGO.AddComponent<Image>();
            shopImg.color = C.hudShopButton;
            shopButton = shopBtnGO.AddComponent<Button>();
            shopButton.targetGraphic = shopImg;
            var shopLabelGO = new GameObject("Label");
            SetupRect(shopLabelGO, shopRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var shopLabel = shopLabelGO.AddComponent<Text>();
            shopLabel.font = font;
            shopLabel.fontSize = UiFonts.Button;
            shopLabel.alignment = TextAnchor.MiddleCenter;
            shopLabel.color = C.textCream;
            shopLabel.text = "상점";
            shopLabel.raycastTarget = false;

            // 상점 바로 아래 윷놀이 버튼
            var yutBtnGO = new GameObject("YutButton");
            var yutRt = SetupRect(yutBtnGO, canvasTf, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-24, -100), new Vector2(120, 64));
            var yutImg = yutBtnGO.AddComponent<Image>();
            yutImg.color = C.hudYutButton;
            yutButton = yutBtnGO.AddComponent<Button>();
            yutButton.targetGraphic = yutImg;
            var yutLabelGO = new GameObject("Label");
            SetupRect(yutLabelGO, yutRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var yutLabel = yutLabelGO.AddComponent<Text>();
            yutLabel.font = font;
            yutLabel.fontSize = UiFonts.Button;
            yutLabel.alignment = TextAnchor.MiddleCenter;
            yutLabel.color = C.textCream;
            yutLabel.text = "윷놀이";
            yutLabel.raycastTarget = false;
        }

        private void BuildSlotBar(Transform canvasTf)
        {
            var slotGO = new GameObject("SlotBar");
            var slotRt = SetupRect(slotGO, canvasTf, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f),
                new Vector2(0, 24), new Vector2(-40, 160));
            var layout = slotGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            slotBarRoot = slotRt;
        }

        /// <summary>
        /// 재화 칩 하나(색 배경 + 아이콘 + 텍스트). icon이 없으면 accentColor로 물들인 원 아이콘을 대신 쓴다
        /// (엽전·향·윷 토큰용 — 정화수처럼 실제 아이콘 에셋이 없어도 색으로 바로 구별되게).
        /// </summary>
        private Text CreateCurrencyChip(Transform parent, string label, Color accentColor, Sprite icon = null)
        {
            const float iconWidth = 30f;
            const float textWidth = 100f;
            const float spacing = 6f;
            var padding = new RectOffset(6, 10, 4, 4);

            var go = new GameObject("Chip");
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredWidth =
                padding.left + padding.right + iconWidth + spacing + textWidth;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.3f);
            bg.raycastTarget = false;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            iconGO.AddComponent<LayoutElement>().preferredWidth = iconWidth;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon != null ? icon : GetCurrencyDotSprite();
            iconImg.color = icon != null ? Color.white : accentColor;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textLe = textGO.AddComponent<LayoutElement>();
            textLe.preferredWidth = textWidth;
            var text = textGO.AddComponent<Text>();
            text.font = font;
            text.fontSize = UiFonts.HudCurrency;
            text.color = C.textOnDark;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = label;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>윷 토큰 칩 옆 작은 [+] — 눌러 YutTokenShopPopup(엽전 구매/광고 충전)을 연다.</summary>
        Button CreateYutTokenPlusButton(Transform parent)
        {
            const float size = 40f;
            var go = new GameObject("YutTokenPlus");
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredWidth = size;

            var img = go.AddComponent<Image>();
            img.color = C.currencyYutToken;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRt = labelGO.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGO.AddComponent<Text>();
            label.font = font;
            label.fontSize = UiFonts.HudCurrency;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = C.textOnDark;
            label.text = "+";
            label.raycastTarget = false;

            return btn;
        }

        private static Sprite s_currencyDotSprite;

        /// <summary>재화 아이콘용 단색 원. 실제 아트가 없는 엽전·향·윷 토큰이 accentColor로 서로 구별되게.</summary>
        private static Sprite GetCurrencyDotSprite()
        {
            if (s_currencyDotSprite != null) return s_currencyDotSprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((r - dist) / 1.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            s_currencyDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return s_currencyDotSprite;
        }

        private static RectTransform SetupRect(GameObject go, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return rt;
        }
    }
}
