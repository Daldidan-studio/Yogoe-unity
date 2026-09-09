using UnityEngine;

namespace Yoegoe.UI
{
    /// <summary>
    /// 공덕 수거: 꽃잎이 흩날리다가 시차로 빨려 들어가는 연출 (월드 스프라이트).
    /// </summary>
    public static class MeritCollectFx
    {
        private const int ItemCount = 18;
        private const float ScatterRadius = 2.4f;
        private const float ScatterDuration = 0.4f;
        private const float BaseHoverDuration = 0.28f;
        private const float FlyDuration = 0.55f;
        private const float StaggerInterval = 0.045f;
        private const float PunchAmp = 0.14f;
        private const float PunchDur = 0.14f;
        private const float HardLifetimeSeconds = 5f;
        private const float PetalWorldHeight = 0.55f; // 조금 작게 (기존 0.95)

        private static Sprite s_PetalSprite;
        private static Texture2D s_PetalTex;
        private static int s_PetalVersion;
        private const int PetalArtVersion = 4;

        public static void Play(Canvas canvas, RectTransform from, RectTransform to, Font font = null)
        {
            if (from == null || to == null) return;
            Spawn(UiToWorld(from), UiToWorld(to), to);
        }

        /// <summary>기물 탭 수거: 퍼지는 연출 없이 공덕바로 곧장 빠르게 날아간다.</summary>
        public static void PlayFromWorld(Canvas canvas, Vector3 worldPos, RectTransform to, Camera worldCam = null)
        {
            if (to == null) return;
            SpawnQuick(worldPos, UiToWorld(to, worldCam), to);
        }

        private static void Spawn(Vector3 origin, Vector3 gather, RectTransform punchTarget)
        {
            var host = new GameObject("MeritCollectFx_World");
            host.AddComponent<Runner>().Begin(origin, gather, punchTarget);
        }

        private const int QuickItemCount = 3;
        private const float QuickFlyDuration = 0.18f;
        private const float QuickStagger = 0.025f;

        private static void SpawnQuick(Vector3 origin, Vector3 gather, RectTransform punchTarget)
        {
            var host = new GameObject("MeritCollectFx_Quick");
            host.AddComponent<QuickRunner>().Begin(origin, gather, punchTarget);
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

        /// <summary>벚꽃/복숭아꽃 느낌의 물방울형 꽃잎 (고해상 프로시저).</summary>
        private static Sprite PetalSprite()
        {
            if (s_PetalSprite != null && s_PetalVersion == PetalArtVersion)
                return s_PetalSprite;

            if (s_PetalTex != null)
                Object.Destroy(s_PetalTex);
            s_PetalSprite = null;

            const int w = 48;
            const int h = 72;
            s_PetalTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            s_PetalTex.filterMode = FilterMode.Bilinear;
            s_PetalTex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 중심 원점, Y는 위가 뾰족한 꽃잎
                float nx = (x + 0.5f) / w * 2f - 1f;
                float ny = (y + 0.5f) / h * 2f - 1f; // -1 아래 ~ +1 위

                // 물방울: 위는 좁고 아래는 둥글게
                float widthAtY = 0.22f + 0.55f * Mathf.Pow(Mathf.Clamp01((ny + 1f) * 0.55f), 0.85f);
                float tip = Mathf.Clamp01((-ny + 0.15f) / 0.4f); // 위쪽 뾰족
                widthAtY *= Mathf.Lerp(1f, 0.15f, tip * tip);

                float rx = Mathf.Abs(nx) / Mathf.Max(0.08f, widthAtY);
                float inside = 1f - rx * rx;
                if (ny < -0.92f || inside <= 0f)
                {
                    pixels[y * w + x] = Color.clear;
                    continue;
                }

                // 가장자리 소프트 + 중앙 하이라이트
                float edge = Mathf.Clamp01(inside);
                float body = Mathf.SmoothStep(0f, 1f, edge);
                float vein = 1f - Mathf.Abs(nx) * 0.35f;
                float alpha = body * vein;
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }

            s_PetalTex.SetPixels(pixels);
            s_PetalTex.Apply(false, true);
            float ppu = h / PetalWorldHeight;
            s_PetalSprite = Sprite.Create(s_PetalTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.35f), ppu);
            s_PetalVersion = PetalArtVersion;
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
                public Vector3 BaseScale;
                public float HoverExtra;
                public float Spin;
                public float SpinSpeed;
                public float FlutterPhase;
                public float FlutterSpeed;
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
                    // 위로 치우친 폭발 + 원형 흩뿌림
                    float ang = (Mathf.PI * 2f * i) / ItemCount + Random.Range(-0.25f, 0.25f);
                    float radius = ScatterRadius * Random.Range(0.55f, 1.05f);
                    Vector3 scatter = origin + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius * 0.75f + Random.Range(0.35f, 1.1f), 0f);

