using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 새 게임 시작 상태 (Docs/00 §2).
    /// Assets/Resources/StartingStateSettings.asset
    /// </summary>
    [CreateAssetMenu(fileName = "StartingStateSettings", menuName = "Yoegoe/Starting State Settings")]
    public class StartingStateSettings : ScriptableObject
    {
        [Header("시작 캐릭터 (토끼·삼족오)")]
        [Tooltip("친밀도 0~100. 시작 2인 = 50.")]
        public float startingIntimacy = 50f;
        [Tooltip("레거시. 혼 시작 기력은 코드에서 25+친밀도로 계산.")]
        public float startingStamina = 75f;

        [Header("시작 재화")]
        public int startingMerit = 1000;
        public int startingYeopjeon = 100;
        public int startingHyang = 2;
        [Tooltip("물. 시작 0.")]
        public int startingPurifiedWater = 0;
        public int startingYutToken = 5;
        public int yutTokenMax = 5;

        [Header("시작 공양물·음식 인벤토리")]
        [Tooltip("시작 지급 목록. 기획: 음식·공양물 0 → 비움.")]
        public OfferingData[] startingOfferings;
        public int startingOfferingCountEach = 0;

        public static StartingStateSettings Get()
        {
            var loaded = Resources.Load<StartingStateSettings>("StartingStateSettings");
            if (loaded != null) return loaded;
            return CreateInstance<StartingStateSettings>();
        }
    }
}
