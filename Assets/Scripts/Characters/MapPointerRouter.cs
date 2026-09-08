using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Yoegoe.Debugging;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 맵 포인터 제스처 단일 진입점.
    /// Down 때 대상(캐릭터 / 맵)을 정하고, 임계 이동 후 캐릭터 드래그 또는 맵 패닝,
    /// 거의 안 움직이면 탭(혼잣말)으로 처리한다.
    /// </summary>
    public class MapPointerRouter : MonoBehaviour
    {
        public Camera targetCamera;
        public MapCameraDrag mapDrag;

        [Tooltip("이보다 많이 움직이면 탭이 아니라 드래그로 확정.")]
        public float dragThresholdPixels = 24f;

        [Tooltip("누른 지점 기준 캐릭터 히트 반경(월드).")]
        public float characterHitRadius = 0.9f;

        [Tooltip("드롭 시 기물 스냅 반경(월드).")]
        public float propDropRadius = 1.2f;

        private enum Phase { Idle, Pending, MapDrag, CharacterDrag }

        private Phase phase = Phase.Idle;
        private bool wasPressed;
        private Vector2 pressStartScreen;
        private Vector2 lastScreen;
        private CharacterAgent pressCharacter; // Pending 때 후보 (드래그 가능 여부와 무관 — 탭용)
        private CharacterAgent dragCharacter;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (mapDrag == null && targetCamera != null)
                mapDrag = targetCamera.GetComponent<MapCameraDrag>();
        }

        private void Update()
        {
            if (!TryReadPointer(out Vector2 screenPos, out bool pressed)) return;

            bool justPressed = pressed && !wasPressed;
            bool justReleased = !pressed && wasPressed;
            wasPressed = pressed;

            if (justPressed) OnPress(screenPos);
            else if (pressed && phase != Phase.Idle) OnHold(screenPos);
            else if (justReleased) OnRelease(screenPos);
        }

        private void OnPress(Vector2 screenPos)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                phase = Phase.Idle;
                return;
            }

            pressStartScreen = screenPos;
            lastScreen = screenPos;
            pressCharacter = FindNearestCharacter(screenPos);
            dragCharacter = null;
            phase = Phase.Pending;
        }

        private void OnHold(Vector2 screenPos)
        {
            Vector2 delta = screenPos - lastScreen;
            lastScreen = screenPos;

            if (phase == Phase.Pending)
            {
                float moved = Vector2.Distance(screenPos, pressStartScreen);
                if (moved <= dragThresholdPixels) return;

                if (pressCharacter != null && pressCharacter.CanBeDraggedByPlayer)
                {
                    dragCharacter = pressCharacter;
                    dragCharacter.BeginPlayerDrag();
                    phase = Phase.CharacterDrag;
                    MoveDragCharacter(screenPos);
                }
                else
                {
                    phase = Phase.MapDrag;
                    if (mapDrag == null && targetCamera != null)
                        mapDrag = targetCamera.GetComponent<MapCameraDrag>();
                    if (mapDrag != null) mapDrag.ApplyScreenDelta(screenPos - pressStartScreen);
                }
                return;
            }

            if (phase == Phase.MapDrag)
            {
                if (mapDrag == null && targetCamera != null)
                    mapDrag = targetCamera.GetComponent<MapCameraDrag>();
                if (mapDrag != null) mapDrag.ApplyScreenDelta(delta);
            }
            else if (phase == Phase.CharacterDrag && dragCharacter != null)
            {
                MoveDragCharacter(screenPos);
            }
        }

        private void OnRelease(Vector2 screenPos)
        {
            if (phase == Phase.Pending)
            {
                // 거의 안 움직임 → 탭
                if (pressCharacter != null) pressCharacter.OnTapped();
            }
            else if (phase == Phase.CharacterDrag && dragCharacter != null)
            {
                var prop = FindDropProp(dragCharacter, screenPos);
                dragCharacter.EndPlayerDrag(prop);
            }

            phase = Phase.Idle;
            pressCharacter = null;
            dragCharacter = null;
        }

        private void MoveDragCharacter(Vector2 screenPos)
        {
            if (dragCharacter == null || targetCamera == null) return;
            float depth = -targetCamera.transform.position.z;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;
            dragCharacter.SetDragWorldPosition(world);
        }

        private CharacterAgent FindNearestCharacter(Vector2 screenPos)
        {
            if (targetCamera == null) return null;
            float depth = -targetCamera.transform.position.z;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;

            CharacterAgent nearest = null;
            float nearestDist = characterHitRadius;
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
            return nearest;
        }

        private PropSlot FindDropProp(CharacterAgent agent, Vector2 screenPos)
        {
            if (targetCamera == null) return null;
            float depth = -targetCamera.transform.position.z;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;

            if (PropManager.Instance != null)
                return PropManager.Instance.FindNearestDropTarget(agent, world, propDropRadius);
            return null;
        }

        private static bool TryReadPointer(out Vector2 screenPos, out bool pressed)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPos = mouse.position.ReadValue();
                pressed = mouse.leftButton.isPressed;
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touch = touchscreen.primaryTouch;
                var touchPhase = touch.phase.ReadValue();
                pressed = touchPhase == UnityEngine.InputSystem.TouchPhase.Began
                          || touchPhase == UnityEngine.InputSystem.TouchPhase.Moved
                          || touchPhase == UnityEngine.InputSystem.TouchPhase.Stationary;
                screenPos = touch.position.ReadValue();
                return true;
            }

            screenPos = default;
            pressed = false;
            return false;
        }
    }
}
