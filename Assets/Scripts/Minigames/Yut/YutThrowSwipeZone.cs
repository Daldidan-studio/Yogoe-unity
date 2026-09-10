using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 탭 대신 아래→위 슬라이드로 윷을 던지는 입력 영역. 슬라이드 속도로 power(0~1)를 계산해서
    /// 던지는 연출(아치 높이·회전·착지 퍼짐)에만 반영한다 — 확률표(YutThrowRoller)는 전혀 모른다,
    /// 속도로 결과를 조작할 수 없게 하기 위한 의도적 분리.
    /// </summary>
    public class YutThrowSwipeZone : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>위로 충분히 슬라이드가 끝났을 때. power는 속도 기반 0~1(연출 전용).</summary>
        public event Action<float> OnSwipeThrow;

        const float MinUpDistance = 50f;    // 이만큼은 위로 밀어야 슬라이드로 인정(탭·실수 방지)
        const float ReferenceSpeed = 2200f; // 이 속도(px/sec) 이상이면 power = 1

        Vector2 startPos;
        float startTime;
        bool tracking;

        public void OnPointerDown(PointerEventData e)
        {
            startPos = e.position;
            startTime = Time.unscaledTime;
            tracking = true;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!tracking) return;
            tracking = false;

            float upDistance = e.position.y - startPos.y;
            if (upDistance < MinUpDistance) return;

            float elapsed = Mathf.Max(0.01f, Time.unscaledTime - startTime);
            float speed = upDistance / elapsed;
            float power = Mathf.Clamp01(speed / ReferenceSpeed);
            OnSwipeThrow?.Invoke(power);
        }
    }
}
