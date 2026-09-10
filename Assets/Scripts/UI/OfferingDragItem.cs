using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.UI
{
    /// <summary>
    /// 상세화면 공양물/정화수 드래그. 본문(초상·정보) 위에 놓으면 DetailScreen이 급여 처리.
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
        bool dragActive;

        public void Configure(DetailScreen owner, OfferingData data, bool purified, Sprite icon)
        {
            screen = owner;
            offering = data;
            isPurifiedWater = purified;
            dragIcon = icon;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragActive = false;
            if (screen == null) return;
            if (!isPurifiedWater && offering == null) return;

            if (isPurifiedWater)
            {
                if (GameEconomy.Instance == null || GameEconomy.Instance.PurifiedWater < 1)
                {
                    screen.NotifyFeedBlocked("정화수가 없어요");
                    return;
                }
            }
            else if (GameEconomy.Instance == null
                     || GameEconomy.Instance.GetOfferingCount(offering) < 1)
            {
                screen.NotifyFeedBlocked("공양물이 없어요");
                return;
            }

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
            dragActive = true;
            screen.NotifyOfferingDragBegan();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragActive || ghostRt == null) return;
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

            bool dropped = false;
            if (dragActive && screen != null)
                dropped = screen.TryAcceptOfferingDrop(eventData.position, offering, isPurifiedWater);

            if (ghostRt != null)
                Destroy(ghostRt.gameObject);
            ghostRt = null;
            ghostImage = null;
            dragActive = false;

            screen?.NotifyOfferingDragEnded(dropped);
        }
    }
}
