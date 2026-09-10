using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 탭 대신 아래→위 슬라이드로 윷을 던지는 입력 영역. 슬라이드 속도로 power(0~1)를 계산해서
    /// 던지는 연출(아치 높이·회전·착지 퍼짐)에만 반영한다 — 확률표(YutThrowRoller)는 전혀 모른다,
    /// 속도로 결과를 조작할 수 없게 하기 위한 의도적 분리.
    ///
    /// IPointerDown/UpHandler로 만들었더니 반응이 없었다 — 새 Input System(InputSystemUIInputModule)
    /// 에서는 중간에 실제 드래그 이벤트가 없으면 PointerUp 시점 position이 갱신 안 되는 경우가 있어서,
    /// 이 프로젝트에서 이미 검증된 방식(OfferingDragItem, YutPieceDragHandle)과 같은 Begin/Drag/EndDrag
    /// 기반으로 바꿨다.
    /// </summary>
    public class YutThrowSwipeZone : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>위로 충분히 슬라이드가 끝났을 때. power는 속도 기반 0~1(연출 전용).</summary>
        public event Action<float> OnSwipeThrow;

        const float MinUpDistance = 50f;    // 이만큼은 위로 밀어야 슬라이드로 인정(탭·실수 방지)
        const float ReferenceSpeed = 2200f; // 이 속도(px/sec) 이상이면 power = 1

        Vector2 startPos;
        float startTime;

        public void OnBeginDrag(PointerEventData e)
        {
            startPos = e.position;
            startTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData e)
        {
            // 연출(예: 손 힌트 따라가기)이 필요해지면 여기서 처리 — 지금은 판정에 필요 없음.
        }

        public void OnEndDrag(PointerEventData e)
        {
            float upDistance = e.position.y - startPos.y;
            if (upDistance < MinUpDistance) return;

            float elapsed = Mathf.Max(0.01f, Time.unscaledTime - startTime);
            float speed = upDistance / elapsed;
            float power = Mathf.Clamp01(speed / ReferenceSpeed);
            OnSwipeThrow?.Invoke(power);
        }
    }
}
