using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Core;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 요괴 드래그 중, 그 요괴가 지금 앉을 수 있는 기물 위에 작은 금색 ▼를 띄운다 (2프레임 톡톡).
    /// 아트 전 임시 스프라이트는 ProceduralSprite로 그린다.
    /// </summary>
    public class PropDragMarkers : MonoBehaviour
    {
        const float MarkerWorldSize = 0.28f;
        const float AbovePropPad = 0.12f;
        const float FrameSeconds = 0.3f;
        const float BobHeight = 0.05f;
        const int SortingOrder = 1500;
        const int TexSize = 64;

        static PropDragMarkers instance;
        static Sprite markerSprite;

        readonly List<(PropSlot prop, SpriteRenderer sr)> markers = new List<(PropSlot, SpriteRenderer)>();
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        float frameTimer;
        bool upFrame;

        public static void Show(CharacterAgent agent)
        {
            if (agent == null || PropManager.Instance == null) return;
            if (instance == null)
                instance = new GameObject("PropDragMarkers").AddComponent<PropDragMarkers>();
            instance.Rebuild(agent);
        }

        public static void Hide()
        {
            if (instance != null) instance.Clear();
        }

        void Rebuild(CharacterAgent agent)
        {
            Clear();
            foreach (var prop in PropManager.Instance.All)
            {
                if (prop == null || !prop.CanSitNow(agent)) continue;
                var sr = TakeRenderer();
                markers.Add((prop, sr));
            }
            frameTimer = 0f;
            upFrame = false;
            Place();
        }

        void Clear()
        {
            foreach (var (_, sr) in markers)
            {
                if (sr == null) continue;
                sr.gameObject.SetActive(false);
                pool.Add(sr);
            }
            markers.Clear();
        }

        SpriteRenderer TakeRenderer()
        {
            SpriteRenderer sr;
            if (pool.Count > 0)
            {
                sr = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
            }
            else
            {
                var go = new GameObject("DragMarker");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetSprite();
                sr.sortingOrder = SortingOrder;
            }
            sr.gameObject.SetActive(true);
            return sr;
        }

        void Update()
        {
            if (markers.Count == 0) return;
            frameTimer += Time.unscaledDeltaTime;
            if (frameTimer >= FrameSeconds)
            {
                frameTimer = 0f;
                upFrame = !upFrame;
            }
            Place();
        }

        void Place()
        {
            float bob = upFrame ? BobHeight : 0f;
            foreach (var (prop, sr) in markers)
            {
                if (prop == null || sr == null) continue;
                var p = prop.TopAnchorWorld(AbovePropPad + MarkerWorldSize * 0.5f);
                p.y += bob;
                sr.transform.position = p;
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ---------------- 임시 스프라이트: 금색 역삼각형 ----------------

        static readonly Vector2 TriLeft = new Vector2(8f, 54f);
        static readonly Vector2 TriRight = new Vector2(56f, 54f);
        static readonly Vector2 TriBottom = new Vector2(32f, 12f);
        static readonly Color OutlineColor = new Color(0.43f, 0.27f, 0.04f, 1f);

        static Sprite GetSprite() =>
            markerSprite != null
                ? markerSprite
                : (markerSprite = ProceduralSprite.Build("DragMarker_Triangle", TexSize, TexSize / MarkerWorldSize, Sample));

        static Color Sample(Vector2 p)
        {
            float d = ProceduralSprite.SdTriangle(p, TriLeft, TriRight, TriBottom);
            if (d > 0f) return Color.clear;
            if (d > -4f) return OutlineColor;
            // 아래 → 위로 밝아지는 금색
            float t = Mathf.Clamp01((p.y - TriBottom.y) / (TriLeft.y - TriBottom.y));
            return new Color(0.92f + 0.08f * t, 0.67f + 0.18f * t, 0.12f + 0.16f * t, 1f);
        }
    }
}
