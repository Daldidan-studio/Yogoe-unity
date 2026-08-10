using System;
using System.Collections;
using System.Collections.Generic;
using KSpirits.Animation;
using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;
using UnityEngine;
using UnityEngine.UI;
namespace KSpirits.UI
{
    public class ScrollScreenUI : MonoBehaviour
    {
        public event Action OnYokaiTapped;
        public event Action OnOfferPurifiedWater;
        public event Action OnTrainingPressed;
        public event Action OnThrowYutPressed;
        public event Action OnLeaveTrainingPressed;
        public event Action<string> OnChoiceSelected;
        public event Action OnDialogueContinue;

        Text _coinText;
        Text _heartText;
        Text _incenseText;
        Text _energyText;
        Text _intimacyText;
        Text _stageText;
        Text _statusText;
        Text _stepText;
        Text _dialogueSpeaker;
        Text _dialogueBody;
        Text _dialogueContinueHint;
        Text _yutResultText;
        Text _yokaiLabel;
        Image _yokaiImage;
        Image _energyFill;
        Image _intimacyFill;
        Image _bg;
        GameObject _dialogueRoot;
        GameObject _choiceRoot;
        GameObject _offerHighlight;
        GameObject _trainingPanel;
        GameObject _cardPanel;
        GameObject _summonPanel;
        GameObject _glitchOverlay;
        GameObject _waterSlotRoot;
        DraggableOfferItem _waterDrag;
        DialogueTypewriter _typewriter;
        UiAnimPlayer _anims;
        Button _trainingButton;
        Button _throwButton;
        Button _leaveTrainingButton;
        Button _yokaiButton;
        RectTransform _yokaiDropZone;
        Transform _choiceContainer;
        bool _blackened;
        bool _offerEnabled;

        static readonly Color SpiritColor = new(0.55f, 0.85f, 1f);
        static readonly Color ApparitionColor = new(1f, 0.85f, 0.7f);
        static readonly Color ManifestColor = new(0.95f, 0.95f, 0.9f);
        static readonly Color BlackColor = new(0.15f, 0.15f, 0.2f);
        static readonly Color BgSpirit = new(0.05f, 0.05f, 0.08f);
        static readonly Color BgApparition = new(0.18f, 0.18f, 0.2f);
        static readonly Color BgManifest = new(0.35f, 0.32f, 0.28f);
        static readonly Color BgTraining = new(0.22f, 0.28f, 0.22f);

