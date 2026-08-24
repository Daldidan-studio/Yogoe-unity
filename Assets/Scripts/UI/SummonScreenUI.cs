using System;
using System.Collections;
using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;
using KSpirits.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.UI
{
    /// <summary>향 3개 소환 화면 — UI/UX 프로토타입.</summary>
    public class SummonScreenUI : MonoBehaviour
    {
        public event Action<YokaiInstance, SummonEntry> OnSummonConfirmed;
        public event Action OnClosed;

        GameObject _root;
        Text _titleText;
        Text _subtitleText;
        Text _incenseLabel;
        Text _hintText;
        Text _resultName;
        Text _resultFlavor;
        Image _altarGlow;
        Image _yokaiPreview;
        Image[] _incenseSlots;
        GameObject _resultPanel;
        Button _summonButton;
        Button _confirmButton;
        Button _closeButton;

        GameState _state;
        YokaiInstance _pendingYokai;
        SummonEntry _pendingEntry;
        bool _animating;
        Coroutine _animRoutine;

        static readonly Color IncenseFilled = new(1f, 0.78f, 0.38f);
        static readonly Color IncenseEmpty = new(0.22f, 0.2f, 0.18f, 0.85f);
        static readonly Color AltarIdle = new(0.18f, 0.14f, 0.28f, 0.95f);
        static readonly Color AltarGlow = new(0.55f, 0.42f, 0.85f, 0.55f);

        public void EnsureBuilt()
        {
            if (_root != null) return;
            BuildUi();
        }

        public void Show(GameState state)
        {
            EnsureBuilt();
            _state = state;
            _pendingYokai = null;
            _animating = false;

            gameObject.SetActive(true);
            _root.SetActive(true);
            _resultPanel.SetActive(false);
            _confirmButton.gameObject.SetActive(false);
            _summonButton.gameObject.SetActive(true);
            _yokaiPreview.color = new Color(1, 1, 1, 0);
            _altarGlow.color = AltarIdle;

            RefreshIncense();
            RefreshButtons();

            _subtitleText.text = state.TotalSummons == 0
                ? "옥토끼가 준 향으로 첫 요괴를 불러보세요"
                : "향을 태워 족자에 새 요괴를 불러옵니다";
            _hintText.text = SummonService.CanSummon(state)
                ? "제단 위로 향의 연기가 모이면 요괴가 나타납니다"
                : $"향이 부족합니다 (필요 {GameConstants.IncensePerSummon}개)";

            var firstSummon = state.TutorialFinished && state.TotalSummons == 0;
            _closeButton.gameObject.SetActive(!firstSummon);
        }

        public void Hide()
        {
            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }
            _animating = false;
            if (_root != null) _root.SetActive(false);
            gameObject.SetActive(false);
        }

        void RefreshIncense()
        {
            if (_state == null || _incenseSlots == null) return;

            int have = _state.Wallet.Incense;
            int need = GameConstants.IncensePerSummon;
            _incenseLabel.text = $"보유 향 {have} / 필요 {need}";

            for (int i = 0; i < _incenseSlots.Length; i++)
            {
                bool filled = i < Mathf.Min(have, need);
                _incenseSlots[i].color = filled ? IncenseFilled : IncenseEmpty;
            }
        }

        void RefreshButtons()
        {
            bool can = _state != null && SummonService.CanSummon(_state) && !_animating;
            _summonButton.interactable = can;
            var summonLabel = _summonButton.GetComponentInChildren<Text>();
            if (summonLabel != null)
            {
                summonLabel.text = can ? "소환하기" : "향 부족";
                UIFont.Apply(summonLabel, UIFontRole.Default);
            }
        }

        void OnSummonPressed()
        {
            if (_animating || _state == null) return;
            if (!SummonService.TrySummon(_state, out var yokai, out var entry)) return;

            _pendingYokai = yokai;
            _pendingEntry = entry;
            _animRoutine = StartCoroutine(SummonSequence(entry));
        }

        IEnumerator SummonSequence(SummonEntry entry)
        {
            _animating = true;
            RefreshButtons();
            _summonButton.gameObject.SetActive(true);
            _confirmButton.gameObject.SetActive(false);
            _resultPanel.SetActive(false);
            _hintText.text = "향이 타오릅니다…";

            for (int i = _incenseSlots.Length - 1; i >= 0; i--)
            {
                _incenseSlots[i].color = IncenseEmpty;
                yield return new WaitForSecondsRealtime(0.35f);
            }

            RefreshIncense();

            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.unscaledDeltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
                _altarGlow.color = Color.Lerp(AltarIdle, AltarGlow, pulse);
                yield return null;
            }

            _hintText.text = "…";
            yield return new WaitForSecondsRealtime(0.4f);

            _yokaiPreview.color = entry.Accent;
            _resultName.text = entry.DisplayName;
            _resultFlavor.text = entry.Flavor;
            _resultPanel.SetActive(true);
            _hintText.text = $"{entry.DisplayName}(이)가 나타났습니다!";

            _summonButton.gameObject.SetActive(false);
            _confirmButton.gameObject.SetActive(true);
            _animating = false;
        }

        void OnConfirmPressed()
        {
            if (_pendingYokai == null) return;
            OnSummonConfirmed?.Invoke(_pendingYokai, _pendingEntry);
            _pendingYokai = null;
            Hide();
        }

        void OnClosePressed()
        {
            if (_animating) return;
            OnClosed?.Invoke();
            Hide();
        }

        void BuildUi()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            StretchRect(transform as RectTransform);

            _root = CreatePanel(transform, "SummonRoot", new Color(0.05f, 0.04f, 0.08f, 0.97f));
            StretchRect(_root.GetComponent<RectTransform>());

            _titleText = CreateText(_root.transform, "Title", "요괴 소환", 40, TextAnchor.UpperCenter);
            SetAnchor(_titleText.rectTransform, 0.06f, 0.88f, 0.94f, 0.97f, 0, 0, 0, 0);

            _subtitleText = CreateText(_root.transform, "Subtitle", "", 22, TextAnchor.UpperCenter);
            SetAnchor(_subtitleText.rectTransform, 0.08f, 0.82f, 0.92f, 0.88f, 0, 0, 0, 0);
            _subtitleText.color = new Color(1f, 0.92f, 0.75f, 0.85f);

            var incenseRow = CreatePanel(_root.transform, "IncenseRow", new Color(0, 0, 0, 0)).transform;
            SetAnchor(incenseRow.GetComponent<RectTransform>(), 0.12f, 0.72f, 0.88f, 0.8f, 0, 0, 0, 0);
            _incenseSlots = new Image[GameConstants.IncensePerSummon];
            for (int i = 0; i < _incenseSlots.Length; i++)
            {
                float xmin = i / (float)_incenseSlots.Length;
                float xmax = (i + 1) / (float)_incenseSlots.Length;
                var slot = CreatePanel(incenseRow, $"Incense{i}", IncenseFilled);
                SetAnchor(slot.GetComponent<RectTransform>(), xmin + 0.04f, 0.1f, xmax - 0.04f, 0.9f, 0, 0, 0, 0);
                _incenseSlots[i] = slot.GetComponent<Image>();
            }

            _incenseLabel = CreateText(_root.transform, "IncenseLabel", "", 20, TextAnchor.MiddleCenter);
            SetAnchor(_incenseLabel.rectTransform, 0.1f, 0.67f, 0.9f, 0.72f, 0, 0, 0, 0);
            _incenseLabel.color = new Color(1f, 1f, 1f, 0.7f);

            var altarOuter = CreatePanel(_root.transform, "AltarOuter", new Color(0.85f, 0.72f, 0.35f, 0.9f));
            SetAnchor(altarOuter.GetComponent<RectTransform>(), 0.22f, 0.32f, 0.78f, 0.64f, 0, 0, 0, 0);
            _altarGlow = CreatePanel(altarOuter.transform, "AltarInner", AltarIdle).GetComponent<Image>();
            StretchRect(_altarGlow.rectTransform);

            _yokaiPreview = CreatePanel(altarOuter.transform, "YokaiPreview", Color.white).GetComponent<Image>();
            SetAnchor(_yokaiPreview.rectTransform, 0.18f, 0.15f, 0.82f, 0.88f, 0, 0, 0, 0);
            _yokaiPreview.color = new Color(1, 1, 1, 0);

            _resultPanel = CreatePanel(_root.transform, "Result", new Color(0.1f, 0.08f, 0.14f, 0.92f));
            SetAnchor(_resultPanel.GetComponent<RectTransform>(), 0.14f, 0.48f, 0.86f, 0.66f, 0, 0, 0, 0);
            _resultName = CreateText(_resultPanel.transform, "Name", "", 32, TextAnchor.UpperCenter);
            SetAnchor(_resultName.rectTransform, 0.05f, 0.55f, 0.95f, 0.95f, 0, 0, 0, 0);
            _resultFlavor = CreateText(_resultPanel.transform, "Flavor", "", 20, TextAnchor.MiddleCenter);
            SetAnchor(_resultFlavor.rectTransform, 0.08f, 0.1f, 0.92f, 0.5f, 0, 0, 0, 0);
            _resultFlavor.color = new Color(1f, 1f, 1f, 0.75f);
            _resultPanel.SetActive(false);

            _hintText = CreateText(_root.transform, "Hint", "", 22, TextAnchor.MiddleCenter);
            SetAnchor(_hintText.rectTransform, 0.08f, 0.24f, 0.92f, 0.3f, 0, 0, 0, 0);
            _hintText.color = new Color(1f, 0.95f, 0.82f, 0.9f);

            _summonButton = CreateButton(_root.transform, "SummonBtn", "소환하기", OnSummonPressed);
            SetAnchor(_summonButton.GetComponent<RectTransform>(), 0.18f, 0.1f, 0.82f, 0.2f, 0, 0, 0, 0);
            StylePrimaryButton(_summonButton, new Color(0.45f, 0.32f, 0.62f, 0.98f));

            _confirmButton = CreateButton(_root.transform, "ConfirmBtn", "육성 시작", OnConfirmPressed);
            SetAnchor(_confirmButton.GetComponent<RectTransform>(), 0.18f, 0.1f, 0.82f, 0.2f, 0, 0, 0, 0);
            StylePrimaryButton(_confirmButton, new Color(0.28f, 0.48f, 0.38f, 0.98f));
            _confirmButton.gameObject.SetActive(false);

            _closeButton = CreateButton(_root.transform, "CloseBtn", "닫기", OnClosePressed);
            SetAnchor(_closeButton.GetComponent<RectTransform>(), 0.35f, 0.02f, 0.65f, 0.08f, 0, 0, 0, 0);
            StylePrimaryButton(_closeButton, new Color(0.22f, 0.2f, 0.18f, 0.85f));

            gameObject.SetActive(false);
        }

        static void StylePrimaryButton(Button btn, Color bg)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = bg;
            var label = btn.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = 28;
                UIFont.Apply(label, UIFontRole.Default);
            }
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
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            UIFont.Apply(text, UIFontRole.Default);
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = CreatePanel(parent, name, new Color(0.25f, 0.22f, 0.18f, 0.95f));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var t = CreateText(go.transform, "Label", label, 26, TextAnchor.MiddleCenter);
            StretchRect(t.rectTransform);
            t.raycastTarget = false;
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

        static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
