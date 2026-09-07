using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>캐릭터(요괴) 정의 (기획서 4장).</summary>
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Yoegoe/Character Data")]
    public class CharacterData : ScriptableObject
    {
        public CharacterId id;
        public string displayName;

        [Tooltip("옥토끼/삼족오/구미호는 Hon으로 시작. 고라니만 Neok으로 시작해 진화(9-3)를 거친다.")]
        public GrowthStage startingStage;

        [Header("선호 공양물 3~4종 (4장 표)")]
        public OfferingData[] preferredOfferings;

        [Header("엔딩 기물 (있으면 연결, 예: 옥토끼-떡절구)")]
        public PropData endingProp;

        [TextArea(3, 6)]
        public string detailDescription; // 상세 화면 설명 [제안]

        [Header("스탯 초기값 (2장: 시작 3인 친밀도 50, 기력 100)")]
        public float startingIntimacy = 50f;
        public float startingStamina = 100f;
    }
}
