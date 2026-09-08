using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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

        [Tooltip("캐릭터 스프라이트 bounds에 더하는 여유(월드). 너무 크면 근처 맵 드래그가 캐릭터로 잡힘.")]
        public float characterHitPadding = 0.12f;

        [Tooltip("스프라이트가 없을 때 쓰는 고정 히트 반경(월드).")]
        public float characterHitRadiusFallback = 0.45f;

        [Tooltip("드롭 시 기물 스냅 반경(월드).")]
        public float propDropRadius = 1.2f;

        [Tooltip("탭으로 공덕 수거할 때 기물 히트 반경(월드).")]
        public float propTapRadius = 1.0f;

        private enum Phase { Idle, Pending, MapDrag, CharacterDrag }

        private Phase phase = Phase.Idle;
        private bool wasPressed;
        private Vector2 pressStartScreen;
        private Vector2 lastScreen;
        private CharacterAgent pressCharacter;
        private CharacterAgent dragCharacter;
        private PropSlot pressProp;

        private static readonly List<RaycastResult> UiRaycastHits = new List<RaycastResult>(8);

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            ResolveMapDrag();
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
            // HUD 장식 Image까지 막으면 맵/캐릭터 드래그가 통째로 죽는다.
            // 버튼·모달(전체화면 딤)만 입력 차단.
            if (IsBlockingUi(screenPos))
            {
                phase = Phase.Idle;
                return;
            }

            pressStartScreen = screenPos;
            lastScreen = screenPos;
            pressCharacter = FindNearestCharacter(screenPos);
            pressProp = FindNearestProp(screenPos);
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
                    ResolveMapDrag();
                    if (mapDrag != null) mapDrag.ApplyScreenDelta(screenPos - pressStartScreen);
                }
                return;
            }

            if (phase == Phase.MapDrag)
            {
                ResolveMapDrag();
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
                // 8장: 미건립 자물쇠 탭 → 구매 팝업
                if (pressProp != null && !pressProp.IsBuilt)
                {
                    var popup = Yoegoe.UI.PropPurchasePopup.Instance;
                    if (popup != null) popup.Open(pressProp);
                }
                // 7-2: 더미 있는 기물 탭 → 수거. 없으면 캐릭터 혼잣말.
                else if (pressProp != null && pressProp.HasPendingMerit)
                    pressProp.TryCollectMerit();
                else if (pressCharacter != null)
                    pressCharacter.OnTapped();
            }
            else if (phase == Phase.CharacterDrag && dragCharacter != null)
            {
                var prop = FindDropProp(dragCharacter, screenPos);
                dragCharacter.EndPlayerDrag(prop);
            }

            phase = Phase.Idle;
            pressCharacter = null;
            dragCharacter = null;
            pressProp = null;
        }

        private void ResolveMapDrag()
        {
            if (mapDrag != null) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
                mapDrag = targetCamera.GetComponent<MapCameraDrag>();
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
            float nearestScore = float.MaxValue;
            foreach (var agent in CharacterAgent.All)
            {
                if (agent == null) continue;
                if (!TryGetCharacterHitScore(agent, world, out float score)) continue;
                if (score < nearestScore)
                {
                    nearestScore = score;
                    nearest = agent;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 스프라이트 사각형(+패딩) 안이면 점수=중심거리, 밖이면 미히트.
        /// 피벗(발) 기준 큰 원 히트는 근처 맵 드래그를 캐릭터로 오판한다.
        /// </summary>
        private bool TryGetCharacterHitScore(CharacterAgent agent, Vector3 world, out float score)
        {
            score = 0f;
            var sr = agent.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Bounds b = sr.bounds;
                b.Expand(characterHitPadding);
                if (!b.Contains(world)) return false;
                score = Vector2.Distance(b.center, world);
                return true;
            }

            float d = Vector2.Distance(agent.transform.position, world);
            if (d > characterHitRadiusFallback) return false;
            score = d;
            return true;
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

        private PropSlot FindNearestProp(Vector2 screenPos)
        {
            if (targetCamera == null || PropManager.Instance == null) return null;
            float depth = -targetCamera.transform.position.z;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;
            return PropManager.Instance.FindNearestProp(world, propTapRadius);
        }

        /// <summary>
        /// 맵 입력을 막을 UI만 true.
        /// Selectable(버튼 등) 또는 전체화면 딤 패널.
        /// </summary>
        private static bool IsBlockingUi(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current) { position = screenPos };
            UiRaycastHits.Clear();
            EventSystem.current.RaycastAll(eventData, UiRaycastHits);

            for (int i = 0; i < UiRaycastHits.Count; i++)
            {
                var go = UiRaycastHits[i].gameObject;
                if (go == null || !go.activeInHierarchy) continue;

                if (go.GetComponentInParent<Selectable>() != null)
                    return true;

                // 구매/상세 등 전체화면 딤
                var rt = go.transform as RectTransform;
                if (rt != null
                    && rt.anchorMin == Vector2.zero
                    && rt.anchorMax == Vector2.one
                    && go.GetComponent<Graphic>() != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// WebGL/모바일: Mouse 디바이스가 항상 있어서 터치가 무시되면 드래그가 전부 죽는다.
        /// 손가락이 내려가 있으면 터치를 우선한다.
        /// </summary>
        private static bool TryReadPointer(out Vector2 screenPos, out bool pressed)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touch = touchscreen.primaryTouch;
                var touchPhase = touch.phase.ReadValue();
                bool touchDown = touchPhase == UnityEngine.InputSystem.TouchPhase.Began
                                 || touchPhase == UnityEngine.InputSystem.TouchPhase.Moved
                                 || touchPhase == UnityEngine.InputSystem.TouchPhase.Stationary;
                if (touchDown || touch.press.isPressed)
                {
                    screenPos = touch.position.ReadValue();
                    pressed = true;
                    return true;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPos = mouse.position.ReadValue();
                pressed = mouse.leftButton.isPressed;
                return true;
            }

            screenPos = default;
            pressed = false;
            return false;
        }
    }
}
