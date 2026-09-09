using System;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>12장 고가구점. 음식/향/음식 + 패키지 미리보기(결제 없음).</summary>
    public class ShopScreen : MonoBehaviour
    {
        public static ShopScreen Instance { get; private set; }

        public Font font;
        public OfferingData[] offerings;
        public Sprite shopBackground;
        public Sprite imugiSprite;

        static readonly string[] ImugiLines =
        {
            "어서 오거라.",
            "뭐가 더 필요하냐.",
            "천천히 둘러보아라."
        };

        struct PackageDef
        {
            public string Id;
            public string Name;
            public string PriceLabel;
            public string Contents;
        }

        static readonly PackageDef[] Packages =
        {
            new PackageDef
            {
                Id = "intro1",
                Name = "입문 패키지",
                PriceLabel = "₩1,500",
                Contents = "엽전 50\n향 1\n정화수 5"
            },
            new PackageDef
            {
                Id = "intro2",
                Name = "나들이 패키지",
                PriceLabel = "₩4,900",
                Contents = "엽전 200\n향 3\n공양물 묶음"
            },
            new PackageDef
            {
                Id = "intro3",
                Name = "풍요 패키지",
                PriceLabel = "₩9,900",
                Contents = "엽전 500\n향 5\n정화수 20"
            }
        };

        GameObject root;
        Text dialogueText;
        int dialogueIndex;

        Image leftIcon;
        Text leftName;
        Text leftPriceLabel;
        Image rightIcon;
        Text rightName;
        Text rightPriceLabel;
        Text resetCostLabel;

        GameObject packagePopup;
        Text packageTitle;
        Text packageBody;
        Text packagePriceBtnLabel;

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
            ShopStock.SetCatalog(offerings);
            ShopStock.EnsureFresh(DateTime.UtcNow);
            dialogueIndex = 0;
            SetDialogue(ImugiLines[0]);
            RefreshSlots();
            if (packagePopup != null) packagePopup.SetActive(false);
            root.SetActive(true);
        }

        public void Close()
        {
            if (packagePopup != null) packagePopup.SetActive(false);
            if (root != null) root.SetActive(false);
            GameSaveBridge.SaveFromWorld();
        }

        void OnImugiTapped()
        {
            dialogueIndex = (dialogueIndex + 1) % ImugiLines.Length;
            SetDialogue(ImugiLines[dialogueIndex]);
        }

        void SetDialogue(string line)
        {
            if (dialogueText != null) dialogueText.text = line ?? "";
        }

        void RefreshSlots()
        {
            BindOfferingSlot(ShopStock.Side.Left, leftIcon, leftName);
            BindOfferingSlot(ShopStock.Side.Right, rightIcon, rightName);
            if (leftPriceLabel != null)
                leftPriceLabel.text = ShopStock.OfferingPriceYeopjeon + " 엽전";
            if (rightPriceLabel != null)
                rightPriceLabel.text = ShopStock.OfferingPriceYeopjeon + " 엽전";
            if (resetCostLabel != null)
                resetCostLabel.text = "리셋 (공덕 " + ShopStock.GetResetCostMerit().ToDisplayString() + ")";
        }

        void BindOfferingSlot(ShopStock.Side side, Image icon, Text nameLabel)
        {
            var o = ShopStock.GetOffering(side);
            if (icon != null)
            {
                icon.sprite = o != null ? o.icon : null;
                icon.enabled = o != null && o.icon != null;
                icon.color = icon.enabled ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
            if (nameLabel != null)
                nameLabel.text = o != null
                    ? (string.IsNullOrEmpty(o.displayName) ? o.offeringId : o.displayName)
                    : "—";
        }

        void OnBuyLeft() => TryBuyOffering(ShopStock.Side.Left);
        void OnBuyRight() => TryBuyOffering(ShopStock.Side.Right);

        void TryBuyOffering(ShopStock.Side side)
        {
            if (ShopStock.TryBuyOffering(side, out var fail))
            {
                SetDialogue("잘 골라갔구나.");
                GameSaveBridge.SaveFromWorld();
                return;
            }
            if (fail == ShopStock.BuyFail.NotEnoughYeopjeon)
                SetDialogue("돈을 더 모아와라.");
        }

        void OnBuyHyang()
        {
            if (ShopStock.TryBuyHyang(out var fail))
            {
                SetDialogue("향이 필요하구나.");
                GameSaveBridge.SaveFromWorld();
                return;
            }
            if (fail == ShopStock.BuyFail.NotEnoughYeopjeon)
                SetDialogue("돈을 더 모아와라.");
        }

        void OnResetStock()
        {
            if (ShopStock.TryResetWithMerit(out _, out bool notEnough))
            {
                SetDialogue("새로 꺼내 두었다.");
                RefreshSlots();
                GameSaveBridge.SaveFromWorld();
                return;
            }
            if (notEnough)
                SetDialogue("공덕이 부족하구나.");
        }

        void OpenPackage(int index)
        {
            if (index < 0 || index >= Packages.Length) return;
            var p = Packages[index];
            if (packagePopup == null) return;
            packageTitle.text = p.Name;
            packageBody.text = p.Contents;
            packagePriceBtnLabel.text = p.PriceLabel;
            packagePopup.SetActive(true);
        }

        void ClosePackage()
        {
            if (packagePopup != null) packagePopup.SetActive(false);
        }

        void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (shopBackground == null)
                shopBackground = Resources.Load<Sprite>("UI/ShopInterior");
            if (imugiSprite == null)
                imugiSprite = Resources.Load<Sprite>("UI/ImugiPortrait");

            var canvasGO = new GameObject("Canvas_Shop");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 820;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            var rootRt = Stretch(root, canvasGO.transform);

            var bgGO = new GameObject("Background");
            Stretch(bgGO, rootRt);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = Color.white;
            if (shopBackground != null)
            {
                bgImg.sprite = shopBackground;
                bgImg.preserveAspect = false;
            }
            else
                bgImg.color = new Color(0.25f, 0.18f, 0.14f, 1f);
            bgImg.raycastTarget = false;

            // 이무기
            var imugiGO = new GameObject("Imugi");
            var imugiRt = imugiGO.AddComponent<RectTransform>();
            imugiRt.SetParent(rootRt, false);
            imugiRt.anchorMin = imugiRt.anchorMax = imugiRt.pivot = new Vector2(0.5f, 0.55f);
            imugiRt.anchoredPosition = new Vector2(0, 80);
            imugiRt.sizeDelta = new Vector2(420, 480);
            var imugiImg = imugiGO.AddComponent<Image>();
            imugiImg.preserveAspect = true;
            imugiImg.raycastTarget = true;
            if (imugiSprite != null)
            {
                imugiImg.sprite = imugiSprite;
                imugiImg.color = Color.white;
            }
            else
                imugiImg.color = new Color(0.45f, 0.55f, 0.7f, 1f);
            var imugiBtn = imugiGO.AddComponent<Button>();
            imugiBtn.targetGraphic = imugiImg;
            imugiBtn.onClick.AddListener(OnImugiTapped);

            // 닫기
            var closeGO = new GameObject("Close");
            var closeRt = closeGO.AddComponent<RectTransform>();
            closeRt.SetParent(rootRt, false);
            closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-32, -32);
            closeRt.sizeDelta = new Vector2(72, 72);
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.2f, 0.15f, 0.12f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);
            MakeText(closeRt, "×", 42, Vector2.zero, Color.white);

            // 책상 위 슬롯 행
            var deskGO = new GameObject("DeskSlots");
            var deskRt = deskGO.AddComponent<RectTransform>();
            deskRt.SetParent(rootRt, false);
            deskRt.anchorMin = new Vector2(0.05f, 0.28f);
            deskRt.anchorMax = new Vector2(0.95f, 0.42f);
            deskRt.offsetMin = deskRt.offsetMax = Vector2.zero;
            var deskLayout = deskGO.AddComponent<HorizontalLayoutGroup>();
            deskLayout.spacing = 16;
            deskLayout.childAlignment = TextAnchor.MiddleCenter;
            deskLayout.childControlWidth = true;
            deskLayout.childControlHeight = true;
            deskLayout.childForceExpandWidth = true;
            deskLayout.childForceExpandHeight = true;

            BuildOfferingSlot(deskRt, "LeftFood", out leftIcon, out leftName, out leftPriceLabel, OnBuyLeft);
            BuildHyangSlot(deskRt);
            BuildOfferingSlot(deskRt, "RightFood", out rightIcon, out rightName, out rightPriceLabel, OnBuyRight);

            // 리셋
            var resetGO = new GameObject("Reset");
            var resetRt = resetGO.AddComponent<RectTransform>();
            resetRt.SetParent(rootRt, false);
            resetRt.anchorMin = resetRt.anchorMax = resetRt.pivot = new Vector2(0.5f, 0.28f);
            resetRt.anchoredPosition = new Vector2(0, -8);
            resetRt.sizeDelta = new Vector2(420, 56);
            var resetImg = resetGO.AddComponent<Image>();
            resetImg.color = new Color(0.35f, 0.28f, 0.22f, 0.95f);
            var resetBtn = resetGO.AddComponent<Button>();
            resetBtn.targetGraphic = resetImg;
            resetBtn.onClick.AddListener(OnResetStock);
            resetCostLabel = MakeText(resetRt, "리셋", 24, Vector2.zero, new Color(1f, 0.92f, 0.75f));

            // 패키지 스크롤
            var packArea = new GameObject("Packages");
            var packAreaRt = packArea.AddComponent<RectTransform>();
            packAreaRt.SetParent(rootRt, false);
            packAreaRt.anchorMin = new Vector2(0.04f, 0.12f);
            packAreaRt.anchorMax = new Vector2(0.96f, 0.26f);
            packAreaRt.offsetMin = packAreaRt.offsetMax = Vector2.zero;
            var scroll = packArea.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            var vpRt = Stretch(viewport, packAreaRt);
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.01f);
            scroll.viewport = vpRt;

            var content = new GameObject("Content");
            var contentRt = content.AddComponent<RectTransform>();
            contentRt.SetParent(vpRt, false);
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(960, 0);
            var h = content.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 18;
            h.padding = new RectOffset(8, 8, 6, 6);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRt;

            for (int i = 0; i < Packages.Length; i++)
            {
                int captured = i;
                BuildPackageCard(contentRt, Packages[i], () => OpenPackage(captured));
            }

            // 대화창
            var dialGO = new GameObject("Dialogue");
            var dialRt = dialGO.AddComponent<RectTransform>();
            dialRt.SetParent(rootRt, false);
            dialRt.anchorMin = new Vector2(0.06f, 0.02f);
            dialRt.anchorMax = new Vector2(0.94f, 0.11f);
            dialRt.offsetMin = dialRt.offsetMax = Vector2.zero;
            var dialBg = dialGO.AddComponent<Image>();
            dialBg.color = new Color(0.08f, 0.06f, 0.05f, 0.88f);
            dialogueText = MakeText(dialRt, ImugiLines[0], 28, Vector2.zero, new Color(1f, 0.95f, 0.85f));
            dialogueText.alignment = TextAnchor.MiddleLeft;
            var dialTextRt = dialogueText.GetComponent<RectTransform>();
            dialTextRt.offsetMin = new Vector2(28, 8);
            dialTextRt.offsetMax = new Vector2(-28, -8);

            BuildPackagePopup(rootRt);
        }

        void BuildOfferingSlot(Transform parent, string name, out Image icon, out Text nameLabel,
            out Text priceLabel, UnityEngine.Events.UnityAction onBuy)
        {
            var slot = new GameObject(name);
            slot.transform.SetParent(parent, false);
            var bg = slot.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.09f, 0.07f, 0.92f);
            var v = slot.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slot.transform, false);
            iconGO.AddComponent<LayoutElement>().preferredHeight = 72;
            icon = iconGO.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            nameLabel = MakeChildText(slot.transform, "—", 20, 28);
            priceLabel = MakeChildText(slot.transform, "10 엽전", 22, 36);

            var buyGO = new GameObject("Buy");
            buyGO.transform.SetParent(slot.transform, false);
            buyGO.AddComponent<LayoutElement>().preferredHeight = 40;
            var buyImg = buyGO.AddComponent<Image>();
            buyImg.color = new Color(0.55f, 0.4f, 0.22f, 1f);
            var buyBtn = buyGO.AddComponent<Button>();
            buyBtn.targetGraphic = buyImg;
            buyBtn.onClick.AddListener(onBuy);
            var buyLabel = MakeText(buyGO.GetComponent<RectTransform>(), "구매", 22, Vector2.zero, Color.white);
            buyLabel.raycastTarget = false;
        }

        void BuildHyangSlot(Transform parent)
        {
            var slot = new GameObject("Hyang");
            slot.transform.SetParent(parent, false);
            var bg = slot.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.1f, 0.08f, 0.95f);
            var v = slot.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;

            MakeChildText(slot.transform, "향", 26, 40);
            MakeChildText(slot.transform, "×1", 22, 28);
            MakeChildText(slot.transform, ShopStock.HyangPriceYeopjeon + " 엽전", 22, 32);

            var buyGO = new GameObject("Buy");
            buyGO.transform.SetParent(slot.transform, false);
            buyGO.AddComponent<LayoutElement>().preferredHeight = 40;
            var buyImg = buyGO.AddComponent<Image>();
            buyImg.color = new Color(0.4f, 0.45f, 0.55f, 1f);
            var buyBtn = buyGO.AddComponent<Button>();
            buyBtn.targetGraphic = buyImg;
            buyBtn.onClick.AddListener(OnBuyHyang);
            MakeText(buyGO.GetComponent<RectTransform>(), "구매", 22, Vector2.zero, Color.white);
        }

        void BuildPackageCard(Transform parent, PackageDef def, Action onTap)
        {
            var card = new GameObject("Pkg_" + def.Id);
            card.transform.SetParent(parent, false);
            var le = card.AddComponent<LayoutElement>();
            le.preferredWidth = 280;
            le.preferredHeight = 110;
            var bg = card.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.12f, 0.1f, 0.95f);
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onTap());
            MakeText(card.GetComponent<RectTransform>(), def.Name + "\n" + def.PriceLabel, 24,
                Vector2.zero, new Color(1f, 0.92f, 0.8f));
        }

        void BuildPackagePopup(Transform parent)
        {
            packagePopup = new GameObject("PackagePopup");
            var rt = Stretch(packagePopup, parent);
            var dim = packagePopup.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimBtn = packagePopup.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.onClick.AddListener(ClosePackage);

            var box = new GameObject("Box");
            var boxRt = box.AddComponent<RectTransform>();
            boxRt.SetParent(rt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(640, 520);
            var boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);
            // 박스 클릭이 딤 닫기를 막도록
            box.AddComponent<Button>().targetGraphic = boxBg;

            packageTitle = MakeText(boxRt, "패키지", 34, new Vector2(0, 180), new Color(1f, 0.95f, 0.85f));
            packageBody = MakeText(boxRt, "", 26, new Vector2(0, 40), new Color(1f, 0.9f, 0.78f));
            packageBody.GetComponent<RectTransform>().sizeDelta = new Vector2(520, 200);

            var priceGO = new GameObject("PriceBtn");
            var priceRt = priceGO.AddComponent<RectTransform>();
            priceRt.SetParent(boxRt, false);
            priceRt.anchorMin = priceRt.anchorMax = priceRt.pivot = new Vector2(0.5f, 0.5f);
            priceRt.anchoredPosition = new Vector2(0, -120);
            priceRt.sizeDelta = new Vector2(280, 64);
            var priceImg = priceGO.AddComponent<Image>();
            priceImg.color = new Color(0.45f, 0.35f, 0.25f, 1f);
            // 결제 연동 없음 — 버튼은 보이기만
            packagePriceBtnLabel = MakeText(priceRt, "₩0", 28, Vector2.zero, Color.white);

            var closeGO = new GameObject("ClosePkg");
            var closeRt = closeGO.AddComponent<RectTransform>();
            closeRt.SetParent(boxRt, false);
            closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(0.5f, 0.5f);
            closeRt.anchoredPosition = new Vector2(0, -200);
            closeRt.sizeDelta = new Vector2(220, 60);
            var cImg = closeGO.AddComponent<Image>();
            cImg.color = new Color(0.35f, 0.28f, 0.24f, 1f);
            var cBtn = closeGO.AddComponent<Button>();
            cBtn.targetGraphic = cImg;
            cBtn.onClick.AddListener(ClosePackage);
            MakeText(closeRt, "닫기", 28, Vector2.zero, Color.white);

            packagePopup.SetActive(false);
        }

        Text MakeChildText(Transform parent, string msg, int size, float height)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = height;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.95f, 0.85f);
            t.text = msg;
            t.raycastTarget = false;
            return t;
        }

        Text MakeText(Transform parent, string msg, int size, Vector2 pos, Color color)
        {
            var go = new GameObject("Text");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (pos != Vector2.zero)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(560, 80);
            }
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.text = msg;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
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
    }
}
