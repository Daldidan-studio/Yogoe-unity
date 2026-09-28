namespace Yoegoe.Data
{
    /// <summary>행동 상태 5종 (기획서 6-2). 룰: Docs/06_행동룰.md</summary>
    public enum ActionState { Walking, Staying, Slumped, Fainted, Playing } // Slumped=구세이브 호환(런타임은 Playing으로 이관)

    /// <summary>기물 산출물 (Docs/00 §6-1). 시트 props.resourceType.</summary>
    public enum PropResourceType { None, Merit, PurifiedWater, Yeopjeon, Hunt, Gather }

    /// <summary>요리 재료가 아닌 특수 수집품 (활터·약초밭 1% — 기획 7-3). 지금은 인벤에 쌓기만.</summary>
    public enum SpecialItemId { GoldenRice, GoldenHoney }

    public enum CharacterId { Rabbit, SamjokO, Gumiho, Gorani } // 옥토끼, 삼족오, 구미호, 고라니

    /// <summary>공양/음식 종류. Preferred는 캐릭터 선호 판정용(데이터 kind로는 거의 안 씀).</summary>
    public enum OfferingKind { General, Preferred, PurifiedWater, Food } // 공양물, (선호표시), 물, 음식
}
