using System;
using System.Collections;
using KSpirits.Model;
using KSpirits.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KSpirits.Cards
{
    /// <summary>요괴패 카드 한 장을 보여줄 때 필요한 표시 문구. 카드 종류를 몰라도 되게 파라미터로 받는다.</summary>
    public readonly struct CardContent
    {
        public readonly string Title;
        public readonly string BackLabel;
        public readonly string FrontLabel;

        public CardContent(string title, string backLabel, string frontLabel)
        {
            Title = title;
            BackLabel = backLabel;
            FrontLabel = frontLabel;
        }
    }

    /// <summary>
    /// 요괴패 카드 확대 뷰어 — 튜토리얼 전용이 아니라 나중에 도감 화면에서도 그대로 재사용한다.
    /// 카드 GameObject에 런타임에 자동 부착된다 (ScrollScreenUI.EnsureCardViewer 참고).
    /// 좌우 드래그로 앞/뒷면을 넘기고, X로 닫으면 마지막 본 면이 CardFaceState.PreferBackView에
    /// 저장된다. 재생(▶)은 이 뷰어가 직접 재생하지 않고 이벤트만 쏴서, 어떤 대사를 재생할지는
    /// 호출부(TutorialController 등, OktoDialogue 등을 아는 쪽) 책임으로 남긴다.
    /// </summary>
    public class CardViewer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action OnClosed;
        public event Action<bool> OnReplayRequested;

        const float DragThreshold = 140f;

        Text _titleText;
        Text _faceText;
        RectTransform _faceContainer;

        CardFaceState _card;
        CardContent _content;
        bool _showingBack;
        bool _built;

        float _dragStartX;
        Coroutine _settleRoutine;

        public void Show(CardFaceState card, CardContent content)
        {
            EnsureBuilt();
            _card = card;
            _content = content;
            _showingBack = card.PreferBackView;
            RefreshFace();

            gameObject.SetActive(true);
            transform.localScale = Vector3.one * 0.6f;
            StopAllCoroutines();
            StartCoroutine(PopIn());
        }

        public void Hide() => gameObject.SetActive(false);

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _titleText = CreateText(transform, "Title", "", 30, TextAnchor.UpperCenter);
            SetAnchor(_titleText.rectTransform, 0.05f, 0.84f, 0.95f, 0.97f, 0, 0, 0, 0);

            var faceGo = new GameObject("Face", typeof(RectTransform));
            faceGo.transform.SetParent(transform, false);
            _faceContainer = faceGo.GetComponent<RectTransform>();
            SetAnchor(_faceContainer, 0.08f, 0.2f, 0.92f, 0.82f, 0, 0, 0, 0);

            _faceText = CreateText(_faceContainer, "FaceText", "", 26, TextAnchor.MiddleCenter);
            Stretch(_faceText.rectTransform);

            var closeBtn = CreateButton(transform, "Close", "X", HandleClosePressed);
            SetAnchor(closeBtn.GetComponent<RectTransform>(), 0.86f, 0.88f, 0.98f, 0.98f, 0, 0, 0, 0);

            var replayBtn = CreateButton(transform, "Replay", "다시보기", HandleReplayPressed);
            SetAnchor(replayBtn.GetComponent<RectTransform>(), 0.32f, 0.04f, 0.68f, 0.15f, 0, 0, 0, 0);
        }

        void RefreshFace()
        {
            bool unlocked = _showingBack ? _card.BackUnlocked : _card.FrontUnlocked;
            string label = _showingBack ? _content.BackLabel : _content.FrontLabel;
            _titleText.text = _content.Title;
            _faceText.text = unlocked
                ? $"【 {(_showingBack ? "뒷면" : "앞면")} 】\n{label}"
                : $"【 {(_showingBack ? "뒷면" : "앞면")} 】\n???";
        }

        void HandleClosePressed()
        {
            _card.PreferBackView = _showingBack;
            Hide();
            OnClosed?.Invoke();
        }

        void HandleReplayPressed() => OnReplayRequested?.Invoke(_showingBack);

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_settleRoutine != null) StopCoroutine(_settleRoutine);
            _dragStartX = eventData.position.x;
        }

        public void OnDrag(PointerEventData eventData)
        {
            float delta = eventData.position.x - _dragStartX;
            float progress = Mathf.Clamp(delta / DragThreshold, -1f, 1f);
            _faceContainer.localScale = new Vector3(1f - Mathf.Abs(progress) * 0.9f, 1f, 1f);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            float delta = eventData.position.x - _dragStartX;
            float progress = Mathf.Clamp(delta / DragThreshold, -1f, 1f);
            bool commit = Mathf.Abs(progress) >= 1f;
            _settleRoutine = StartCoroutine(SettleFlip(commit));
        }

        IEnumerator SettleFlip(bool commit)
        {
            const float duration = 0.12f;
            float t = 0f;
            float startScale = _faceContainer.localScale.x;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                _faceContainer.localScale = new Vector3(Mathf.Lerp(startScale, commit ? 0f : 1f, u), 1f, 1f);
                yield return null;
            }

            if (commit)
            {
                _showingBack = !_showingBack;
                RefreshFace();
                t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / duration);
                    _faceContainer.localScale = new Vector3(Mathf.Lerp(0f, 1f, u), 1f, 1f);
                    yield return null;
                }
            }

            _faceContainer.localScale = Vector3.one;
        }

        IEnumerator PopIn()
        {
            const float duration = 0.18f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - (1f - u) * (1f - u);
                transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, eased);
                yield return null;
            }
            transform.localScale = Vector3.one;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            UIFont.Apply(text, UIFontRole.Default);
            text.fontSize = size + 1;
            text.color = Color.white;
            text.alignment = anchor;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.22f, 0.18f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText(go.transform, "Label", label, 24, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
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
