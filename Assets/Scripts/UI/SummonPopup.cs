using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>빈 캐릭터 슬롯 탭 → 고라니 소환 확인 (기획 9장).</summary>
    public class SummonPopup : MonoBehaviour
    {
        public static SummonPopup Instance { get; private set; }

        public Font font;
        public CharacterData goraniData;

        private GameObject root;
        private Text titleText;
        private Text costText;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            EnsureBuilt();
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (CharacterSummon.IsPresent(CharacterId.Gorani)) return;
            EnsureBuilt();
            titleText.text = "고라니를 소환할까요?";
            costText.text = "향 " + CharacterSummon.HyangCost + "개 소모";
            root.SetActive(true);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        private void OnSummonClicked()
        {
            if (CharacterSummon.IsPresent(CharacterId.Gorani))
            {
                Close();
                return;
            }

            if (!CharacterSummon.CanSummonGorani())
            {
                costText.text = "향이 부족합니다\n(필요 " + CharacterSummon.HyangCost + ")";
                return;
            }

            var agent = CharacterSummon.TrySummonGorani(goraniData, font);
            if (agent == null)
            {
                costText.text = "소환에 실패했습니다";
                return;
            }

            Close();
            GameSaveBridge.SaveFromWorld();
        }

        private void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void Build()
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
                Vector2.zero, new Vector2(620, 360));
            var boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);
            var boxBlock = box.AddComponent<Button>();
            boxBlock.targetGraphic = boxBg;
            boxBlock.transition = Selectable.Transition.None;

            var titleGO = new GameObject("Title");
            SetupRect(titleGO, box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -40), new Vector2(560, 80));
            titleText = titleGO.AddComponent<Text>();
            ApplyFont(titleText);
            titleText.fontSize = 36;
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
            costText.fontSize = 30;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.85f, 0.45f);
            costText.raycastTarget = false;
            costText.horizontalOverflow = HorizontalWrapMode.Wrap;
            costText.verticalOverflow = VerticalWrapMode.Overflow;

            CreateActionButton(box.transform, "소환", new Vector2(-130, -120), new Color(0.3f, 0.5f, 0.55f, 1f),
                OnSummonClicked);
            CreateActionButton(box.transform, "닫기", new Vector2(130, -120), new Color(0.4f, 0.3f, 0.28f, 1f),
                Close);
        }

        private void ApplyFont(Text text)
        {
            if (text == null) return;
            if (font != null) text.font = font;
        }

        private void CreateActionButton(Transform parent, string label, Vector2 pos, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            SetupRect(go, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(200, 70));
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Label");
            SetupRect(textGO, go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var text = textGO.AddComponent<Text>();
            ApplyFont(text);
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;
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
