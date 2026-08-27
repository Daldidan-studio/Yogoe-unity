using System.Collections;
using System.Collections.Generic;
using KSpirits.Animation;
using KSpirits.Data;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.UI
{
    /// <summary>
    /// 2인 대화 전용 대화창(옛날 JRPG처럼 위/아래 고정 바 + 초상화). 지금 말하는 화자가
    /// 배정된 쪽 바만 활성화해서 보여주고, 나머지 한쪽은 숨긴다. 나레이션(화자 없음) 줄은
    /// 가운데 작은 배너로 보여준다.
    ///
    /// 특정 캐릭터/장면에 종속되지 않는 독립 모듈 — Configure()로 화자 이름·색만 새로 넣으면
    /// 다른 두 캐릭터 대화에도 그대로 재사용할 수 있다. 기존 ScrollScreenUI의 단일 대사창
    /// (ShowDialogue/PlayLines)과는 별개로, 필요한 장면에서만 이걸 대신 쓴다.
    /// 다른 모듈들(YutMiniGame/CardViewer)과 같은 패턴으로, 기존 GameObject에 런타임
    /// AddComponent로 붙여서 쓴다 — 씬/프리팹 수정 불필요.
    /// </summary>
    public class TwoSpeakerDialogueBox : MonoBehaviour
    {
        public readonly struct Speaker
        {
            public readonly string Name;
            public readonly Color PortraitColor;

            public Speaker(string name, Color portraitColor)
            {
                Name = name;
                PortraitColor = portraitColor;
            }
        }

        Speaker _top;
        Speaker _bottom;

        GameObject _topBar;
        GameObject _bottomBar;
        GameObject _narrationBar;
        Text _topName, _topBody;
        Text _bottomName, _bottomBody;
        Text _narrationText;
        Image _topPortrait, _bottomPortrait;
        DialogueTypewriter _typewriter;
        bool _built;
        bool _waitingAdvance;

        /// <summary>위쪽 바에 나올 화자, 아래쪽 바에 나올 화자를 지정한다.</summary>
        public void Configure(Speaker top, Speaker bottom)
        {
            EnsureBuilt();
            _top = top;
            _bottom = bottom;
            _topName.text = top.Name;
            _bottomName.text = bottom.Name;
            _topPortrait.color = top.PortraitColor;
            _bottomPortrait.color = bottom.PortraitColor;
        }

        public void Hide()
        {
            if (_topBar != null) _topBar.SetActive(false);
            if (_bottomBar != null) _bottomBar.SetActive(false);
            if (_narrationBar != null) _narrationBar.SetActive(false);
        }

        /// <summary>lines를 한 줄씩, 탭할 때마다 다음으로 넘기며 재생한다. 다 끝나면 자동으로 숨긴다.</summary>
        public IEnumerator PlayLines(IReadOnlyList<DialogueLine> lines)
        {
            EnsureBuilt();
            foreach (var line in lines)
            {
                Text body;
                if (line.Speaker == _top.Name)
                {
                    _topBar.SetActive(true);
                    _bottomBar.SetActive(false);
                    _narrationBar.SetActive(false);
                    body = _topBody;
                }
                else if (line.Speaker == _bottom.Name)
                {
                    _topBar.SetActive(false);
                    _bottomBar.SetActive(true);
                    _narrationBar.SetActive(false);
                    body = _bottomBody;
                }
                else
                {
                    _topBar.SetActive(false);
                    _bottomBar.SetActive(false);
                    _narrationBar.SetActive(true);
                    body = _narrationText;
                }

                _typewriter.Bind(body);
                _typewriter.Play(line.Text ?? "");

                _waitingAdvance = true;
                while (_waitingAdvance) yield return null;
            }

            Hide();
        }

        void HandleAdvance() => _waitingAdvance = false;

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _typewriter = gameObject.AddComponent<DialogueTypewriter>();

            _topBar = BuildBar("TopSpeakerBar", 0.8f, 0.98f, portraitOnLeft: true,
                out _topPortrait, out _topName, out _topBody);
            _bottomBar = BuildBar("BottomSpeakerBar", 0.02f, 0.2f, portraitOnLeft: false,
                out _bottomPortrait, out _bottomName, out _bottomBody);

            _narrationBar = new GameObject("NarrationBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _narrationBar.transform.SetParent(transform, false);
            SetAnchor(_narrationBar.GetComponent<RectTransform>(), 0.12f, 0.42f, 0.88f, 0.58f, 0, 0, 0, 0);
            _narrationBar.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            _narrationBar.AddComponent<DialogueAdvanceInput>().Bind(_typewriter, HandleAdvance);
            _narrationText = CreateText(_narrationBar.transform, "Text", "", 24, TextAnchor.MiddleCenter);
            Stretch(_narrationText.rectTransform);

            _topBar.SetActive(false);
            _bottomBar.SetActive(false);
            _narrationBar.SetActive(false);
        }

        GameObject BuildBar(string name, float yMin, float yMax, bool portraitOnLeft,
            out Image portrait, out Text nameText, out Text bodyText)
        {
            var bar = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bar.transform.SetParent(transform, false);
            SetAnchor(bar.GetComponent<RectTransform>(), 0.02f, yMin, 0.98f, yMax, 0, 0, 0, 0);
            bar.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 0.92f);
            bar.AddComponent<DialogueAdvanceInput>().Bind(_typewriter, HandleAdvance);

            float portraitX0 = portraitOnLeft ? 0f : 0.8f;
            float portraitX1 = portraitOnLeft ? 0.2f : 1f;
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitGo.transform.SetParent(bar.transform, false);
            SetAnchor(portraitGo.GetComponent<RectTransform>(), portraitX0, 0f, portraitX1, 1f, 0, 0, 0, 0);
            portrait = portraitGo.GetComponent<Image>();
            portrait.raycastTarget = false;

            float textX0 = portraitOnLeft ? 0.22f : 0.02f;
            float textX1 = portraitOnLeft ? 0.98f : 0.78f;

            nameText = CreateText(bar.transform, "Name", "", 22, TextAnchor.UpperLeft);
            SetAnchor(nameText.rectTransform, textX0, 0.68f, textX1, 0.95f, 0, 0, 0, 0);
            nameText.raycastTarget = false;
            nameText.fontStyle = FontStyle.Bold;

            bodyText = CreateText(bar.transform, "Body", "", 24, TextAnchor.UpperLeft);
            SetAnchor(bodyText.rectTransform, textX0, 0.05f, textX1, 0.68f, 0, 0, 0, 0);
            bodyText.raycastTarget = false;

            return bar;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            UIFont.Apply(text, UIFontRole.Dialogue);
            text.fontSize = size + 1;
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
