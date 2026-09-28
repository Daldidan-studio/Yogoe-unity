using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Core;

namespace Yoegoe.UI
{
    /// <summary>
    /// 친밀도 상승 시 하트가 하늘로 떠오르는 연출 (기획 5·11장).
    /// 개수 = floor(상승분). 소수만 오른 경우(윷 +0.25 단발)는 0개 — 누적 후 호출.
    /// </summary>
    public static class IntimacyHeartFx
    {
        const float WorldRise = 1.6f;
        const float WorldDuration = 1.15f;
        const float WorldScale = 0.42f;
        const float Stagger = 0.09f;
        const float SideSpread = 0.22f;

        const float UiRise = 110f;
        const float UiDuration = 1.05f;
        const float UiSize = 56f;

        static Sprite heartSprite;

        /// <summary>상승분 → 띄울 하트 수(반내림). 0이면 연출 없음.</summary>
        public static int HeartCountFromGain(float intimacyGain) =>
            intimacyGain <= 0f ? 0 : Mathf.FloorToInt(intimacyGain);

        public static void PlayFromAgent(CharacterAgent agent, float intimacyGain)
        {
            int n = HeartCountFromGain(intimacyGain);
            if (n <= 0 || agent == null) return;
            PlayWorld(HeadAnchor(agent), n);
        }

        public static void PlayWorld(Vector3 origin, int count)
        {
            if (count <= 0) return;
            var host = new GameObject("IntimacyHeartFx_World");
            host.AddComponent<WorldRunner>().Begin(origin, count);
        }

        /// <summary>상세화면 초상 위 — OfferingGainPopup과 같이 UI 캔버스에 띄운다.</summary>
        public static void PlayUi(Transform canvasRoot, RectTransform anchor, float intimacyGain)
        {
            int n = HeartCountFromGain(intimacyGain);
            if (n <= 0 || canvasRoot == null || anchor == null) return;
            var host = new GameObject("IntimacyHeartFx_UI");
            host.transform.SetParent(canvasRoot, false);
            host.AddComponent<UiRunner>().Begin(anchor, n);
        }

        static Vector3 HeadAnchor(CharacterAgent agent)
        {
            float top = 0.55f;
            var body = agent.GetComponentInChildren<SpriteRenderer>();
            if (body != null && body.sprite != null)
                top = body.bounds.extents.y + 0.15f;
            return agent.transform.position + Vector3.up * top;
        }

        static Sprite HeartSprite()
        {
            if (heartSprite != null) return heartSprite;
            heartSprite = ProceduralSprite.Build("IntimacyHeart", 96, 100f, SampleHeart);
            return heartSprite;
        }

        static readonly Color HeartRed = new Color(0.95f, 0.28f, 0.4f, 1f);
        static readonly Color HeartOutline = new Color(0.55f, 0.12f, 0.22f, 1f);

        static Color SampleHeart(Vector2 p)
        {
            // 중심 (48, 50), 정규화 후 classic heart 곡선
            var q = (p - new Vector2(48f, 50f)) / 36f;
            float u = q.x;
            float v = q.y + 0.12f;
            float a = u * u + v * v - 1f;
            bool inside = a * a * a - u * u * v * v * v <= 0f;
            if (!inside) return Color.clear;
            // 가장자리 살짝 어둡게
            float edge = a * a * a - u * u * v * v * v;
            if (edge > -0.08f) return HeartOutline;
            return HeartRed;
        }

        class WorldRunner : MonoBehaviour
        {
            public void Begin(Vector3 origin, int count)
            {
                StartCoroutine(Run(origin, Mathf.Min(count, 12)));
            }

            IEnumerator Run(Vector3 origin, int count)
            {
                var hearts = new SpriteRenderer[count];
                var starts = new Vector3[count];
                var drifts = new float[count];
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject("Heart");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = HeartSprite();
                    sr.sortingOrder = 1300;
                    float xOff = (i - (count - 1) * 0.5f) * SideSpread;
                    starts[i] = origin + new Vector3(xOff, 0.05f * i, 0f);
                    go.transform.position = starts[i];
                    go.transform.localScale = Vector3.one * WorldScale * 0.6f;
                    sr.color = new Color(1f, 1f, 1f, 0f);
                    hearts[i] = sr;
                    drifts[i] = Random.Range(-0.12f, 0.12f);
                }

