using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yoegoe.Data;

namespace Yoegoe.UI
{
    /// <summary>
    /// 상세화면 공양물/정화수 드래그. 초상 위에 놓으면 DetailScreen이 급여 처리.
    /// </summary>
    public class OfferingDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public DetailScreen screen;
        public OfferingData offering;
        public bool isPurifiedWater;
        public Sprite dragIcon;

        Canvas rootCanvas;
        RectTransform ghostRt;
        Image ghostImage;
        CanvasGroup sourceGroup;

        public void Configure(DetailScreen owner, OfferingData data, bool purified, Sprite icon)
        {
            screen = owner;
            offering = data;
            isPurifiedWater = purified;
            dragIcon = icon;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (screen == null) return;
            if (!isPurifiedWater && offering == null) return;

            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null) return;

            sourceGroup = gameObject.GetComponent<CanvasGroup>();
            if (sourceGroup == null) sourceGroup = gameObject.AddComponent<CanvasGroup>();
            sourceGroup.blocksRaycasts = false;
            sourceGroup.alpha = 0.55f;

            var ghost = new GameObject("DragGhost");
            ghost.transform.SetParent(rootCanvas.transform, false);
            ghostRt = ghost.AddComponent<RectTransform>();
            ghostRt.sizeDelta = new Vector2(72f, 72f);
            ghostImage = ghost.AddComponent<Image>();
            ghostImage.sprite = dragIcon;
            ghostImage.preserveAspect = true;
            ghostImage.raycastTarget = false;
            if (dragIcon == null)
                ghostImage.color = new Color(0.45f, 0.7f, 1f, 0.9f);

            var cg = ghost.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 0.92f;

            ghostRt.position = eventData.position;
            screen.NotifyOfferingDragBegan();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ghostRt == null) return;
            ghostRt.position = eventData.position;
            screen?.NotifyOfferingDragMoved(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (sourceGroup != null)
            {
                sourceGroup.blocksRaycasts = true;
                sourceGroup.alpha = 1f;
            }

            bool dropped = screen != null && screen.TryAcceptOfferingDrop(eventData.position, offering, isPurifiedWater);
            if (ghostRt != null)
                Destroy(ghostRt.gameObject);
            ghostRt = null;
            ghostImage = null;

            screen?.NotifyOfferingDragEnded(dropped);
        }
    }
}
