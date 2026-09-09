using UnityEngine;
using UnityEngine.UI;

namespace Yoegoe.UI
{
    /// <summary>12장 상점 미구현 — 향 부족 시 연결용 플레이스홀더.</summary>
    public class ShopStubPopup : MonoBehaviour
    {
        public static ShopStubPopup Instance { get; private set; }

        public Font font;
        GameObject root;

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
            EnsureBuilt();
            root.SetActive(true);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject("Canvas_ShopStub");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 820;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Panel");
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.SetParent(canvasGO.transform, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimBtn = root.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.onClick.AddListener(Close);

            var box = new GameObject("Box");
            var boxRt = box.AddComponent<RectTransform>();
            boxRt.SetParent(rootRt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(620, 340);
            var boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            AddText(boxRt, "상점", 40, new Vector2(0, 90), new Color(1f, 0.95f, 0.85f));
            AddText(boxRt, "상점은 아직 준비 중입니다.\n향은 윷놀이·상점에서 구할 수 있습니다.", 28,
                new Vector2(0, 0), new Color(1f, 0.9f, 0.75f));

            var btnGO = new GameObject("Btn_Close");
            var btnRt = btnGO.AddComponent<RectTransform>();
            btnRt.SetParent(boxRt, false);
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0, -110);
            btnRt.sizeDelta = new Vector2(220, 70);
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.4f, 0.3f, 0.28f, 1f);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Close);
            AddText(btnRt, "닫기", 32, Vector2.zero, Color.white);
        }

        void AddText(Transform parent, string msg, int size, Vector2 pos, Color color)
        {
            var go = new GameObject("Text");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(560, 120);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = msg;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
