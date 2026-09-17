using UnityEngine;

namespace Yoegoe.Data
{
    /// <summary>음식·공양물·물 정의. 효과는 Docs/00 §4 본문 수치.</summary>
    [CreateAssetMenu(fileName = "OfferingData", menuName = "Yoegoe/Offering Data")]
    public class OfferingData : ScriptableObject
    {
        public string offeringId;
        public string displayName;
        public OfferingKind kind;
        public Sprite icon;

        [Header("공양 효과 (본문 §5)")]
        [Tooltip("0이면 종류 기본값: 음식+8 / 공양물·선호+3 / 물+3")]
        public int staminaGain;

        [Tooltip("0이면 종류 기본값: 음식·물 0 / 공양물+2 / 선호는 코드에서 +5")]
        public float intimacyGain;

        [Header("상점 (12장)")]
        [Tooltip("엽전 가격. 물은 0.")]
        public int shopPriceYeopjeon = 10;

        public int ResolveStaminaGain(bool isPreferred)
        {
            if (staminaGain > 0) return staminaGain;
            if (kind == OfferingKind.PurifiedWater) return 3;
            if (kind == OfferingKind.Food) return 8;
            return 3; // 공양물·선호
        }

        public float ResolveIntimacyGain(bool isPreferred)
        {
            if (kind == OfferingKind.PurifiedWater || kind == OfferingKind.Food) return 0f;
            if (isPreferred) return intimacyGain > 0f ? intimacyGain : 5f;
            return intimacyGain > 0f ? intimacyGain : 2f;
        }
    }
}
