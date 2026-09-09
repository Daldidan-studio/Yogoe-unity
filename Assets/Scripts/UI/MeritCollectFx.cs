using UnityEngine;

namespace Yoegoe.UI
{
    /// <summary>
    /// 공덕 수거 Scatter → Hover(시차) → Gather. 월드 스프라이트만 사용 (HUD Canvas 미사용).
    /// UI Image를 캔버스에 붙이면 전체 캔버스 리빌드가 일어나 프레임이 끊길 수 있어 분리함.
    /// </summary>
    public static class MeritCollectFx
    {
        private const int ItemCount = 12;
        private const float ScatterRadius = 1.35f;
        private const float ScatterDuration = 0.35f;
        private const float BaseHoverDuration = 0.1f;
        private const float FlyDuration = 0.45f;
        private const float StaggerInterval = 0.04f;
        private const float PunchAmp = 0.12f;
        private const float PunchDur = 0.12f;
        private const float HardLifetimeSeconds = 4f;

        private static Sprite s_PetalSprite;
        private static Texture2D s_PetalTex;

        public static void Play(Canvas canvas, RectTransform from, RectTransform to, Font font = null)
        {
            if (from == null || to == null) return;
            Vector3 origin = UiToWorld(from);
            Vector3 gather = UiToWorld(to);
            Spawn(origin, gather, to);
        }

        public static void PlayFromWorld(Canvas canvas, Vector3 worldPos, RectTransform to, Camera worldCam = null)
        {
            if (to == null) return;
            Vector3 gather = UiToWorld(to, worldCam);
            Spawn(worldPos, gather, to);
        }

        private static void Spawn(Vector3 origin, Vector3 gather, RectTransform punchTarget)
        {
            var host = new GameObject("MeritCollectFx_World");
            host.AddComponent<Runner>().Begin(origin, gather, punchTarget);
        }

        private static Vector3 UiToWorld(RectTransform ui, Camera worldCam = null)
        {
            if (worldCam == null) worldCam = Camera.main;
            if (ui == null || worldCam == null) return Vector3.zero;

            Canvas canvas = ui.GetComponentInParent<Canvas>();
            Camera uiCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCam = canvas.worldCamera;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, ui.TransformPoint(ui.rect.center));
            float z = Mathf.Abs(worldCam.transform.position.z);
            Vector3 w = worldCam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
            w.z = 0f;
            return w;
        }

