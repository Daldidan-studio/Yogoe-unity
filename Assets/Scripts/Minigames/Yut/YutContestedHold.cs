using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 경합 밭(같은 칸으로 갈 수 있는 후보가 여럿) 하나를 꾹 누르고 있으면 상하좌우로 후보가
    /// 펼쳐지고, 원하는 쪽으로 끌고 가서 손을 떼면 그 말이 이동한다 — 레퍼런스(yokai-yut-garden)의
    /// "경합 밭은 꾹 눌러 드래그" 인터랙션. 펼쳐지기 전에 많이 움직이면(다른 곳을 드래그하려던 것)
    /// 취소되고, 살짝 눌렀다 떼기만 해도(꾹 누르는 시간을 못 채우면) 아무 일도 안 일어난다 —
    /// 실수로 아무 말이나 골라지는 걸 막기 위한 의도적 장치.
    /// </summary>
    public class YutContestedHold : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public YutMiniGame Owner;
        public int NodeId;
        public List<YutMiniGame.YokaiMoveCandidate> Candidates;

        const float HoldDelay = 0.32f;
        const float CancelMoveDistance = 12f;
        const float MinSelectDistance = 22f;
        const float MaxSelectDistance = 95f;

        Vector2 startScreenPos;
        Vector2 centerScreenPos;
        bool revealed;
        bool finished;
        Coroutine holdRoutine;
        int selectedIndex = -1;

        public void OnPointerDown(PointerEventData e)
        {
            startScreenPos = e.position;
            centerScreenPos = ((RectTransform)transform).position; // ScreenSpaceOverlay라 world pos = screen pos
            revealed = false;
            finished = false;
            selectedIndex = -1;
            holdRoutine = StartCoroutine(HoldTimer());
        }

        IEnumerator HoldTimer()
        {
            yield return new WaitForSecondsRealtime(HoldDelay);
            if (finished) yield break;
            revealed = true;
            Owner.RevealContested(NodeId, Candidates);
        }

        public void OnDrag(PointerEventData e)
        {
            if (finished) return;
            if (!revealed)
            {
                if (Vector2.Distance(e.position, startScreenPos) > CancelMoveDistance)
                    Cancel(); // 펼쳐지기 전에 많이 움직였다 — 딴 데를 드래그하려던 것으로 보고 취소
                return;
            }
            UpdateSelection(e.position);
        }

        void UpdateSelection(Vector2 screenPos)
        {
            Vector2 delta = screenPos - centerScreenPos;
            float distance = delta.magnitude;

            int index = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x < 0 ? 2 : 3)   // 왼쪽 : 오른쪽
                : (delta.y > 0 ? 0 : 1);  // 위 : 아래

            selectedIndex = distance >= MinSelectDistance && distance <= MaxSelectDistance && index < Candidates.Count
                ? index
                : -1;
            Owner.HighlightContested(selectedIndex);
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnEndDrag(PointerEventData e) => Release();

        void Release()
        {
            if (finished) return;
            finished = true;
            if (holdRoutine != null) { StopCoroutine(holdRoutine); holdRoutine = null; }

            if (revealed && selectedIndex >= 0 && selectedIndex < Candidates.Count)
                Owner.CommitContested(Candidates[selectedIndex]);
            else if (revealed)
                Owner.CancelContestedReveal(); // 펼쳤는데 방향을 못 골랐다 — 마커는 남기고 펼친 것만 걷는다
            // revealed==false(꾹 누르는 시간을 못 채우고 뗌)면 애초에 편 게 없으니 지울 것도 없다.
        }

        void Cancel()
        {
            finished = true;
            if (holdRoutine != null) { StopCoroutine(holdRoutine); holdRoutine = null; }
            if (revealed) Owner.CancelContestedReveal();
        }
    }
}
