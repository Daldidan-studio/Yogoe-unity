using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>공양물 정의 (기획서 5-3, 5-4). 24종 예정, 캐릭터별 선호 공양물은 CharacterData에서 참조.</summary>
    [CreateAssetMenu(fileName = "OfferingData", menuName = "Yoegoe/Offering Data")]
    public class OfferingData : ScriptableObject
    {
        public string offeringId;
        public string displayName;
        public OfferingKind kind;
        public Sprite icon;

        [Header("공양 효과 (5-4 표 기준)")]
        [Tooltip("기력 +20 (일반/선호/정화수 공통)")]
        public int staminaGain = 20;

        [Tooltip("친밀도 증가량. [결정] 직접 먹이는 선호 공양 = +0.25로 확정 " +
                 "(5-4 표의 +0.5는 다른 경로용 표기였던 것으로 정리, Docs/05_기획_미확정사항.md 1번 참고). " +
                 "요구 들어주기/10장 경로는 +0.5, 윷놀이 말 이동/11장 경로는 +0.25로 별도 유지.")]
        public float intimacyGain = 0.25f;

        [Header("상점 (12장)")]
        [Tooltip("엽전 가격. 정화수처럼 상점에서 안 파는 아이템은 0.")]
        public int shopPriceYeopjeon = 10;
    }
}
