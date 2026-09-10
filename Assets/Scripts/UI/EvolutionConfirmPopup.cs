using System;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;

namespace Yoegoe.UI
{
    /// <summary>넋→혼 진화 연출 종료 후 확인 창 (9-3).</summary>
    public class EvolutionConfirmPopup : MonoBehaviour
    {
        public static EvolutionConfirmPopup Instance { get; private set; }

        public Font font;
        GameObject root;
        Text bodyText;
        Action onConfirm;

        void Awake() => Instance = this;

        void OnEnable() => CharacterAgent.EvolutionConfirmRequested += Open;
        void OnDisable() => CharacterAgent.EvolutionConfirmRequested -= Open;

        void Start()
        {
            EnsureBuilt();
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open(string message, Action confirmed)
        {
            EnsureBuilt();
            onConfirm = confirmed;
            bodyText.text = message;
            root.SetActive(true);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            onConfirm = null;
        }

        void OnConfirmClicked()
        {
            var cb = onConfirm;
            Close();
            cb?.Invoke();
        }

        void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject("Canvas_EvolutionConfirm");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;
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
            dim.color = new Color(0f, 0f, 0f, 0.45f);

            var box = new GameObject("Box");
            var boxRt = box.AddComponent<RectTransform>();
            boxRt.SetParent(rootRt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(640, 300);
            var boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var bodyGO = new GameObject("Body");
            var bodyRt = bodyGO.AddComponent<RectTransform>();
            bodyRt.SetParent(boxRt, false);
            bodyRt.anchorMin = bodyRt.anchorMax = bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.anchoredPosition = new Vector2(0, 30);
            bodyRt.sizeDelta = new Vector2(580, 140);
            bodyText = bodyGO.AddComponent<Text>();
            bodyText.font = font;
            bodyText.fontSize = UiFonts.Size(34);
            bodyText.alignment = TextAnchor.MiddleCenter;
            bodyText.color = new Color(1f, 0.95f, 0.85f);
            bodyText.raycastTarget = false;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;

            var btnGO = new GameObject("Btn_Ok");
            var btnRt = btnGO.AddComponent<RectTransform>();
            btnRt.SetParent(boxRt, false);
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0, -90);
            btnRt.sizeDelta = new Vector2(220, 70);
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.3f, 0.5f, 0.45f, 1f);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnConfirmClicked);

            var labelGO = new GameObject("Label");
            var labelRt = labelGO.AddComponent<RectTransform>();
            labelRt.SetParent(btnRt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
            var label = labelGO.AddComponent<Text>();
            label.font = font;
            label.fontSize = UiFonts.Size(32);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "확인";
            label.raycastTarget = false;
        }
    }
}
