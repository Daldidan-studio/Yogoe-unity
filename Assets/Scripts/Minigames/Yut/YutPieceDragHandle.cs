using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 요괴 말 아이콘을 누르고 후보 칸까지 드래그해서 이동시키는 핸들 — 탭도 여전히 되지만
    /// (후보 마커 자체가 버튼이라) 드래그로도 같은 후보를 고를 수 있게 한다. 후보가 아닌
    /// 자리에 놓으면 YutMiniGame.ResolveDrop이 매칭되는 후보를 못 찾아서 그냥 무시된다.
    /// </summary>
    public class YutPieceDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public YutMiniGame Owner;
        public string PieceId;

        Canvas rootCanvas;
        RectTransform ghostRt;
        bool dragActive;

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragActive = false;
            if (Owner == null) return;

            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null) return;

            var img = GetComponent<Image>();
            var ghost = new GameObject("DragGhost");
            ghost.transform.SetParent(rootCanvas.transform, false);
            ghostRt = ghost.AddComponent<RectTransform>();
            ghostRt.sizeDelta = new Vector2(80f, 80f);
            var ghostImg = ghost.AddComponent<Image>();
            ghostImg.raycastTarget = false;
            ghostImg.preserveAspect = true;
            if (img != null && img.sprite != null)
            {
                ghostImg.sprite = img.sprite;
                ghostImg.color = Color.white;
            }
            else
            {
                ghostImg.color = img != null ? img.color : Color.white;
            }

            ghostRt.position = eventData.position;
            dragActive = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragActive || ghostRt == null) return;
            ghostRt.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ghostRt != null) Destroy(ghostRt.gameObject);
            ghostRt = null;

            if (dragActive) Owner?.ResolveDrop(PieceId, eventData.position);
            dragActive = false;
        }
    }
}
