using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.Animation
{
    /// <summary>
    /// 대사 타이핑. 재생 중 탭 → 전체 표시. 완료 후 탭 → 다음 줄(외부에서 처리).
    /// </summary>
    public class DialogueTypewriter : MonoBehaviour
    {
        Text _body;
        string _fullText = "";
        Coroutine _routine;
        float _charsPerSecond = 40f;
        float _punctuationHold = 0.06f;

        public bool IsTyping { get; private set; }
        public bool IsComplete { get; private set; }

        public void Bind(Text body) => _body = body;

        public void Configure(float charsPerSecond, float punctuationHold = 0.06f)
        {
            _charsPerSecond = Mathf.Max(1f, charsPerSecond);
            _punctuationHold = Mathf.Max(0f, punctuationHold);
        }

        public void Play(string fullText)
        {
            Stop();
            _fullText = fullText ?? "";
            IsTyping = true;
            IsComplete = false;
            if (_body != null) _body.text = "";
            _routine = StartCoroutine(TypeRoutine());
        }

        /// <returns>true면 이번 탭으로 타이핑만 스킵함. false면 이미 완료라 다음 대사로 넘겨도 됨.</returns>
        public bool HandleTap()
        {
            if (IsTyping)
            {
                RevealAll();
                return true;
            }
            return false;
        }

        public void RevealAll()
        {
            Stop();
            if (_body != null) _body.text = _fullText;
            IsTyping = false;
            IsComplete = true;
        }

        public void Stop()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            IsTyping = false;
        }

        IEnumerator TypeRoutine()
        {
            if (_body == null)
            {
                IsTyping = false;
                IsComplete = true;
                yield break;
            }

            float delay = 1f / _charsPerSecond;
            for (int i = 0; i < _fullText.Length; i++)
            {
                _body.text = _fullText.Substring(0, i + 1);
                char c = _fullText[i];
                if (c is '.' or '!' or '?' or '…' or ',' or '，' or '。')
                    yield return new WaitForSeconds(delay + _punctuationHold);
                else
                    yield return new WaitForSeconds(delay);
            }

            IsTyping = false;
            IsComplete = true;
            _routine = null;
        }
    }
}
