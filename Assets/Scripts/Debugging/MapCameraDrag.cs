using UnityEngine;
using UnityEngine.InputSystem;

namespace Yoegoe.Debugging
{
    /// <summary>
    /// 맵(배경)이 화면보다 커졌을 때, 마우스/터치로 드래그해서 카메라를 이동시켜 둘러볼 수 있게 해준다.
    /// 이 프로젝트는 Project Settings에서 Active Input Handling이 "Input System Package (New)"로
    /// 되어 있어서(구 UnityEngine.Input 사용 시 예외 발생) 새 Input System으로 작성함.
    ///
    /// WebGL에서는 Pointer.current(가상 디바이스)를 읽으면 WASM "memory access out of bounds"로
    /// 크래시하는 경우가 있어, 실제 Mouse/Touchscreen만 사용한다.
    ///
    /// 카메라가 배경 밖(빈 공간)을 비추지 않도록, Main이 계산해주는 범위로 카메라
    /// 중심 좌표를 clamp한다 (SetBounds 호출 전에는 자유롭게 움직임).
    /// </summary>
    public class MapCameraDrag : MonoBehaviour
    {
        private Camera cam;
        private Vector2 lastPointerScreenPos;
        private bool dragging;

        private Vector2 minCenter;
        private Vector2 maxCenter;
        private bool boundsSet;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Update()
        {
            if (cam == null) return;
            if (!TryReadPointer(out Vector2 screenPos, out bool pressedNow)) return;

            if (pressedNow && !dragging)
            {
                dragging = true;
                lastPointerScreenPos = screenPos;
                return; // 이번 프레임은 시작점만 기록 (튀는 것 방지)
            }

            if (!pressedNow)
            {
                dragging = false;
                return;
            }

            Vector2 screenDelta = screenPos - lastPointerScreenPos;
            lastPointerScreenPos = screenPos;
            if (screenDelta.sqrMagnitude <= 0f) return;

            // 화면 픽셀 이동량을 카메라의 orthographicSize 기준 월드 단위로 환산
            // (드래그하면 손가락 아래 지점이 그대로 따라오는 느낌이 나도록 반대 방향으로 카메라를 옮김).
            float worldPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
            Vector3 worldDelta = new Vector3(-screenDelta.x * worldPerPixel, -screenDelta.y * worldPerPixel, 0f);
            Vector3 newPos = cam.transform.position + worldDelta;

            if (boundsSet)
            {
                newPos.x = Mathf.Clamp(newPos.x, minCenter.x, maxCenter.x);
                newPos.y = Mathf.Clamp(newPos.y, minCenter.y, maxCenter.y);
            }

            cam.transform.position = newPos;
        }

        /// <summary>
        /// WebGL에서 Pointer.current는 실제 디바이스가 아닌 합성 디바이스라 native 포인터가
        /// 비어 있는 채로 반환되는 경우가 있다. Mouse/Touchscreen의 added 상태만 본다.
        /// </summary>
        private static bool TryReadPointer(out Vector2 screenPos, out bool pressed)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.added)
            {
                screenPos = mouse.position.ReadValue();
                pressed = mouse.leftButton.isPressed;
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.added)
            {
                var touch = touchscreen.primaryTouch;
                var phase = touch.phase.ReadValue();
                pressed = phase == UnityEngine.InputSystem.TouchPhase.Began
                          || phase == UnityEngine.InputSystem.TouchPhase.Moved
                          || phase == UnityEngine.InputSystem.TouchPhase.Stationary;
                screenPos = touch.position.ReadValue();
                return true;
            }

            screenPos = default;
            pressed = false;
            return false;
        }

        /// <summary>카메라 중심이 이동할 수 있는 범위(= 맵 절반 크기 - 카메라 뷰 절반 크기)를 설정.</summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            minCenter = min;
            maxCenter = max;
            boundsSet = true;
        }
    }
}
