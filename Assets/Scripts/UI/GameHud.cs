using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.UI
{
    /// <summary>
    /// 메인 HUD (기획서/프로토타입 1장: 상단 재화 바 + 하단 슬롯바)를 코드로 직접 만든다.
    /// 유니티 에디터에서 손으로 uGUI 배치하는 대신, 지금까지 캐릭터/기물을 만들어온 것과 같은
    /// 방식(런타임 코드 생성)으로 만들어서 씬 파일을 손으로 안 건드려도 되게 함.
    ///
    /// 아직 없는 것(다음 단계): 우상단 5개 아이콘 버튼(윷놀이/상점/업적/패방/디버그) 클릭 시 화면 전환,
    /// 공덕 더미 탭 수거, 슬롯 탭 → 상세화면. 지금은 "항상 보이는 정보 표시"까지만.
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
            public Image StaminaFill;
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

        // ---------------- 상단 재화 바 ----------------

        private void RefreshCurrencies()
        {
            if (meritText != null) meritText.text = "공덕 " + GameEconomy.MeritPile.ToDisplayString();
            if (yeopjeonText != null) yeopjeonText.text = "엽전 " + GameEconomy.Yeopjeon;
            if (hyangText != null) hyangText.text = "향 " + GameEconomy.Hyang;
            if (purifiedWaterText != null) purifiedWaterText.text = GameEconomy.PurifiedWater.ToString();
            if (yutTokenText != null) yutTokenText.text = "윷 " + GameEconomy.YutToken + "/" + GameEconomy.YutTokenMax;
        }

        // ---------------- 하단 슬롯바 ----------------

        private void RefreshSlotBar()
        {
            // 새로 생긴/사라진 캐릭터가 있으면 슬롯바를 다시 그린다 (기획서 8장: 슬롯 3~4개, 소환으로 늘어남).
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
                if (chip.StaminaFill != null)
                    chip.StaminaFill.fillAmount = Mathf.Clamp01(chip.Agent.Stats.Stamina / 100f);
            }
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
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 90));
                var chipLayout = chipGO.AddComponent<LayoutElement>();
                chipLayout.preferredWidth = 160;
                chipLayout.preferredHeight = 90;

                var bg = chipGO.AddComponent<Image>();
                bg.color = new Color(0.11f, 0.08f, 0.07f, 0.85f);

                // 탭하면 상세화면 열기 (기획서 1장 "요괴 탭 → 전체화면").
                var capturedAgent = agent;
                var button = chipGO.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() =>
                {
                    if (detailScreen != null) detailScreen.Open(capturedAgent);
                });

                var nameGO = new GameObject("Name");
                SetupRect(nameGO, chipRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
                    new Vector2(0, -6), new Vector2(-10, 26));
                var nameText = nameGO.AddComponent<Text>();
                nameText.font = font;
                nameText.fontSize = 20;
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.color = Color.white;

                var barBgGO = new GameObject("StaminaBarBg");
                SetupRect(barBgGO, chipRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 10), new Vector2(130, 14));
                var barBg = barBgGO.AddComponent<Image>();
                barBg.color = new Color(0.25f, 0.2f, 0.18f, 1f);

                var barFillGO = new GameObject("StaminaBarFill");
                SetupRect(barFillGO, barBgGO.transform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                var barFill = barFillGO.AddComponent<Image>();
                barFill.color = new Color(0.35f, 0.75f, 0.4f, 1f);
                barFill.type = Image.Type.Filled;
                barFill.fillMethod = Image.FillMethod.Horizontal;
                barFill.fillAmount = 1f;

                slotChips.Add(new SlotChip { Agent = agent, NameText = nameText, StaminaFill = barFill });
            }
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
                new Vector2(0, 24), new Vector2(-40, 110));
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
