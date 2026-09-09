using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 캐릭터(요괴) 정의. 스프라이트는 이 SO.
    /// 이름·선호공양·설명·혼잣말은 Resources/characters.json (CharacterCatalog)이 소스.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Yoegoe/Character Data")]
    public class CharacterData : ScriptableObject
    {
        public CharacterId id;

        [Tooltip("characters.json 이 소스. Apply 전 폴백.")]
        public string displayName;

        [Tooltip("characters.json 이 소스.")]
        public GrowthStage startingStage;

        [Header("선호 공양물 — characters.json preferredOfferings 가 소스")]
        public OfferingData[] preferredOfferings;

        [Header("엔딩 기물 — characters.json endingPropId 조회")]
        public PropData endingProp;

        [TextArea(3, 6)]
        [Tooltip("characters.json 이 소스.")]
        public string detailDescription;

        [Header("스탯 초기값 — StartingStateSettings.asset 이 우선 (여기 값은 폴백/참고용)")]
        public float startingIntimacy = 50f;
        public float startingStamina = 100f;

        [Header("혼잣말 (6-4장): 가만히 있을 때 머리 위에 랜덤으로 뜨는 대사")]
        public string[] monologueLines;

        [Header("걷기 애니메이션 스프라이트 (방향별 4프레임, 순서대로 재생)")]
        [Tooltip("정면(카메라 쪽)으로 걷는 4프레임")]
        public Sprite[] walkDown = new Sprite[4];
        [Tooltip("왼쪽으로 걷는 프레임. 비우면 CharacterAgent가 walkRight를 flipX로 반전해서 사용")]
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
