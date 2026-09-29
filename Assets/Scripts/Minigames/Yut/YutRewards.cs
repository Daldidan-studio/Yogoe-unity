using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Yoegoe.Cooking;
using Yoegoe.Data;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>특수 칸·보물상자·완주에서 고른 재화 종류.</summary>
    public enum YutSquareRewardKind
    {
        Offering,
        Yeopjeon,
        Hyang,
        AdTicket,
        YutToken,
        PurifiedWater,
        IngredientBundle,
        Charm,
    }

    /// <summary>특수 칸 보상 팝업에 올리기 전, 지급할 내용(배율 적용 전).</summary>
    public readonly struct YutSquareReward
    {
        public readonly YutSquareRewardKind Kind;
        public readonly OfferingData Offering; // Kind == Offering일 때만
        public readonly int Amount; // 배율 적용 전 기본 개수
        public readonly CookingIngredientId[] Ingredients; // IngredientBundle
        public readonly CookingCharmType Charm; // Charm

        public YutSquareReward(YutSquareRewardKind kind, OfferingData offering, int amount,
            CookingIngredientId[] ingredients = null, CookingCharmType charm = CookingCharmType.None)
        {
            Kind = kind;
            Offering = offering;
            Amount = amount;
            Ingredients = ingredients;
            Charm = charm;
        }
    }

    /// <summary>
    /// 윷놀이 보상 수치·판정·뽑기. 지급(GameEconomy)·팝업은 Presenter/Screen 책임.
    /// Docs/00 §11: 특수칸 5 · 보물상자 확률 · 완주 부적.
    /// </summary>
    public static class YutRewards
    {
        /// <summary>재료보따리 — 랜덤 재료 개수(광고 2배 시 ×배수).</summary>
        public const int IngredientBundleCount = 3;

        /// <summary>특수 칸 — "그냥 받기" / "광고 보고 2배".</summary>
        public const int SquareRewardBase = 1;
        public const int SquareRewardAdMultiplier = 2;
        public const float SquareRewardAdWatchSeconds = 0.8f;

        /// <summary>보물상자 윷 토큰은 더 이상 나오지 않음(구 세이브·도전 호환용 상수 유지).</summary>
        public const int YutTokenHardCap = 7;

        /// <summary>매 판 도전과제 — 완료 시 보물상자 개수(한 번에 개봉, 광고 2배 없음).</summary>
        public const int ChallengeChestCount = 3;
        /// <summary>연속 모/빽도 과제에 필요한 연속 횟수.</summary>
        public const int ChallengeConsecutiveNeeded = 2;
        /// <summary>미잡힘 전원 완주 과제에 필요한 최소 말 수(“넷 다”).</summary>
        public const int ChallengeFinishAllPieceCount = 4;

        /// <summary>완주 시 인벤에 쌓이는 사전 부적(나가리 제외). 종류별 확률은 플레이테스트 후 조정 — 균등.</summary>
        static readonly CookingCharmType[] FinishCharmPool =
        {
            CookingCharmType.PlusFive,
            CookingCharmType.Diagonal,
            CookingCharmType.Clairvoyance,
            CookingCharmType.Recycle,
            CookingCharmType.Double,
        };

        public static CookingCharmType RollFinishCharm() =>
            FinishCharmPool[UnityEngine.Random.Range(0, FinishCharmPool.Length)];

        /// <summary>재료보따리 — 선물꾸러미와 같은 추첨(채집/사냥 50% → 7-3 확률표). IngredientDraw 참고.</summary>
        public static CookingIngredientId[] RollIngredientBundle(int count = IngredientBundleCount) =>
            Yoegoe.Economy.IngredientDraw.Roll(count);

        /// <summary>
        /// 보물상자 — 공양물 50% · 광고보상권 30% · 향 5% · 엽전 3개 15%.
        /// </summary>
        public static YutSquareReward RollTreasure(IReadOnlyList<OfferingData> offeringPool)
        {
            float r = UnityEngine.Random.value;
            if (r < 0.50f)
            {
                var offering = offeringPool != null && offeringPool.Count > 0
                    ? offeringPool[UnityEngine.Random.Range(0, offeringPool.Count)]
                    : null;
                return new YutSquareReward(YutSquareRewardKind.Offering, offering, 1);
            }
            if (r < 0.80f)
                return new YutSquareReward(YutSquareRewardKind.AdTicket, null, 1);
            if (r < 0.85f)
                return new YutSquareReward(YutSquareRewardKind.Hyang, null, 1);
            return new YutSquareReward(YutSquareRewardKind.Yeopjeon, null, 3);
        }

        /// <summary>지급 전 미리보기용 문구(배율 1 기준).</summary>
        public static string DescribeSquareReward(YutSquareReward reward)
        {
            int amount = reward.Amount;
            switch (reward.Kind)
            {
                case YutSquareRewardKind.Yeopjeon: return $"엽전 {amount}개";
                case YutSquareRewardKind.PurifiedWater: return $"정화수 {amount}개";
                case YutSquareRewardKind.Hyang: return $"향 {amount}개";
                case YutSquareRewardKind.AdTicket: return $"광고보상권 {amount}개";
                case YutSquareRewardKind.YutToken: return $"윷 토큰 {amount}개";
                case YutSquareRewardKind.Offering:
                    string name = reward.Offering != null ? reward.Offering.displayName : "공양물";
                    return $"{name} {amount}개";
                case YutSquareRewardKind.IngredientBundle:
                    return DescribeIngredients(reward.Ingredients);
                case YutSquareRewardKind.Charm:
                    return CharmDisplayName(reward.Charm);
                default: return "보상";
            }
        }

        public static string CharmDisplayName(CookingCharmType charm) => charm switch
        {
            CookingCharmType.PlusFive => "+5초 부적",
            CookingCharmType.Diagonal => "대각선 부적",
            CookingCharmType.Clairvoyance => "천리안 부적",
            CookingCharmType.Recycle => "회수 부적",
            CookingCharmType.Double => "몰빵 부적",
            CookingCharmType.Cancel => "나가리",
            _ => "부적",
        };

        public static string DescribeIngredients(CookingIngredientId[] ingredients)
        {
            if (ingredients == null || ingredients.Length == 0) return "재료보따리";
            var sb = new StringBuilder();
            sb.Append("재료 ");
            for (int i = 0; i < ingredients.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(CookingRecipeCatalog.DisplayName(ingredients[i]));
            }
            return sb.ToString();
        }
    }
}
