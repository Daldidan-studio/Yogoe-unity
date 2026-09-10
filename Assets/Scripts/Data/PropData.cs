using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>기물(맵 오브젝트) 정의 (기획서 7,8장). Assets/Data/Props/*.asset. 배치는 PropLayoutSettings.</summary>
    [CreateAssetMenu(fileName = "PropData", menuName = "Yoegoe/Prop Data")]
    public class PropData : ScriptableObject
    {
        public string propId;
        public string displayName;
        public Sprite icon;

        [Header("생산 (7-1): 분당생산량 = baseProductionPerMinute * 1.1^(L-1) * 친밀도보정 * 엔딩기물보정")]
        public double baseProductionPerMinute = 100;

        [Header("업그레이드 비용 (8장): 500 * 1.15^(L-1), 레벨업마다 15% 증가")]
        public double upgradeBaseCost = 500;
        public double upgradeCostMultiplier = 1.15;

        [Tooltip("MVP 시작 시 이미 지어져 있는 기물인지 (돌탑/우물/떡절구). " +
                 "false면 빈 자리(자물쇠)로 시작하며, 최초 구매 비용은 800 * 1.4^(n-1) (n=구매 순서)로 별도 계산.")]
        public bool isPrebuilt;

        [Header("엔딩 기물 (8장): 지정된 캐릭터만 착석 가능, 주인이 앉으면 생산 x2")]
        public bool isEndingProp;
        public CharacterId owner;

        [Tooltip("떡절구처럼 전용 애니메이션이 있는지. 없으면 '자기 엔딩 기물 앞에서 기도하기'로 통일 (8장).")]
        public bool hasUniqueEndingAnimation;

        [Tooltip("주인 캐릭터가 점유 중일 때 기물에 표시할 전용 스프라이트 (예: 옥토끼+떡절구). 비우면 기본 기물 그림 유지.")]
        public Sprite occupiedByOwnerSprite;
    }
}
