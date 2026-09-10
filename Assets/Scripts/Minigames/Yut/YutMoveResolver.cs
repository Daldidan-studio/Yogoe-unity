using System.Collections.Generic;

namespace Yoegoe.Minigames.Yut
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
    ///
    /// 지름길은 모(5)/뒷모(10)/방(22)에 "정확히 멈춰 있던" 말이 다음 던지기를 할 때만
    /// 첫 걸음으로 진입할 수 있다 — 그 칸을 그냥 "지나가는" 중에는 원래 바깥 둘레 길을 그대로
    /// 따라간다(기획서 7-3 지름길 분기 표 기준). takeShortcut로 실제로 그 갈림길을 탈지 유저가
    /// 고를 수 있게 한다(기본값 true) — 바깥길을 고르면 그 지점에서도 평범하게 다음 칸으로 진행.
    /// 방을 지나가기만 할 때는 들어온 대각선을 따라 반대쪽으로 계속 진행하고, 방에 멈춰
    /// 있었다면(그리고 지름길을 골랐다면) 다음 걸음은 참 쪽(27, 최단 완주)으로 나간다.
    ///
    /// 빽도(뒤로 1칸)는 PreviousNode로 지름길 포함 전체 노드에서 계산한다 — 방(22)처럼 두
    /// 대각선이 합류하는 지점만, 어느 쪽에서 왔는지 기억하지 않으므로 모(5) 쪽 대각선을
    /// 기본값으로 삼는다(실전에서 아주 드문 경우라 이 정도 단순화는 감안).
    /// </summary>
    public static class YutMoveResolver
    {
        /// <summary>시작 노드부터 결과만큼 이동한 노드 id 경로(시작점 포함, MoveYutPiece에 그대로 전달 가능).</summary>
        public static int[] GetPath(int fromNode, YutThrowResult result, bool takeShortcut = true)
        {
            int steps = (int)result;

            if (steps < 0)
                return new[] { fromNode, PreviousNode(fromNode) };

            var path = new List<int> { fromNode };
            int current = fromNode;
            int previous = -1;
            int remaining = steps;

            if (remaining > 0 && takeShortcut)
            {
                int shortcutEntry = current switch
                {
                    YutBoardLayout.Mo => 20,
                    YutBoardLayout.DwitMo => 25,
                    YutBoardLayout.Bang => 27,
                    _ => -1,
                };
                if (shortcutEntry >= 0)
                {
                    path.Add(shortcutEntry);
                    previous = current;
                    current = shortcutEntry;
                    remaining--;
                }
            }

            for (int i = 0; i < remaining; i++)
            {
                int next = NextNode(current, previous);
                path.Add(next);
                previous = current;
                current = next;
            }

            return path.ToArray();
        }

        /// <summary>모(5)/뒷모(10)/방(22)에 "정확히 멈춰 있는" 말만 지름길 갈림길을 고를 수 있다.</summary>
        public static bool IsForkNode(int nodeId) =>
            nodeId == YutBoardLayout.Mo || nodeId == YutBoardLayout.DwitMo || nodeId == YutBoardLayout.Bang;

        static int NextNode(int current, int previous)
        {
            switch (current)
            {
                case YutBoardLayout.Bang:                   // 방을 지나가는 중 → 들어온 대각선 반대쪽으로
                    return previous == 21 ? 23 : 27;
                case 20: return 21;
                case 21: return 22;
                case 23: return 24;
                case 24: return YutBoardLayout.JjiMo;
                case 25: return 26;
                case 26: return 22;
                case 27: return 28;
                case 28: return YutBoardLayout.Start;
                default: return (current + 1) % 20;         // 모/뒷모를 지나가는 경우 포함, 바깥 둘레 순환
            }
        }

        /// <summary>NextNode의 역방향(빽도용). 방(22)만 두 대각선이 합류해서 모호한데,
        /// 모(5) 쪽 대각선(→21)을 기본값으로 고정한다.</summary>
        static int PreviousNode(int current) => current switch
        {
            20 => YutBoardLayout.Mo,      // 5
            21 => 20,
            YutBoardLayout.Bang => 21,    // 22 → 21 (모 쪽 대각선 기본값)
            23 => YutBoardLayout.Bang,    // 22
            24 => 23,
            25 => YutBoardLayout.DwitMo,  // 10
            26 => 25,
            27 => YutBoardLayout.Bang,    // 22
            28 => 27,
            _ => (current + 19) % 20,     // 바깥 둘레(0~19, 참·모·뒷모·찌모 포함) — 한 칸 전으로
        };
    }
}
