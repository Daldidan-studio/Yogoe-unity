namespace KSpirits.Minigames.Yut
{
    /// <summary>윷 던지기 결과값. 정수는 이동 칸 수(빽도는 -1).</summary>
    public enum YutThrowResult
    {
        Baekdo = -1,
        Do = 1,
        Gae = 2,
        Geol = 3,
        Yut = 4,
        Mo = 5,
    }

    /// <summary>
    /// 던지기 결과에 따라 말이 29발 윷판 위에서 실제로 지나가는 노드 id 경로를 계산.
    /// YutBoardLayout(좌표)과 짝을 이루는 순수 로직 — 화면 렌더링과 무관.
    /// 모(5)/뒷모(10)를 지나면 대각선 지름길로 자동 진입하고, 방(22)에서는 들어온
    /// 대각선의 반대쪽으로 계속 진행한다. 빽도(뒤로 1칸)는 바깥 둘레 기준으로만
    /// 지원한다 — 대각선 위에서 빽도를 맞는 경우는 실전에서 거의 없어 제자리로 둔다.
    /// </summary>
    public static class YutMoveResolver
    {
        /// <summary>시작 노드부터 결과만큼 이동한 노드 id 경로(시작점 포함, MoveYutPiece에 그대로 전달 가능).</summary>
        public static int[] GetPath(int fromNode, YutThrowResult result)
        {
            int steps = (int)result;

            if (steps < 0)
            {
                int back = fromNode <= 19 ? (fromNode + 19) % 20 : fromNode;
                return new[] { fromNode, back };
            }

            var path = new int[steps + 1];
            path[0] = fromNode;
            int current = fromNode;
            int previous = -1;
            for (int i = 0; i < steps; i++)
            {
                int next = NextNode(current, previous);
                path[i + 1] = next;
                previous = current;
                current = next;
            }
            return path;
        }

        static int NextNode(int current, int previous)
        {
            switch (current)
            {
                case YutBoardLayout.Mo: return 20;         // 모 → 대각선 A 진입
                case YutBoardLayout.DwitMo: return 25;      // 뒷모 → 대각선 B 진입
                case YutBoardLayout.Bang:                   // 방 → 들어온 대각선 반대쪽으로
                    return previous == 21 ? 23 : 27;
                case 20: return 21;
                case 21: return 22;
                case 23: return 24;
                case 24: return YutBoardLayout.JjiMo;
                case 25: return 26;
                case 26: return 22;
                case 27: return 28;
                case 28: return YutBoardLayout.Start;
                default: return (current + 1) % 20;        // 바깥 둘레 순환
            }
        }
    }
}
