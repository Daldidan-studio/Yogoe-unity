using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>
    /// 새 게임 시작 상태 (기획서 2장).
    /// Assets/Resources/StartingStateSettings.asset 하나만 바꾸면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "StartingStateSettings", menuName = "Yoegoe/Starting State Settings")]
    public class StartingStateSettings : ScriptableObject
    {
        [Header("시작 캐릭터 (혼 3인 공통)")]
        [Tooltip("친밀도 0~100. 기획서 시작 3인 = 50.")]
        public float startingIntimacy = 50f;
        [Tooltip("기력 상한 100. 기획서 시작 3인 = 100.")]
        public float startingStamina = 100f;

        [Header("시작 재화")]
        [Tooltip("공덕. 기획서 = 1,000.")]
        public int startingMerit = 1000;
        [Tooltip("엽전. 기획서 = 0.")]
        public int startingYeopjeon = 0;
        [Tooltip("향. 기획서 = 2 (소환에 3개 필요).")]
        public int startingHyang = 2;
        [Tooltip("정화수. 기획서 = 10.")]
        public int startingPurifiedWater = 10;
        [Tooltip("윷 토큰 시작 개수. 기획서 = 5.")]
        public int startingYutToken = 5;
        [Tooltip("윷 토큰 최대. 기획서 = 5.")]
        public int yutTokenMax = 5;

        [Header("시작 공양물 인벤토리")]
        [Tooltip("시작할 때 지급할 공양물 목록 (정화수 제외 — 위 재화 칸). 기획서 24종.")]
        public OfferingData[] startingOfferings;
        [Tooltip("위 목록 각 종류마다 줄 개수. 기획서 = 각 1개.")]
        public int startingOfferingCountEach = 1;

        public static StartingStateSettings Get()
        {
            var loaded = Resources.Load<StartingStateSettings>("StartingStateSettings");
            if (loaded != null) return loaded;
            return CreateInstance<StartingStateSettings>();
        }
    }
}
