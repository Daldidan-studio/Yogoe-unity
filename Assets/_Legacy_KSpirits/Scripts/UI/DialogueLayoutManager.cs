using System.Collections.Generic;
using KSpirits.Core;
using KSpirits.Data;
using UnityEngine;

namespace KSpirits.UI
{
    /// <summary>
    /// DialogueAnchors 슬롯 좌표를 Dialogue 패널에 복사한다.
    /// </summary>
    public class DialogueLayoutManager : MonoBehaviour
    {
        [SerializeField] DialogueLayoutCatalog _catalog;
        [SerializeField] Transform _anchorRoot;

        readonly Dictionary<DialogueLayoutId, RectTransform> _anchors = new();

        void Awake()
        {
            CacheAnchors();
        }

        public void SetCatalog(DialogueLayoutCatalog catalog) => _catalog = catalog;

        public void CacheAnchors()
        {
            _anchors.Clear();
            var root = _anchorRoot != null ? _anchorRoot : transform.Find("DialogueAnchors");
            if (root == null) return;

            foreach (DialogueLayoutId id in System.Enum.GetValues(typeof(DialogueLayoutId)))
            {
                var child = root.Find(id.ToString()) as RectTransform;
                if (child != null)
                    _anchors[id] = child;
            }
        }

        public void ApplyForLine(DialogueLine line, string sectionId, RectTransform dialogueRect)
        {
            if (dialogueRect == null) return;

            var layoutId = _catalog != null
                ? _catalog.Resolve(line, sectionId)
                : DialogueLayoutId.BottomWide;

            Apply(layoutId, dialogueRect);
        }

        public void Apply(DialogueLayoutId layoutId, RectTransform dialogueRect)
        {
            if (dialogueRect == null) return;

            if (!_anchors.TryGetValue(layoutId, out var anchor) || anchor == null)
            {
                if (!_anchors.TryGetValue(DialogueLayoutId.BottomWide, out anchor) || anchor == null)
                    return;
            }

            CopyRectLayout(anchor, dialogueRect);
        }

        public static void CopyRectLayout(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.offsetMin = from.offsetMin;
            to.offsetMax = from.offsetMax;
            to.localRotation = from.localRotation;
            to.localScale = from.localScale;
        }
    }
}