        public static ScrollScreenUI Create(Transform parent)
        {
            var root = new GameObject("ScrollScreenUI", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var ui = root.AddComponent<ScrollScreenUI>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            var rt = (RectTransform)transform;
            Stretch(rt);

            _bg = CreateImage(transform, "Background", BgSpirit);
            Stretch(_bg.rectTransform);

            // Top bar
            var top = CreatePanel(transform, "TopBar", new Color(0, 0, 0, 0.55f));
            var topRt = top.GetComponent<RectTransform>();
            SetAnchor(topRt, 0, 1, 1, 1, 0, -100, 0, 0);

            _coinText = CreateText(top.transform, "Coins", "엽전 0", 28, TextAnchor.MiddleLeft);
            SetAnchor(_coinText.rectTransform, 0, 0, 0.33f, 1, 24, 0, 0, 0);

            _heartText = CreateText(top.transform, "Hearts", "하트 5", 28, TextAnchor.MiddleCenter);
            SetAnchor(_heartText.rectTransform, 0.33f, 0, 0.66f, 1, 0, 0, 0, 0);

            _incenseText = CreateText(top.transform, "Incense", "향 0", 28, TextAnchor.MiddleRight);
            SetAnchor(_incenseText.rectTransform, 0.66f, 0, 1, 1, 0, 0, -24, 0);

            _stepText = CreateText(transform, "Step", "STEP", 22, TextAnchor.UpperCenter);
            SetAnchor(_stepText.rectTransform, 0, 1, 1, 1, 0, -140, 0, -100);
            _stepText.color = new Color(1, 1, 1, 0.7f);

            // Yokai area
            var yokaiArea = CreatePanel(transform, "YokaiArea", new Color(0, 0, 0, 0.15f));
            SetAnchor(yokaiArea.GetComponent<RectTransform>(), 0.1f, 0.28f, 0.9f, 0.78f, 0, 0, 0, 0);
            _yokaiDropZone = yokaiArea.GetComponent<RectTransform>();

            _yokaiButton = CreateButton(yokaiArea.transform, "YokaiButton", "", () => OnYokaiTapped?.Invoke());
            Stretch(_yokaiButton.GetComponent<RectTransform>());
            _yokaiImage = _yokaiButton.GetComponent<Image>();
            _yokaiImage.color = SpiritColor;

            _yokaiLabel = CreateText(yokaiArea.transform, "YokaiLabel", "넋 · 도깨비불", 32, TextAnchor.LowerCenter);
            SetAnchor(_yokaiLabel.rectTransform, 0, 0, 1, 0.2f, 0, 8, 0, 0);
            _yokaiLabel.raycastTarget = false;

            _glitchOverlay = CreatePanel(yokaiArea.transform, "Glitch", new Color(1, 1, 1, 0.15f)).gameObject;
            Stretch(_glitchOverlay.GetComponent<RectTransform>());
            _glitchOverlay.SetActive(false);
            var glitchText = CreateText(_glitchOverlay.transform, "GlitchText", "지직… 괴 / 혼", 36, TextAnchor.MiddleCenter);
            Stretch(glitchText.rectTransform);
            glitchText.color = Color.white;

            // Bars
            var bars = CreatePanel(transform, "Bars", new Color(0, 0, 0, 0.4f));
            SetAnchor(bars.GetComponent<RectTransform>(), 0.08f, 0.18f, 0.92f, 0.27f, 0, 0, 0, 0);

            _energyFill = CreateBar(bars.transform, "Energy", new Color(0.3f, 0.8f, 0.55f), out _energyText, "기력");
            SetAnchor(_energyFill.transform.parent.GetComponent<RectTransform>(), 0.02f, 0.55f, 0.98f, 0.95f, 0, 0, 0, 0);

            _intimacyFill = CreateBar(bars.transform, "Intimacy", new Color(0.95f, 0.45f, 0.55f), out _intimacyText, "친밀도");
            SetAnchor(_intimacyFill.transform.parent.GetComponent<RectTransform>(), 0.02f, 0.05f, 0.98f, 0.45f, 0, 0, 0, 0);

            _stageText = CreateText(transform, "Stage", "", 24, TextAnchor.MiddleCenter);
            SetAnchor(_stageText.rectTransform, 0, 0.27f, 1, 0.31f, 0, 0, 0, 0);

            // Status
            _statusText = CreateText(transform, "Status", "", 26, TextAnchor.MiddleCenter);
            SetAnchor(_statusText.rectTransform, 0.05f, 0.12f, 0.95f, 0.17f, 0, 0, 0, 0);
            _statusText.color = new Color(1f, 0.92f, 0.7f);

            // Bottom item bar
            var itemBar = CreatePanel(transform, "ItemBar", new Color(0.1f, 0.08f, 0.06f, 0.9f));
            SetAnchor(itemBar.GetComponent<RectTransform>(), 0, 0, 1, 0, 0, 0, 0, 110);

            _offerHighlight = CreatePanel(itemBar.transform, "OfferHighlight", new Color(1f, 0.85f, 0.2f, 0.45f)).gameObject;
            SetAnchor(_offerHighlight.GetComponent<RectTransform>(), 0.04f, 0.08f, 0.36f, 0.92f, -8, -8, 8, 8);
            _offerHighlight.SetActive(false);

            _waterSlotRoot = CreatePanel(itemBar.transform, "WaterSlot", new Color(0.35f, 0.65f, 0.95f, 1f));
            SetAnchor(_waterSlotRoot.GetComponent<RectTransform>(), 0.05f, 0.12f, 0.35f, 0.88f, 0, 0, 0, 0);
            var waterLabel = CreateText(_waterSlotRoot.transform, "Label", "정화수 ×0\n(드래그)", 22, TextAnchor.MiddleCenter);
            Stretch(waterLabel.rectTransform);
            waterLabel.raycastTarget = false;

            var canvas = GetComponentInParent<Canvas>();
            _waterDrag = _waterSlotRoot.AddComponent<DraggableOfferItem>();
            _waterDrag.Setup(canvas, _yokaiDropZone);
            _waterDrag.OnDroppedOnYokai += () =>
            {
                if (_offerEnabled)
                    OnOfferPurifiedWater?.Invoke();
            };
            _waterSlotRoot.SetActive(false);

            _trainingButton = CreateButton(itemBar.transform, "Training", "수련장", () => OnTrainingPressed?.Invoke());
            SetAnchor(_trainingButton.GetComponent<RectTransform>(), 0.55f, 0.15f, 0.95f, 0.85f, 0, 0, 0, 0);
            _trainingButton.gameObject.SetActive(false);

            // Training panel
            _trainingPanel = CreatePanel(transform, "TrainingPanel", new Color(0, 0, 0, 0.25f)).gameObject;
            SetAnchor(_trainingPanel.GetComponent<RectTransform>(), 0.1f, 0.32f, 0.9f, 0.72f, 0, 0, 0, 0);
            var trainTitle = CreateText(_trainingPanel.transform, "Title", "수련장 · 윷놀이", 34, TextAnchor.UpperCenter);
            SetAnchor(trainTitle.rectTransform, 0, 0.75f, 1, 1, 0, 0, 0, 0);
            _yutResultText = CreateText(_trainingPanel.transform, "YutResult", "", 40, TextAnchor.MiddleCenter);
            SetAnchor(_yutResultText.rectTransform, 0.1f, 0.35f, 0.9f, 0.75f, 0, 0, 0, 0);
            _throwButton = CreateButton(_trainingPanel.transform, "Throw", "윷 던지기", () => OnThrowYutPressed?.Invoke());
            SetAnchor(_throwButton.GetComponent<RectTransform>(), 0.15f, 0.08f, 0.5f, 0.28f, 0, 0, 0, 0);
            _leaveTrainingButton = CreateButton(_trainingPanel.transform, "Leave", "수련장 나가기", () => OnLeaveTrainingPressed?.Invoke());
            SetAnchor(_leaveTrainingButton.GetComponent<RectTransform>(), 0.52f, 0.08f, 0.85f, 0.28f, 0, 0, 0, 0);
            _trainingPanel.SetActive(false);
            _throwButton.gameObject.SetActive(false);
            _leaveTrainingButton.gameObject.SetActive(false);

            // Dialogue
            _dialogueRoot = CreatePanel(transform, "Dialogue", new Color(0.05f, 0.04f, 0.03f, 0.92f)).gameObject;
            SetAnchor(_dialogueRoot.GetComponent<RectTransform>(), 0.04f, 0.2f, 0.96f, 0.42f, 0, 0, 0, 0);
            _dialogueSpeaker = CreateText(_dialogueRoot.transform, "Speaker", "", 24, TextAnchor.UpperLeft);
            SetAnchor(_dialogueSpeaker.rectTransform, 0, 0.7f, 1, 1, 20, -8, -20, 0);
            _dialogueSpeaker.color = new Color(1f, 0.85f, 0.45f);
            _dialogueBody = CreateText(_dialogueRoot.transform, "Body", "", 28, TextAnchor.UpperLeft);
            SetAnchor(_dialogueBody.rectTransform, 0, 0.05f, 1, 0.7f, 20, 10, -20, 0);
            _dialogueBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _dialogueBody.verticalOverflow = VerticalWrapMode.Overflow;
            _dialogueContinueHint = CreateText(_dialogueRoot.transform, "ContinueHint", "▼", 22, TextAnchor.LowerRight);
            SetAnchor(_dialogueContinueHint.rectTransform, 0.7f, 0, 1, 0.25f, 0, 8, -16, 0);
            _dialogueContinueHint.color = new Color(1f, 1f, 1f, 0.55f);
            _dialogueContinueHint.gameObject.SetActive(false);
            _dialogueContinueHint.raycastTarget = false;

            _typewriter = gameObject.AddComponent<DialogueTypewriter>();
            _typewriter.Bind(_dialogueBody);
            AnimCatalog.EnsureLoaded();
            var tw = AnimCatalog.Get("dialogue_typewriter");
            if (tw != null)
                _typewriter.Configure(tw.charsPerSecond, tw.punctuationHold);

            var dialogueBtn = _dialogueRoot.AddComponent<Button>();
            dialogueBtn.targetGraphic = _dialogueRoot.GetComponent<Image>();
            dialogueBtn.onClick.AddListener(HandleDialogueTap);
            _dialogueRoot.SetActive(false);

            // Choices
            _choiceRoot = CreatePanel(transform, "Choices", new Color(0, 0, 0, 0.55f)).gameObject;
            Stretch(_choiceRoot.GetComponent<RectTransform>());
            _choiceContainer = _choiceRoot.transform;
            _choiceRoot.SetActive(false);

            // Card panel
            _cardPanel = CreatePanel(transform, "CardPanel", new Color(0.08f, 0.07f, 0.1f, 0.95f)).gameObject;
            SetAnchor(_cardPanel.GetComponent<RectTransform>(), 0.15f, 0.35f, 0.85f, 0.75f, 0, 0, 0, 0);
            CreateText(_cardPanel.transform, "CardTitle", "옥토끼 요괴패", 36, TextAnchor.UpperCenter);
            _cardPanel.SetActive(false);

            // Summon placeholder
            _summonPanel = CreatePanel(transform, "Summon", new Color(0.12f, 0.1f, 0.08f, 0.96f)).gameObject;
            SetAnchor(_summonPanel.GetComponent<RectTransform>(), 0.1f, 0.3f, 0.9f, 0.7f, 0, 0, 0, 0);
            CreateText(_summonPanel.transform, "SummonText",
                "소환 화면\n(향 3개로 첫 요괴 소환)\n\n튜토리얼 클리어!",
                34, TextAnchor.MiddleCenter);
            _summonPanel.SetActive(false);

            _anims = gameObject.AddComponent<UiAnimPlayer>();
            _anims.YokaiRoot = _yokaiButton.GetComponent<RectTransform>();
            _anims.YokaiImage = _yokaiImage;
            _anims.EnergyFill = _energyFill;
            _anims.GlitchOverlay = _glitchOverlay;
            AnimCatalog.EnsureLoaded();
        }

        public void RefreshAll(GameState state)
        {
            _coinText.text = $"엽전 {state.Wallet.Coins}";
            _heartText.text = $"하트 {state.Wallet.Hearts}/{GameConstants.HeartMax}";
            _incenseText.text = $"향 {state.Wallet.Incense}";

            var y = state.FocusYokai;
            _energyText.text = $"기력 {y.Energy}/{GameConstants.EnergyMax}";
            _intimacyText.text = $"친밀도 {y.Intimacy}/{GameConstants.IntimacyMax}";
            _energyFill.rectTransform.anchorMax = new Vector2(y.Energy / (float)GameConstants.EnergyMax, 1);
            _intimacyFill.rectTransform.anchorMax = new Vector2(y.Intimacy / (float)GameConstants.IntimacyMax, 1);

            string stageName = y.Stage switch
            {
                YokaiStage.Spirit => "넋",
                YokaiStage.Apparition => "괴",
                YokaiStage.Manifest => "혼",
                _ => "?"
            };
            _stageText.text = $"{y.DisplayName} · {stageName}";
            _yokaiLabel.text = y.Stage == YokaiStage.Spirit ? "넋 · 도깨비불" :
                y.Stage == YokaiStage.Apparition ? "괴 · 어린 토끼" : "혼 · 옥토끼";

            if (_blackened)
                _yokaiImage.color = BlackColor;
            else
                _yokaiImage.color = y.Stage switch
                {
                    YokaiStage.Spirit => SpiritColor,
                    YokaiStage.Apparition => ApparitionColor,
                    _ => ManifestColor
                };

            if (state.ScrollMode == ScrollMode.Training)
                _bg.color = BgTraining;
            else
                _bg.color = y.Stage switch
                {
                    YokaiStage.Spirit => BgSpirit,
                    YokaiStage.Apparition => BgApparition,
                    _ => BgManifest
                };

            _waterDrag?.SetCountLabel($"정화수 ×{state.Wallet.PurifiedWater}\n(드래그)");
            _waterDrag?.SetInteractable(_offerEnabled && state.Wallet.PurifiedWater > 0);
        }

        public void SetStepLabel(string text) => _stepText.text = text;
        public void ShowStatus(string text) => _statusText.text = text;

        public void ShowDialogue(DialogueLine line, int index, int total)
        {
            _dialogueRoot.SetActive(true);
            _choiceRoot.SetActive(false);
            _dialogueContinueHint.gameObject.SetActive(false);

            if (line.IsNarration)
                _dialogueSpeaker.text = $"나레이션  ({index}/{total})";
            else
                _dialogueSpeaker.text = $"{line.Speaker ?? ""}  ({index}/{total})";

            StopCoroutine(nameof(WatchTypewriterComplete));
            _typewriter.Play(line.Text ?? "");
            StartCoroutine(WatchTypewriterComplete());
        }

        public void HideDialogue()
        {
            _typewriter.Stop();
            _dialogueContinueHint.gameObject.SetActive(false);
            _dialogueRoot.SetActive(false);
        }

        void HandleDialogueTap()
        {
            // 타이핑 중: 전체 표시만. 이미 전체면 다음 대사.
            if (_typewriter != null && _typewriter.HandleTap())
            {
                _dialogueContinueHint.gameObject.SetActive(true);
                return;
            }

            OnDialogueContinue?.Invoke();
        }

        IEnumerator WatchTypewriterComplete()
        {
            while (_typewriter != null && _typewriter.IsTyping)
                yield return null;
            if (_dialogueRoot.activeSelf && _typewriter != null && _typewriter.IsComplete)
                _dialogueContinueHint.gameObject.SetActive(true);
        }

        public void ShowChoices(IReadOnlyList<ChoiceOption> choices)
        {
            _dialogueRoot.SetActive(false);
            _choiceRoot.SetActive(true);
            for (int i = _choiceContainer.childCount - 1; i >= 0; i--)
                Destroy(_choiceContainer.GetChild(i).gameObject);

            var title = CreateText(_choiceContainer, "ChoiceTitle", "선택", 30, TextAnchor.UpperCenter);
            SetAnchor(title.rectTransform, 0.1f, 0.75f, 0.9f, 0.9f, 0, 0, 0, 0);

            float y = 0.55f;
            foreach (var c in choices)
            {
                var captured = c.Id;
                var btn = CreateButton(_choiceContainer, c.Id, c.Label, () =>
                {
                    _choiceRoot.SetActive(false);
                    OnChoiceSelected?.Invoke(captured);
                });
                SetAnchor(btn.GetComponent<RectTransform>(), 0.1f, y - 0.12f, 0.9f, y, 0, 0, 0, 0);
                y -= 0.16f;
            }
        }

        public void SetYokaiInteractable(bool on) => _yokaiButton.interactable = on;

        public void SetOfferButtonVisible(bool on)
        {
            _offerEnabled = on;
            _waterSlotRoot.SetActive(on);
            _waterDrag?.SetInteractable(on);
        }

        public void SetOfferingHighlight(bool on)
        {
            _offerHighlight.SetActive(on);
            _waterDrag?.SetHighlight(on);
        }
        public void SetTrainingButtonVisible(bool on) => _trainingButton.gameObject.SetActive(on);
        public void SetThrowYutVisible(bool on) => _throwButton.gameObject.SetActive(on);
        public void SetLeaveTrainingVisible(bool on) => _leaveTrainingButton.gameObject.SetActive(on);
        public void EnterTrainingMode(bool on) => _trainingPanel.SetActive(on);
        public void ShowYutResult(string text) => _yutResultText.text = text;
        public void SetGlitchVisible(bool on) => _anims.PlayFireAndForget(on ? "glitch_on" : "glitch_off");

        public void SetYokaiBlackened(bool on)
        {
            _blackened = on;
            _yokaiLabel.text = on ? "흑토끼" : "혼 · 옥토끼";
            _anims.PlayFireAndForget(on ? "blacken" : "restore_white");
        }

        public void ShowCardComplete(CardFaceState card)
        {
            _cardPanel.SetActive(true);
            var detail = CreateText(_cardPanel.transform, "Detail",
                $"뒷면(흑토끼): {(card.BackUnlocked ? "해금" : "-")}\n앞면(백토끼 둘): {(card.FrontUnlocked ? "해금" : "-")}\n\n탭해서 계속",
                26, TextAnchor.MiddleCenter);
            SetAnchor(detail.rectTransform, 0.05f, 0.05f, 0.95f, 0.75f, 0, 0, 0, 0);
            var btn = _cardPanel.GetComponent<Button>() ?? _cardPanel.AddComponent<Button>();
            btn.targetGraphic = _cardPanel.GetComponent<Image>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                _cardPanel.SetActive(false);
                OnDialogueContinue?.Invoke();
            });
        }

