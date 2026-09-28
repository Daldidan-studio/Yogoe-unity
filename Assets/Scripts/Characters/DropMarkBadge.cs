using UnityEngine;
using Yoegoe.Core;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 드래그로 내려놓은 요괴 머리 위 2초 딱지 — 바닥 (?) / 못 앉는 기물 (x).
    /// 아트 전 임시 스프라이트는 ProceduralSprite로 그린다.
    /// </summary>
    public static class DropMarkBadge
    {
        public enum Kind { Question, Cross }

        public const float WorldSize = 0.3f;
        const int TexSize = 64;

        static Sprite questionSprite;
        static Sprite crossSprite;

        public static Sprite Get(Kind kind)
        {
            if (kind == Kind.Question)
                return questionSprite != null ? questionSprite : (questionSprite = Build("DropMark_Question", kind));
            return crossSprite != null ? crossSprite : (crossSprite = Build("DropMark_Cross", kind));
        }

        static Sprite Build(string name, Kind kind) =>
            ProceduralSprite.Build(name, TexSize, TexSize / WorldSize, p => Sample(p, kind));

        static readonly Vector2 Center = new Vector2(32f, 32f);
        const float Radius = 28f;
        const float OutlineWidth = 4f;
        static readonly Color OutlineColor = new Color(0.24f, 0.2f, 0.2f, 1f);
        static readonly Color QuestionColor = new Color(0.24f, 0.35f, 0.63f, 1f);
        static readonly Color CrossColor = new Color(0.84f, 0.19f, 0.16f, 1f);

        static Color Sample(Vector2 p, Kind kind)
        {
            float d = (p - Center).magnitude - Radius;
            if (d > 0f) return Color.clear;
            if (d > -OutlineWidth) return OutlineColor;
            if (kind == Kind.Question && InQuestion(p)) return QuestionColor;
            if (kind == Kind.Cross && InCross(p)) return CrossColor;
            return Color.white;
        }

        /// <summary>? = 윗 고리(-170°~-50° 구간만 비움) + 줄기 + 점.</summary>
        static bool InQuestion(Vector2 p)
        {
            var hook = new Vector2(32f, 39f);
            const float hookR = 9f, half = 3.3f;
            var d = p - hook;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(d.magnitude - hookR) <= half && !(ang > -170f && ang < -50f)) return true;

            float rad = -50f * Mathf.Deg2Rad;
            var stemTop = hook + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * hookR;
            if (DistToSegment(p, stemTop, new Vector2(32f, 23f)) <= half) return true;

            return (p - new Vector2(32f, 14f)).magnitude <= 3.8f;
        }

        static bool InCross(Vector2 p) =>
            DistToSegment(p, new Vector2(21f, 21f), new Vector2(43f, 43f)) <= 4f
            || DistToSegment(p, new Vector2(21f, 43f), new Vector2(43f, 21f)) <= 4f;

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return (p - (a + ab * t)).magnitude;
        }
    }
}
