namespace Yoegoe.Data
{
    /// <summary>
    /// 요괴 성장 단계. [괴 단계 제거 결정 반영] 원래 넋→괴→혼 3단계였으나 괴를 없애고 넋→혼 2단계로 단순화.
    /// 옥토끼/삼족오/구미호는 처음부터 혼, 고라니만 넋으로 시작해 혼으로 진화한다.
    /// 진화 조건은 docs/DESIGN_DECISIONS_NEEDED.md 4번 항목 참고 (임시로 기력100=혼 진화로 구현해둠).
    /// </summary>
    public enum GrowthStage { Neok, Hon } // 넋, 혼

    /// <summary>행동 상태 4종 (기획서 6-2).</summary>
    public enum ActionState { Walking, Staying, Slumped, Fainted } // 걷기, 머물기, 주저앉기, 기절

    public enum CharacterId { Rabbit, SamjokO, Gumiho, Gorani } // 옥토끼, 삼족오, 구미호, 고라니

    /// <summary>공양물 종류 (기획서 5-4). 정화수는 넋/혼 공통으로 기력만 채운다.</summary>
    public enum OfferingKind { General, Preferred, PurifiedWater } // 일반 공양물, 선호 공양물, 정화수
}