        public void SetSummonPlaceholderVisible(bool on) => _summonPanel.SetActive(on);

        public void PlayShakeYokai() => _anims.PlayFireAndForget("offer_react");

        public IEnumerator PlayEvolutionFlash() => _anims.Play("evolve_flash");

        public void PulseEnergyBar(bool on) => _anims.SetLoop("energy_warning_pulse", on);

        public IEnumerator PlayAnim(string clipId) => _anims.Play(clipId);

        // --- UI helpers ---

        static Image CreateBar(Transform parent, string name, Color fill, out Text label, string title)
        {
            var root = CreatePanel(parent, name, new Color(1, 1, 1, 0.15f));
            var fillImg = CreateImage(root.transform, "Fill", fill);
            SetAnchor(fillImg.rectTransform, 0, 0, 0.5f, 1, 2, 2, -2, -2);
            fillImg.rectTransform.pivot = new Vector2(0, 0.5f);
            label = CreateText(root.transform, "Label", title, 20, TextAnchor.MiddleLeft);
            SetAnchor(label.rectTransform, 0, 0, 1, 1, 12, 0, 0, 0);
            label.raycastTarget = false;
            return fillImg;
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

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("AppleSDGothicNeo-Regular", size);
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = CreatePanel(parent, name, new Color(0.25f, 0.22f, 0.18f, 0.95f));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.35f, 0.28f);
            colors.pressedColor = new Color(0.18f, 0.16f, 0.12f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
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
