using UnityEngine;

namespace KSpirits.Minigames.Yut
{
    /// <summary>
    /// 전통 윷판 29발 좌표(바깥 둘레 20 + 대각선 지름길 8 + 중앙 방 1).
    /// 원본 설계 좌표는 0~460 그리드(네 모서리를 5등분). 여기선 0~1로 정규화해서 보관.
    /// </summary>
    public static class YutBoardLayout
    {
        public const int NodeCount = 29;
        public const int Start = 0;   // 참(참먹이) - 출발/완주
        public const int Mo = 5;      // 모 - 대각선 A 진입
        public const int DwitMo = 10; // 뒷모 - 대각선 B 진입
        public const int JjiMo = 15;  // 찌모 - 모서리
        public const int Bang = 22;   // 방 - 중앙 교차점

        static readonly Vector2[] Points = BuildPoints();

        /// <summary>노드 id(0~28) → 보드 영역 기준 정규화 좌표(0~1, 좌하단 원점).</summary>
        public static Vector2 Normalized(int nodeId)
        {
            var p = Points[nodeId] / 460f;
            return new Vector2(p.x, 1f - p.y);
        }

        static Vector2[] BuildPoints()
        {
            var corner0 = new Vector2(460, 460);  // 참
            var corner5 = new Vector2(460, 40);   // 모
            var corner10 = new Vector2(40, 40);   // 뒷모
            var corner15 = new Vector2(40, 460);  // 찌모
            var center = new Vector2(250, 250);   // 방

            var p = new Vector2[NodeCount];
            p[0] = corner0;
            for (int i = 1; i <= 4; i++) p[i] = Vector2.Lerp(corner0, corner5, i / 5f);
            p[5] = corner5;
            for (int i = 6; i <= 9; i++) p[i] = Vector2.Lerp(corner5, corner10, (i - 5) / 5f);
            p[10] = corner10;
            for (int i = 11; i <= 14; i++) p[i] = Vector2.Lerp(corner10, corner15, (i - 10) / 5f);
            p[15] = corner15;
            for (int i = 16; i <= 19; i++) p[i] = Vector2.Lerp(corner15, corner0, (i - 15) / 5f);

            // 대각 A: 모(5) - 방(22) - 찌모(15)
            p[20] = Vector2.Lerp(corner5, center, 1f / 3f);
            p[21] = Vector2.Lerp(corner5, center, 2f / 3f);
            p[22] = center;
            p[23] = Vector2.Lerp(center, corner15, 1f / 3f);
            p[24] = Vector2.Lerp(center, corner15, 2f / 3f);

            // 대각 B: 뒷모(10) - 방(22) - 참(0)
            p[25] = Vector2.Lerp(corner10, center, 1f / 3f);
            p[26] = Vector2.Lerp(corner10, center, 2f / 3f);
            p[27] = Vector2.Lerp(center, corner0, 1f / 3f);
            p[28] = Vector2.Lerp(center, corner0, 2f / 3f);

            return p;
        }
    }
}
