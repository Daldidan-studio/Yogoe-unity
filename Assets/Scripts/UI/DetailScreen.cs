using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.UI
{
    /// <summary>
    /// 상세 화면 (프로토타입/기획서 1장: "요괴 탭 → 전체화면"). 슬롯바에서 캐릭터를 탭하면 열림.
    ///
    /// 원본 프로토타입은 아이템을 손가락으로 끌어 초상 위에 드롭해서 공양하는 방식인데, 이 프로젝트가
    /// 쓰는 새 Input System 기준으로 드래그앤드롭까지 한 번에 만들면 실제로 만져보기 전엔 검증이 안 되는
    /// 위험이 커서, 1차로는 "공양물 아이콘을 탭하면 바로 먹인다"로 단순화했다. 효과(기력/친밀도 증가)는
    /// 완전히 동일해서 나중에 드래그로 바꿔도 로직은 안 바뀜 — UI만 교체하면 됨.
    /// </summary>
    public class DetailScreen : MonoBehaviour
    {
        public Font font;
        public OfferingData[] offerings;

        private CharacterAgent currentAgent;
        private GameObject root;
        private Image portraitImage;
        private Text nameText;
        private Text staminaText;
        private Image staminaFill;
        private RectTransform staminaFillRt;
        private Text heartsText;
        private Text descriptionText;
        private Text statusText;

        private void Awake()
        {
            // Build은 Start에서 — Main이 font를 넣은 뒤여야 한글이 보인다.
        }

        private void Start()
        {
            EnsureBuilt();
            root.SetActive(false);
        }

        private void Update()
        {
            if (currentAgent == null || root == null || !root.activeSelf) return;
            RefreshStats();
        }

        public void Open(CharacterAgent agent)
        {
            EnsureBuilt();
            currentAgent = agent;
            root.SetActive(true);

            var data = agent.Data;
            nameText.text = data != null ? data.displayName : "?";
            descriptionText.text = data != null ? data.detailDescription : "";
            portraitImage.sprite = data != null ? FirstSprite(data) : null;
            portraitImage.preserveAspect = true;
            RefreshStats();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            currentAgent = null;
        }

        private void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void RefreshStats()
        {
            var stats = currentAgent.Stats;
            staminaText.text = "기력 " + Mathf.RoundToInt(stats.Stamina);
            float ratio = Mathf.Clamp01(stats.Stamina / 100f);
            if (staminaFillRt != null)
            {
                var parentRt = staminaFillRt.parent as RectTransform;
                float parentW = parentRt != null ? parentRt.rect.width : 500f;
                if (parentW < 1f) parentW = 500f;

                staminaFillRt.anchorMin = new Vector2(0f, 0f);
                staminaFillRt.anchorMax = new Vector2(0f, 1f);
                staminaFillRt.pivot = new Vector2(0f, 0.5f);
                staminaFillRt.anchoredPosition = Vector2.zero;
                staminaFillRt.sizeDelta = new Vector2(parentW * ratio, 0f);
                staminaFillRt.localScale = Vector3.one;
            }
            if (staminaFill != null)
                staminaFill.color = CharacterStatusPresentation.ForStaminaBar(stats.State, Time.unscaledTime);

            // 20칸, 하트 1개 = 5점 (기획서 1장).
            int filled = Mathf.Clamp(Mathf.RoundToInt(stats.Intimacy / 5f), 0, 20);
            var sb = new StringBuilder(20);
            for (int i = 0; i < 20; i++) sb.Append(i < filled ? '♥' : '♡');
            heartsText.text = sb.ToString();

            statusText.text = CharacterStatusPresentation.ForDetail(stats.State);
        }

        private void OnFeed(OfferingData offering)
        {
            if (currentAgent == null || offering == null) return;
            currentAgent.ReceiveOffering(offering.staminaGain, offering.intimacyGain);
            RefreshStats();
        }

        private static Sprite FirstSprite(CharacterData data)
        {
            if (data.walkDown != null) foreach (var s in data.walkDown) if (s != null) return s;
            if (data.walkLeft != null) foreach (var s in data.walkLeft) if (s != null) return s;
            if (data.walkRight != null) foreach (var s in data.walkRight) if (s != null) return s;
            if (data.walkUp != null) foreach (var s in data.walkUp) if (s != null) return s;
            return null;
        }

        // ---------------- 빌드 ----------------

        private void Build()
        {
            var canvasGO = new GameObject("Canvas_Detail");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600; // HUD(500)보다 위에

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            var rootRt = SetupRect(root, canvasGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.03f, 0.03f, 0.92f);

            // 닫기 버튼 (좌상단) — 한글 "닫기" 대신 × (기본 폰트에서도 보임)
            var closeGO = new GameObject("CloseButton");
            SetupRect(closeGO, rootRt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(72, 72));
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.35f, 0.25f, 0.2f, 0.95f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);

            // 두 막대로 X 그리기 (Text/한글 폰트 의존 없음)
            CreateCloseBar(closeGO.transform, 45f);
            CreateCloseBar(closeGO.transform, -45f);

            // 이름 + 단계
            nameText = CreateText(rootRt, "", 44, TextAnchor.MiddleCenter);
            SetupRect(nameText.gameObject, rootRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -110), new Vector2(600, 60));

            // 초상
            var portraitGO = new GameObject("Portrait");
            SetupRect(portraitGO, rootRt, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(420, 420));
            portraitImage = portraitGO.AddComponent<Image>();

            // 기력 바
            var staminaBgGO = new GameObject("StaminaBarBg");
            SetupRect(staminaBgGO, rootRt, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(500, 26));
            var staminaBg = staminaBgGO.AddComponent<Image>();
            staminaBg.color = new Color(0.25f, 0.2f, 0.18f, 1f);
            var staminaFillGO = new GameObject("StaminaBarFill");
            var staminaFillRt = SetupRect(staminaFillGO, staminaBgGO.transform, Vector2.zero, Vector2.one,
                new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            staminaFill = staminaFillGO.AddComponent<Image>();
            staminaFill.color = new Color(0.35f, 0.75f, 0.4f, 1f);
            staminaFill.raycastTarget = false;
            this.staminaFillRt = staminaFillRt;
            staminaText = CreateText(staminaBgGO.transform, "", 20, TextAnchor.MiddleCenter);
            SetupRect(staminaText.gameObject, staminaBgGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // 친밀도 하트
            heartsText = CreateText(rootRt, "", 26, TextAnchor.MiddleCenter);
            heartsText.color = new Color(0.95f, 0.4f, 0.45f);
            SetupRect(heartsText.gameObject, rootRt, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(700, 40));

            // 상태 텍스트
            statusText = CreateText(rootRt, "", 24, TextAnchor.MiddleCenter);
            statusText.color = new Color(0.8f, 0.75f, 0.65f);
            SetupRect(statusText.gameObject, rootRt, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(700, 40));

            // 설명
            descriptionText = CreateText(rootRt, "", 22, TextAnchor.UpperCenter);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetupRect(descriptionText.gameObject, rootRt, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(700, 100));

            // 하단 공양물 급여 바
            var feedRowGO = new GameObject("FeedRow");
            var feedRt = SetupRect(feedRowGO, rootRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 40), new Vector2(900, 120));
            var feedLayout = feedRowGO.AddComponent<HorizontalLayoutGroup>();
            feedLayout.spacing = 12;
            feedLayout.childAlignment = TextAnchor.MiddleCenter;

            if (offerings != null)
            {
                foreach (var offering in offerings)
                {
                    if (offering == null) continue;
                    var itemGO = new GameObject("Offering_" + offering.offeringId);
                    itemGO.transform.SetParent(feedRt, false);
                    var le = itemGO.AddComponent<LayoutElement>();
                    le.preferredWidth = 90;
                    le.preferredHeight = 90;
                    var img = itemGO.AddComponent<Image>();
                    img.sprite = offering.icon;
                    img.preserveAspect = true;
                    var btn = itemGO.AddComponent<Button>();
                    btn.targetGraphic = img;
                    var captured = offering;
                    btn.onClick.AddListener(() => OnFeed(captured));
                }
            }
        }

        private static void CreateCloseBar(Transform parent, float zAngle)
        {
            var barGO = new GameObject("XBar");
            var rt = SetupRect(barGO, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(36, 5));
            rt.localRotation = Quaternion.Euler(0f, 0f, zAngle);
            var img = barGO.AddComponent<Image>();
            img.color = new Color(1f, 0.95f, 0.9f, 1f);
            img.raycastTarget = false;
        }

        private Text CreateText(Transform parent, string initial, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = initial;
            return text;
        }

        private static RectTransform SetupRect(GameObject go, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
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
