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
    /// 12장 고가구점. 레이아웃은 Prefab(<c>Assets/Prefabs/UI/ShopScreen.prefab</c>)만 사용.
    /// UI 생성 코드는 에디터 Bake 전용.
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

        [SerializeField] GameObject root;
        [SerializeField] Text dialogueText;
        [SerializeField] Image leftIcon;
        [SerializeField] Text leftName;
        [SerializeField] Text leftPriceLabel;
        [SerializeField] Image rightIcon;
        [SerializeField] Text rightName;
        [SerializeField] Text rightPriceLabel;
        [SerializeField] GameObject packagePopup;
        [SerializeField] Text packageTitle;
        [SerializeField] Text packageBody;
        [SerializeField] Text packagePriceBtnLabel;
        [SerializeField] Text currencyText;

        int dialogueIndex;

        void Awake() => Instance = this;

        void Start()
        {
            if (!EnsureShell()) return;
            WireRuntimeListeners();
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (!EnsureShell()) return;
            WireRuntimeListeners();
            ShopStock.SetCatalog(offerings);
            ShopStock.EnsureFresh(DateTime.UtcNow);
            dialogueIndex = 0;
            SetDialogue(ImugiLines[0]);
            RefreshSlots();
            RefreshCurrencyBar();
            if (GameEconomy.Instance != null)
            {
                GameEconomy.Instance.OnYeopjeonChanged -= OnYeopjeonChanged;
                GameEconomy.Instance.OnMeritChanged -= OnMeritChanged;
                GameEconomy.Instance.OnYeopjeonChanged += OnYeopjeonChanged;
                GameEconomy.Instance.OnMeritChanged += OnMeritChanged;
            }
            if (packagePopup != null) packagePopup.SetActive(false);
            root.SetActive(true);
        }

        public void Close()
        {
            if (GameEconomy.Instance != null)
            {
                GameEconomy.Instance.OnYeopjeonChanged -= OnYeopjeonChanged;
                GameEconomy.Instance.OnMeritChanged -= OnMeritChanged;
            }
            if (packagePopup != null) packagePopup.SetActive(false);
            if (root != null) root.SetActive(false);
            GameSaveBridge.SaveFromWorld();
        }

        void OnYeopjeonChanged(int _) => RefreshCurrencyBar();
        void OnMeritChanged(BigNumber _) => RefreshCurrencyBar();

        void RefreshCurrencyBar()
        {
            if (currencyText == null || GameEconomy.Instance == null) return;
            currencyText.text = $"엽전 {GameEconomy.Instance.Yeopjeon}   공덕 {GameEconomy.Instance.MeritPile.ToDisplayString()}";
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

        void OpenPackage(int index)
        {
            if (index < 0 || index >= Packages.Length || packagePopup == null) return;
            var p = Packages[index];
            if (packageTitle != null) packageTitle.text = p.Name;
            if (packageBody != null) packageBody.text = p.Contents;
            if (packagePriceBtnLabel != null) packagePriceBtnLabel.text = p.PriceLabel;
            packagePopup.SetActive(true);
        }

        void ClosePackage()
        {
            if (packagePopup != null) packagePopup.SetActive(false);
        }

        /// <summary>Prefab 셸만 사용. 없으면 에러 (런타임 생성 없음).</summary>
        bool EnsureShell()
        {
            BindMissingRefsFromHierarchy();
            if (root != null) return true;
            Debug.LogError(
                "[ShopScreen] Prefab 셸이 없습니다. 메뉴 Yoegoe → Bake ShopScreen Prefab (Into Main Scene) 을 실행하세요.");
            return false;
        }

        /// <summary>에디터 Bake용. Prefab 레이아웃 생성.</summary>
        public void EnsureBuiltForBake()
        {
#if UNITY_EDITOR
            if (root == null)
            {
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (shopBackground == null)
                    shopBackground = Resources.Load<Sprite>("UI/ShopInterior");
                if (imugiSprite == null)
                    imugiSprite = Resources.Load<Sprite>("UI/ImugiPortrait");
                Build();
            }
            WireRuntimeListeners();
            if (root != null) root.SetActive(true);
#else
            EnsureShell();
#endif
        }

        void WireRuntimeListeners()
        {
            if (root == null) return;
            BindMissingRefsFromHierarchy();

            BindButton(root.transform.Find("Imugi"), OnImugiTapped);
            BindButton(root.transform.Find("LeftFood/PriceBuy"), OnBuyLeft);
            BindButton(root.transform.Find("RightFood/PriceBuy"), OnBuyRight);
            BindButton(root.transform.Find("Hyang/PriceBuy"), OnBuyHyang);
            BindButton(root.transform.Find("Close"), Close);

            for (int i = 0; i < Packages.Length; i++)
            {
                int captured = i;
                BindButton(root.transform.Find("Pkg_" + Packages[i].Id), () => OpenPackage(captured));
            }

            if (packagePopup != null)
            {
                BindButton(packagePopup.transform, ClosePackage);
                BindButton(packagePopup.transform.Find("Box/ClosePkg"), ClosePackage);
            }
        }

        void BindMissingRefsFromHierarchy()
        {
            if (root == null)
            {
                var canvas = transform.Find("Canvas_Shop");
                if (canvas != null) root = canvas.Find("Root")?.gameObject;
            }
            if (root == null) return;

            var rt = root.transform;
            if (dialogueText == null)
                dialogueText = rt.Find("Dialogue/Text")?.GetComponent<Text>();
            if (currencyText == null)
                currencyText = rt.Find("CurrencyBar/Text")?.GetComponent<Text>();
            if (leftIcon == null)
                leftIcon = rt.Find("LeftFood/Icon")?.GetComponent<Image>();
            if (leftName == null)
                leftName = rt.Find("LeftFood/Name/Text")?.GetComponent<Text>();
            if (leftPriceLabel == null)
                leftPriceLabel = rt.Find("LeftFood/PriceBuy/Text")?.GetComponent<Text>();
            if (rightIcon == null)
                rightIcon = rt.Find("RightFood/Icon")?.GetComponent<Image>();
            if (rightName == null)
                rightName = rt.Find("RightFood/Name/Text")?.GetComponent<Text>();
            if (rightPriceLabel == null)
                rightPriceLabel = rt.Find("RightFood/PriceBuy/Text")?.GetComponent<Text>();
            if (packagePopup == null)
                packagePopup = rt.Find("PackagePopup")?.gameObject;
            if (packagePopup != null)
            {
                if (packageTitle == null)
                    packageTitle = packagePopup.transform.Find("Box/Title/Text")?.GetComponent<Text>();
                if (packageBody == null)
                    packageBody = packagePopup.transform.Find("Box/Body/Text")?.GetComponent<Text>();
                if (packagePriceBtnLabel == null)
                    packagePriceBtnLabel = packagePopup.transform.Find("Box/PriceBtn/Text")?.GetComponent<Text>();
            }
        }

        static void BindButton(Transform t, UnityEngine.Events.UnityAction action)
        {
            if (t == null || action == null) return;
            var btn = t.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

#if UNITY_EDITOR
        static readonly Vector2 DeskLeft = new Vector2(0.28f, 0.60f);
        static readonly Vector2 DeskHyang = new Vector2(0.50f, 0.61f);
        static readonly Vector2 DeskRight = new Vector2(0.72f, 0.60f);
        static readonly Vector2 ImugiPos = new Vector2(0.50f, 0.34f);
        static readonly Vector2[] PackagePos =
        {
            new Vector2(0.22f, 0.20f),
            new Vector2(0.50f, 0.18f),
            new Vector2(0.78f, 0.20f)
        };

        void Build()
        {
            var canvasGO = new GameObject("Canvas_Shop");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 820;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            var rootRt = Stretch(root, canvasGO.transform);

            var bgGO = new GameObject("Background");
            Stretch(bgGO, rootRt);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;
            if (shopBackground != null)
            {
                bgImg.sprite = shopBackground;
                bgImg.preserveAspect = false;
            }
            else
                bgImg.color = new Color(0.22f, 0.16f, 0.12f, 1f);

            BuildCurrencyBar(rootRt);

            var imugiGO = new GameObject("Imugi");
            Place(imugiGO, rootRt, ImugiPos, new Vector2(0.5f, 0f), new Vector2(300, 380));
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

            BuildDeskItem(rootRt, "LeftFood", DeskLeft, out leftIcon, out leftName, out leftPriceLabel,
                ShopStock.OfferingPriceYeopjeon + " 엽전");
            BuildDeskHyang(rootRt, DeskHyang);
            BuildDeskItem(rootRt, "RightFood", DeskRight, out rightIcon, out rightName, out rightPriceLabel,
                ShopStock.OfferingPriceYeopjeon + " 엽전");

            for (int i = 0; i < Packages.Length; i++)
                BuildPackageOnChest(rootRt, Packages[i], PackagePos[i]);

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

            var closeGO = new GameObject("Close");
            var closeRt = Place(closeGO, rootRt, new Vector2(0.94f, 0.96f), new Vector2(0.5f, 0.5f),
                new Vector2(68, 68));
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.12f, 0.09f, 0.07f, 0.85f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            MakeLabel(closeRt, "×", 40, Color.white);

            BuildPackagePopup(rootRt);
        }

        void BuildDeskItem(Transform parent, string name, Vector2 normPos,
            out Image icon, out Text nameLabel, out Text priceLabel,
            string priceText)
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
            MakeLabel(buyRt, ShopStock.HyangPriceYeopjeon + " 엽전", 22, Color.white);
        }

        void BuildPackageOnChest(Transform parent, PackageDef def, Vector2 normPos)
        {
            var card = new GameObject("Pkg_" + def.Id);
            var cardRt = Place(card, parent, normPos, new Vector2(0.5f, 0.5f), new Vector2(220, 100));
            var bg = card.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.07f, 0.05f, 0.78f);
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = bg;
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
            MakeLabel(closeRt, "닫기", 28, Color.white);

            packagePopup.SetActive(false);
        }

        void BuildCurrencyBar(RectTransform parent)
        {
            var go = new GameObject("CurrencyBar");
            var rt = Place(go, parent, new Vector2(0.06f, 0.96f), new Vector2(0f, 1f), new Vector2(380, 64));
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.09f, 0.07f, 0.85f);

            currencyText = MakeLabel(rt, "", 24, new Color(1f, 0.95f, 0.85f));
            currencyText.alignment = TextAnchor.MiddleLeft;
            currencyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var textRt = currencyText.GetComponent<RectTransform>();
            textRt.offsetMin = new Vector2(18, 0);
            textRt.offsetMax = new Vector2(-18, 0);
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
#endif
    }
}
