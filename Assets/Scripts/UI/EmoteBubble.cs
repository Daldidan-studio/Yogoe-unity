using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Core;

namespace Yoegoe.UI
{
    /// <summary>
    /// 상세화면 공양 드래그 힌트 말풍선 — 선호면 ♥(기쁨), 아니면 💢(싫음).
    /// 표정 초상 아트가 나오기 전 임시 표현. 스프라이트는 ProceduralSprite로 그려 캐시한다(폰트에 이모지 글리프가 없어서).
    /// </summary>
    public class EmoteBubble : MonoBehaviour
    {
        public enum Kind { Happy, Dislike }

        const int TexSize = 128;
        const float PopDuration = 0.18f;

        static Sprite happySprite;
        static Sprite dislikeSprite;

        Image image;
        RectTransform rt;
        Kind currentKind;
        float shownAt = -1f;

        /// <summary>anchor(초상 패널) 우상단 안쪽에 비활성 상태로 만든다.</summary>
        public static EmoteBubble Create(RectTransform anchor, float size = 96f)
        {
            var go = new GameObject("EmoteBubble", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(anchor, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var bubble = go.AddComponent<EmoteBubble>();
            bubble.image = img;
            bubble.rt = rt;
            go.SetActive(false);
            return bubble;
        }

        public void Show(Kind kind)
        {
            if (gameObject.activeSelf && currentKind == kind) return;
            currentKind = kind;
            image.sprite = kind == Kind.Happy ? GetHappySprite() : GetDislikeSprite();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
            rt.localScale = Vector3.zero;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Update()
        {
            float t = Time.unscaledTime - shownAt;
            float scale;
            if (t < PopDuration)
            {
                // 0 → 1.15 → 1 튀어나옴
                float u = t / PopDuration;
                scale = u < 0.7f ? Mathf.Lerp(0f, 1.15f, u / 0.7f) : Mathf.Lerp(1.15f, 1f, (u - 0.7f) / 0.3f);
            }
            else
            {
                scale = 1f + Mathf.Sin((t - PopDuration) * 6f) * 0.04f;
            }
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        // ---------------- 절차 스프라이트 ----------------

        static readonly Color Outline = new Color(0.24f, 0.2f, 0.2f, 1f);
        static readonly Color Fill = Color.white;
        static readonly Color HeartRed = new Color(0.93f, 0.27f, 0.38f, 1f);
        static readonly Color AngerRed = new Color(0.86f, 0.16f, 0.14f, 1f);

        static Sprite GetHappySprite() =>
            happySprite != null ? happySprite : (happySprite = BuildSprite("Emote_Happy", Kind.Happy));

        static Sprite GetDislikeSprite() =>
            dislikeSprite != null ? dislikeSprite : (dislikeSprite = BuildSprite("Emote_Dislike", Kind.Dislike));

        static Sprite BuildSprite(string name, Kind kind) =>
            ProceduralSprite.Build(name, TexSize, 100f, p => Sample(p, kind));

        static readonly Vector2 BubbleCenter = new Vector2(68f, 70f);
        const float BubbleRadius = 50f;
        const float OutlineWidth = 4f;

        static Color Sample(Vector2 p, Kind kind)
        {
            // 원 + 좌하단 꼬리(초상 쪽을 가리킴)
            float circle = (p - BubbleCenter).magnitude - BubbleRadius;
            float tail = ProceduralSprite.SdTriangle(p, new Vector2(30f, 52f), new Vector2(54f, 28f), new Vector2(8f, 6f));
            float d = Mathf.Min(circle, tail);
            if (d > 0f) return Color.clear;
            if (d > -OutlineWidth) return Outline;

            // 심볼: 풍선 중심 기준 정규화 좌표
            const float s = 28f;
            var q = (p - BubbleCenter) / s;
            if (kind == Kind.Happy)
            {
                float u = q.x * 1.05f, v = q.y + 0.15f;
                float a = u * u + v * v - 1f;
                if (a * a * a - u * u * v * v * v <= 0f) return HeartRed;
            }
            else if (InAngerMark(q * 0.7f))
            {
                return AngerRed;
            }
            return Fill;
        }

        /// <summary>💢 — 사분면마다 중심 쪽으로 볼록한 1/4 호 4개.</summary>
        static bool InAngerMark(Vector2 q)
        {
            const float offset = 0.7f, radius = 0.5f, halfWidth = 0.12f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? 1f : -1f;
                float sy = (i & 2) == 0 ? 1f : -1f;
                var c = new Vector2(sx * offset, sy * offset);
                var d = q - c;
                if (sx * d.x > 0f || sy * d.y > 0f) continue;
                if (Mathf.Abs(d.magnitude - radius) <= halfWidth) return true;
            }
            return false;
        }
    }
}
