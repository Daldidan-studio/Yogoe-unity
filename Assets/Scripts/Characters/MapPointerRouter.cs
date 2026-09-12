using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yoegoe.Core;
using Yoegoe.Debugging;
using Yoegoe.UI;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 맵 포인터 제스처 단일 진입점.
    /// 캐릭터: 길게 누르기(또는 임계 이동) → 들어올림 드래그.
    /// 맵: 임계 이동 후 패닝. 두 손가락 핀치·마우스 휠 → 줌.
    /// 캐릭터 탭: 더블탭이면 상세, 단일탭(더블탭 창 만료 후)이면 혼잣말/넋 리액션.
    ///
    /// 설계 원칙(반복된 회귀 버그를 겪고 정리함): "무엇을 눌렀는지"는 press 시점에 딱 한 번만
    /// 정한다(<see cref="PressTarget"/>). Hold·Release는 그 판정을 다시 계산하지 않고 그대로
    /// 따라간다 — 특히 release 시점에 손 위치로 반경을 재검사하지 않는다. press 이후 화면이
    /// 줌되거나 손이 살짝 흔들려도(스크린 픽셀 vs 월드 반경 단위 차이) 판정이 뒤집히지 않게 하기
    /// 위함. 이 파일을 고칠 때 새 특수 케이스가 필요하면, 기존 분기에 조건을 덧붙이지 말고
    /// PressTarget에 값을 추가하는 방향으로 확장할 것.
    /// </summary>
    public class MapPointerRouter : MonoBehaviour
    {
        /// <summary>
        /// 캐릭터 상세화면을 열어달라는 요청 (agent, 하이라이트할 offeringId).
        /// UI(GameHud)가 구독해서 실제 DetailScreen을 연다 — 이 클래스는 UI를 모른다.
        /// </summary>
        public static event System.Action<CharacterAgent, string> CharacterDetailRequested;

        /// <summary>
        /// 잠긴 기물 탭 → 구매 팝업 요청. UI(PropPurchasePopup)가 구독한다 — 이 클래스는 UI를 모른다.
        /// </summary>
        public static event System.Action<PropSlot> PropPurchaseRequested;

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

        [Tooltip("드롭 시 기물 스프라이트 bounds 바깥으로 허용할 여유(월드). 0이면 PNG 박스 안만.")]
        public float propDropRadius = 0.15f;

        [Tooltip("기물 탭(공덕 수거) 시 스프라이트 bounds 바깥 여유(월드). TEMP: 본체 수거 비활성 — 자물쇠/드롭용 FindNearestProp에만 사용.")]
        public float propTapRadius = 0.12f;

        [Tooltip("TEMP: 공덕 수거는 더미(***·숫자) 라벨만. 라벨 bounds 바깥 여유(월드).")]
        public float pileLabelTapPadding = 0.18f;

        [Tooltip("자물쇠(미건립) 탭 여유. 0이면 bounds 안만 — 초가집처럼 작고 캐릭터와 겹치면 구매가 잘 안 됨.")]
        public float lockTapRadius = 0.28f;

        private enum Phase { Idle, Pending, MapDrag, CharacterDrag, PinchZoom }

        /// <summary>press 시점에 딱 한 번 정해지는 "무엇을 눌렀는지". Hold/Release는 이 값만 본다.</summary>
        private enum PressTarget { Empty, LockedProp, CollectibleProp, Character }

        private Phase phase = Phase.Idle;
        private PressTarget pressTarget = PressTarget.Empty;
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

        bool pinchActive;
        float lastPinchDistance;

        private static readonly List<RaycastResult> UiRaycastHits = new List<RaycastResult>(8);

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            ResolveMapDrag();
        }

        private void Update()
        {
            FlushPendingMonologueTapIfDue();

            if (CeremonyGate.BlocksWorldInput) return;
            // 윷은 풀스크린 UI인데 보드 칸 Image가 raycast를 안 먹는 구멍이 있어,
            // 휠/핀치가 IsBlockingUi를 통과해 본맵 카메라로 새는 경우가 있다.
            if (YutScreen.Instance != null && YutScreen.Instance.IsOpen) return;

            // 핀치·휠은 단일 포인터 제스처보다 우선
            if (TryHandlePinchZoom()) return;
            TryHandleScrollZoom();

            if (!TryReadPointer(out Vector2 screenPos, out bool pressed)) return;

            bool justPressed = pressed && !wasPressed;
            bool justReleased = !pressed && wasPressed;
            wasPressed = pressed;

            if (justPressed) OnPress(screenPos);
            else if (pressed && phase != Phase.Idle) OnHold(screenPos);
            else if (justReleased) OnRelease(screenPos);
        }

        /// <summary>두 손가락 핀치 → 맵 줌.</summary>
        bool TryHandlePinchZoom()
        {
            var ts = Touchscreen.current;
            if (ts == null) return false;

            int n = 0;
            Vector2 p0 = default;
            Vector2 p1 = default;
            int touchCount = ts.touches.Count;
            for (int i = 0; i < touchCount; i++)
            {
                var touch = ts.touches[i];
                var ph = touch.phase.ReadValue();
                if (ph == UnityEngine.InputSystem.TouchPhase.None
                    || ph == UnityEngine.InputSystem.TouchPhase.Ended
                    || ph == UnityEngine.InputSystem.TouchPhase.Canceled)
                    continue;

                Vector2 pos = touch.position.ReadValue();
                if (n == 0) p0 = pos;
                else if (n == 1) p1 = pos;
                n++;
                if (n >= 2) break;
            }

            if (n < 2)
            {
                if (pinchActive)
                {
                    pinchActive = false;
                    phase = Phase.Idle;
                }
                return false;
            }

            float dist = Vector2.Distance(p0, p1);
            if (dist < 8f) return true;

            Vector2 mid = (p0 + p1) * 0.5f;
            if (IsBlockingUi(mid) || IsBlockingUi(p0) || IsBlockingUi(p1))
            {
                if (pinchActive)
                {
                    pinchActive = false;
                    phase = Phase.Idle;
                }
                return true;
            }

            if (!pinchActive)
            {
                pinchActive = true;
                lastPinchDistance = dist;
                AbortSingleFingerGesture();
                phase = Phase.PinchZoom;
                return true;
            }

            if (lastPinchDistance > 0.01f)
            {
                // 손가락을 벌리면 dist↑ → 확대(ortho↓) → ratio = last/dist
                float ratio = lastPinchDistance / dist;
                ratio = Mathf.Clamp(ratio, 0.85f, 1.18f);
                ResolveMapDrag();
                if (mapDrag != null) mapDrag.ZoomByRatio(mid, ratio);
            }

            lastPinchDistance = dist;
            phase = Phase.PinchZoom;
            wasPressed = true;
            return true;
        }

        /// <summary>에디터·데스크톱 WebGL용 마우스 휠 줌.</summary>
        void TryHandleScrollZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            if (IsBlockingUi(mouse.position.ReadValue())) return;

            ResolveMapDrag();
            if (mapDrag == null) return;

            float ratio = scroll > 0f ? 0.90f : 1.11f;
            mapDrag.ZoomByRatio(mouse.position.ReadValue(), ratio);
        }

        void AbortSingleFingerGesture()
        {
            CancelPendingMonologueTap();
            if (phase == Phase.CharacterDrag && dragCharacter != null)
            {
                dragCharacter.EndPlayerDrag(null);
                dragCharacter = null;
            }
            pressCharacter = null;
            pressProp = null;
            pressTarget = PressTarget.Empty;
        }

        private void OnPress(Vector2 screenPos)
        {
            if (IsBlockingUi(screenPos))
            {
                Debug.Log("[DEBUG-LOCK] OnPress: UI가 막아서 무시됨 at " + screenPos);
                phase = Phase.Idle;
                return;
            }

            pressStartScreen = screenPos;
            lastScreen = screenPos;
            pressUnscaledTime = Time.unscaledTime;
            pressCharacter = FindNearestCharacter(screenPos);
            pressProp = FindNearestProp(screenPos);
            // TEMP: 수거는 더미 라벨 전용 — 기물 본체 히트와 분리
            var pileProp = FindNearestPileLabel(screenPos);
            if (pileProp != null) pressProp = pileProp;
            dragCharacter = null;
            pressTarget = ClassifyPressTarget(pileProp);
            phase = Phase.Pending;
            Debug.Log("[DEBUG-LOCK] OnPress: pressProp=" + (pressProp != null ? pressProp.name + " IsBuilt=" + pressProp.IsBuilt : "null")
                + " pressCharacter=" + (pressCharacter != null ? pressCharacter.name : "null")
                + " pile=" + (pileProp != null)
                + " => pressTarget=" + pressTarget);
        }

        /// <summary>
        /// "무엇을 눌렀는지"를 press 시점에 딱 한 번 정한다.
        /// TEMP 우선순위: 잠긴 기물 > 더미(*** ) 라벨 수거 > 캐릭터 > 빈 맵.
        /// 기물 본체 탭으로는 수거하지 않는다. Hold/Release는 이 결과만 본다.
        /// </summary>
        PressTarget ClassifyPressTarget(PropSlot pileProp)
        {
            if (pressProp != null && !pressProp.IsBuilt) return PressTarget.LockedProp;
            // TEMP: 더미 라벨이 요괴보다 위 — 라벨 탭은 수거 우선
            if (pileProp != null) return PressTarget.CollectibleProp;
            // 기절 등으로 드래그 불가한 캐릭터도 탭(상세화면 진입)은 가능해야 한다 —
            // "기절한 요괴는 상세 화면 공양으로만 깨어난다" — 그래서 CanBeDraggedByPlayer로
            // 걸러내지 않는다. 드래그 가능 여부는 Hold에서 따로 본다.
            if (pressCharacter != null) return PressTarget.Character;
            return PressTarget.Empty;
        }

        private void OnHold(Vector2 screenPos)
        {
            Vector2 delta = screenPos - lastScreen;
            lastScreen = screenPos;

            if (phase == Phase.Pending)
            {
                float moved = Vector2.Distance(screenPos, pressStartScreen);
                float held = Time.unscaledTime - pressUnscaledTime;

                switch (pressTarget)
                {
                    case PressTarget.LockedProp:
                        // 자물쇠 탭은 캐릭터·지도 드래그로 절대 전환되지 않는다 — release에서 구매 판정.
                        return;

                    case PressTarget.CollectibleProp:
                        // 공덕 더미가 있는 기물 위엔 보통 생산 중인 요괴가 앉아있다. 가만히 오래
                        // 누르는 것만으로는 드래그로 넘어가지 않게 해서 수거 탭이 채이지 않게 하되,
                        // 손가락이 실제로 움직이면(진짜 드래그 의도) 정상적으로 드래그로 전환한다.
                        if (pressCharacter != null
                            && pressCharacter.CanBeDraggedByPlayer
                            && moved > dragThresholdPixels)
                            BeginCharacterDrag(screenPos);
                        return;

                    case PressTarget.Character:
                        if (!pressCharacter.CanBeDraggedByPlayer)
                        {
                            // 드래그 불가 캐릭터(기절 등) 위는 지도 패닝 임계값만 적용 —
                            // 움직임이 없으면 release에서 탭(상세화면 진입)으로 처리된다.
                            if (moved <= dragThresholdPixels) return;
                            phase = Phase.MapDrag;
                            ResolveMapDrag();
                            if (mapDrag != null) mapDrag.ApplyScreenDelta(screenPos - pressStartScreen);
                            return;
                        }
                        if (held >= longPressSeconds || moved > dragThresholdPixels)
                            BeginCharacterDrag(screenPos);
                        return;

                    default: // Empty — 빈 맵 위: 임계 이동 이상이면 패닝
                        if (moved <= dragThresholdPixels) return;
                        phase = Phase.MapDrag;
                        ResolveMapDrag();
                        if (mapDrag != null) mapDrag.ApplyScreenDelta(screenPos - pressStartScreen);
                        return;
                }
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
            Debug.Log("[DEBUG-LOCK] OnRelease: phase=" + phase + " pressTarget=" + pressTarget
                + " pressProp=" + (pressProp != null ? pressProp.name : "null"));
            if (phase == Phase.Pending)
            {
                // press 시점 판정(pressTarget)만 신뢰한다 — release 손 위치로 반경을 다시 재는
                // 순간, 화면 픽셀 이동과 월드 반경 단위가 안 맞아(특히 줌아웃 상태) 손이 살짝만
                // 흔들려도 오탐 실패하는 회귀가 반복됐다.
                switch (pressTarget)
                {
                    case PressTarget.LockedProp:
                        CancelPendingMonologueTap();
                        Debug.Log("[DEBUG-LOCK] OnRelease: PropPurchaseRequested 발행, 구독자 있음="
                            + (PropPurchaseRequested != null));
                        PropPurchaseRequested?.Invoke(pressProp);
                        break;

                    case PressTarget.CollectibleProp:
                        CancelPendingMonologueTap();
                        pressProp.TryCollectMerit();
                        break;

                    case PressTarget.Character:
                        HandleCharacterTap(pressCharacter);
                        break;
                }
            }
            else if (phase == Phase.CharacterDrag && dragCharacter != null)
            {
                CancelPendingMonologueTap();
                var prop = FindDropProp(dragCharacter, screenPos);
                dragCharacter.EndPlayerDrag(prop);
            }

            phase = Phase.Idle;
            pressTarget = PressTarget.Empty;
            pressCharacter = null;
            dragCharacter = null;
            pressProp = null;
        }

        /// <summary>
        /// 더블탭이면 상세화면. 아니면 창이 끝날 때까지 기다렸다가 혼잣말
        /// (상세 진입 시 말풍선이 뜨지 않도록 단일탭을 즉시 처리하지 않음).
        /// 기물 요구 ? : 단일탭 무반응, 더블탭은 상세 (Docs/07 5행).
        /// </summary>
        void HandleCharacterTap(CharacterAgent agent)
        {
            if (agent == null) return;

            // 더블탭은 기물 요구 중에도 상세
            if (pendingMonologueTap == agent && Time.unscaledTime <= pendingMonologueDeadline)
            {
                CancelPendingMonologueTap();
                OpenCharacterDetail(agent);
                return;
            }

            if (pendingMonologueTap != null && pendingMonologueTap != agent)
                FlushPendingMonologueTap();

            // 기물 요구 ? 단일탭: 혼잣말·상세 없음. 더블탭 창만 연다.
            if (agent.Requests != null && agent.Requests.HasVisiblePropRequest)
            {
                pendingMonologueTap = agent;
                pendingMonologueDeadline = Time.unscaledTime + doubleTapSeconds;
                return;
            }

            // 공양물 요구 말풍선 탭 → 즉시 상세(강조)
            if (agent.HasOfferingRequest)
            {
                CancelPendingMonologueTap();
                OpenCharacterDetail(agent);
                return;
            }

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
            if (agent == null) return;
            if (agent.Requests != null && agent.Requests.HasVisiblePropRequest)
                return;
            if (agent.HasOfferingRequest)
            {
                OpenCharacterDetail(agent);
                return;
            }
            agent.OnTapped();
        }

        void CancelPendingMonologueTap()
        {
            pendingMonologueTap = null;
        }

        static void OpenCharacterDetail(CharacterAgent agent)
        {
            if (agent == null) return;
            string highlight = null;
            if (agent.HasOfferingRequest && agent.Requests.OfferingRequest != null)
                highlight = agent.Requests.OfferingRequest.offeringId;
            CharacterDetailRequested?.Invoke(agent, highlight);
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
            // 자물쇠는 더 넓은 반경으로 먼저 찾고, 없으면 일반 반경
            var locked = PropManager.Instance.FindNearestUnbuiltProp(world, lockTapRadius);
            if (locked != null) return locked;
            return PropManager.Instance.FindNearestProp(world, propTapRadius);
        }

        /// <summary>TEMP: 공덕 더미(***·숫자) TextMesh 라벨 히트만.</summary>
        PropSlot FindNearestPileLabel(Vector2 screenPos)
        {
            if (targetCamera == null || PropManager.Instance == null) return null;
            float depth = -targetCamera.transform.position.z;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;

            PropSlot best = null;
            float bestScore = float.MaxValue;
            var all = PropManager.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null) continue;
                if (!p.TryGetPileLabelHitScore(world, pileLabelTapPadding, out float score)) continue;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
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
