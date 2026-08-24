using System;
using System.Collections;
using System.Collections.Generic;
using KSpirits.Animation;
using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;
using UnityEngine;
using UnityEngine.EventSystems;
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
        SummonScreenUI _summonScreen;
        bool _blackened;
        bool _offerEnabled;
        bool _wired;
        bool _dialogueHeld;
        bool _holdAdvancedAny;
        Coroutine _dialogueHoldRoutine;
        RectTransform _yutBoardRoot;
        RectTransform _yutPiece;
        Image[] _yutPads;
        Color _trainingButtonBaseColor = new(0f, 0f, 0f, 0f);
        GameObject _storyOverlay;
        Text _storyOverlayTitle;
        GameObject _fxOverlay;
        Text _fxOverlayLabel;
        GameObject _doppelImage;
        Vector2 _yokaiHomeAnchored;
        bool _yokaiHomeCached;
        bool _cardShowingBack = true;

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

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                HandleDialoguePointerUp();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                HandleDialoguePointerUp();
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
            UIFont.Apply(_statusText, UIFontRole.Default);

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

                var trigger = _dialogueRoot.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = _dialogueRoot.AddComponent<EventTrigger>();
                trigger.triggers.Clear();
                AddTrigger(trigger, EventTriggerType.PointerDown, HandleDialoguePointerDown);
                AddTrigger(trigger, EventTriggerType.PointerUp, HandleDialoguePointerUp);
                AddTrigger(trigger, EventTriggerType.PointerExit, HandleDialoguePointerUp);
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

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
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

            // 스토리 오버레이보다 대사를 위에 두고 클릭 가능하게
            if (_storyOverlay != null && _storyOverlay.activeSelf)
                _dialogueRoot.transform.SetAsLastSibling();

            _dialogueRoot.SetActive(true);
            _choiceRoot.SetActive(false);
            _dialogueContinueHint.gameObject.SetActive(false);

            if (line.IsNarration)
                _dialogueSpeaker.text = $"나레이션  ({index}/{total})";
            else
                _dialogueSpeaker.text = $"{line.Speaker ?? ""}  ({index}/{total})";

            if (!string.IsNullOrEmpty(line.Fx))
                _anims?.PlayFireAndForget(line.Fx);

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
            if (_holdAdvancedAny)
            {
                _holdAdvancedAny = false;
                return;
            }

            if (_typewriter != null && _typewriter.HandleTap())
            {
                _dialogueContinueHint.gameObject.SetActive(true);
                return;
            }

            OnDialogueContinue?.Invoke();
        }

        void HandleDialoguePointerDown()
        {
            _dialogueHeld = true;
            _holdAdvancedAny = false;
            _typewriter?.SetFastForward(true);
            if (_dialogueHoldRoutine == null)
                _dialogueHoldRoutine = StartCoroutine(AutoAdvanceWhileHeld());
        }

        void HandleDialoguePointerUp()
        {
            _dialogueHeld = false;
            _typewriter?.SetFastForward(false);
        }

        /// <summary>꾹 누르고 있는 동안: 타이핑 3배속 + 완료된 줄은 자동으로 다음 대사까지 진행.</summary>
        IEnumerator AutoAdvanceWhileHeld()
        {
            while (_dialogueHeld)
            {
                yield return null;
                if (_dialogueRoot != null && _dialogueRoot.activeSelf &&
                    _typewriter != null && _typewriter.IsComplete && !_typewriter.IsTyping)
                {
                    _holdAdvancedAny = true;
                    OnDialogueContinue?.Invoke();
                    yield return null; // 다음 줄이 Play()로 갱신될 시간을 한 프레임 확보
                }
            }
            _dialogueHoldRoutine = null;
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

        public void SetTrainingButtonVisible(bool on)
        {
            if (_trainingButton == null) return;
            _trainingButton.gameObject.SetActive(on);
            if (on) EnsureTrainingDockLabel();
        }

        public void SetTrainingHighlight(bool on)
        {
            if (_trainingButton == null) return;
            EnsureTrainingDockLabel();
            var img = _trainingButton.GetComponent<Image>();
            if (img == null) return;
            if (!on)
            {
                img.color = _trainingButtonBaseColor;
                return;
            }

            _trainingButtonBaseColor = img.color.a < 0.01f
                ? new Color(0.2f, 0.18f, 0.14f, 0.85f)
                : img.color;
            img.color = new Color(1f, 0.85f, 0.25f, 0.95f);
        }

        void EnsureTrainingDockLabel()
        {
            if (_trainingButton == null) return;

            // 아이콘 숨기고 라벨을 「수련장」으로 크게 표시 (폰트 미적용 시 박스만 보이던 문제 방지)
            var icon = _trainingButton.transform.Find("Icon");
            if (icon != null)
                icon.gameObject.SetActive(false);

            var labelTf = _trainingButton.transform.Find("Label");
            Text label = labelTf != null ? labelTf.GetComponent<Text>() : null;
            if (label == null)
                label = _trainingButton.GetComponentInChildren<Text>(true);

            if (label == null)
            {
                label = CreateText(_trainingButton.transform, "Label", "수련장", 26, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.raycastTarget = false;
            }
            else
            {
                var rt = label.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            label.text = "수련장";
            UIFont.Apply(label, UIFontRole.Default);
            label.fontSize = 26;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.gameObject.SetActive(true);
        }

        public void SetThrowYutVisible(bool on)
        {
            if (_throwButton == null) return;
            _throwButton.gameObject.SetActive(on);
            if (on) EnsureButtonLabel(_throwButton, "윷 던지기");
        }

        public void SetLeaveTrainingVisible(bool on)
        {
            if (_leaveTrainingButton == null) return;
            _leaveTrainingButton.gameObject.SetActive(on);
            if (on) EnsureButtonLabel(_leaveTrainingButton, "나가기");
        }

        public void EnterTrainingMode(bool on)
        {
            if (_trainingPanel == null) return;
            _trainingPanel.SetActive(on);
            if (on) EnsureYutBoard();
        }

        public void ShowYutResult(string text)
        {
            if (_yutResultText != null)
                _yutResultText.text = text;
        }

        public void SetYutPieceIndex(int index)
        {
            EnsureYutBoard();
            if (_yutPads == null || _yutPads.Length == 0 || _yutPiece == null) return;

            index = Mathf.Clamp(index, 0, _yutPads.Length - 1);
            for (int i = 0; i < _yutPads.Length; i++)
            {
                bool here = i == index;
                _yutPads[i].color = here
                    ? new Color(1f, 0.85f, 0.35f, 1f)
                    : i == 3
                        ? new Color(0.85f, 0.7f, 0.25f, 0.85f) // 엽전칸
                        : new Color(0.35f, 0.32f, 0.28f, 0.9f);
            }

            var pad = _yutPads[index].rectTransform;
            _yutPiece.SetParent(pad, false);
            _yutPiece.anchorMin = new Vector2(0.15f, 0.15f);
            _yutPiece.anchorMax = new Vector2(0.85f, 0.85f);
            _yutPiece.offsetMin = Vector2.zero;
            _yutPiece.offsetMax = Vector2.zero;
        }

        void EnsureYutBoard()
        {
            if (_trainingPanel == null) return;
            if (_yutBoardRoot != null) return;

            var existing = _trainingPanel.transform.Find("YutBoard");
            if (existing != null)
            {
                _yutBoardRoot = existing as RectTransform;
            }
            else
            {
                var boardGo = new GameObject("YutBoard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                boardGo.transform.SetParent(_trainingPanel.transform, false);
                _yutBoardRoot = boardGo.GetComponent<RectTransform>();
                SetAnchor(_yutBoardRoot, 0.08f, 0.32f, 0.92f, 0.78f, 0, 0, 0, 0);
                boardGo.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.18f, 0.92f);
            }

            if (_yutResultText != null)
                SetAnchor(_yutResultText.rectTransform, 0.1f, 0.78f, 0.9f, 0.92f, 0, 0, 0, 0);

            _yutPads = new Image[8];
            string[] labels = { "출", "·", "·", "엽", "·", "·", "·", "골" };
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float x = Mathf.Lerp(0.06f, 0.82f, t);
                var padGo = new GameObject($"Pad{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                padGo.transform.SetParent(_yutBoardRoot, false);
                var padRt = padGo.GetComponent<RectTransform>();
                SetAnchor(padRt, x, 0.28f, x + 0.12f, 0.72f, 0, 0, 0, 0);
                var padImg = padGo.GetComponent<Image>();
                padImg.color = i == 3
                    ? new Color(0.85f, 0.7f, 0.25f, 0.85f)
                    : new Color(0.35f, 0.32f, 0.28f, 0.9f);
                _yutPads[i] = padImg;

                var label = CreateText(_yutBoardRoot, $"PadLabel{i}", labels[i], 18, TextAnchor.MiddleCenter);
                SetAnchor(label.rectTransform, x, 0.05f, x + 0.12f, 0.28f, 0, 0, 0, 0);
                label.raycastTarget = false;
                label.color = new Color(1f, 1f, 1f, 0.7f);
            }

            var pieceGo = new GameObject("YutPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pieceGo.transform.SetParent(_yutPads[0].transform, false);
            _yutPiece = pieceGo.GetComponent<RectTransform>();
            _yutPiece.anchorMin = new Vector2(0.15f, 0.15f);
            _yutPiece.anchorMax = new Vector2(0.85f, 0.85f);
            _yutPiece.offsetMin = Vector2.zero;
            _yutPiece.offsetMax = Vector2.zero;
            pieceGo.GetComponent<Image>().color = new Color(0.95f, 0.9f, 0.85f, 1f);
        }
        public void SetGlitchVisible(bool on)
        {
            _anims.PlayFireAndForget(on ? "glitch_on" : "glitch_off");
            if (_glitchOverlay != null)
                _glitchOverlay.SetActive(on);
        }

        public void SetYokaiBlackened(bool on)
        {
            _blackened = on;
            _yokaiLabel.text = on ? "흑토끼" : "혼 · 옥토끼";
            _anims.PlayFireAndForget(on ? "blacken" : "restore_white");
            if (_yokaiImage != null)
                _yokaiImage.color = on ? BlackColor : ManifestColor;
        }

        public void HighlightItemBar(bool on)
        {
            if (_waterSlotRoot == null) return;
            var img = _waterSlotRoot.GetComponent<Image>();
            if (img == null) return;
            img.color = on
                ? new Color(1f, 0.9f, 0.3f, 1f)
                : new Color(0.35f, 0.65f, 0.95f, 1f);
        }

        public void ShowStoryOverlay(string title, Color bg)
        {
            EnsureStoryOverlay();
            _storyOverlay.SetActive(true);
            var img = _storyOverlay.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = false; // 대사 클릭이 막히지 않게
            if (_storyOverlayTitle != null)
            {
                _storyOverlayTitle.text = title;
                _storyOverlayTitle.raycastTarget = false;
            }
            // 배경은 대사 뒤에 두고, 제목만 상단에
            if (_dialogueRoot != null)
                _storyOverlay.transform.SetSiblingIndex(_dialogueRoot.transform.GetSiblingIndex());
        }

        public void HideStoryOverlay()
        {
            if (_storyOverlay != null)
                _storyOverlay.SetActive(false);
        }

        public IEnumerator PlayYokaiFlee(float duration = 0.85f)
        {
            if (_yokaiButton == null) yield break;
            CacheYokaiHome();
            var rt = _yokaiButton.GetComponent<RectTransform>();
            var start = rt.anchoredPosition;
            var end = start + new Vector2(900f, 120f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, u);
                if (_yokaiImage != null)
                {
                    var c = _yokaiImage.color;
                    c.a = 1f - u;
                    _yokaiImage.color = c;
                }
                yield return null;
            }
            _yokaiButton.gameObject.SetActive(false);
            if (_yokaiLabel != null)
                _yokaiLabel.text = "빈 족자";
        }

        public void RestoreYokaiOnScroll()
        {
            if (_yokaiButton == null) return;
            CacheYokaiHome();
            _yokaiButton.gameObject.SetActive(true);
            var rt = _yokaiButton.GetComponent<RectTransform>();
            rt.anchoredPosition = _yokaiHomeAnchored;
            if (_yokaiImage != null)
            {
                var c = _yokaiImage.color;
                c.a = 1f;
                _yokaiImage.color = c;
            }
        }

        public IEnumerator PlayImugiCapture(float duration = 1.1f)
        {
            EnsureFxOverlay();
            _fxOverlay.SetActive(true);
            _fxOverlay.GetComponent<Image>().color = new Color(0.55f, 0.75f, 0.65f, 0.35f);
            if (_fxOverlayLabel != null)
                _fxOverlayLabel.text = "이무기 · 곰방대 연기";
            yield return new WaitForSecondsRealtime(duration * 0.55f);
            if (_fxOverlayLabel != null)
                _fxOverlayLabel.text = "탁기를 삼킨다…";
            yield return new WaitForSecondsRealtime(duration * 0.45f);
            _fxOverlay.SetActive(false);
        }

        public void ShowDoppelganger(bool on)
        {
            if (_yokaiImage == null) return;
            if (!on)
            {
                if (_doppelImage != null)
                    _doppelImage.SetActive(false);
                return;
            }

            if (_doppelImage == null)
            {
                var go = new GameObject("Doppelganger", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_yokaiImage.transform.parent, false);
                var rt = go.GetComponent<RectTransform>();
                var src = _yokaiImage.rectTransform;
                rt.anchorMin = src.anchorMin;
                rt.anchorMax = src.anchorMax;
                rt.pivot = src.pivot;
                rt.sizeDelta = src.sizeDelta;
                rt.anchoredPosition = src.anchoredPosition + new Vector2(140f, 0f);
                var img = go.GetComponent<Image>();
                img.color = new Color(0.92f, 0.92f, 0.95f, 0.85f);
                var label = CreateText(go.transform, "Label", "도플", 20, TextAnchor.LowerCenter);
                Stretch(label.rectTransform);
                label.raycastTarget = false;
                _doppelImage = go;
            }
            _doppelImage.SetActive(true);
        }

        public void ShowCardComplete(CardFaceState card)
        {
            _cardPanel.SetActive(true);
            for (int i = _cardPanel.transform.childCount - 1; i >= 0; i--)
                Destroy(_cardPanel.transform.GetChild(i).gameObject);

            _cardShowingBack = true;
            var title = CreateText(_cardPanel.transform, "CardTitle", "옥토끼 요괴패", 32, TextAnchor.UpperCenter);
            SetAnchor(title.rectTransform, 0.05f, 0.82f, 0.95f, 0.98f, 0, 0, 0, 0);

            var face = CreateText(_cardPanel.transform, "Face", "", 26, TextAnchor.MiddleCenter);
            SetAnchor(face.rectTransform, 0.08f, 0.28f, 0.92f, 0.78f, 0, 0, 0, 0);
            void RefreshFace()
            {
                if (_cardShowingBack)
                    face.text = card.BackUnlocked
                        ? "【 뒷면 】\n흑토끼\n\n(탭하여 뒤집기)"
                        : "【 뒷면 】\n???\n\n(탭하여 뒤집기)";
                else
                    face.text = card.FrontUnlocked
                        ? "【 앞면 】\n백토끼 둘\n\n(탭하여 뒤집기 / 길게 보면 계속)"
                        : "【 앞면 】\n???\n\n(탭하여 뒤집기)";
            }
            RefreshFace();

            var hint = CreateText(_cardPanel.transform, "Hint", "앞뒤를 확인한 뒤 다시 탭하면 계속", 20, TextAnchor.LowerCenter);
            SetAnchor(hint.rectTransform, 0.05f, 0.04f, 0.95f, 0.22f, 0, 0, 0, 0);
            hint.color = new Color(1f, 1f, 1f, 0.65f);

            int taps = 0;
            var btn = _cardPanel.GetComponent<Button>() ?? _cardPanel.AddComponent<Button>();
            btn.targetGraphic = _cardPanel.GetComponent<Image>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                taps++;
                if (taps == 1)
                {
                    _cardShowingBack = false;
                    RefreshFace();
                    return;
                }
                _cardPanel.SetActive(false);
                OnDialogueContinue?.Invoke();
            });
        }

        public SummonScreenUI EnsureSummonScreen()
        {
            EnsureWired();
            if (_summonScreen != null) return _summonScreen;

            if (_summonPanel == null)
            {
                _summonPanel = CreatePanel(transform, "Summon", new Color(0, 0, 0, 0));
                Stretch(_summonPanel.GetComponent<RectTransform>());
            }

            Stretch(_summonPanel.GetComponent<RectTransform>());
            _summonScreen = _summonPanel.GetComponent<SummonScreenUI>();
            if (_summonScreen == null)
                _summonScreen = _summonPanel.AddComponent<SummonScreenUI>();
            _summonScreen.EnsureBuilt();
            return _summonScreen;
        }

        public void ShowSummonScreen(GameState state)
        {
            EnsureSummonScreen().Show(state);
        }

        public void HideSummonScreen() => _summonScreen?.Hide();

        public void PlayShakeYokai() => _anims.PlayFireAndForget("offer_react");
        public IEnumerator PlayEvolutionFlash() => _anims.Play("evolve_flash");
        public void PulseEnergyBar(bool on) => _anims.SetLoop("energy_warning_pulse", on);
        public IEnumerator PlayAnim(string clipId) => _anims.Play(clipId);

        void CacheYokaiHome()
        {
            if (_yokaiHomeCached || _yokaiButton == null) return;
            _yokaiHomeAnchored = _yokaiButton.GetComponent<RectTransform>().anchoredPosition;
            _yokaiHomeCached = true;
        }

        void EnsureStoryOverlay()
        {
            if (_storyOverlay != null) return;
            _storyOverlay = CreatePanel(transform, "StoryOverlay", new Color(0.02f, 0.04f, 0.1f, 0.92f));
            Stretch(_storyOverlay.GetComponent<RectTransform>());
            _storyOverlay.GetComponent<Image>().raycastTarget = false;
            _storyOverlayTitle = CreateText(_storyOverlay.transform, "Title", "", 36, TextAnchor.UpperCenter);
            SetAnchor(_storyOverlayTitle.rectTransform, 0.05f, 0.78f, 0.95f, 0.92f, 0, 0, 0, 0);
            _storyOverlayTitle.raycastTarget = false;
            _storyOverlay.SetActive(false);
        }

        static void EnsureButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<Text>(true);
            if (text == null)
            {
                text = CreateText(button.transform, "Label", label, 28, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
                text.raycastTarget = false;
            }
            text.text = label;
            UIFont.Apply(text, UIFontRole.Default);
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
        }

        void EnsureFxOverlay()
        {
            if (_fxOverlay != null) return;
            _fxOverlay = CreatePanel(transform, "FxOverlay", new Color(0.4f, 0.6f, 0.5f, 0.4f));
            Stretch(_fxOverlay.GetComponent<RectTransform>());
            _fxOverlay.transform.SetAsLastSibling();
            _fxOverlayLabel = CreateText(_fxOverlay.transform, "Label", "", 28, TextAnchor.MiddleCenter);
            Stretch(_fxOverlayLabel.rectTransform);
            _fxOverlay.SetActive(false);
        }

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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