        private static Sprite PetalSprite()
        {
            if (s_PetalSprite != null) return s_PetalSprite;
            s_PetalTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            s_PetalTex.filterMode = FilterMode.Bilinear;
            s_PetalTex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                float nx = (x + 0.5f) / 8f * 2f - 1f;
                float ny = (y + 0.5f) / 8f * 2f - 1f;
                // 길쭉한 타원 꽃잎
                float v = (nx * nx) / 0.35f + (ny * ny) / 1f;
                pixels[y * 8 + x] = v <= 1f
                    ? new Color(1f, 1f, 1f, 1f - v * 0.35f)
                    : new Color(1f, 1f, 1f, 0f);
            }
            s_PetalTex.SetPixels(pixels);
            s_PetalTex.Apply(false, true);
            s_PetalSprite = Sprite.Create(s_PetalTex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32f);
            return s_PetalSprite;
        }

        private sealed class Runner : MonoBehaviour
        {
            private struct Petal
            {
                public Transform Tr;
                public SpriteRenderer Sr;
                public Vector3 Origin;
                public Vector3 ScatterPos;
                public float HoverExtra;
                public float Spin;
                public float SpinSpeed;
                public float Life;
                public bool Arrived;
                public Color BaseColor;
            }

            private Petal[] petals;
            private RectTransform punchTarget;
            private Vector3 gatherPos;
            private Vector3 punchBaseScale = Vector3.one;
            private float punchT = -1f;
            private float hardLife;

            public void Begin(Vector3 origin, Vector3 gather, RectTransform punch)
            {
                punchTarget = punch;
                gatherPos = gather;
                hardLife = HardLifetimeSeconds;
                if (punchTarget != null) punchBaseScale = punchTarget.localScale;

                var sprite = PetalSprite();
                petals = new Petal[ItemCount];

                for (int i = 0; i < ItemCount; i++)
                {
                    Vector2 dir = Random.insideUnitCircle;
                    if (dir.sqrMagnitude < 0.05f) dir = Random.insideUnitCircle.normalized;
                    Vector3 scatter = origin + (Vector3)(dir * (ScatterRadius * Random.Range(0.45f, 1f)));
                    scatter.y += Random.Range(0.1f, 0.45f);
                    scatter.z = 0f;

                    var go = new GameObject("Petal_" + i);
                    go.transform.SetParent(transform, false);
                    go.transform.position = origin;
                    float s = Random.Range(0.22f, 0.34f);
                    go.transform.localScale = new Vector3(s * 0.55f, s, 1f);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = 650;
                    Color c = Color.Lerp(
                        new Color(1f, 0.72f, 0.78f, 0.95f),
                        new Color(1f, 0.88f, 0.55f, 0.95f),
                        Random.value);
                    sr.color = c;

                    petals[i] = new Petal
                    {
                        Tr = go.transform,
                        Sr = sr,
                        Origin = origin,
                        ScatterPos = scatter,
                        HoverExtra = i * StaggerInterval,
                        Spin = Random.Range(0f, 360f),
                        SpinSpeed = Random.Range(-180f, 180f),
                        BaseColor = c
                    };
                }
            }

            private void OnDestroy()
            {
                if (punchTarget != null)
                    punchTarget.localScale = punchBaseScale;
            }

            private void Update()
            {
                if (petals == null)
                {
                    Destroy(gameObject);
                    return;
                }

                float dt = Time.unscaledDeltaTime;
                hardLife -= dt;
                if (hardLife <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                bool anyAlive = false;
                for (int i = 0; i < petals.Length; i++)
                {
                    var p = petals[i];
                    if (p.Tr == null) continue;

                    p.Life += dt;
                    float hoverEnd = ScatterDuration + BaseHoverDuration + p.HoverExtra;
                    float flyEnd = hoverEnd + FlyDuration;

                    if (p.Life < ScatterDuration)
                    {
                        float u = EaseOutQuad(p.Life / ScatterDuration);
                        p.Tr.position = Vector3.LerpUnclamped(p.Origin, p.ScatterPos, u);
                        float pop = Mathf.Lerp(0.4f, 1f, u);
                        Vector3 baseScale = new Vector3(0.55f, 1f, 1f) * Mathf.Lerp(0.22f, 0.3f, pop);
                        p.Tr.localScale = baseScale * pop;
                        p.Spin += p.SpinSpeed * dt * 0.35f;
                        p.Tr.localRotation = Quaternion.Euler(0f, 0f, p.Spin);
                        anyAlive = true;
                    }
                    else if (p.Life < hoverEnd)
                    {
                        float hoverT = p.Life - ScatterDuration;
                        float bob = Mathf.Sin(hoverT * 7f + i) * 0.05f;
                        float sway = Mathf.Cos(hoverT * 5f + i * 0.7f) * 0.04f;
                        p.Tr.position = p.ScatterPos + new Vector3(sway, bob, 0f);
                        p.Spin += p.SpinSpeed * dt * 0.2f;
                        p.Tr.localRotation = Quaternion.Euler(0f, 0f, p.Spin);
                        anyAlive = true;
                    }
                    else if (p.Life < flyEnd)
                    {
                        float u = (p.Life - hoverEnd) / FlyDuration;
                        float e = EaseInBack(Mathf.Clamp01(u));
                        Vector3 delta = gatherPos - p.ScatterPos;
                        Vector3 mid = p.ScatterPos + delta * 0.4f
                                      + new Vector3(-delta.y, delta.x, 0f).normalized
                                      * ((i % 2 == 0 ? 1f : -1f) * 0.55f);
                        p.Tr.position = QuadBezier(p.ScatterPos, mid, gatherPos, e);
                        float shrink = Mathf.Lerp(1f, 0.2f, u * u);
                        p.Tr.localScale = new Vector3(0.55f, 1f, 1f) * (0.28f * shrink);
                        float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                        p.Spin = Mathf.LerpAngle(p.Spin, ang, u);
                        p.Tr.localRotation = Quaternion.Euler(0f, 0f, p.Spin);
                        if (p.Sr != null)
                        {
                            var c = p.BaseColor;
                            c.a = Mathf.Lerp(p.BaseColor.a, 0.4f, u);
                            p.Sr.color = c;
                        }
                        anyAlive = true;
                    }
                    else if (!p.Arrived)
                    {
                        p.Arrived = true;
                        punchT = 0f;
                        Destroy(p.Tr.gameObject);
                        p.Tr = null;
                        p.Sr = null;
                    }

                    petals[i] = p;
                    if (p.Tr != null) anyAlive = true;
                }

                if (punchT >= 0f && punchTarget != null)
                {
                    punchT += dt;
                    float t = Mathf.Clamp01(punchT / PunchDur);
                    float scale = 1f + Mathf.Sin(t * Mathf.PI) * PunchAmp * (1f - t * 0.3f);
                    punchTarget.localScale = punchBaseScale * scale;
                    if (t >= 1f)
                    {
                        punchTarget.localScale = punchBaseScale;
                        punchT = -1f;
                    }
                }

                if (!anyAlive && punchT < 0f)
                    Destroy(gameObject);
            }

            private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

            private static float EaseInBack(float t)
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                return c3 * t * t * t - c1 * t * t;
            }

            private static Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
            {
                float u = 1f - t;
                return u * u * a + 2f * u * t * b + t * t * c;
            }
        }
    }
}