                float t = 0f;
                float total = WorldDuration + Stagger * (count - 1);
                while (t < total)
                {
                    t += Time.unscaledDeltaTime;
                    for (int i = 0; i < count; i++)
                    {
                        float local = t - i * Stagger;
                        if (local < 0f || hearts[i] == null) continue;
                        float u = Mathf.Clamp01(local / WorldDuration);
                        float ease = 1f - (1f - u) * (1f - u);
                        float y = WorldRise * ease;
                        float x = drifts[i] * ease;
                        hearts[i].transform.position = starts[i] + new Vector3(x, y, 0f);
                        float scale = Mathf.Lerp(0.55f, 1.05f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.25f)));
                        if (u > 0.5f) scale = Mathf.Lerp(1.05f, 0.85f, (u - 0.5f) / 0.5f);
                        hearts[i].transform.localScale = Vector3.one * WorldScale * scale;
                        float alpha = u < 0.12f ? u / 0.12f : (u > 0.55f ? 1f - (u - 0.55f) / 0.45f : 1f);
                        var c = hearts[i].color;
                        c.a = Mathf.Clamp01(alpha);
                        hearts[i].color = c;
                    }
                    yield return null;
                }

                Destroy(gameObject);
            }
        }

        class UiRunner : MonoBehaviour
        {
            public void Begin(RectTransform anchor, int count)
            {
                StartCoroutine(Run(anchor, Mathf.Min(count, 12)));
            }

            IEnumerator Run(RectTransform anchor, int count)
            {
                var canvas = GetComponentInParent<Canvas>();
                Camera uiCam = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    uiCam = canvas.worldCamera;

                Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, anchor.TransformPoint(anchor.rect.center));
                var parentRt = transform.parent as RectTransform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screen, uiCam, out Vector2 local);

                var images = new Image[count];
                var starts = new Vector2[count];
                var rts = new RectTransform[count];
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject("Heart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(transform, false);
                    var rt = go.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(UiSize, UiSize);
                    float xOff = (i - (count - 1) * 0.5f) * (UiSize * 0.55f);
                    starts[i] = local + new Vector2(xOff, 20f + i * 4f);
                    rt.anchoredPosition = starts[i];
                    rt.localScale = Vector3.one * 0.5f;
                    var img = go.GetComponent<Image>();
                    img.sprite = HeartSprite();
                    img.raycastTarget = false;
                    img.preserveAspect = true;
                    img.color = new Color(1f, 1f, 1f, 0f);
                    images[i] = img;
                    rts[i] = rt;
                }

                float t = 0f;
                float total = UiDuration + Stagger * (count - 1);
                while (t < total)
                {
                    t += Time.unscaledDeltaTime;
                    for (int i = 0; i < count; i++)
                    {
                        float localT = t - i * Stagger;
                        if (localT < 0f || images[i] == null) continue;
                        float u = Mathf.Clamp01(localT / UiDuration);
                        float ease = 1f - (1f - u) * (1f - u);
                        rts[i].anchoredPosition = starts[i] + new Vector2(0f, UiRise * ease);
                        float scale = Mathf.Lerp(0.5f, 1.1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.2f)));
                        if (u > 0.4f) scale = Mathf.Lerp(1.1f, 0.9f, (u - 0.4f) / 0.6f);
                        rts[i].localScale = Vector3.one * scale;
                        float alpha = u < 0.12f ? u / 0.12f : (u > 0.55f ? 1f - (u - 0.55f) / 0.45f : 1f);
                        var c = images[i].color;
                        c.a = Mathf.Clamp01(alpha);
                        images[i].color = c;
                    }
                    yield return null;
                }

                Destroy(gameObject);
            }
        }
    }
}
