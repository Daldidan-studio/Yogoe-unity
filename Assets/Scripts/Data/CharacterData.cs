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

        [Header("스탯 초기값 — StartingStateSettings.asset 이 우선 (여기 값은 폴백/참고용)")]
        public float startingIntimacy = 50f;
        public float startingStamina = 100f;

        [Header("혼잣말 (6-4장): 가만히 있을 때 머리 위에 랜덤으로 뜨는 대사")]
        public string[] monologueLines;

        [Header("걷기 애니메이션 스프라이트 (방향별 4프레임, 순서대로 재생)")]
        [Tooltip("정면(카메라 쪽)으로 걷는 4프레임")]
        public Sprite[] walkDown = new Sprite[4];
        [Tooltip("왼쪽으로 걷는 4프레임 (시트의 Right를 좌우반전)")]
        public Sprite[] walkLeft = new Sprite[4];
        [Tooltip("오른쪽으로 걷는 4프레임")]
        public Sprite[] walkRight = new Sprite[4];
        [Tooltip("뒤(화면 위쪽)로 걷는 4프레임")]
        public Sprite[] walkUp = new Sprite[4];

        [Header("상태 애니메이션 스프라이트 (각 4프레임)")]
        [Tooltip("기물에 앉아 머물기(생산) — 스프라이트 시트 5행")]
        public Sprite[] stay = new Sprite[4];
        [Tooltip("기력 0 주저앉기")]
        public Sprite[] slumped = new Sprite[4];
        [Tooltip("기절(누워 있음)")]
        public Sprite[] fainted = new Sprite[4];
        [Tooltip("놀기·대기 등 서 있는 아이들 (없으면 walkDown 사용)")]
        public Sprite[] idle = new Sprite[4];
    }
}
