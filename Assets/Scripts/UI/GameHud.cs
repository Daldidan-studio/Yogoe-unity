using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>
    /// 메인 HUD (기획서/프로토타입 1장: 상단 재화 바 + 하단 슬롯바)를 코드로 직접 만든다.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public Font font;
        public Sprite purifiedWaterIcon;
        public DetailScreen detailScreen;

        private Canvas hudCanvas;
        private Text meritText;
        private RectTransform meritTextRt;
        private Text yeopjeonText;
        private Text hyangText;
        private Text purifiedWaterText;
        private Text yutTokenText;

        private GameObject upgradeButtonRoot;
        private RectTransform upgradeButtonRt;
        private Image upgradeIconImage;
        private Text upgradeNameText;
        private Text upgradeCostText;
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
        }

        private void Start()
        {
            BuildCanvas();
            RefreshCurrencies();
            RefreshUpgradeButton();
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
            if (meritText != null) meritText.text = "공덕 " + GameEconomy.MeritPile.ToDisplayString();
            if (yeopjeonText != null) yeopjeonText.text = "엽전 " + GameEconomy.Yeopjeon;
            if (hyangText != null) hyangText.text = "향 " + GameEconomy.Hyang;
            if (purifiedWaterText != null) purifiedWaterText.text = GameEconomy.PurifiedWater.ToString();
            if (yutTokenText != null) yutTokenText.text = "윷 " + GameEconomy.YutToken + "/" + GameEconomy.YutTokenMax;
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
                chip.NameText.text = name + " · " + stageLabel;
                if (chip.StaminaFill != null && chip.StaminaFillRt != null)
                {
                    var state = chip.Agent.Stats.State;
                    bool alert = state == ActionState.Slumped || state == ActionState.Fainted;
                    float ratio = alert ? 1f : Mathf.Clamp01(chip.Agent.Stats.Stamina / 100f);

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

                RefreshStatusTag(chip);
                RefreshBatchButton(chip);
            }
        }

        private static void RefreshStatusTag(SlotChip chip)
        {
            if (chip.StatusTagRoot == null || chip.StatusTagText == null) return;

            var badge = CharacterStatusPresentation.ForSlot(chip.Agent.Stats.State);
            chip.StatusTagRoot.SetActive(badge.Visible);
            if (!badge.Visible) return;

            chip.StatusTagText.text = badge.Label;
            if (chip.StatusTagBg != null) chip.StatusTagBg.color = badge.Color;
        }

        /// <summary>7-2: 옥토끼 슬롯 위 앱 재시작 일괄 수거.</summary>
        private static void RefreshBatchButton(SlotChip chip)
        {
            if (chip.BatchButtonRoot == null || chip.BatchButtonLabel == null) return;
            bool show = GameEconomy.HasPendingBatchMerit;
            chip.BatchButtonRoot.SetActive(show);
            if (show)
                chip.BatchButtonLabel.text = "일괄 수거\n" + GameEconomy.PendingBatchMerit.ToDisplayString();
        }

        private Transform slotBarRoot;

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
                bg.color = new Color(0.11f, 0.08f, 0.07f, 0.85f);

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
                tagBg.color = new Color(0.2f, 0.45f, 0.85f, 0.95f);
                var tagTextGO = new GameObject("Label");
                SetupRect(tagTextGO, tagGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                var tagText = tagTextGO.AddComponent<Text>();
                tagText.font = font;
                tagText.fontSize = 16;
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
                        new Vector2(0, 30), new Vector2(148, 44));
                    var batchBg = batchRoot.AddComponent<Image>();
                    batchBg.color = new Color(0.85f, 0.55f, 0.15f, 0.95f);
                    var batchBtn = batchRoot.AddComponent<Button>();
                    batchBtn.targetGraphic = batchBg;
                    var batchRt = batchRoot.GetComponent<RectTransform>();
                    batchBtn.onClick.AddListener(() => OnBatchCollectClicked(batchRt));
                    var batchLabelGO = new GameObject("Label");
                    SetupRect(batchLabelGO, batchRoot.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                        Vector2.zero, Vector2.zero);
                    batchLabel = batchLabelGO.AddComponent<Text>();
                    batchLabel.font = font;
                    batchLabel.fontSize = 16;
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
                nameText.fontSize = 20;
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.color = Color.white;
                nameText.raycastTarget = false;

                var barBgGO = new GameObject("StaminaBarBg");
                SetupRect(barBgGO, chipRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 10), new Vector2(130, 14));
                var barBg = barBgGO.AddComponent<Image>();
                barBg.color = new Color(0.25f, 0.2f, 0.18f, 1f);

                var barFillGO = new GameObject("StaminaBarFill");
                var barFillRt = SetupRect(barFillGO, barBgGO.transform, Vector2.zero, Vector2.one,
                    new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                var barFill = barFillGO.AddComponent<Image>();
                barFill.color = new Color(0.35f, 0.75f, 0.4f, 1f);
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
            bg.color = new Color(0.08f, 0.12f, 0.14f, 0.85f);

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
            nameText.fontSize = 22;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = new Color(0.7f, 0.9f, 1f, 1f);
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
            if (!GameEconomy.TryClaimBatchMerit()) return;
            GameSaveBridge.SaveFromWorld();

            if (hudCanvas != null && from != null && meritTextRt != null)
                MeritCollectFx.Play(hudCanvas, from, meritTextRt, font);
        }

        // ---------------- 우하단 업그레이드 (8장) ----------------

        private void RefreshUpgradeButton()
        {
            if (upgradeButtonRoot == null) return;

            var target = PropEconomy.FindCheapestUpgradeTarget();
            if (target == null)
            {
                upgradeButtonRoot.SetActive(false);
                upgradeHoldActive = false;
                return;
            }

            upgradeButtonRoot.SetActive(true);
            var cost = PropEconomy.GetUpgradeCost(target);
            bool canAfford = GameEconomy.MeritPile >= cost;

            if (upgradeNameText != null)
                upgradeNameText.text = target.DisplayName;
            if (upgradeCostText != null)
            {
                upgradeCostText.text = cost.ToDisplayString();
                upgradeCostText.color = canAfford
                    ? new Color(1f, 0.92f, 0.55f)
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
            BuildTempResetButton(canvasGO.transform);
            BuildSlotBar(canvasGO.transform);
            BuildUpgradeButton(canvasGO.transform);
        }

        /// <summary>임시: 세이브 삭제 후 씬 리로드. 웹/에디터 공통.</summary>
        private void BuildTempResetButton(Transform canvasTf)
        {
            var go = new GameObject("TempResetButton");
            SetupRect(go, canvasTf, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-24, -24), new Vector2(160, 56));
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.55f, 0.18f, 0.16f, 0.92f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(OnTempResetClicked);

            var labelGO = new GameObject("Label");
            SetupRect(labelGO, go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var label = labelGO.AddComponent<Text>();
            label.font = font;
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "초기화";
            label.raycastTarget = false;
        }

        private static void OnTempResetClicked()
        {
            GameSaveService.DeleteSave();
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }

        private void BuildUpgradeButton(Transform canvasTf)
        {
            upgradeButtonRoot = new GameObject("UpgradeButton");
            upgradeButtonRt = SetupRect(upgradeButtonRoot, canvasTf,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-28, 200), new Vector2(200, 96));
            var bg = upgradeButtonRoot.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.12f, 0.08f, 0.92f);

            var btn = upgradeButtonRoot.AddComponent<Button>();
            btn.targetGraphic = bg;

            var trigger = upgradeButtonRoot.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => OnUpgradePointerDown());
            trigger.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => OnUpgradePointerUp());
            trigger.triggers.Add(up);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => OnUpgradePointerUp());
            trigger.triggers.Add(exit);

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
            upgradeNameText.fontSize = 20;
            upgradeNameText.alignment = TextAnchor.MiddleLeft;
            upgradeNameText.color = Color.white;
            upgradeNameText.raycastTarget = false;

            var costGO = new GameObject("Cost");
            SetupRect(costGO, upgradeButtonRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(20, 10), new Vector2(-70, 32));
            upgradeCostText = costGO.AddComponent<Text>();
            upgradeCostText.font = font;
            upgradeCostText.fontSize = 24;
            upgradeCostText.alignment = TextAnchor.MiddleLeft;
            upgradeCostText.color = new Color(1f, 0.92f, 0.55f);
            upgradeCostText.raycastTarget = false;

            upgradeButtonRoot.SetActive(false);
        }

        private void BuildTopBar(Transform canvasTf)
        {
            var topGO = new GameObject("TopBar");
            var topRt = SetupRect(topGO, canvasTf, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(24, -24), new Vector2(480, 140));
            var topBg = topGO.AddComponent<Image>();
            topBg.color = new Color(0.1f, 0.07f, 0.06f, 0.6f);
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
            meritText.fontSize = 40;
            meritText.color = new Color(1f, 0.95f, 0.85f);
            meritText.alignment = TextAnchor.MiddleLeft;
            meritText.raycastTarget = false;

            var rowGO = new GameObject("CurrencyRow");
            rowGO.transform.SetParent(topRt, false);
            rowGO.AddComponent<LayoutElement>().preferredHeight = 40;
            var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;

            yeopjeonText = CreateChipText(rowGO.transform, "엽전 0");
            hyangText = CreateChipText(rowGO.transform, "향 0");

            var pwGO = new GameObject("PurifiedWaterChip");
            pwGO.transform.SetParent(rowGO.transform, false);
            var pwLayout = pwGO.AddComponent<HorizontalLayoutGroup>();
            pwLayout.spacing = 4;
            pwLayout.childControlWidth = false;
            pwLayout.childControlHeight = true;
            pwGO.AddComponent<LayoutElement>().preferredWidth = 60;
            if (purifiedWaterIcon != null)
            {
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(pwGO.transform, false);
                var iconRt = iconGO.AddComponent<RectTransform>();
                iconRt.sizeDelta = new Vector2(28, 28);
                var img = iconGO.AddComponent<Image>();
                img.sprite = purifiedWaterIcon;
                img.preserveAspect = true;
            }
            purifiedWaterText = CreateChipText(pwGO.transform, "0");

            yutTokenText = CreateChipText(rowGO.transform, "윷 0/0");
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

        private Text CreateChipText(Transform parent, string initial)
        {
            var go = new GameObject("Chip");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 90;
            le.preferredHeight = 32;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 22;
            text.color = new Color(0.95f, 0.9f, 0.8f);
            text.alignment = TextAnchor.MiddleLeft;
            text.text = initial;
            text.raycastTarget = false;
            return text;
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
