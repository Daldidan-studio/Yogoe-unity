namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 윷판의 두 대각선(참↔뒷모, 모↔찌모)이 방(중앙)에서 교차하며 나누는 4개 삼각형 구역.
    /// 동서남북으로 부른다 — 각 구역은 서로 다른 UI/인터랙션을 담는 자리로 쓴다.
    /// </summary>
    public enum YutBoardQuadrant
    {
        /// <summary>북쪽(위) 삼각형 — 던진 윷가락이 정리되어 나열되는 자리.</summary>
        North,

        /// <summary>남쪽(아래) 삼각형 — 대기말(아직 보드에 오르지 않은 말). 보유한 수만큼 나란히.</summary>
        South,

        /// <summary>동쪽(오른) 삼각형 — 이 판에서 얻은 물건 정리 자리. 아직 기능 없음(예약만).</summary>
        East,

        /// <summary>서쪽(왼) 삼각형 — 당장은 비워둠(용도 미정).</summary>
        West,
    }
}
