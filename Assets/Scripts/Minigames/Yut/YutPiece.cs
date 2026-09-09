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

        public bool OnBoard => NodeId >= 0 && !Finished;

        public YutPiece(string id, string displayName, bool isPlayer)
        {
            Id = id;
            DisplayName = displayName;
            IsPlayer = isPlayer;
        }
    }
}
