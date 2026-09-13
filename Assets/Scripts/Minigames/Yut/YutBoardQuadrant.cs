namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 윷판의 두 대각선(참↔뒷모, 모↔찌모)이 방(중앙)에서 교차하며 나누는 구역.
    /// 각 구역은 서로 다른 UI/인터랙션을 담는 자리로 쓴다.
    /// </summary>
    public enum YutBoardQuadrant
    {
        /// <summary>북쪽(위) — 던진 윷가락이 정리되어 나열되는 자리.</summary>
        North,

            /// <summary>남쪽(아래) — 예전 대기말 자리. 지금은 로스터 슬롯에 대기하므로 비워 둔다.</summary>
            South,

        /// <summary>동쪽(오른) — 이 판에서 얻은 물건·완주 말 정리 자리.</summary>
        East,
    }
}
