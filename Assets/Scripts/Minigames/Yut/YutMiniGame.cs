using System;
using System.Collections;
using KSpirits.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.Minigames.Yut
{
    /// <summary>
    /// 윷놀이 화면(전체화면 TrainingPanel)을 소유하는 독립 모듈.
    /// TrainingPanel GameObject에 런타임에 자동 부착된다 (ScrollScreenUI.EnsureWired 참고).
    /// 결과 판정·재화 소모 같은 게임 로직은 호출부(TutorialController, 추후 본게임 컨트롤러)의
    /// 책임이고, 이 컴포넌트는 화면 표시와 입력 이벤트만 담당한다.
    /// </summary>
    public class YutMiniGame : MonoBehaviour
    {
        public event Action OnThrowPressed;
        public event Action OnLeavePressed;

        Text _resultText;
        Text _heartText;
        Button _throwButton;
        Button _leaveButton;
        RectTransform _boardRoot;
        RectTransform _piece;
        Image[] _pads;

        static readonly Color YutStickFront = new(0.92f, 0.88f, 0.78f);
        static readonly Color YutStickBack = new(0.35f, 0.3f, 0.26f);

        public void BindFromHierarchy()
        {
            _resultText = transform.Find("YutResult")?.GetComponent<Text>();
            _throwButton = transform.Find("Throw")?.GetComponent<Button>();
            _leaveButton = transform.Find("Leave")?.GetComponent<Button>();

            WireButton(_throwButton, () => OnThrowPressed?.Invoke());
            WireButton(_leaveButton, () => OnLeavePressed?.Invoke());
        }

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureBoard();
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetThrowVisible(bool on)
        {
            if (_throwButton == null) return;
            _throwButton.gameObject.SetActive(on);
            if (on) EnsureButtonLabel(_throwButton, "윷 던지기");
        }

        public void SetLeaveVisible(bool on)
        {
            if (_leaveButton == null) return;
            _leaveButton.gameObject.SetActive(on);
            if (on) EnsureButtonLabel(_leaveButton, "나가기");
        }

        public void ShowResult(string text)
        {
            if (_resultText != null)
                _resultText.text = text;
        }

        public void RefreshHearts(int hearts)
        {
            if (_heartText != null)
                _heartText.text = $"♥ 하트 {hearts}";
        }

        public void SetPieceIndex(int index)
        {
            EnsureBoard();
            if (_pads == null || _pads.Length == 0 || _piece == null) return;

            index = Mathf.Clamp(index, 0, _pads.Length - 1);
            for (int i = 0; i < _pads.Length; i++)
            {
                bool here = i == index;
                _pads[i].color = here
                    ? new Color(1f, 0.85f, 0.35f, 1f)
                    : i == 3
                        ? new Color(0.85f, 0.7f, 0.25f, 0.85f) // 엽전칸
                        : new Color(0.35f, 0.32f, 0.28f, 0.9f);
            }

            var pad = _pads[index].rectTransform;
            _piece.SetParent(pad, false);
            _piece.anchorMin = new Vector2(0.15f, 0.15f);
            _piece.anchorMax = new Vector2(0.85f, 0.85f);
            _piece.offsetMin = Vector2.zero;
            _piece.offsetMax = Vector2.zero;
        }

        void EnsureBoard()
        {
            if (_boardRoot != null) return;

            var existing = transform.Find("YutBoard");
            if (existing != null)
            {
                _boardRoot = existing as RectTransform;
            }
            else
            {
                var boardGo = new GameObject("YutBoard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                boardGo.transform.SetParent(transform, false);
                _boardRoot = boardGo.GetComponent<RectTransform>();
                SetAnchor(_boardRoot, 0.08f, 0.22f, 0.92f, 0.64f, 0, 0, 0, 0);
                boardGo.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.18f, 0.92f);
            }

            if (_resultText != null)
                SetAnchor(_resultText.rectTransform, 0.1f, 0.68f, 0.9f, 0.82f, 0, 0, 0, 0);

            if (_heartText == null)
            {
                _heartText = CreateText(transform, "TrainingHearts", "", 26, TextAnchor.MiddleCenter);
                SetAnchor(_heartText.rectTransform, 0.25f, 0.82f, 0.75f, 0.88f, 0, 0, 0, 0);
                _heartText.color = new Color(1f, 0.4f, 0.45f);
                UIFont.Apply(_heartText, UIFontRole.Default);
            }

            _pads = new Image[8];
            string[] labels = { "출", "·", "·", "엽", "·", "·", "·", "골" };
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float x = Mathf.Lerp(0.06f, 0.82f, t);
                var padGo = new GameObject($"Pad{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                padGo.transform.SetParent(_boardRoot, false);
                var padRt = padGo.GetComponent<RectTransform>();
                SetAnchor(padRt, x, 0.28f, x + 0.12f, 0.72f, 0, 0, 0, 0);
                var padImg = padGo.GetComponent<Image>();
                padImg.color = i == 3
                    ? new Color(0.85f, 0.7f, 0.25f, 0.85f)
                    : new Color(0.35f, 0.32f, 0.28f, 0.9f);
                _pads[i] = padImg;

                var label = CreateText(_boardRoot, $"PadLabel{i}", labels[i], 18, TextAnchor.MiddleCenter);
                SetAnchor(label.rectTransform, x, 0.05f, x + 0.12f, 0.28f, 0, 0, 0, 0);
                label.raycastTarget = false;
                label.color = new Color(1f, 1f, 1f, 0.7f);
            }

            var pieceGo = new GameObject("YutPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pieceGo.transform.SetParent(_pads[0].transform, false);
            _piece = pieceGo.GetComponent<RectTransform>();
            _piece.anchorMin = new Vector2(0.15f, 0.15f);
            _piece.anchorMax = new Vector2(0.85f, 0.85f);
            _piece.offsetMin = Vector2.zero;
            _piece.offsetMax = Vector2.zero;
            pieceGo.GetComponent<Image>().color = new Color(0.95f, 0.9f, 0.85f, 1f);
        }

        /// <summary>
        /// 윷가락 4개를 던져서 흩뿌리는 연출. 결과 판정과는 무관한 순수 시각 효과.
        /// </summary>
        public IEnumerator PlayThrowAnim()
        {
            EnsureBoard();

            var panelRect = ((RectTransform)transform).rect;
            Vector2 ToLocal(Vector2 norm) =>
                new((norm.x - 0.5f) * panelRect.width, (norm.y - 0.5f) * panelRect.height);

            var origin = new Vector2(0.5f, 0.19f);
            var sticks = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"YutStick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(16f, 90f);
                rt.anchoredPosition = ToLocal(origin);
                go.GetComponent<Image>().color = YutStickFront;
                go.transform.SetAsLastSibling();
                sticks[i] = rt;
            }

            var routines = new Coroutine[4];
            for (int i = 0; i < 4; i++)
                routines[i] = StartCoroutine(ThrowOneStick(sticks[i], origin, ToLocal, i * 0.05f));
            for (int i = 0; i < 4; i++)
                yield return routines[i];

            yield return new WaitForSecondsRealtime(0.35f);

            foreach (var rt in sticks)
                if (rt != null) Destroy(rt.gameObject);
        }

        IEnumerator ThrowOneStick(RectTransform rt, Vector2 originNorm, Func<Vector2, Vector2> toLocal, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            var landNorm = new Vector2(
                UnityEngine.Random.Range(0.28f, 0.72f),
                UnityEngine.Random.Range(0.32f, 0.55f));
            float arcHeight = UnityEngine.Random.Range(160f, 260f);
            float spin = UnityEngine.Random.Range(720f, 1260f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            float duration = UnityEngine.Random.Range(0.45f, 0.6f);

            Vector2 start = toLocal(originNorm);
            Vector2 end = toLocal(landNorm);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eu = 1f - (1f - u) * (1f - u);
                var pos = Vector2.Lerp(start, end, eu);
                pos.y += arcHeight * 4f * u * (1f - u);
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, spin, u));
                yield return null;
            }
            rt.anchoredPosition = end;

            bool front = UnityEngine.Random.value < 0.5f;
            var img = rt.GetComponent<Image>();
            const float flipDuration = 0.12f;
            float flipT = 0f;
            while (flipT < flipDuration)
            {
                flipT += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(flipT / flipDuration);
                rt.localScale = new Vector3(Mathf.Abs(Mathf.Cos(u * Mathf.PI)), 1f, 1f);
                if (u >= 0.5f)
                    img.color = front ? YutStickFront : YutStickBack;
                yield return null;
            }
            rt.localScale = Vector3.one;

            const float settleDuration = 0.18f;
            float settleT = 0f;
            Vector2 settled = rt.anchoredPosition;
            while (settleT < settleDuration)
            {
                settleT += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(settleT / settleDuration);
                float bounce = Mathf.Sin(u * Mathf.PI) * 10f * (1f - u);
                rt.anchoredPosition = settled + new Vector2(0, bounce);
                yield return null;
            }
            rt.anchoredPosition = settled;
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

        static void WireButton(Button btn, Action action)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action?.Invoke());
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
