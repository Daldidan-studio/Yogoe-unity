namespace Yoegoe.Data
{
    /// <summary>
    /// 요괴 성장 단계. [괴 단계 제거] 넋→혼 2단계.
    /// 옥토끼/삼족오/구미호는 처음부터 혼, 고라니만 넋으로 시작해 혼으로 진화.
    /// 진화: Docs/05 4항 확정 — 기력 100 도달 시 혼, 친밀도 0부터.
    /// </summary>
    public enum GrowthStage { Neok, Hon } // 넋, 혼

    /// <summary>행동 상태 5종 (기획서 6-2). 룰: Docs/06_행동룰.md</summary>
    public enum ActionState { Walking, Staying, Slumped, Fainted, Playing } // 걷기, 머물기, 주저앉기, 기절, 놀기

    public enum CharacterId { Rabbit, SamjokO, Gumiho, Gorani } // 옥토끼, 삼족오, 구미호, 고라니

    /// <summary>공양물 종류 (기획서 5-4). 정화수는 넋/혼 공통으로 기력만 채운다.</summary>
    public enum OfferingKind { General, Preferred, PurifiedWater } // 일반 공양물, 선호 공양물, 정화수
}
