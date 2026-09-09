using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yoegoe.Debugging;
using Yoegoe.UI;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 맵 포인터 제스처 단일 진입점.
    /// 캐릭터: 길게 누르기(또는 임계 이동) → 들어올림 드래그.
    /// 맵: 임계 이동 후 패닝. 거의 안 움직이면 탭.
    /// 캐릭터 탭: 더블탭이면 상세, 단일탭(더블탭 창 만료 후)이면 혼잣말.
    /// </summary>
    public class MapPointerRouter : MonoBehaviour
    {
        public Camera targetCamera;
        public MapCameraDrag mapDrag;

        [Tooltip("이보다 많이 움직이면 탭이 아니라 드래그로 확정.")]
        public float dragThresholdPixels = 24f;

        [Tooltip("캐릭터를 이 시간 이상 누르고 있으면 이동 없이도 들어올림.")]
        public float longPressSeconds = 0.35f;

        [Tooltip("더블탭으로 인정할 최대 간격(초). 이 안에 같은 캐릭터를 다시 탭하면 상세화면.")]
        public float doubleTapSeconds = 0.35f;

        [Tooltip("캐릭터 스프라이트 bounds에 더하는 여유(월드). 너무 크면 근처 맵 드래그가 캐릭터로 잡힘.")]
        public float characterHitPadding = 0.12f;

        [Tooltip("스프라이트가 없을 때 쓰는 고정 히트 반경(월드).")]
        public float characterHitRadiusFallback = 0.45f;

        [Tooltip("드롭 시 기물 스프라이트 bounds 바깥으로 허용할 여유(월드). 0이면 PNG(스프라이트) 박스 안에 있을 때만 앉힘.")]
        public float propDropRadius = 0f;

        [Tooltip("기물 탭(자물쇠 구매·공덕 수거) 시 스프라이트 bounds 바깥 여유(월드). 너무 크면 멀리서도 구매 팝업이 뜸.")]
        public float propTapRadius = 0.12f;

        private enum Phase { Idle, Pending, MapDrag, CharacterDrag }

        private Phase phase = Phase.Idle;
        private bool wasPressed;
        private Vector2 pressStartScreen;
        private Vector2 lastScreen;
        private float pressUnscaledTime;
        private CharacterAgent pressCharacter;
        private CharacterAgent dragCharacter;
        private PropSlot pressProp;

        /// <summary>첫 탭 후 더블탭 대기 중인 캐릭터. 창이 지나면 혼잣말.</summary>
        private CharacterAgent pendingMonologueTap;
        private float pendingMonologueDeadline;

        private static readonly List<RaycastResult> UiRaycastHits = new List<RaycastResult>(8);

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            ResolveMapDrag();
        }

        private void Update()
        {
            FlushPendingMonologueTapIfDue();

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
            if (IsBlockingUi(screenPos))
            {
                phase = Phase.Idle;
                return;
            }

            pressStartScreen = screenPos;
            lastScreen = screenPos;
            pressUnscaledTime = Time.unscaledTime;
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
                float held = Time.unscaledTime - pressUnscaledTime;

                if (pressCharacter != null && pressCharacter.CanBeDraggedByPlayer)
                {
                    if (held >= longPressSeconds || moved > dragThresholdPixels)
                        BeginCharacterDrag(screenPos);
                    return;
                }

                if (moved <= dragThresholdPixels) return;

                phase = Phase.MapDrag;
                ResolveMapDrag();
                if (mapDrag != null) mapDrag.ApplyScreenDelta(screenPos - pressStartScreen);
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

        void BeginCharacterDrag(Vector2 screenPos)
        {
            CancelPendingMonologueTap();
            dragCharacter = pressCharacter;
            dragCharacter.BeginPlayerDrag();
            phase = Phase.CharacterDrag;
            MoveDragCharacter(screenPos);
        }

        private void OnRelease(Vector2 screenPos)
        {
            if (phase == Phase.Pending)
            {
                // 캐릭터가 잡힌 탭이면 멀리 있는 자물쇠 구매보다 캐릭터 제스처 우선
                bool propHitTight = pressProp != null && IsPropUnderFinger(pressProp, screenPos);
                if (propHitTight && pressProp != null && !pressProp.IsBuilt)
                {
                    CancelPendingMonologueTap();
                    var popup = PropPurchasePopup.Instance;
                    if (popup != null) popup.Open(pressProp);
                }
                else if (propHitTight && pressProp != null && pressProp.HasPendingMerit)
                {
                    CancelPendingMonologueTap();
                    pressProp.TryCollectMerit();
                }
                else if (pressCharacter != null)
                    HandleCharacterTap(pressCharacter);
            }
            else if (phase == Phase.CharacterDrag && dragCharacter != null)
            {
                CancelPendingMonologueTap();
                var prop = FindDropProp(dragCharacter, screenPos);
                dragCharacter.EndPlayerDrag(prop);
            }

            phase = Phase.Idle;
            pressCharacter = null;
            dragCharacter = null;
            pressProp = null;
        }

        /// <summary>
        /// 더블탭이면 상세화면. 아니면 창이 끝날 때까지 기다렸다가 혼잣말
        /// (상세 진입 시 말풍선이 뜨지 않도록 단일탭을 즉시 처리하지 않음).
        /// </summary>
        void HandleCharacterTap(CharacterAgent agent)
        {
            if (agent == null) return;

            if (pendingMonologueTap == agent && Time.unscaledTime <= pendingMonologueDeadline)
            {
                CancelPendingMonologueTap();
                OpenCharacterDetail(agent);
                return;
            }

            // 다른 캐릭터를 탭했다면 대기 중이던 단일탭은 바로 혼잣말로 확정
            if (pendingMonologueTap != null && pendingMonologueTap != agent)
                FlushPendingMonologueTap();

            pendingMonologueTap = agent;
            pendingMonologueDeadline = Time.unscaledTime + doubleTapSeconds;
        }

        void FlushPendingMonologueTapIfDue()
        {
            if (pendingMonologueTap == null) return;
            if (Time.unscaledTime < pendingMonologueDeadline) return;
            FlushPendingMonologueTap();
        }

        void FlushPendingMonologueTap()
        {
            var agent = pendingMonologueTap;
            pendingMonologueTap = null;
            if (agent != null) agent.OnTapped();
        }

        void CancelPendingMonologueTap()
        {
            pendingMonologueTap = null;
        }

        static void OpenCharacterDetail(CharacterAgent agent)
        {
            if (agent == null) return;
            if (GameHud.Instance == null || GameHud.Instance.detailScreen == null) return;
            GameHud.Instance.detailScreen.Open(agent);
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
            if (targetCamera == null || PropManager.Instance == null || agent == null) return null;
            float depth = -targetCamera.transform.position.z;
            Vector3 fingerWorld = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            fingerWorld.z = 0f;

            // 빈 기물(착석) → 점유 기물(옆 배치) → 타 엔딩 기물(거절 연출)
            var atChar = PropManager.Instance.FindNearestDropTarget(agent, agent.transform.position, propDropRadius, allowOccupied: false);
            if (atChar != null) return atChar;
            atChar = PropManager.Instance.FindNearestDropTarget(agent, fingerWorld, propDropRadius, allowOccupied: false);
            if (atChar != null) return atChar;

            atChar = PropManager.Instance.FindNearestDropTarget(agent, agent.transform.position, propDropRadius, allowOccupied: true);
            if (atChar != null) return atChar;
            atChar = PropManager.Instance.FindNearestDropTarget(agent, fingerWorld, propDropRadius, allowOccupied: true);
            if (atChar != null) return atChar;

            atChar = PropManager.Instance.FindNearestDropTarget(
                agent, agent.transform.position, propDropRadius, allowOccupied: true, allowEndingRefuse: true);
            if (atChar != null && atChar.IsForbiddenEndingFor(agent)) return atChar;
            atChar = PropManager.Instance.FindNearestDropTarget(
                agent, fingerWorld, propDropRadius, allowOccupied: true, allowEndingRefuse: true);
            if (atChar != null && atChar.IsForbiddenEndingFor(agent)) return atChar;
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

        bool IsPropUnderFinger(PropSlot prop, Vector2 screenPos)
        {
            if (prop == null || targetCamera == null || PropManager.Instance == null) return false;
            float depth = -targetCamera.transform.position.z;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;
            return PropManager.Instance.FindNearestProp(world, propTapRadius) == prop;
        }

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

                var rt = go.transform as RectTransform;
                if (rt != null
                    && rt.anchorMin == Vector2.zero
                    && rt.anchorMax == Vector2.one
                    && go.GetComponent<Graphic>() != null)
                    return true;
            }
            return false;
        }

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
