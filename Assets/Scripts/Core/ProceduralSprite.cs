using System;
using UnityEngine;

namespace Yoegoe.Core
{
    /// <summary>
    /// 아트가 나오기 전 임시 아이콘을 코드로 그리는 도우미 (말풍선 ♥/💢, 드래그 ▼ 등).
    /// 픽셀 좌표(좌하단 원점, y 위)를 받아 색을 돌려주는 함수로 그리고 2x2 슈퍼샘플로 가장자리를 다듬는다.
    /// </summary>
    public static class ProceduralSprite
    {
        public static Sprite Build(string name, int size, float pixelsPerUnit, Func<Vector2, Color> sample)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        var c = sample(new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f));
                        acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                    acc /= 4f;
                    px[y * size + x] = acc.a > 0.0001f
                        ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a)
                        : Color.clear;
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        /// <summary>삼각형 부호 거리(내부 음수).</summary>
        public static float SdTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float sgn = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float dx = Mathf.Min(Mathf.Min(pq0.sqrMagnitude, pq1.sqrMagnitude), pq2.sqrMagnitude);
            float dy = Mathf.Min(Mathf.Min(
                    sgn * (v0.x * e0.y - v0.y * e0.x),
                    sgn * (v1.x * e1.y - v1.y * e1.x)),
                sgn * (v2.x * e2.y - v2.y * e2.x));
            return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
        }
    }
}
