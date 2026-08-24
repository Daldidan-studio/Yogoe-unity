using KSpirits.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.UI
{
    /// <summary>
    /// ScrollScreenUI 계층을 에디터/Setup 메뉴에서 한 번 생성할 때 사용.
    /// 런타임 Play 시에는 생성하지 않음.
    /// </summary>
    public static class ScrollScreenUIBuilder
    {
        public const int StatSegmentCount = 10;

        public static readonly Color SpiritColor = new(0.55f, 0.85f, 1f);
        public static readonly Color SkySpirit = new(0.04f, 0.08f, 0.18f);
        public static readonly Color MoonSurface = new(0.62f, 0.64f, 0.68f);
        public static readonly Color EnergyOn = new(0.35f, 0.78f, 1f);
        public static readonly Color EnergyOff = new(0.12f, 0.18f, 0.28f);
        public static readonly Color IntimacyOn = new(0.92f, 0.28f, 0.32f);
        public static readonly Color IntimacyOff = new(0.22f, 0.12f, 0.14f);

        public static void Build(ScrollScreenUI ui)
        {
            var rt = (RectTransform)ui.transform;
            Stretch(rt);

            Image skyBg = null;
            Image moonGround = null;
            Image energyPulseBar = null;
            Image[] energySegments = null;
            Image[] intimacySegments = null;
            Text coinText = null;
            Text heartText = null;
            Text incenseText = null;
            Text yokaiNameText = null;
            Text stepText = null;
            Text yokaiLabel = null;
            Text statusText = null;
            Text yutResultText = null;
            Text dialogueSpeaker = null;
            Text dialogueBody = null;
            Text dialogueContinueHint = null;
            Text[] itemCountLabels = new Text[4];
            Button yokaiButton = null;
            Image yokaiImage = null;
            Button trainingButton = null;
            Button throwButton = null;
            Button leaveTrainingButton = null;
            RectTransform yokaiDropZone = null;
            GameObject dialogueRoot = null;
            GameObject choiceRoot = null;
            GameObject offerHighlight = null;
            GameObject trainingPanel = null;
            GameObject cardPanel = null;
            GameObject summonPanel = null;
            GameObject glitchOverlay = null;
            GameObject waterSlotRoot = null;
            Transform choiceContainer = null;

            BuildBackground(ui.transform, ref skyBg, ref moonGround);
            BuildHeader(ui.transform, ref coinText, ref heartText, ref incenseText, ref yokaiNameText,
                ref stepText, ref energySegments, ref intimacySegments, ref energyPulseBar);
            BuildScene(ui.transform, ref yokaiDropZone, ref yokaiButton, ref yokaiImage, ref yokaiLabel,
                ref glitchOverlay);
            BuildBottomDock(ui.transform, ref trainingButton, ref offerHighlight, ref itemCountLabels,
                ref waterSlotRoot, ref statusText);
            BuildDialogueAnchors(ui.transform);
            BuildOverlays(ui.transform, ref trainingPanel, ref yutResultText, ref throwButton,
                ref leaveTrainingButton, ref dialogueRoot, ref dialogueSpeaker, ref dialogueBody,
                ref dialogueContinueHint, ref choiceRoot, ref choiceContainer, ref cardPanel, ref summonPanel);

            ui.BindHierarchy(
                coinText, heartText, incenseText, statusText, stepText,
                dialogueSpeaker, dialogueBody, dialogueContinueHint, yutResultText,
                yokaiNameText, yokaiLabel, itemCountLabels,
                yokaiImage, energyPulseBar, skyBg, moonGround, energySegments, intimacySegments,
                dialogueRoot, choiceRoot, offerHighlight, trainingPanel, cardPanel, summonPanel,
                glitchOverlay, waterSlotRoot, yokaiDropZone, choiceContainer,
                trainingButton, throwButton, leaveTrainingButton, yokaiButton);

            EnsureDialogueLayoutManager(ui);
        }

        public static void BuildDialogueAnchorsPublic(Transform root) => BuildDialogueAnchors(root);

        static void BuildDialogueAnchors(Transform root)
        {
            var anchorsRoot = CreatePanel(root, "DialogueAnchors", new Color(0, 0, 0, 0));
            Stretch(anchorsRoot.GetComponent<RectTransform>());

            // 에디터에서 위치 확인용 반투명. Play/UI 렌더에는 영향 없음(raycast off).
            CreateLayoutAnchor(anchorsRoot.transform, DialogueLayoutId.BottomWide,
                0.04f, 0.22f, 0.96f, 0.44f, new Color(1f, 1f, 1f, 0.08f));
            CreateLayoutAnchor(anchorsRoot.transform, DialogueLayoutId.NearYokai,
                0.04f, 0.38f, 0.58f, 0.56f, new Color(0.4f, 0.85f, 1f, 0.12f));
            CreateLayoutAnchor(anchorsRoot.transform, DialogueLayoutId.AboveMortar,
                0.12f, 0.48f, 0.88f, 0.68f, new Color(1f, 0.85f, 0.4f, 0.1f));
            CreateLayoutAnchor(anchorsRoot.transform, DialogueLayoutId.TopNarration,
                0.06f, 0.72f, 0.94f, 0.84f, new Color(0.85f, 0.75f, 1f, 0.1f));
        }

        static void CreateLayoutAnchor(Transform parent, DialogueLayoutId id,
            float xmin, float ymin, float xmax, float ymax, Color color)
        {
            var go = CreatePanel(parent, id.ToString(), color);
            SetAnchor(go.GetComponent<RectTransform>(), xmin, ymin, xmax, ymax, 0, 0, 0, 0);
            go.GetComponent<Image>().raycastTarget = false;
        }

        static void EnsureDialogueLayoutManager(ScrollScreenUI ui)
        {
            var manager = ui.GetComponent<DialogueLayoutManager>();
            if (manager == null)
                manager = ui.gameObject.AddComponent<DialogueLayoutManager>();

            var catalog = Resources.Load<DialogueLayoutCatalog>("Settings/DialogueLayoutCatalog");
            if (catalog != null)
                manager.SetCatalog(catalog);
            manager.CacheAnchors();
        }

        static void BuildBackground(Transform root, ref Image skyBg, ref Image moonGround)
        {
            skyBg = CreateImage(root, "Sky", SkySpirit);
            SetAnchor(skyBg.rectTransform, 0, 0.16f, 1, 1, 0, 0, 0, 0);

            moonGround = CreateImage(root, "MoonGround", MoonSurface);
            SetAnchor(moonGround.rectTransform, -0.05f, 0, 1.05f, 0.42f, 0, 0, 0, 0);

            var stars = CreatePanel(root, "Stars", new Color(0, 0, 0, 0));
            SetAnchor(stars.GetComponent<RectTransform>(), 0, 0.42f, 1, 1, 0, 0, 0, 0);
            var rng = new System.Random(42);
            for (int i = 0; i < 28; i++)
            {
                var star = CreateImage(stars.transform, $"Star{i}", Color.white);
                var srt = star.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2((float)rng.NextDouble(), 0.35f + (float)rng.NextDouble() * 0.6f);
                var size = rng.Next(2, 5);
                srt.sizeDelta = new Vector2(size, size);
                star.color = new Color(1, 1, 1, 0.35f + (float)rng.NextDouble() * 0.5f);
            }
        }

        static void BuildHeader(Transform root, ref Text coinText, ref Text heartText, ref Text incenseText,
            ref Text yokaiNameText, ref Text stepText, ref Image[] energySegments, ref Image[] intimacySegments,
            ref Image energyPulseBar)
        {
            var header = CreatePanel(root, "Header", new Color(0, 0, 0, 0));
            SetAnchor(header.GetComponent<RectTransform>(), 0, 0.855f, 1, 1, 0, 0, 0, 0);

            var backBtn = CreateButton(header.transform, "Back", "<");
            SetAnchor(backBtn.GetComponent<RectTransform>(), 0.02f, 0.62f, 0.1f, 0.92f, 0, 0, 0, 0);
            backBtn.interactable = false;
            backBtn.GetComponent<Image>().color = new Color(1, 1, 1, 0.08f);

            yokaiNameText = CreateText(header.transform, "YokaiName", "옥토끼 | 넋", 30, TextAnchor.MiddleLeft, UIFontRole.UserInfo);
            SetAnchor(yokaiNameText.rectTransform, 0.1f, 0.62f, 0.62f, 0.92f, 0, 0, 0, 0);

            energySegments = CreateSegmentBar(header.transform, "EnergyBar", EnergyOn, EnergyOff, out energyPulseBar);
            SetAnchor(energySegments[0].transform.parent.GetComponent<RectTransform>(), 0.04f, 0.34f, 0.58f, 0.58f, 0, 0, 0, 0);

            intimacySegments = CreateSegmentBar(header.transform, "IntimacyBar", IntimacyOn, IntimacyOff, out _);
            SetAnchor(intimacySegments[0].transform.parent.GetComponent<RectTransform>(), 0.04f, 0.06f, 0.58f, 0.3f, 0, 0, 0, 0);

            var shopBtn = CreateButton(header.transform, "Shop", "상점");
            SetAnchor(shopBtn.GetComponent<RectTransform>(), 0.62f, 0.58f, 0.82f, 0.88f, 0, 0, 0, 0);
            shopBtn.interactable = false;

            var settingsBtn = CreateButton(header.transform, "Settings", "⚙");
            SetAnchor(settingsBtn.GetComponent<RectTransform>(), 0.84f, 0.58f, 0.96f, 0.88f, 0, 0, 0, 0);
            settingsBtn.interactable = false;

            var coinRow = CreatePanel(header.transform, "CoinRow", new Color(0, 0, 0, 0));
            SetAnchor(coinRow.GetComponent<RectTransform>(), 0.58f, 0.08f, 0.98f, 0.52f, 0, 0, 0, 0);
            var coinIcon = CreateImage(coinRow.transform, "CoinIcon", new Color(1f, 0.82f, 0.2f));
            SetAnchor(coinIcon.rectTransform, 0, 0.15f, 0.12f, 0.85f, 0, 0, 0, 0);
            coinText = CreateText(coinRow.transform, "Coins", "0", 26, TextAnchor.MiddleLeft, UIFontRole.HudNumeric);
            SetAnchor(coinText.rectTransform, 0.14f, 0, 0.82f, 1, 0, 0, 0, 0);

            heartText = CreateText(header.transform, "HeartsHidden", "", 1, TextAnchor.MiddleCenter);
            heartText.gameObject.SetActive(false);
            incenseText = CreateText(header.transform, "IncenseHidden", "", 1, TextAnchor.MiddleCenter);
            incenseText.gameObject.SetActive(false);

            stepText = CreateText(root, "Step", "STEP", 20, TextAnchor.UpperCenter);
            SetAnchor(stepText.rectTransform, 0, 0.84f, 1, 0.855f, 0, 0, 0, 0);
            stepText.color = new Color(1, 1, 1, 0.45f);
        }

        static void BuildScene(Transform root, ref RectTransform yokaiDropZone, ref Button yokaiButton,
            ref Image yokaiImage, ref Text yokaiLabel, ref GameObject glitchOverlay)
        {
            var scene = CreatePanel(root, "Scene", new Color(0, 0, 0, 0));
            SetAnchor(scene.GetComponent<RectTransform>(), 0, 0.18f, 1, 0.84f, 0, 0, 0, 0);

            var mortar = CreatePanel(scene.transform, "Mortar", new Color(0.45f, 0.47f, 0.5f, 1f));
            SetAnchor(mortar.GetComponent<RectTransform>(), 0.18f, 0.08f, 0.82f, 0.52f, 0, 0, 0, 0);
            var mortarInner = CreatePanel(mortar.transform, "MortarInner", new Color(0.32f, 0.34f, 0.38f, 1f));
            SetAnchor(mortarInner.GetComponent<RectTransform>(), 0.08f, 0.12f, 0.92f, 0.88f, 0, 0, 0, 0);
            var pestle = CreatePanel(mortar.transform, "Pestle", new Color(0.55f, 0.38f, 0.22f, 1f));
            SetAnchor(pestle.GetComponent<RectTransform>(), 0.38f, 0.55f, 0.52f, 1.05f, 0, 0, 0, 0);

            var tree = CreatePanel(scene.transform, "Tree", new Color(0.18f, 0.42f, 0.2f, 1f));
            SetAnchor(tree.GetComponent<RectTransform>(), 0.72f, 0.28f, 0.94f, 0.72f, 0, 0, 0, 0);
            var trunk = CreatePanel(tree.transform, "Trunk", new Color(0.42f, 0.28f, 0.16f, 1f));
            SetAnchor(trunk.GetComponent<RectTransform>(), 0.35f, 0, 0.65f, 0.35f, 0, 0, 0, 0);

            var yokaiArea = CreatePanel(scene.transform, "YokaiArea", new Color(0, 0, 0, 0));
            SetAnchor(yokaiArea.GetComponent<RectTransform>(), 0.12f, 0.18f, 0.72f, 0.78f, 0, 0, 0, 0);
            yokaiDropZone = yokaiArea.GetComponent<RectTransform>();

            yokaiButton = CreateButton(yokaiArea.transform, "YokaiButton", "");
            SetAnchor(yokaiButton.GetComponent<RectTransform>(), 0.05f, 0.18f, 0.48f, 0.95f, 0, 0, 0, 0);
            yokaiImage = yokaiButton.GetComponent<Image>();
            yokaiImage.color = SpiritColor;

            yokaiLabel = CreateText(yokaiArea.transform, "YokaiLabel", "넋 · 도깨비불", 24, TextAnchor.LowerCenter, UIFontRole.UserInfo);
            SetAnchor(yokaiLabel.rectTransform, 0, 0, 1, 0.18f, 0, 4, 0, 0);
            yokaiLabel.raycastTarget = false;

            glitchOverlay = CreatePanel(yokaiArea.transform, "Glitch", new Color(1, 1, 1, 0.15f)).gameObject;
            Stretch(glitchOverlay.GetComponent<RectTransform>());
            glitchOverlay.SetActive(false);
            var glitchText = CreateText(glitchOverlay.transform, "GlitchText", "지직… 괴 / 혼", 32, TextAnchor.MiddleCenter);
            Stretch(glitchText.rectTransform);
            glitchText.color = Color.white;
        }

        static void BuildBottomDock(Transform root, ref Button trainingButton, ref GameObject offerHighlight,
            ref Text[] itemCountLabels, ref GameObject waterSlotRoot, ref Text statusText)
        {
            var dock = CreatePanel(root, "BottomDock", new Color(0, 0, 0, 0));
            SetAnchor(dock.GetComponent<RectTransform>(), 0, 0, 1, 0.18f, 0, 0, 0, 0);

            trainingButton = CreateDockButton(dock.transform, "TrainingDock", "수련장", "📜");
            SetAnchor(trainingButton.GetComponent<RectTransform>(), 0.02f, 0.04f, 0.18f, 0.96f, 0, 0, 0, 0);
            trainingButton.gameObject.SetActive(false);

            var storageBtn = CreateDockButton(dock.transform, "StorageDock", "보관함", "🎒");
            SetAnchor(storageBtn.GetComponent<RectTransform>(), 0.82f, 0.04f, 0.98f, 0.96f, 0, 0, 0, 0);
            storageBtn.interactable = false;

            var itemBar = CreatePanel(dock.transform, "ItemBar", new Color(0.12f, 0.11f, 0.1f, 0.92f));
            SetAnchor(itemBar.GetComponent<RectTransform>(), 0.16f, 0.1f, 0.84f, 0.9f, 0, 0, 0, 0);

            offerHighlight = CreatePanel(itemBar.transform, "OfferHighlight", new Color(1f, 0.85f, 0.2f, 0.35f)).gameObject;
            SetAnchor(offerHighlight.GetComponent<RectTransform>(), 0.02f, 0.1f, 0.26f, 0.9f, -4, -4, 4, 4);
            offerHighlight.SetActive(false);

            var itemDefs = new (string name, Color color)[]
            {
                ("정화수", new Color(0.35f, 0.65f, 0.95f, 1f)),
                ("인삼", new Color(0.78f, 0.42f, 0.28f, 1f)),
                ("복숭아", new Color(1f, 0.72f, 0.35f, 1f)),
                ("김치", new Color(0.85f, 0.22f, 0.18f, 1f)),
            };

            for (int i = 0; i < 4; i++)
            {
                float xmin = 0.02f + i * 0.245f;
                float xmax = xmin + 0.22f;
                var slot = CreatePanel(itemBar.transform, $"ItemSlot{i}", itemDefs[i].color);
                SetAnchor(slot.GetComponent<RectTransform>(), xmin, 0.12f, xmax, 0.88f, 0, 0, 0, 0);

                var badgeColors = new[] {
                    new Color(0.2f, 0.65f, 0.35f, 0.95f),
                    new Color(0.85f, 0.25f, 0.2f, 0.95f),
                    new Color(0.9f, 0.75f, 0.15f, 0.95f),
                    new Color(0.85f, 0.25f, 0.2f, 0.95f),
                };
                var badge = CreatePanel(slot.transform, "Badge", badgeColors[i]);
                SetAnchor(badge.GetComponent<RectTransform>(), 0.55f, 0.62f, 0.98f, 0.95f, 0, 0, 0, 0);
                itemCountLabels[i] = CreateText(badge.transform, "Count", "×0", 18, TextAnchor.MiddleCenter, UIFontRole.HudNumeric);
                Stretch(itemCountLabels[i].rectTransform);
                itemCountLabels[i].raycastTarget = false;

                if (i == 0)
                    waterSlotRoot = slot;
                else
                {
                    var nameLabel = CreateText(slot.transform, "Name", itemDefs[i].name, 16, TextAnchor.LowerCenter);
                    SetAnchor(nameLabel.rectTransform, 0, 0, 1, 0.28f, 0, 2, 0, 0);
                    nameLabel.raycastTarget = false;
                    nameLabel.color = new Color(1, 1, 1, 0.75f);
                }
            }

            statusText = CreateText(root, "Status", "", 24, TextAnchor.MiddleCenter);
            SetAnchor(statusText.rectTransform, 0.08f, 0.17f, 0.92f, 0.22f, 0, 0, 0, 0);
            statusText.color = new Color(1f, 0.92f, 0.7f);
        }

        static void BuildOverlays(Transform root, ref GameObject trainingPanel, ref Text yutResultText,
            ref Button throwButton, ref Button leaveTrainingButton, ref GameObject dialogueRoot,
            ref Text dialogueSpeaker, ref Text dialogueBody, ref Text dialogueContinueHint,
            ref GameObject choiceRoot, ref Transform choiceContainer, ref GameObject cardPanel,
            ref GameObject summonPanel)
        {
            trainingPanel = CreatePanel(root, "TrainingPanel", new Color(0, 0, 0, 0.35f)).gameObject;
            SetAnchor(trainingPanel.GetComponent<RectTransform>(), 0.08f, 0.28f, 0.92f, 0.78f, 0, 0, 0, 0);
            var trainTitle = CreateText(trainingPanel.transform, "Title", "수련장 · 윷놀이", 34, TextAnchor.UpperCenter);
            SetAnchor(trainTitle.rectTransform, 0, 0.75f, 1, 1, 0, 0, 0, 0);
            yutResultText = CreateText(trainingPanel.transform, "YutResult", "", 40, TextAnchor.MiddleCenter);
            SetAnchor(yutResultText.rectTransform, 0.1f, 0.35f, 0.9f, 0.75f, 0, 0, 0, 0);
            throwButton = CreateButton(trainingPanel.transform, "Throw", "윷 던지기");
            SetAnchor(throwButton.GetComponent<RectTransform>(), 0.15f, 0.08f, 0.5f, 0.28f, 0, 0, 0, 0);
            leaveTrainingButton = CreateButton(trainingPanel.transform, "Leave", "나가기");
            SetAnchor(leaveTrainingButton.GetComponent<RectTransform>(), 0.52f, 0.08f, 0.85f, 0.28f, 0, 0, 0, 0);
            trainingPanel.SetActive(false);
            throwButton.gameObject.SetActive(false);
            leaveTrainingButton.gameObject.SetActive(false);

            dialogueRoot = CreatePanel(root, "Dialogue", new Color(0.05f, 0.04f, 0.03f, 0.92f)).gameObject;
            SetAnchor(dialogueRoot.GetComponent<RectTransform>(), 0.04f, 0.22f, 0.96f, 0.44f, 0, 0, 0, 0);
            dialogueSpeaker = CreateText(dialogueRoot.transform, "Speaker", "", 24, TextAnchor.UpperLeft, UIFontRole.Dialogue);
            SetAnchor(dialogueSpeaker.rectTransform, 0, 0.7f, 1, 1, 20, -8, -20, 0);
            dialogueSpeaker.color = new Color(1f, 0.85f, 0.45f);
            dialogueBody = CreateText(dialogueRoot.transform, "Body", "", 28, TextAnchor.UpperLeft, UIFontRole.Dialogue);
            SetAnchor(dialogueBody.rectTransform, 0, 0.05f, 1, 0.7f, 20, 10, -20, 0);
            dialogueBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueBody.verticalOverflow = VerticalWrapMode.Overflow;
            dialogueContinueHint = CreateText(dialogueRoot.transform, "ContinueHint", "▼", 22, TextAnchor.LowerRight, UIFontRole.Dialogue);
            SetAnchor(dialogueContinueHint.rectTransform, 0.7f, 0, 1, 0.25f, 0, 8, -16, 0);
            dialogueContinueHint.color = new Color(1f, 1f, 1f, 0.55f);
            dialogueContinueHint.gameObject.SetActive(false);
            dialogueContinueHint.raycastTarget = false;
            dialogueRoot.AddComponent<Button>().targetGraphic = dialogueRoot.GetComponent<Image>();
            dialogueRoot.SetActive(false);

            choiceRoot = CreatePanel(root, "Choices", new Color(0, 0, 0, 0.55f)).gameObject;
            Stretch(choiceRoot.GetComponent<RectTransform>());
            choiceContainer = choiceRoot.transform;
            choiceRoot.SetActive(false);

            cardPanel = CreatePanel(root, "CardPanel", new Color(0.08f, 0.07f, 0.1f, 0.95f)).gameObject;
            SetAnchor(cardPanel.GetComponent<RectTransform>(), 0.15f, 0.35f, 0.85f, 0.75f, 0, 0, 0, 0);
            CreateText(cardPanel.transform, "CardTitle", "옥토끼 요괴패", 36, TextAnchor.UpperCenter);
            cardPanel.SetActive(false);

            summonPanel = CreatePanel(root, "Summon", new Color(0.12f, 0.1f, 0.08f, 0.96f)).gameObject;
            SetAnchor(summonPanel.GetComponent<RectTransform>(), 0.1f, 0.3f, 0.9f, 0.7f, 0, 0, 0, 0);
            CreateText(summonPanel.transform, "SummonText",
                "소환 화면\n(향 3개로 첫 요괴 소환)\n\n튜토리얼 클리어!",
                34, TextAnchor.MiddleCenter);
            summonPanel.SetActive(false);
        }

        static Image[] CreateSegmentBar(Transform parent, string name, Color on, Color off, out Image pulseTarget)
        {
            var track = CreatePanel(parent, name, new Color(0.08f, 0.08f, 0.1f, 0.65f));
            pulseTarget = track.GetComponent<Image>();
            var segments = new Image[StatSegmentCount];
            for (int i = 0; i < StatSegmentCount; i++)
            {
                float xmin = 0.01f + i * 0.098f;
                var seg = CreateImage(track.transform, $"Seg{i}", off);
                SetAnchor(seg.rectTransform, xmin, 0.12f, xmin + 0.088f, 0.88f, 0, 0, 0, 0);
                segments[i] = seg;
            }
            return segments;
        }

        static Button CreateDockButton(Transform parent, string name, string label, string icon)
        {
            var go = CreatePanel(parent, name, new Color(0, 0, 0, 0));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();

            var iconText = CreateText(go.transform, "Icon", icon, 34, TextAnchor.UpperCenter);
            SetAnchor(iconText.rectTransform, 0.1f, 0.38f, 0.9f, 0.92f, 0, 0, 0, 0);
            iconText.raycastTarget = false;

            var labelText = CreateText(go.transform, "Label", label, 22, TextAnchor.LowerCenter);
            SetAnchor(labelText.rectTransform, 0, 0, 1, 0.38f, 0, 4, 0, 0);
            labelText.raycastTarget = false;
            return btn;
        }

        static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor,
            UIFontRole role = UIFontRole.Default)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            UIFont.Apply(text, role);
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label)
        {
            var go = CreatePanel(parent, name, new Color(0.25f, 0.22f, 0.18f, 0.95f));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.35f, 0.28f);
            colors.pressedColor = new Color(0.18f, 0.16f, 0.12f);
            btn.colors = colors;
            if (!string.IsNullOrEmpty(label))
            {
                var t = CreateText(go.transform, "Label", label, 26, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform);
                t.raycastTarget = false;
            }
            return btn;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetAnchor(RectTransform rt, float xmin, float ymin, float xmax, float ymax,
            float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(xmin, ymin);
            rt.anchorMax = new Vector2(xmax, ymax);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }
    }
}
