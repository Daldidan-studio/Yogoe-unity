using System.Collections.Generic;
using UnityEngine;
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

        private Text meritText;
        private Text yeopjeonText;
        private Text hyangText;
        private Text purifiedWaterText;
        private Text yutTokenText;

        private readonly List<SlotChip> slotChips = new List<SlotChip>();

        private class SlotChip
        {
            public CharacterAgent Agent;
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
        }

        private void Update()
        {
            RefreshCurrencies();
            RefreshSlotBar();
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
            bool mismatched = slotChips.Count != CharacterAgent.All.Count;
            if (!mismatched)
            {
                for (int i = 0; i < slotChips.Count; i++)
                {
                    if (slotChips[i].Agent != CharacterAgent.All[i]) { mismatched = true; break; }
                }
            }
            if (mismatched) RebuildSlotBar();

            foreach (var chip in slotChips)
            {
                if (chip.Agent == null) continue;
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
                    batchBtn.onClick.AddListener(OnBatchCollectClicked);
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
        }

        private static void OnBatchCollectClicked()
        {
            if (!GameEconomy.TryClaimBatchMerit()) return;
            GameSaveBridge.SaveFromWorld();
        }

        // ---------------- 빌드 ----------------

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("Canvas_HUD");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            BuildTopBar(canvasGO.transform);
            BuildSlotBar(canvasGO.transform);
        }

        private void BuildTopBar(Transform canvasTf)
        {
            var topGO = new GameObject("TopBar");
            var topRt = SetupRect(topGO, canvasTf, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(24, -24), new Vector2(480, 140));
            var topBg = topGO.AddComponent<Image>();
            topBg.color = new Color(0.1f, 0.07f, 0.06f, 0.6f);
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
