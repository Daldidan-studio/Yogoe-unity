using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>빈 자리(자물쇠) 탭 → 구매 확인 팝업 (기획 8장).</summary>
    public class PropPurchasePopup : MonoBehaviour
    {
        public static PropPurchasePopup Instance { get; private set; }

        public Font font;

        private GameObject root;
        private Text titleText;
        private Text costText;
        private PropSlot target;

        private void Awake()
        {
            Instance = this;
            Build();
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open(PropSlot prop)
        {
            if (prop == null || prop.IsBuilt) return;
            target = prop;
            string name = prop.DisplayName;
            titleText.text = name + "을(를) 그릴까요?";
            costText.text = "비용 " + PropEconomy.GetNextPurchaseCost().ToDisplayString();
            root.SetActive(true);
        }

        public void Close()
        {
            root.SetActive(false);
            target = null;
        }

        private void OnBuildClicked()
        {
            if (target == null) return;
            var prop = target;
            if (!PropEconomy.TryPurchase(prop))
            {
                costText.text = "공덕이 부족합니다\n(" + PropEconomy.GetNextPurchaseCost().ToDisplayString() + ")";
                return;
            }
            Close();
            GameSaveBridge.SaveFromWorld();
        }

        private void Build()
        {
            var canvasGO = new GameObject("Canvas_PropPurchase");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;

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
            // 박스 클릭이 배경 닫기를 치지 않게
            var boxBlock = box.AddComponent<Button>();
            boxBlock.targetGraphic = boxBg;
            boxBlock.transition = Selectable.Transition.None;

            var titleGO = new GameObject("Title");
            SetupRect(titleGO, box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -40), new Vector2(560, 80));
            titleText = titleGO.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 36;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.95f, 0.85f);
            titleText.raycastTarget = false;

            var costGO = new GameObject("Cost");
            SetupRect(costGO, box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 10), new Vector2(560, 70));
            costText = costGO.AddComponent<Text>();
            costText.font = font;
            costText.fontSize = 30;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.85f, 0.45f);
            costText.raycastTarget = false;

            CreateActionButton(box.transform, "건설", new Vector2(-130, -120), new Color(0.35f, 0.55f, 0.3f, 1f),
                OnBuildClicked);
            CreateActionButton(box.transform, "닫기", new Vector2(130, -120), new Color(0.4f, 0.3f, 0.28f, 1f),
                Close);
        }

        private void CreateActionButton(Transform parent, string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction onClick)
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
            text.font = font;
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
