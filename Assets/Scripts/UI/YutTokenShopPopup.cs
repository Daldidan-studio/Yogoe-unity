using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>윷 토큰 칩 옆 [+] 팝업 — "엽전 10개로 토큰 5개 구매" / "광고 보고 토큰 1개 충전".
    /// 구조는 BatchCollectPopup(일괄 수거)과 동일한 패턴.</summary>
    public class YutTokenShopPopup : MonoBehaviour
    {
        public static YutTokenShopPopup Instance { get; private set; }

        public Font font;

        const int BuyCostYeopjeon = 10;
        const int BuyGrantTokens = 5;
        const int AdGrantTokens = 1;
        const float AdWatchSeconds = 0.8f;

        GameObject root;
        Text tokenText;
        Text buyLabel;
        Text adLabel;
        Button buyBtn;
        Button adBtn;
        Button closeBtn;
        bool busy;

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
            busy = false;
            RefreshLabels();
            if (closeBtn != null) closeBtn.interactable = true;
            root.SetActive(true);
        }

        public void Close()
        {
            if (busy) return;
            if (root != null) root.SetActive(false);
        }

        void RefreshLabels()
        {
            var eco = GameEconomy.Instance;
            bool full = eco.YutToken >= eco.YutTokenMax;

            if (tokenText != null)
                tokenText.text = "윷 토큰 " + eco.YutToken + "/" + eco.YutTokenMax;
            if (buyLabel != null)
                buyLabel.text = full ? "토큰이 가득 찼어요" : $"엽전 {BuyCostYeopjeon}개로\n토큰 {BuyGrantTokens}개 구매";
            if (adLabel != null)
                adLabel.text = full ? "토큰이 가득 찼어요" : $"광고 보고\n토큰 {AdGrantTokens}개 충전";

            if (buyBtn != null) buyBtn.interactable = !full && eco.Yeopjeon >= BuyCostYeopjeon;
            if (adBtn != null) adBtn.interactable = !full;
        }

        void OnBuyClicked()
        {
            if (busy) return;
            var eco = GameEconomy.Instance;
            if (eco.YutToken >= eco.YutTokenMax) return;
            if (!eco.TrySpendYeopjeon(BuyCostYeopjeon)) return;

            eco.AddYutToken(BuyGrantTokens);
            GameSaveBridge.SaveFromWorld();
            RefreshLabels();
        }

        void OnAdClicked()
        {
            if (busy) return;
            StartCoroutine(AdRoutine());
        }

        IEnumerator AdRoutine()
        {
            busy = true;
            if (buyBtn != null) buyBtn.interactable = false;
            if (adBtn != null) adBtn.interactable = false;
            if (closeBtn != null) closeBtn.interactable = false;
            if (adLabel != null) adLabel.text = "광고 시청 중…";

            // 실제 광고 SDK가 붙기 전까지 BatchCollectPopup과 동일한 짧은 지연으로 시청을 흉내낸다.
            yield return new WaitForSecondsRealtime(AdWatchSeconds);

            var eco = GameEconomy.Instance;
            if (eco.YutToken < eco.YutTokenMax)
                eco.AddYutToken(AdGrantTokens);
            GameSaveBridge.SaveFromWorld();

            busy = false;
            if (closeBtn != null) closeBtn.interactable = true;
            RefreshLabels();
        }

        void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Build()
        {
            var canvasGO = new GameObject("Canvas_YutTokenShop");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Panel");
            var rootRt = Stretch(root, canvasGO.transform);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);

            var box = MakeBox(rootRt, new Vector2(590, 510));
            MakeText(box, "윷 토큰", 41, new Vector2(0, 190));
            tokenText = MakeText(box, "", 31, new Vector2(0, 125), new Color(0.9f, 0.85f, 0.7f));

            buyBtn = MakeButton(box, "BtnBuy", "", new Vector2(0, 20),
                new Vector2(450, 115), new Color(0.35f, 0.5f, 0.35f, 1f), OnBuyClicked);
            buyLabel = buyBtn.GetComponentInChildren<Text>();

            adBtn = MakeButton(box, "BtnAd", "", new Vector2(0, -110),
                new Vector2(450, 115), new Color(0.55f, 0.4f, 0.2f, 1f), OnAdClicked);
            adLabel = adBtn.GetComponentInChildren<Text>();

            closeBtn = MakeButton(box, "BtnClose", "닫기", new Vector2(0, -220),
                new Vector2(220, 66), new Color(0.35f, 0.3f, 0.28f, 1f), Close);
        }

        Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var text = MakeText(rt, label, 29, Vector2.zero);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return btn;
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
            var go = new GameObject("Box");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.12f, 0.1f, 0.97f);
            return rt;
        }

        Text MakeText(Transform parent, string msg, int size, Vector2 pos, Color? color = null)
        {
            var go = new GameObject("Text");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(520, 95);
            var text = go.AddComponent<Text>();
            text.font = font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = UiFonts.Size(size);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color ?? Color.white;
            text.text = msg;
            text.raycastTarget = false;
            return text;
        }
    }
}
