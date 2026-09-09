using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>7-2 일괄 수거: 1배 받기 / 광고(또는 보상권) 3배.</summary>
    public class BatchCollectPopup : MonoBehaviour
    {
        public static BatchCollectPopup Instance { get; private set; }

        public Font font;

        GameObject root;
        Text amountText;
        Text tripleHintText;
        Text adBtnLabel;
        Button claimBtn;
        Button adBtn;
        Button closeBtn;
        bool busy;
        RectTransform fxFrom;

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

        /// <summary>대기분 스윕 후 호출. fxFrom은 꽃잎 출발(옥토끼 일괄 버튼).</summary>
        public void Open(RectTransform fxOrigin = null)
        {
            if (!GameEconomy.Instance.HasPendingBatchMerit) return;
            EnsureBuilt();
            busy = false;
            fxFrom = fxOrigin;
            RefreshLabels();
            SetButtonsInteractable(true);
            root.SetActive(true);
        }

        public void Close()
        {
            if (busy) return;
            if (root != null) root.SetActive(false);
            fxFrom = null;
        }

        void RefreshLabels()
        {
            var amount = GameEconomy.Instance.PendingBatchMerit;
            if (amountText != null)
                amountText.text = amount.ToDisplayString();
            if (tripleHintText != null)
                tripleHintText.text = "광고 시 ×3 → " + (amount * 3.0).ToDisplayString();
            if (adBtnLabel != null)
            {
                if (GiftBundle.AdTickets > 0)
                    adBtnLabel.text = "보상권으로 ×3 (" + GiftBundle.AdTickets + ")";
                else
                    adBtnLabel.text = "광고 보고 ×3";
            }
        }

        void OnClaim1x()
        {
            if (busy) return;
            FinishClaim(1);
        }

        void OnClaim3x()
        {
            if (busy) return;
            StartCoroutine(Claim3xRoutine());
        }

        IEnumerator Claim3xRoutine()
        {
            busy = true;
            SetButtonsInteractable(false);

            bool usedTicket = GiftBundle.TrySpendAdTicket(1);
            if (!usedTicket)
            {
                if (adBtnLabel != null) adBtnLabel.text = "광고 시청 중…";
                yield return new WaitForSecondsRealtime(0.8f);
            }

            FinishClaim(3);
            busy = false;
        }

        void FinishClaim(int multiplier)
        {
            if (!GameEconomy.Instance.HasPendingBatchMerit)
            {
                Close();
                return;
            }

            var before = GameEconomy.Instance.MeritPile;
            if (!GameEconomy.Instance.TryClaimBatchMerit(multiplier))
            {
                busy = false;
                SetButtonsInteractable(true);
                return;
            }

            var after = GameEconomy.Instance.MeritPile;
            GameSaveBridge.SaveFromWorld();

            if (GameHud.Instance != null)
            {
                GameHud.Instance.BeginMeritCountUpPublic(before, after);
                if (fxFrom != null)
                    GameHud.Instance.PlayMeritCollectFx(fxFrom);
            }

            busy = false;
            if (root != null) root.SetActive(false);
            fxFrom = null;
        }

        void SetButtonsInteractable(bool on)
        {
            if (claimBtn != null) claimBtn.interactable = on;
            if (adBtn != null) adBtn.interactable = on;
            if (closeBtn != null) closeBtn.interactable = on;
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
            var canvasGO = new GameObject("Canvas_BatchCollect");
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

            var box = MakeBox(rootRt, new Vector2(560, 520));
            MakeText(box, "일괄 수거", 36, new Vector2(0, 200));
            MakeText(box, "쌓인 공덕", 22, new Vector2(0, 140), new Color(0.9f, 0.85f, 0.7f));
            amountText = MakeText(box, "0", 42, new Vector2(0, 80));
            tripleHintText = MakeText(box, "", 24, new Vector2(0, 20), new Color(1f, 0.85f, 0.45f));

            claimBtn = MakeButton(box, "BtnClaim", "받기", new Vector2(0, -70),
                new Vector2(360, 72), new Color(0.35f, 0.55f, 0.4f, 1f), OnClaim1x);
            adBtn = MakeButton(box, "BtnAd3x", "광고 보고 ×3", new Vector2(0, -160),
                new Vector2(360, 72), new Color(0.55f, 0.4f, 0.2f, 1f), OnClaim3x);
            adBtnLabel = adBtn.GetComponentInChildren<Text>();

            closeBtn = MakeButton(box, "BtnClose", "닫기", new Vector2(0, -250),
                new Vector2(200, 56), new Color(0.35f, 0.3f, 0.28f, 1f), Close);
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
            MakeText(rt, label, 26, Vector2.zero);
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
            rt.sizeDelta = new Vector2(500, 60);
            var text = go.AddComponent<Text>();
            text.font = font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color ?? Color.white;
            text.text = msg;
            text.raycastTarget = false;
            return text;
        }
    }
}
