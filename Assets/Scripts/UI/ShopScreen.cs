using System;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>
    /// 12장 고가구점.
    /// 배경 일러스트 기준: 책상 위=상품, 통로 중앙=이무기, 앞 상자=패키지, 최하단=대화.
    /// </summary>
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

        // 배경(576×1024) 기준 정규화 좌표 — 책상·통로·앞상자에 맞춤
        static readonly Vector2 DeskLeft = new Vector2(0.28f, 0.60f);
        static readonly Vector2 DeskHyang = new Vector2(0.50f, 0.61f);
        static readonly Vector2 DeskRight = new Vector2(0.72f, 0.60f);
        static readonly Vector2 ImugiPos = new Vector2(0.50f, 0.34f);
        static readonly Vector2 ResetPos = new Vector2(0.88f, 0.56f);
        static readonly Vector2[] PackagePos =
        {
            new Vector2(0.22f, 0.20f),
            new Vector2(0.50f, 0.18f),
            new Vector2(0.78f, 0.20f)
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
            BindOfferingSlot(ShopStock.Side.Left, leftIcon, leftName, leftPriceLabel);
            BindOfferingSlot(ShopStock.Side.Right, rightIcon, rightName, rightPriceLabel);
            if (resetCostLabel != null)
                resetCostLabel.text = "리셋\n" + ShopStock.GetResetCostMerit().ToDisplayString();
        }

        void BindOfferingSlot(ShopStock.Side side, Image icon, Text nameLabel, Text priceLabel)
        {
            var o = ShopStock.GetOffering(side);
            if (icon != null)
            {
                icon.sprite = o != null ? o.icon : null;
                icon.enabled = o != null && o.icon != null;
                icon.color = icon.enabled ? Color.white : new Color(1f, 1f, 1f, 0.2f);
            }
            if (nameLabel != null)
                nameLabel.text = o != null
                    ? (string.IsNullOrEmpty(o.displayName) ? o.offeringId : o.displayName)
                    : "—";
            if (priceLabel != null)
                priceLabel.text = ShopStock.OfferingPriceYeopjeon + " 엽전";
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
            if (index < 0 || index >= Packages.Length || packagePopup == null) return;
            var p = Packages[index];
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
            scaler.matchWidthOrHeight = 1f; // 세로 기준 — 배경 비율 유지에 유리
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            var rootRt = Stretch(root, canvasGO.transform);

            // 1) 배경 풀블리드
            var bgGO = new GameObject("Background");
            Stretch(bgGO, rootRt);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;
            if (shopBackground != null)
            {
                bgImg.sprite = shopBackground;
                bgImg.preserveAspect = false; // 캔버스와 동일 세로비
            }
            else
                bgImg.color = new Color(0.22f, 0.16f, 0.12f, 1f);

            // 2) 이무기 — 책상 앞 통로 중앙 (발 기준)
            var imugiGO = new GameObject("Imugi");
            var imugiRt = Place(imugiGO, rootRt, ImugiPos, new Vector2(0.5f, 0f), new Vector2(300, 380));
            var imugiImg = imugiGO.AddComponent<Image>();
            imugiImg.preserveAspect = true;
            imugiImg.raycastTarget = true;
            if (imugiSprite != null)
            {
                imugiImg.sprite = imugiSprite;
                imugiImg.color = Color.white;
            }
            else
                imugiImg.color = new Color(0.5f, 0.55f, 0.7f, 1f);
            var imugiBtn = imugiGO.AddComponent<Button>();
            imugiBtn.targetGraphic = imugiImg;
            imugiBtn.onClick.AddListener(OnImugiTapped);

            // 3) 책상 위 — 음식 / 향 / 음식
            BuildDeskItem(rootRt, "LeftFood", DeskLeft, out leftIcon, out leftName, out leftPriceLabel,
                ShopStock.OfferingPriceYeopjeon + " 엽전", OnBuyLeft);
            BuildDeskHyang(rootRt, DeskHyang);
            BuildDeskItem(rootRt, "RightFood", DeskRight, out rightIcon, out rightName, out rightPriceLabel,
                ShopStock.OfferingPriceYeopjeon + " 엽전", OnBuyRight);

            // 4) 리셋 — 책상 오른쪽
            var resetGO = new GameObject("Reset");
            var resetRt = Place(resetGO, rootRt, ResetPos, new Vector2(0.5f, 0.5f), new Vector2(120, 72));
            var resetImg = resetGO.AddComponent<Image>();
            resetImg.color = new Color(0.12f, 0.09f, 0.07f, 0.82f);
            var resetBtn = resetGO.AddComponent<Button>();
            resetBtn.targetGraphic = resetImg;
            resetBtn.onClick.AddListener(OnResetStock);
            resetCostLabel = MakeLabel(resetRt, "리셋", 20, new Color(1f, 0.9f, 0.7f));

            // 5) 패키지 — 앞쪽 상자 위
            for (int i = 0; i < Packages.Length; i++)
            {
                int captured = i;
                BuildPackageOnChest(rootRt, Packages[i], PackagePos[i], () => OpenPackage(captured));
            }

            // 6) 대화창 — 최하단
            var dialGO = new GameObject("Dialogue");
            var dialRt = dialGO.AddComponent<RectTransform>();
            dialRt.SetParent(rootRt, false);
            dialRt.anchorMin = new Vector2(0.04f, 0.01f);
            dialRt.anchorMax = new Vector2(0.96f, 0.095f);
            dialRt.offsetMin = dialRt.offsetMax = Vector2.zero;
            var dialBg = dialGO.AddComponent<Image>();
            dialBg.color = new Color(0.06f, 0.04f, 0.03f, 0.86f);
            dialogueText = MakeLabel(dialRt, ImugiLines[0], 28, new Color(1f, 0.95f, 0.85f));
            dialogueText.alignment = TextAnchor.MiddleLeft;
            var dRt = dialogueText.GetComponent<RectTransform>();
            dRt.offsetMin = new Vector2(28, 6);
            dRt.offsetMax = new Vector2(-28, -6);

            // 닫기
            var closeGO = new GameObject("Close");
            var closeRt = Place(closeGO, rootRt, new Vector2(0.94f, 0.96f), new Vector2(0.5f, 0.5f),
                new Vector2(68, 68));
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.12f, 0.09f, 0.07f, 0.85f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);
            MakeLabel(closeRt, "×", 40, Color.white);

            BuildPackagePopup(rootRt);
        }

        /// <summary>책상 위 공양 슬롯: 아이콘 + 이름 + [N 엽전] 버튼.</summary>
        void BuildDeskItem(Transform parent, string name, Vector2 normPos,
            out Image icon, out Text nameLabel, out Text priceLabel,
            string priceText, UnityEngine.Events.UnityAction onBuy)
        {
            var slot = new GameObject(name);
            var slotRt = Place(slot, parent, normPos, new Vector2(0.5f, 0.5f), new Vector2(168, 200));
            var plate = slot.AddComponent<Image>();
            plate.color = new Color(0.08f, 0.05f, 0.04f, 0.55f);

            var iconGO = new GameObject("Icon");
            Place(iconGO, slotRt, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(88, 88));
            icon = iconGO.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var nameGO = new GameObject("Name");
            var nameRt = Place(nameGO, slotRt, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f),
                new Vector2(150, 32));
            nameLabel = MakeLabel(nameRt, "—", 18, new Color(1f, 0.95f, 0.85f));

            var buyGO = new GameObject("PriceBuy");
            var buyRt = Place(buyGO, slotRt, new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.5f),
                new Vector2(140, 44));
            var buyImg = buyGO.AddComponent<Image>();
            buyImg.color = new Color(0.55f, 0.38f, 0.18f, 0.95f);
            var buyBtn = buyGO.AddComponent<Button>();
            buyBtn.targetGraphic = buyImg;
            buyBtn.onClick.AddListener(onBuy);
            priceLabel = MakeLabel(buyRt, priceText, 22, Color.white);
        }

        void BuildDeskHyang(Transform parent, Vector2 normPos)
        {
            var slot = new GameObject("Hyang");
            var slotRt = Place(slot, parent, normPos, new Vector2(0.5f, 0.5f), new Vector2(168, 200));
            var plate = slot.AddComponent<Image>();
            plate.color = new Color(0.08f, 0.05f, 0.04f, 0.55f);

            var titleGO = new GameObject("Title");
            var titleRt = Place(titleGO, slotRt, new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.5f),
                new Vector2(140, 48));
            MakeLabel(titleRt, "향", 36, new Color(0.95f, 0.9f, 0.75f));

            var subGO = new GameObject("Sub");
            var subRt = Place(subGO, slotRt, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f),
                new Vector2(140, 28));
            MakeLabel(subRt, "×1", 22, new Color(1f, 0.92f, 0.8f));

            var buyGO = new GameObject("PriceBuy");
            var buyRt = Place(buyGO, slotRt, new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.5f),
                new Vector2(140, 44));
            var buyImg = buyGO.AddComponent<Image>();
            buyImg.color = new Color(0.35f, 0.4f, 0.55f, 0.95f);
            var buyBtn = buyGO.AddComponent<Button>();
            buyBtn.targetGraphic = buyImg;
            buyBtn.onClick.AddListener(OnBuyHyang);
            MakeLabel(buyRt, ShopStock.HyangPriceYeopjeon + " 엽전", 22, Color.white);
        }

        void BuildPackageOnChest(Transform parent, PackageDef def, Vector2 normPos, Action onTap)
        {
            var card = new GameObject("Pkg_" + def.Id);
            var cardRt = Place(card, parent, normPos, new Vector2(0.5f, 0.5f), new Vector2(220, 100));
            var bg = card.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.07f, 0.05f, 0.78f);
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onTap());
            MakeLabel(cardRt, def.Name + "\n" + def.PriceLabel, 22, new Color(1f, 0.93f, 0.82f));
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
            var boxRt = Place(box, rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(640, 520));
            var boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);
            box.AddComponent<Button>().targetGraphic = boxBg;

            packageTitle = MakeLabel(
                Place(new GameObject("Title"), boxRt, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.5f),
                    new Vector2(560, 60)),
                "패키지", 34, new Color(1f, 0.95f, 0.85f));

            packageBody = MakeLabel(
                Place(new GameObject("Body"), boxRt, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f),
                    new Vector2(520, 200)),
                "", 26, new Color(1f, 0.9f, 0.78f));

            var priceGO = new GameObject("PriceBtn");
            var priceRt = Place(priceGO, boxRt, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.5f),
                new Vector2(280, 64));
            var priceImg = priceGO.AddComponent<Image>();
            priceImg.color = new Color(0.45f, 0.35f, 0.25f, 1f);
            packagePriceBtnLabel = MakeLabel(priceRt, "₩0", 28, Color.white);

            var closeGO = new GameObject("ClosePkg");
            var closeRt = Place(closeGO, boxRt, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.5f),
                new Vector2(220, 60));
            var cImg = closeGO.AddComponent<Image>();
            cImg.color = new Color(0.35f, 0.28f, 0.24f, 1f);
            var cBtn = closeGO.AddComponent<Button>();
            cBtn.targetGraphic = cImg;
            cBtn.onClick.AddListener(ClosePackage);
            MakeLabel(closeRt, "닫기", 28, Color.white);

            packagePopup.SetActive(false);
        }

        Text MakeLabel(RectTransform parent, string msg, int size, Color color)
        {
            var go = new GameObject("Text");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = UiFonts.Size(size);
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.text = msg;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static RectTransform Place(GameObject go, Transform parent, Vector2 normAnchor, Vector2 pivot,
            Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = normAnchor;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            return rt;
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
