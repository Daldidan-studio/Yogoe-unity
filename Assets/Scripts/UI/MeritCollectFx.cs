using UnityEngine;
using UnityEngine.UI;

namespace Yoegoe.UI
{
    /// <summary>
    /// 일괄 수거용 간단 Scatter → Hover → Gather 연출 (DOTween 없이).
    /// </summary>
    public static class MeritCollectFx
    {
        private const int ParticleCount = 12;
        private const float ScatterRadius = 90f;
        private const float ScatterDuration = 0.28f;
        private const float HoverDuration = 0.14f;
        private const float GatherDuration = 0.42f;
        private const float Stagger = 0.045f;

        public static void Play(
            Canvas canvas,
            RectTransform from,
            RectTransform to,
            Font font)
        {
            if (canvas == null || from == null || to == null) return;

            var host = new GameObject("MeritCollectFx");
            var hostRt = host.AddComponent<RectTransform>();
            hostRt.SetParent(canvas.transform, false);
            hostRt.anchorMin = Vector2.zero;
            hostRt.anchorMax = Vector2.one;
            hostRt.offsetMin = Vector2.zero;
            hostRt.offsetMax = Vector2.zero;
            hostRt.SetAsLastSibling();

            var runner = host.AddComponent<Runner>();
            runner.Begin(canvas, from, to, font);
        }

        private sealed class Runner : MonoBehaviour
        {
            private struct Shard
            {
                public RectTransform Rt;
                public Vector2 Origin;
                public Vector2 ScatterPos;
                public float Delay;
                public float Life;
                public bool Arrived;
            }

            private Shard[] shards;
            private RectTransform target;
            private Vector2 gatherPos;
            private float punchT = -1f;
            private Vector3 punchBaseScale = Vector3.one;
            private float totalEnd;

            public void Begin(Canvas canvas, RectTransform from, RectTransform to, Font font)
            {
                target = to;
                punchBaseScale = to.localScale;
                gatherPos = WorldToCanvasLocal(canvas, to.TransformPoint(to.rect.center));
                Vector2 origin = WorldToCanvasLocal(canvas, from.TransformPoint(from.rect.center));

                shards = new Shard[ParticleCount];
                for (int i = 0; i < ParticleCount; i++)
                {
                    float angle = (Mathf.PI * 2f * i) / ParticleCount + Random.Range(-0.2f, 0.2f);
                    float radius = ScatterRadius * Random.Range(0.55f, 1.1f);
                    Vector2 scatter = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    // 살짝 위로 치우쳐 튕기게
                    scatter.y += Random.Range(10f, 40f);

                    var go = new GameObject("Shard_" + i);
                    var rt = go.AddComponent<RectTransform>();
                    rt.SetParent(transform, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = origin;
                    float size = Random.Range(28f, 40f);
                    rt.sizeDelta = new Vector2(size, size);

                    var label = go.AddComponent<Text>();
                    label.font = font != null
                        ? font
                        : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.fontSize = Random.Range(22, 30);
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = new Color(1f, 0.88f + Random.Range(0f, 0.1f), 0.35f, 1f);
                    label.text = "◆";
                    label.raycastTarget = false;

                    shards[i] = new Shard
                    {
                        Rt = rt,
                        Origin = origin,
                        ScatterPos = scatter,
                        Delay = i * Stagger,
                        Life = 0f,
                        Arrived = false
                    };
                }

                totalEnd = (ParticleCount - 1) * Stagger + ScatterDuration + HoverDuration + GatherDuration + 0.2f;
            }

            private void Update()
            {
                if (shards == null) return;

                float dt = Time.unscaledDeltaTime;
                bool anyAlive = false;

                for (int i = 0; i < shards.Length; i++)
                {
                    var s = shards[i];
                    if (s.Rt == null) continue;

                    s.Life += dt;
                    float t = s.Life - s.Delay;
                    if (t < 0f)
                    {
                        shards[i] = s;
                        anyAlive = true;
                        continue;
                    }

                    if (t < ScatterDuration)
                    {
                        float u = EaseOutQuad(t / ScatterDuration);
                        s.Rt.anchoredPosition = Vector2.LerpUnclamped(s.Origin, s.ScatterPos, u);
                        float pop = Mathf.Lerp(0.4f, 1f, u);
                        s.Rt.localScale = Vector3.one * pop;
                        anyAlive = true;
                    }
                    else if (t < ScatterDuration + HoverDuration)
                    {
                        // 부유: 살짝 위아래로
                        float hoverT = (t - ScatterDuration) / HoverDuration;
                        float bob = Mathf.Sin(hoverT * Mathf.PI) * 6f;
                        s.Rt.anchoredPosition = s.ScatterPos + new Vector2(0f, bob);
                        anyAlive = true;
                    }
                    else if (t < ScatterDuration + HoverDuration + GatherDuration)
                    {
                        float u = (t - ScatterDuration - HoverDuration) / GatherDuration;
                        float e = EaseInBack(u);
                        // 바깥으로 살짝 휘어 들어오는 S-curve 컨트롤
                        Vector2 mid = s.ScatterPos + (gatherPos - s.ScatterPos) * 0.35f
                                      + Perp(gatherPos - s.ScatterPos).normalized * (i % 2 == 0 ? 40f : -40f);
                        s.Rt.anchoredPosition = QuadBezier(s.ScatterPos, mid, gatherPos, e);
                        float shrink = Mathf.Lerp(1f, 0.35f, u);
                        s.Rt.localScale = Vector3.one * shrink;
                        anyAlive = true;
                    }
                    else if (!s.Arrived)
                    {
                        s.Arrived = true;
                        punchT = 0f;
                        Destroy(s.Rt.gameObject);
                        s.Rt = null;
                    }

                    shards[i] = s;
                    if (s.Rt != null) anyAlive = true;
                }

                if (punchT >= 0f && target != null)
                {
                    punchT += dt;
                    const float punchDur = 0.18f;
                    float p = Mathf.Clamp01(punchT / punchDur);
                    // 두근거림
                    float amp = (1f - p) * 0.18f;
                    float scale = 1f + Mathf.Sin(p * Mathf.PI) * amp;
                    target.localScale = punchBaseScale * scale;
                    if (p >= 1f)
                    {
                        target.localScale = punchBaseScale;
                        punchT = -1f;
                    }
                }

                totalEnd -= dt;
                if (!anyAlive && punchT < 0f && totalEnd <= 0f)
                {
                    if (target != null) target.localScale = punchBaseScale;
                    Destroy(gameObject);
                }
            }

            private static Vector2 WorldToCanvasLocal(Canvas canvas, Vector3 world)
            {
                var canvasRt = canvas.transform as RectTransform;
                Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, screen, cam, out Vector2 local);
                return local;
            }

            private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

            private static float EaseInBack(float t)
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                return c3 * t * t * t - c1 * t * t;
            }

            private static Vector2 QuadBezier(Vector2 a, Vector2 b, Vector2 c, float t)
            {
                float u = 1f - t;
                return u * u * a + 2f * u * t * b + t * t * c;
            }

            private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);
        }
    }
}
