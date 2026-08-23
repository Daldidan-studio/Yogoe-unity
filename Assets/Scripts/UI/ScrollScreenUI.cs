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
    /// <summary>
    /// Boot 씬 Canvas 아래에 배치된 UI. Hierarchy에서 드래그로 배치·수정 가능.
    /// 최초 생성: 메뉴 KSpirits → Setup Boot Scene UI
    /// </summary>
    public class ScrollScreenUI : MonoBehaviour
    {
        public event Action OnYokaiTapped;
        public event Action OnOfferPurifiedWater;
        public event Action OnTrainingPressed;
        public event Action OnThrowYutPressed;
        public event Action OnLeaveTrainingPressed;
        public event Action<string> OnChoiceSelected;
        public event Action OnDialogueContinue;

        [SerializeField] Text _coinText;
        [SerializeField] Text _heartText;
        [SerializeField] Text _incenseText;
        [SerializeField] Text _statusText;
        [SerializeField] Text _stepText;
        [SerializeField] Text _dialogueSpeaker;
        [SerializeField] Text _dialogueBody;
        [SerializeField] Text _dialogueContinueHint;
        [SerializeField] Text _yutResultText;
        [SerializeField] Text _yokaiNameText;
        [SerializeField] Text _yokaiLabel;
        [SerializeField] Text[] _itemCountLabels;
        [SerializeField] Image _yokaiImage;
        [SerializeField] Image _energyPulseBar;
        [SerializeField] Image _skyBg;
        [SerializeField] Image _moonGround;
        [SerializeField] Image[] _energySegments;
        [SerializeField] Image[] _intimacySegments;
        [SerializeField] GameObject _dialogueRoot;
        [SerializeField] GameObject _choiceRoot;
        [SerializeField] GameObject _offerHighlight;
        [SerializeField] GameObject _trainingPanel;
        [SerializeField] GameObject _cardPanel;
        [SerializeField] GameObject _summonPanel;
        [SerializeField] GameObject _glitchOverlay;
        [SerializeField] GameObject _waterSlotRoot;
        [SerializeField] Button _trainingButton;
        [SerializeField] Button _throwButton;
        [SerializeField] Button _leaveTrainingButton;
        [SerializeField] Button _yokaiButton;
        [SerializeField] RectTransform _yokaiDropZone;
        [SerializeField] Transform _choiceContainer;

        DraggableOfferItem _waterDrag;
        DialogueTypewriter _typewriter;
        DialogueLayoutManager _dialogueLayout;
        UiAnimPlayer _anims;
        bool _blackened;
        bool _offerEnabled;
        bool _wired;

        static readonly Color ApparitionColor = new(1f, 0.85f, 0.7f);
        static readonly Color ManifestColor = new(0.95f, 0.95f, 0.9f);
        static readonly Color BlackColor = new(0.15f, 0.15f, 0.2f);
        static readonly Color SkyApparition = new(0.06f, 0.1f, 0.22f);
        static readonly Color SkyManifest = new(0.08f, 0.12f, 0.28f);
        static readonly Color SkyTraining = new(0.05f, 0.12f, 0.14f);

        void Awake()
        {
            EnsureWired();
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
                return;
            EnsureWired();
        }

        internal void BindHierarchy(
            Text coinText, Text heartText, Text incenseText, Text statusText, Text stepText,
            Text dialogueSpeaker, Text dialogueBody, Text dialogueContinueHint, Text yutResultText,
            Text yokaiNameText, Text yokaiLabel, Text[] itemCountLabels,
            Image yokaiImage, Image energyPulseBar, Image skyBg, Image moonGround,
            Image[] energySegments, Image[] intimacySegments,
            GameObject dialogueRoot, GameObject choiceRoot, GameObject offerHighlight,
            GameObject trainingPanel, GameObject cardPanel, GameObject summonPanel,
            GameObject glitchOverlay, GameObject waterSlotRoot, RectTransform yokaiDropZone,
            Transform choiceContainer,
            Button trainingButton, Button throwButton, Button leaveTrainingButton, Button yokaiButton)
        {
            _coinText = coinText;
            _heartText = heartText;
            _incenseText = incenseText;
            _statusText = statusText;
            _stepText = stepText;
            _dialogueSpeaker = dialogueSpeaker;
            _dialogueBody = dialogueBody;
            _dialogueContinueHint = dialogueContinueHint;
            _yutResultText = yutResultText;
            _yokaiNameText = yokaiNameText;
            _yokaiLabel = yokaiLabel;
            _itemCountLabels = itemCountLabels;
            _yokaiImage = yokaiImage;
            _energyPulseBar = energyPulseBar;
            _skyBg = skyBg;
            _moonGround = moonGround;
            _energySegments = energySegments;
            _intimacySegments = intimacySegments;
            _dialogueRoot = dialogueRoot;
            _choiceRoot = choiceRoot;
            _offerHighlight = offerHighlight;
            _trainingPanel = trainingPanel;
            _cardPanel = cardPanel;
            _summonPanel = summonPanel;
            _glitchOverlay = glitchOverlay;
            _waterSlotRoot = waterSlotRoot;
            _yokaiDropZone = yokaiDropZone;
            _choiceContainer = choiceContainer;
            _trainingButton = trainingButton;
            _throwButton = throwButton;
            _leaveTrainingButton = leaveTrainingButton;
            _yokaiButton = yokaiButton;
        }

        public void EnsureWired()
        {
            if (_coinText == null)
                TryAutoBindFromHierarchy();

            if (_coinText == null)
            {
                Debug.LogError("[ScrollScreenUI] UI 참조가 비어 있습니다. KSpirits → Setup Boot Scene UI 를 실행하세요.");
                return;
            }

            if (_wired) return;

            EnsureRuntimeComponents();
            WireEvents();
            ApplyFonts();
            _wired = true;
        }

        void ApplyFonts()
        {
            UIFont.Apply(_dialogueSpeaker, UIFontRole.Dialogue);
            UIFont.Apply(_dialogueBody, UIFontRole.Dialogue);
            UIFont.Apply(_dialogueContinueHint, UIFontRole.Dialogue);

            UIFont.Apply(_yokaiNameText, UIFontRole.UserInfo);
            UIFont.Apply(_yokaiLabel, UIFontRole.UserInfo);

            UIFont.Apply(_coinText, UIFontRole.HudNumeric);
            if (_itemCountLabels != null)
            {
                foreach (var label in _itemCountLabels)
                    UIFont.Apply(label, UIFontRole.HudNumeric);
            }
        }

        void TryAutoBindFromHierarchy()
        {
            _coinText = FindText("Header/CoinRow/Coins");
            _yokaiNameText = FindText("Header/YokaiName");
            _stepText = FindText("Step");
            _statusText = FindText("Status");
            _yokaiLabel = FindText("Scene/YokaiArea/YokaiLabel");
            _yutResultText = FindText("TrainingPanel/YutResult");
            _dialogueSpeaker = FindText("Dialogue/Speaker");
            _dialogueBody = FindText("Dialogue/Body");
            _dialogueContinueHint = FindText("Dialogue/ContinueHint");
            _heartText = FindText("Header/HeartsHidden");
            _incenseText = FindText("Header/IncenseHidden");

            _skyBg = FindImage("Sky");
            _moonGround = FindImage("MoonGround");
            _energyPulseBar = FindImage("Header/EnergyBar");
            _yokaiImage = FindImage("Scene/YokaiArea/YokaiButton");

            _energySegments = CollectSegments("Header/EnergyBar");
            _intimacySegments = CollectSegments("Header/IntimacyBar");

            _dialogueRoot = transform.Find("Dialogue")?.gameObject;
            _choiceRoot = transform.Find("Choices")?.gameObject;
            _offerHighlight = transform.Find("BottomDock/ItemBar/OfferHighlight")?.gameObject;
            _trainingPanel = transform.Find("TrainingPanel")?.gameObject;
            _cardPanel = transform.Find("CardPanel")?.gameObject;
            _summonPanel = transform.Find("Summon")?.gameObject;
            _glitchOverlay = transform.Find("Scene/YokaiArea/Glitch")?.gameObject;
            _waterSlotRoot = transform.Find("BottomDock/ItemBar/ItemSlot0")?.gameObject;
            _yokaiDropZone = transform.Find("Scene/YokaiArea") as RectTransform;
            _choiceContainer = transform.Find("Choices");

            _yokaiButton = transform.Find("Scene/YokaiArea/YokaiButton")?.GetComponent<Button>();
            _trainingButton = transform.Find("BottomDock/TrainingDock")?.GetComponent<Button>();
            _throwButton = transform.Find("TrainingPanel/Throw")?.GetComponent<Button>();
            _leaveTrainingButton = transform.Find("TrainingPanel/Leave")?.GetComponent<Button>();

            _itemCountLabels = new Text[4];
            for (int i = 0; i < 4; i++)
                _itemCountLabels[i] = FindText($"BottomDock/ItemBar/ItemSlot{i}/Badge/Count");
        }

        Text FindText(string path) => transform.Find(path)?.GetComponent<Text>();

        Image FindImage(string path) => transform.Find(path)?.GetComponent<Image>();

        Image[] CollectSegments(string barPath)
        {
            var bar = transform.Find(barPath);
            if (bar == null) return null;
            var list = new List<Image>();
            for (int i = 0; i < ScrollScreenUIBuilder.StatSegmentCount; i++)
            {
                var seg = bar.Find($"Seg{i}")?.GetComponent<Image>();
                if (seg != null) list.Add(seg);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        void EnsureRuntimeComponents()
        {
            if (_typewriter == null)
            {
                _typewriter = GetComponent<DialogueTypewriter>() ?? gameObject.AddComponent<DialogueTypewriter>();
                _typewriter.Bind(_dialogueBody);
                AnimCatalog.EnsureLoaded();
                var tw = AnimCatalog.Get("dialogue_typewriter");
                if (tw != null)
                    _typewriter.Configure(tw.charsPerSecond, tw.punctuationHold);
            }

            if (_anims == null)
            {
                _anims = GetComponent<UiAnimPlayer>() ?? gameObject.AddComponent<UiAnimPlayer>();
                _anims.YokaiRoot = _yokaiButton.GetComponent<RectTransform>();
                _anims.YokaiImage = _yokaiImage;
                _anims.EnergyFill = _energyPulseBar;
                _anims.GlitchOverlay = _glitchOverlay;
            }

            if (_dialogueLayout == null)
            {
                _dialogueLayout = GetComponent<DialogueLayoutManager>();
                if (_dialogueLayout == null)
                    _dialogueLayout = gameObject.AddComponent<DialogueLayoutManager>();

                var catalog = Resources.Load<DialogueLayoutCatalog>("Settings/DialogueLayoutCatalog");
                if (catalog != null)
                    _dialogueLayout.SetCatalog(catalog);
                _dialogueLayout.CacheAnchors();
            }

            if (_waterSlotRoot != null && _waterDrag == null)
            {
                _waterDrag = _waterSlotRoot.GetComponent<DraggableOfferItem>();
                if (_waterDrag == null)
                    _waterDrag = _waterSlotRoot.AddComponent<DraggableOfferItem>();
                var canvas = GetComponentInParent<Canvas>();
                _waterDrag.Setup(canvas, _yokaiDropZone);
            }
        }

        void WireEvents()
        {
            WireButton(_yokaiButton, () => OnYokaiTapped?.Invoke());
            WireButton(_trainingButton, () => OnTrainingPressed?.Invoke());
            WireButton(_throwButton, () => OnThrowYutPressed?.Invoke());
            WireButton(_leaveTrainingButton, () => OnLeaveTrainingPressed?.Invoke());

            if (_dialogueRoot != null)
            {
                var dialogueBtn = _dialogueRoot.GetComponent<Button>();
                if (dialogueBtn == null)
                {
                    dialogueBtn = _dialogueRoot.AddComponent<Button>();
                    dialogueBtn.targetGraphic = _dialogueRoot.GetComponent<Image>();
                }
                dialogueBtn.onClick.RemoveAllListeners();
                dialogueBtn.onClick.AddListener(HandleDialogueTap);
            }

            if (_waterDrag != null)
            {
                _waterDrag.OnDroppedOnYokai -= HandleWaterDropped;
                _waterDrag.OnDroppedOnYokai += HandleWaterDropped;
            }
        }

        void HandleWaterDropped()
        {
            if (_offerEnabled)
                OnOfferPurifiedWater?.Invoke();
        }

        static void WireButton(Button btn, Action action)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action?.Invoke());
        }

        public void RefreshAll(GameState state)
        {
            EnsureWired();
            _coinText.text = state.Wallet.Coins.ToString("N0");
            _heartText.text = state.Wallet.Hearts.ToString();
            _incenseText.text = state.Wallet.Incense.ToString();

            var y = state.FocusYokai;
            UpdateSegmentBar(_energySegments, y.Energy, GameConstants.EnergyMax,
                ScrollScreenUIBuilder.EnergyOn, ScrollScreenUIBuilder.EnergyOff);
            UpdateSegmentBar(_intimacySegments, y.Intimacy, GameConstants.IntimacyMax,
                ScrollScreenUIBuilder.IntimacyOn, ScrollScreenUIBuilder.IntimacyOff);

            string stageName = y.Stage switch
            {
                YokaiStage.Spirit => "넋",
                YokaiStage.Apparition => "괴",
                YokaiStage.Manifest => "혼",
                _ => "?"
            };
            _yokaiNameText.text = $"{y.DisplayName} | {stageName}";
            _yokaiLabel.text = y.Stage == YokaiStage.Spirit ? "넋 · 도깨비불" :
                y.Stage == YokaiStage.Apparition ? "괴 · 어린 토끼" : "혼 · 옥토끼";

            if (_blackened)
                _yokaiImage.color = BlackColor;
            else
                _yokaiImage.color = y.Stage switch
                {
                    YokaiStage.Spirit => ScrollScreenUIBuilder.SpiritColor,
                    YokaiStage.Apparition => ApparitionColor,
                    _ => ManifestColor
                };

            if (state.ScrollMode == ScrollMode.Training)
                _skyBg.color = SkyTraining;
            else
                _skyBg.color = y.Stage switch
                {
                    YokaiStage.Spirit => ScrollScreenUIBuilder.SkySpirit,
                    YokaiStage.Apparition => SkyApparition,
                    _ => SkyManifest
                };

            if (_itemCountLabels != null && _itemCountLabels.Length > 0)
                _itemCountLabels[0].text = $"×{state.Wallet.PurifiedWater}";

            _waterDrag?.SetCountLabel($"×{state.Wallet.PurifiedWater}");
            _waterDrag?.SetInteractable(_offerEnabled && state.Wallet.PurifiedWater > 0);
        }

        public void SetStepLabel(string text) => _stepText.text = text;
        public void ShowStatus(string text) => _statusText.text = text;

        public void ShowDialogue(DialogueLine line, int index, int total, string sectionId = null)
        {
            if (_dialogueRoot == null) return;

            _dialogueLayout?.ApplyForLine(line, sectionId, _dialogueRoot.transform as RectTransform);

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

        static void UpdateSegmentBar(Image[] segments, int value, int max, Color on, Color off)
        {
            if (segments == null) return;
            int filled = Mathf.Clamp(
                Mathf.CeilToInt(value / (max / (float)ScrollScreenUIBuilder.StatSegmentCount)),
                0, ScrollScreenUIBuilder.StatSegmentCount);
            for (int i = 0; i < segments.Length; i++)
                segments[i].color = i < filled ? on : off;
        }

        static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = UIFont.Default;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = anchor;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = CreatePanel(parent, name, new Color(0.25f, 0.22f, 0.18f, 0.95f));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            if (!string.IsNullOrEmpty(label))
            {
                var t = CreateText(go.transform, "Label", label, 26, TextAnchor.MiddleCenter);
                var rt = t.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                t.raycastTarget = false;
            }
            return btn;
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
