namespace KSpirits.Minigames.Yut
{
    /// <summary>
    /// 윷판의 두 대각선(참↔뒷모, 모↔찌모)이 방(중앙)에서 교차하며 나누는 4개 삼각형 구역.
    /// 각 구역은 서로 다른 UI/인터랙션을 담는 자리로 쓴다 (역할은 각 값 주석 참고).
    /// </summary>
    public enum YutBoardQuadrant
    {
        /// <summary>위쪽 삼각형 — 화면 전체에 던져 나열된 윷.</summary>
        ThrownSticks,

        /// <summary>왼쪽 삼각형 — 특수능력 아이콘.</summary>
        SpecialAbility,

        /// <summary>오른쪽 삼각형 — 대기말(아직 보드에 오르지 않은 말).</summary>
        WaitingPieces,

        /// <summary>아래쪽 삼각형 — 완주말 + 이 판에서 얻은 보물.</summary>
        FinishedAndLoot,
    }
}
