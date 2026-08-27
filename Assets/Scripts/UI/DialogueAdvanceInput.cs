using System;
using System.Collections;
using KSpirits.Animation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace KSpirits.UI
{
    /// <summary>
    /// 대사 탭/꾹 눌러 넘기기 공통 입력 처리. 짧게 탭하면 타이핑 중엔 전체 표시,
    /// 다 보였으면 다음으로 진행. 0.25초 이상 누르고 있으면 3배속으로 전환하고,
    /// 문장이 끝날 때마다 0.5초 머물렀다가 자동으로 다음 줄로 넘어간다.
    ///
    /// 이 컴포넌트가 붙은 GameObject 자체가 탭 대상이 되므로, raycastTarget이 켜진
    /// Graphic(Image 등)이 있는 오브젝트에 붙여야 한다. ScrollScreenUI의 단일
    /// 대사창과 TwoSpeakerDialogueBox가 이 하나를 공유해서 쓴다.
    /// </summary>
    public class DialogueAdvanceInput : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        const float HoldEngageDelay = 0.25f;
        const float HoldLineGap = 0.5f;

        DialogueTypewriter _typewriter;
        Action _onAdvance;
        Action _onRevealed;

        bool _held;
        bool _holdAdvancedAny;
        Coroutine _holdRoutine;

        /// <param name="onAdvance">다음 줄로 넘어갈 때(탭으로든 꾹눌러 자동으로든) 호출.</param>
        /// <param name="onRevealed">탭으로 "타이핑 전체 표시"만 하고 안 넘어갔을 때 호출(선택).</param>
        public void Bind(DialogueTypewriter typewriter, Action onAdvance, Action onRevealed = null)
        {
            _typewriter = typewriter;
            _onAdvance = onAdvance;
            _onRevealed = onRevealed;
        }

        void Update()
        {
            // PointerUp/Exit 이벤트가 캔버스 밖 릴리즈 등으로 누락되는 경우를 대비한 안전장치.
            if (_held && !IsPointerDown())
                ReleaseHold();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ReleaseHold();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) ReleaseHold();
        }

        static bool IsPointerDown()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) return true;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            return false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _held = true;
            _holdAdvancedAny = false;
            if (_holdRoutine == null)
                _holdRoutine = StartCoroutine(HoldThenAutoAdvance());
        }

        public void OnPointerUp(PointerEventData eventData) => ReleaseHold();
        public void OnPointerExit(PointerEventData eventData) => ReleaseHold();

        void ReleaseHold()
        {
            _held = false;
            _typewriter?.SetFastForward(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_holdAdvancedAny)
            {
                _holdAdvancedAny = false;
                return;
            }

            if (_typewriter != null && _typewriter.HandleTap())
            {
                _onRevealed?.Invoke();
                return;
            }

            _onAdvance?.Invoke();
        }

        /// <summary>
        /// 짧은 탭은 무시(OnPointerClick 경로로 처리)하고, HoldEngageDelay 이상 눌려
        /// 있을 때만 3배속 + 완료된 줄 자동 진행을 켠다.
        /// </summary>
        IEnumerator HoldThenAutoAdvance()
        {
            float t = 0f;
            while (_held && t < HoldEngageDelay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_held)
            {
                _holdRoutine = null;
                yield break;
            }

            _typewriter?.SetFastForward(true);
            while (_held)
            {
                if (_typewriter != null && _typewriter.IsComplete && !_typewriter.IsTyping)
                {
                    // 문장이 다 끝난 뒤에도 잠깐(0.5초) 머물러서, 빠르게 넘기는 중에도 방금
                    // 끝난 줄을 눈으로 훑어볼 시간을 준다. 그사이 손을 떼면 자동 진행하지 않는다.
                    float hold = 0f;
                    while (_held && hold < HoldLineGap)
                    {
                        hold += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (!_held) break;

                    _holdAdvancedAny = true;
                    _onAdvance?.Invoke();
                    yield return null; // 다음 줄이 Play()로 갱신될 시간을 한 프레임 확보
                }
                else
                {
                    yield return null;
                }
            }
            _holdRoutine = null;
        }
    }
}
