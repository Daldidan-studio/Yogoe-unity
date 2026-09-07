using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 캐릭터를 탭(클릭)하면 CharacterAgent.OnTapped()를 호출해서 혼잣말을 즉시 띄우거나 갱신한다
    /// (05_기획_미확정사항.md 9번, 6-4장). 지도를 드래그(MapCameraDrag)하는 동작과 구분하기 위해,
    /// 누른 지점에서 일정 픽셀 이상 움직이면 탭으로 치지 않는다. HUD 버튼 등 UI를 탭한 경우도 무시한다.
    ///
    /// WebGL에서 Pointer.current(합성 디바이스)를 읽으면 native 크래시가 나는 문제가 있어서
    /// (MapCameraDrag.cs에서 이미 발견/수정된 것과 동일한 이유로) 여기서도 Mouse/Touchscreen을 직접 읽는다.
    /// </summary>
    public class CharacterTapRouter : MonoBehaviour
    {
        public Camera targetCamera;

        /// <summary>이보다 많이 움직이면 드래그로 보고 탭 처리를 하지 않는다.</summary>
        public float tapMaxMovePixels = 24f;

        /// <summary>탭한 지점에서 이 반경(월드 유닛) 안에 있는 캐릭터 중 가장 가까운 것을 탭 대상으로 삼는다.</summary>
        public float tapMaxWorldRadius = 0.9f;

        private bool pressing;
        private bool wasPressedLastFrame;
        private Vector2 pressStartScreenPos;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            if (!TryReadPointer(out Vector2 screenPos, out bool isPressed)) return;

            bool justPressed = isPressed && !wasPressedLastFrame;
            bool justReleased = !isPressed && wasPressedLastFrame;
            wasPressedLastFrame = isPressed;

            if (justPressed)
            {
                pressing = true;
                pressStartScreenPos = screenPos;
            }
            else if (justReleased && pressing)
            {
                pressing = false;
                float moved = Vector2.Distance(screenPos, pressStartScreenPos);
                if (moved <= tapMaxMovePixels) HandleTap(screenPos);
            }
        }

        private void HandleTap(Vector2 screenPos)
        {
            // HUD 버튼(슬롯 탭 등)을 누른 거면 그쪽 클릭 핸들러가 이미 처리하므로 무시.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (targetCamera == null) return;

            float depth = -targetCamera.transform.position.z; // 카메라가 z=-10, 캐릭터가 z=0 근처인 배치 기준
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;

            CharacterAgent nearest = null;
            float nearestDist = tapMaxWorldRadius;
            foreach (var agent in CharacterAgent.All)
            {
                if (agent == null) continue;
                float d = Vector2.Distance(agent.transform.position, world);
                if (d <= nearestDist)
                {
                    nearestDist = d;
                    nearest = agent;
                }
            }

            if (nearest != null) nearest.OnTapped();
        }

        /// <summary>MapCameraDrag.cs와 동일한 이유로 Pointer.current 대신 Mouse/Touchscreen을 직접 읽는다.</summary>
        private static bool TryReadPointer(out Vector2 screenPos, out bool isPressed)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.added)
            {
                screenPos = mouse.position.ReadValue();
                isPressed = mouse.leftButton.isPressed;
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.added)
            {
                var touch = touchscreen.primaryTouch;
                var phase = touch.phase.ReadValue();
                isPressed = phase == UnityEngine.InputSystem.TouchPhase.Began
                            || phase == UnityEngine.InputSystem.TouchPhase.Moved
                            || phase == UnityEngine.InputSystem.TouchPhase.Stationary;
                screenPos = touch.position.ReadValue();
                return true;
            }

            screenPos = default;
            isPressed = false;
            return false;
        }
    }
}