                    var go = new GameObject("Petal_" + i);
                    go.transform.SetParent(transform, false);
                    go.transform.position = origin;

                    float size = Random.Range(0.55f, 0.8f);
                    Vector3 baseScale = new Vector3(size * Random.Range(0.7f, 0.95f), size, 1f);
                    go.transform.localScale = baseScale * 0.2f;
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = 650 + i;
                    // 분홍~살구~연한 보라 꽃잎
                    Color c = Color.HSVToRGB(
                        Random.Range(0.92f, 1.02f) % 1f,
                        Random.Range(0.35f, 0.65f),
                        Random.Range(0.95f, 1f));
                    c.a = 0.95f;
                    if (Random.value > 0.55f)
                        c = Color.Lerp(c, new Color(1f, 0.82f, 0.55f), Random.Range(0.2f, 0.5f));
                    sr.color = c;

                    petals[i] = new Petal
                    {
                        Tr = go.transform,
                        Sr = sr,
                        Origin = origin,
                        ScatterPos = scatter,
                        BaseScale = baseScale,
                        HoverExtra = i * StaggerInterval,
                        Spin = Random.Range(0f, 360f),
                        SpinSpeed = Random.Range(-320f, 320f),
                        FlutterPhase = Random.Range(0f, Mathf.PI * 2f),
                        FlutterSpeed = Random.Range(9f, 14f),
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
                    p.FlutterPhase += dt * p.FlutterSpeed;

                    float hoverEnd = ScatterDuration + BaseHoverDuration + p.HoverExtra;
                    float flyEnd = hoverEnd + FlyDuration;

                    if (p.Life < ScatterDuration)
                    {
                        float u = EaseOutBack(Mathf.Clamp01(p.Life / ScatterDuration));
                        p.Tr.position = Vector3.LerpUnclamped(p.Origin, p.ScatterPos, Mathf.Clamp01(u));
                        ApplyFlutterScale(ref p, Mathf.Lerp(0.25f, 1f, EaseOutQuad(p.Life / ScatterDuration)), 0.6f);
                        p.Spin += p.SpinSpeed * dt;
                        p.Tr.localRotation = Quaternion.Euler(0f, 0f, p.Spin);
                        anyAlive = true;
                    }
                    else if (p.Life < hoverEnd)
                    {
                        // 바람 타고 빙빙 — 눈에 띄게 휘날림
                        float hoverT = p.Life - ScatterDuration;
                        float bob = Mathf.Sin(p.FlutterPhase) * 0.18f;
                        float sway = Mathf.Cos(p.FlutterPhase * 0.7f + i) * 0.22f;
                        float drift = Mathf.Sin(hoverT * 1.8f + i * 0.4f) * 0.08f;
                        p.Tr.position = p.ScatterPos + new Vector3(sway + drift, bob, 0f);
                        ApplyFlutterScale(ref p, 1f, 1f);
                        p.Spin += p.SpinSpeed * dt * 0.55f;
                        p.Tr.localRotation = Quaternion.Euler(0f, 0f, p.Spin + Mathf.Sin(p.FlutterPhase) * 25f);
                        anyAlive = true;
                    }
                    else if (p.Life < flyEnd)
                    {
                        float u = Mathf.Clamp01((p.Life - hoverEnd) / FlyDuration);
                        float e = EaseInBack(u);
                        Vector3 delta = gatherPos - p.ScatterPos;
                        float side = (i % 2 == 0 ? 1f : -1f) * RandomSignStable(i) * Mathf.Lerp(1.1f, 0.3f, u);
                        Vector3 mid = p.ScatterPos + delta * 0.35f
                                      + new Vector3(-delta.y, delta.x, 0f).normalized * side;
                        // 날아가며 살짝 더 출렁
                        Vector3 pos = QuadBezier(p.ScatterPos, mid, gatherPos, e);
                        pos += new Vector3(
                            Mathf.Sin(p.FlutterPhase) * 0.12f * (1f - u),
                            Mathf.Cos(p.FlutterPhase * 1.1f) * 0.08f * (1f - u),
                            0f);
                        p.Tr.position = pos;

                        float shrink = Mathf.Lerp(1f, 0.35f, u * u);
                        ApplyFlutterScale(ref p, shrink, 1f - u * 0.5f);
                        float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                        p.Spin = Mathf.LerpAngle(p.Spin, ang, u * 0.85f);
                        p.Tr.localRotation = Quaternion.Euler(0f, 0f, p.Spin + Mathf.Sin(p.FlutterPhase) * 18f * (1f - u));

                        if (p.Sr != null)
                        {
                            var c = p.BaseColor;
                            c.a = Mathf.Lerp(p.BaseColor.a, 0.55f, u);
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

            private static void ApplyFlutterScale(ref Petal p, float sizeMul, float flutterAmount)
            {
                // X스케일 출렁 = 바람 따라 뒤집히는 꽃잎
                float flip = Mathf.Lerp(1f, 0.15f + Mathf.Abs(Mathf.Sin(p.FlutterPhase)) * 0.85f, flutterAmount);
                p.Tr.localScale = new Vector3(p.BaseScale.x * sizeMul * flip, p.BaseScale.y * sizeMul, 1f);
            }

            private static int RandomSignStable(int i) => (i * 37) % 2 == 0 ? 1 : -1;

            private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

            private static float EaseOutBack(float t)
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            }

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

        /// <summary>퍼짐·호버링 없이 시작점 → 공덕바로 곧장 빠르게 날아가는 단순 연출.</summary>
        private sealed class QuickRunner : MonoBehaviour
        {
            private struct Item
            {
                public Transform Tr;
                public SpriteRenderer Sr;
                public Vector3 Origin;
                public Vector3 BaseScale;
                public float StartDelay;
                public float Elapsed;
                public bool Arrived;
                public Color BaseColor;
            }

            private Item[] items;
            private RectTransform punchTarget;
            private Vector3 gatherPos;
            private Vector3 punchBaseScale = Vector3.one;
            private float punchT = -1f;
            private float hardLife;

            public void Begin(Vector3 origin, Vector3 gather, RectTransform punch)
            {
                punchTarget = punch;
                gatherPos = gather;
                hardLife = 2f;
                if (punchTarget != null) punchBaseScale = punchTarget.localScale;

                var sprite = PetalSprite();
                items = new Item[QuickItemCount];

                for (int i = 0; i < QuickItemCount; i++)
                {
                    var go = new GameObject("MeritQuick_" + i);
                    go.transform.SetParent(transform, false);
                    go.transform.position = origin;
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                    float size = Random.Range(0.4f, 0.55f);
                    Vector3 baseScale = new Vector3(size, size, 1f);
                    go.transform.localScale = baseScale;

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = 650 + i;
                    Color c = Color.HSVToRGB(Random.Range(0.92f, 1.02f) % 1f, Random.Range(0.35f, 0.6f), 1f);
                    c.a = 0.95f;
                    sr.color = c;

                    items[i] = new Item
                    {
                        Tr = go.transform,
                        Sr = sr,
                        Origin = origin,
                        BaseScale = baseScale,
                        StartDelay = i * QuickStagger,
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
                if (items == null)
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
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    if (it.Tr == null) continue;

                    it.Elapsed += dt;
                    float t = it.Elapsed - it.StartDelay;
                    if (t < 0f)
                    {
                        items[i] = it;
                        anyAlive = true;
                        continue;
                    }

                    float u = Mathf.Clamp01(t / QuickFlyDuration);
                    float e = 1f - (1f - u) * (1f - u); // 빠르게 출발해서 슉 도착
                    it.Tr.position = Vector3.LerpUnclamped(it.Origin, gatherPos, e);

                    float shrink = Mathf.Lerp(1f, 0.3f, u);
                    it.Tr.localScale = it.BaseScale * shrink;
                    if (it.Sr != null)
                    {
                        var c = it.BaseColor;
                        c.a = Mathf.Lerp(it.BaseColor.a, 0.15f, u * u);
                        it.Sr.color = c;
                    }

                    if (u >= 1f && !it.Arrived)
                    {
                        it.Arrived = true;
                        punchT = 0f;
                        Destroy(it.Tr.gameObject);
                        it.Tr = null;
                        it.Sr = null;
                    }
                    else
                    {
                        anyAlive = true;
                    }

                    items[i] = it;
                }

                if (punchT >= 0f && punchTarget != null)
                {
                    punchT += dt;
                    float pt = Mathf.Clamp01(punchT / PunchDur);
                    float scale = 1f + Mathf.Sin(pt * Mathf.PI) * PunchAmp * (1f - pt * 0.3f);
                    punchTarget.localScale = punchBaseScale * scale;
                    if (pt >= 1f)
                    {
                        punchTarget.localScale = punchBaseScale;
                        punchT = -1f;
                    }
                }

                if (!anyAlive && punchT < 0f)
                    Destroy(gameObject);
            }
        }
    }
}
