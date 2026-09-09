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

        [Tooltip("친밀도 증가량. [결정] 직접 먹이는 선호 공양 = +0.25. " +
                 "요구 들어주기(선호)도 +0.25(요구 추가분 없음). 윷 말 이동도 +0.25. Docs/05 1번.")]
        public float intimacyGain = 0.25f;

        [Header("상점 (12장)")]
        [Tooltip("엽전 가격. 정화수처럼 상점에서 안 파는 아이템은 0.")]
        public int shopPriceYeopjeon = 10;
    }
}
