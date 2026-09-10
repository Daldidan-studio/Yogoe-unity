using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.UI
{
    /// <summary>빈 캐릭터 슬롯 탭 → 고라니 소환 확인 (기획 9-1).</summary>
    public class SummonPopup : MonoBehaviour
    {
        public static SummonPopup Instance { get; private set; }

        public Font font;
        public CharacterData goraniData;

        GameObject root;
        Text titleText;
        Text costText;
        Button summonButton;
        Image summonButtonImage;
        Text summonButtonLabel;
        static readonly Color SummonEnabled = new Color(0.3f, 0.5f, 0.55f, 1f);
        static readonly Color SummonDisabled = new Color(0.25f, 0.25f, 0.28f, 1f);

        void Awake() => Instance = this;

        void Start()
        {
            EnsureBuilt();
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (CharacterSummon.IsPresent(CharacterId.Gorani)) return;
            EnsureBuilt();
            titleText.text = "향 3개를 피워 요괴를 부르시겠습니까?";
            RefreshAffordState();
            root.SetActive(true);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        void RefreshAffordState()
        {
            bool can = CharacterSummon.CanSummonGorani();
            costText.text = can
                ? "향 " + CharacterSummon.HyangCost + "개 소모 (보유 " + GameEconomy.Instance.Hyang + ")"
                : "향이 부족합니다 (필요 " + CharacterSummon.HyangCost + ", 보유 " + GameEconomy.Instance.Hyang + ")";
            if (summonButtonImage != null)
                summonButtonImage.color = can ? SummonEnabled : SummonDisabled;
            if (summonButtonLabel != null)
                summonButtonLabel.color = can ? Color.white : new Color(0.7f, 0.7f, 0.72f, 1f);
            // 부족해도 눌러서 상점으로 갈 수 있게 interactable 유지
            if (summonButton != null) summonButton.interactable = true;
        }

        void OnSummonClicked()
        {
            if (CharacterSummon.IsPresent(CharacterId.Gorani))
            {
                Close();
                return;
            }

            if (!CharacterSummon.CanSummonGorani())
            {
                Close();
                if (ShopScreen.Instance != null) ShopScreen.Instance.Open();
                return;
            }

            Close();
            if (SummonCeremony.Instance != null && SummonCeremony.Instance.TryPlay())
                return;

            // 연출 호스트 없으면 즉시 소환 폴백
            var agent = CharacterSummon.TrySummonGorani(goraniData, font);
            if (agent == null)
                Debug.LogWarning("[SummonPopup] 소환 실패");
            else
                Yoegoe.Save.GameSaveBridge.SaveFromWorld();
        }

        void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Build()
        {
            var canvasGO = new GameObject("Canvas_Summon");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 810;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Panel");
            var rootRt = SetupRect(root, canvasGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimBtn = root.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.onClick.AddListener(Close);

            var box = new GameObject("Box");
            SetupRect(box, rootRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(640, 380));
            var boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);
            var boxBlock = box.AddComponent<Button>();
            boxBlock.targetGraphic = boxBg;
            boxBlock.transition = Selectable.Transition.None;

            var titleGO = new GameObject("Title");
            SetupRect(titleGO, box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -50), new Vector2(580, 100));
            titleText = titleGO.AddComponent<Text>();
            ApplyFont(titleText);
            titleText.fontSize = UiFonts.Size(32);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.95f, 0.85f);
            titleText.raycastTarget = false;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;

            var costGO = new GameObject("Cost");
            SetupRect(costGO, box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 10), new Vector2(560, 70));
            costText = costGO.AddComponent<Text>();
            ApplyFont(costText);
            costText.fontSize = UiFonts.Size(28);
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.85f, 0.45f);
            costText.raycastTarget = false;
            costText.horizontalOverflow = HorizontalWrapMode.Wrap;
            costText.verticalOverflow = VerticalWrapMode.Overflow;

            summonButton = CreateActionButton(box.transform, "부르기", new Vector2(-130, -120), SummonEnabled,
                OnSummonClicked, out summonButtonImage, out summonButtonLabel);
            CreateActionButton(box.transform, "닫기", new Vector2(130, -120), new Color(0.4f, 0.3f, 0.28f, 1f),
                Close, out _, out _);
        }

        void ApplyFont(Text text)
        {
            if (text == null) return;
            if (font != null) text.font = font;
        }

        Button CreateActionButton(Transform parent, string label, Vector2 pos, Color color,
            UnityEngine.Events.UnityAction onClick, out Image img, out Text labelText)
        {
            var go = new GameObject("Btn_" + label);
            SetupRect(go, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(200, 70));
            img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Label");
            SetupRect(textGO, go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            labelText = textGO.AddComponent<Text>();
            ApplyFont(labelText);
            labelText.fontSize = UiFonts.Size(32);
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;
            return btn;
        }

        static RectTransform SetupRect(GameObject go, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
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
