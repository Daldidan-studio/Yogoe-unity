using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KSpirits.UI
{
    /// <summary>
    /// 정화수 슬롯을 드래그해 요괴 위에 놓으면 공양.
    /// </summary>
    public class DraggableOfferItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action OnDroppedOnYokai;

        Canvas _canvas;
        RectTransform _yokaiDropZone;
        RectTransform _ghost;
        Image _slotImage;
        Text _label;
        bool _enabled = true;
        bool _dragging;

        public void Setup(Canvas canvas, RectTransform yokaiDropZone)
        {
            _canvas = canvas;
            _yokaiDropZone = yokaiDropZone;
            _slotImage = GetComponent<Image>();
            _label = GetComponentInChildren<Text>();
        }

        public void SetInteractable(bool on)
        {
            _enabled = on;
            if (_slotImage != null)
                _slotImage.raycastTarget = on;
        }

        public void SetCountLabel(string text)
        {
            if (_label != null) _label.text = text;
        }

        public void SetHighlight(bool on)
        {
            if (_slotImage == null) return;
            _slotImage.color = on
                ? new Color(1f, 0.85f, 0.25f, 1f)
                : new Color(0.35f, 0.65f, 0.95f, 1f);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_enabled) return;
            _dragging = true;
            CreateGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _ghost == null) return;
            MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            bool hit = _yokaiDropZone != null &&
                       RectTransformUtility.RectangleContainsScreenPoint(
                           _yokaiDropZone, eventData.position, eventData.pressEventCamera);

            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
                _ghost = null;
            }

            if (hit)
                OnDroppedOnYokai?.Invoke();
        }

        void CreateGhost(PointerEventData eventData)
        {
            var go = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            go.transform.SetAsLastSibling();

            _ghost = go.GetComponent<RectTransform>();
            _ghost.sizeDelta = ((RectTransform)transform).rect.size;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.45f, 0.8f, 1f, 0.85f);
            img.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var t = labelGo.GetComponent<Text>();
            t.text = _label != null ? _label.text : "정화수";
            t.font = _label != null ? _label.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            MoveGhost(eventData);
        }

        void MoveGhost(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var local);
            _ghost.anchoredPosition = local;
        }
    }
}
