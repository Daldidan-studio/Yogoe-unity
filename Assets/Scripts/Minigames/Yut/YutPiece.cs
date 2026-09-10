using System.Collections.Generic;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>윷놀이 말 하나(또는 업힌 스택의 대표) 상태. 순수 데이터.</summary>
    public class YutPiece
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly bool IsPlayer;

        /// <summary>-1이면 아직 보드에 오르지 않은 대기 상태.</summary>
        public int NodeId = -1;
        public bool Finished;

        /// <summary>
        /// 이 말이 지금까지 실제로 지나온 노드 순서(마지막 도착 칸 제외). 빽도가 나왔을 때 여기서
        /// 마지막 걸 하나 꺼내(pop) 그대로 되짚어 가는 데 쓴다 — 방(갈림길 합류점)처럼 "전 칸"이
        /// 애매한 지점도 실제로 왔던 길을 기억하고 있어서 정확하다. 잡히거나 새로 들어올 때 비워진다.
        /// </summary>
        public readonly List<int> History = new List<int>();

        public bool OnBoard => NodeId >= 0 && !Finished;

        public YutPiece(string id, string displayName, bool isPlayer)
        {
            Id = id;
            DisplayName = displayName;
            IsPlayer = isPlayer;
        }
    }
}
