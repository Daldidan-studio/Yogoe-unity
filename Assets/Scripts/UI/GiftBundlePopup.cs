using System;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.UI
{
    /// <summary>10장 선물꾸러미 중앙 팝업.</summary>
    public class GiftBundlePopup : MonoBehaviour
    {
        public static GiftBundlePopup Instance { get; private set; }

        public Font font;
        public OfferingData[] offerings;

        GameObject root;
        GameObject closedView;
        GameObject openView;
        Text titleText;
        Text rewardText;
        Image rewardIcon;
        Action onClosed;

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

        public void OpenForReward(string messageBeforeOpen, Action closed = null)
        {
            EnsureBuilt();
            onClosed = closed;
            titleText.text = string.IsNullOrEmpty(messageBeforeOpen)
                ? "선물꾸러미"
                : messageBeforeOpen;
            closedView.SetActive(true);
            openView.SetActive(false);
            root.SetActive(true);
        }

        void OnTapClosed()
        {
            var kind = GiftBundle.RollContent();
            GiftBundle.Grant(kind, offerings, out string name, out Sprite icon);
            closedView.SetActive(false);
            openView.SetActive(true);
            rewardText.text = name;
            if (rewardIcon != null)
            {
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
                rewardIcon.color = icon != null ? Color.white : new Color(1f, 0.9f, 0.5f, 1f);
            }
        }

        void OnDismiss()
        {
            root.SetActive(false);
            var cb = onClosed;
            onClosed = null;
            cb?.Invoke();
            Yoegoe.Save.GameSaveBridge.SaveFromWorld();
        }

        void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject("Canvas_GiftBundle");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Panel");
            var rootRt = Stretch(root, canvasGO.transform);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);

            closedView = new GameObject("Closed");
            Stretch(closedView, rootRt);
            var closedBtn = closedView.AddComponent<Button>();
            closedBtn.targetGraphic = dim;
            closedBtn.onClick.AddListener(OnTapClosed);

            var box = MakeBox(closedView.transform, new Vector2(520, 420));
            titleText = MakeText(box, "선물꾸러미", 36, new Vector2(0, 80));
            MakeText(box, "탭하여 열기", 26, new Vector2(0, -40), new Color(1f, 0.85f, 0.55f));

            // 닫힌 상자 느낌
            var chest = new GameObject("Chest");
            var chestRt = chest.AddComponent<RectTransform>();
            chestRt.SetParent(box, false);
            chestRt.anchorMin = chestRt.anchorMax = chestRt.pivot = new Vector2(0.5f, 0.5f);
            chestRt.anchoredPosition = new Vector2(0, 10);
            chestRt.sizeDelta = new Vector2(140, 110);
            var chestImg = chest.AddComponent<Image>();
            chestImg.color = new Color(0.75f, 0.55f, 0.25f, 1f);
            chestImg.raycastTarget = false;

            openView = new GameObject("Open");
            Stretch(openView, rootRt);
            openView.SetActive(false);
            var openBox = MakeBox(openView.transform, new Vector2(560, 480));
            MakeText(openBox, "획득!", 34, new Vector2(0, 150));

            var iconGO = new GameObject("RewardIcon");
            var iconRt = iconGO.AddComponent<RectTransform>();
            iconRt.SetParent(openBox, false);
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0, 40);
            iconRt.sizeDelta = new Vector2(120, 120);
            rewardIcon = iconGO.AddComponent<Image>();
            rewardIcon.preserveAspect = true;
            rewardIcon.raycastTarget = false;

            rewardText = MakeText(openBox, "", 30, new Vector2(0, -70));

            var ok = new GameObject("BtnOk");
            var okRt = ok.AddComponent<RectTransform>();
            okRt.SetParent(openBox, false);
            okRt.anchorMin = okRt.anchorMax = okRt.pivot = new Vector2(0.5f, 0.5f);
            okRt.anchoredPosition = new Vector2(0, -160);
            okRt.sizeDelta = new Vector2(240, 70);
            var okImg = ok.AddComponent<Image>();
            okImg.color = new Color(0.3f, 0.5f, 0.45f, 1f);
            var okBtn = ok.AddComponent<Button>();
            okBtn.targetGraphic = okImg;
            okBtn.onClick.AddListener(OnDismiss);
            MakeText(okRt, "확인", 30, Vector2.zero);
        }

        static RectTransform Stretch(GameObject go, Transform parent)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform MakeBox(Transform parent, Vector2 size)
        {
            var box = new GameObject("Box");
            var rt = box.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var bg = box.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);
            return rt;
        }

        Text MakeText(Transform parent, string msg, int size, Vector2 pos, Color? color = null)
        {
            var go = new GameObject("Text");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(500, 80);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color ?? new Color(1f, 0.95f, 0.85f);
            t.text = msg;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }
    }
}
