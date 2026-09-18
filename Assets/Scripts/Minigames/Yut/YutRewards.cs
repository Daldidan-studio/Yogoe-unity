using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>특수 칸·보물상자에서 고른 재화 종류.</summary>
    public enum YutSquareRewardKind { Offering, Yeopjeon, Hyang, AdTicket, YutToken, PurifiedWater }

    /// <summary>특수 칸 보상 팝업에 올리기 전, 지급할 내용(배율 적용 전).</summary>
    public readonly struct YutSquareReward
    {
        public readonly YutSquareRewardKind Kind;
        public readonly OfferingData Offering; // Kind == Offering일 때만
        public readonly int Amount; // 배율 적용 전 기본 개수

        public YutSquareReward(YutSquareRewardKind kind, OfferingData offering, int amount)
        {
            Kind = kind;
            Offering = offering;
            Amount = amount;
        }
    }

    /// <summary>
    /// 윷놀이 보상 수치·판정·뽑기. 지급(GameEconomy)·팝업은 Presenter/Screen 책임.
    /// </summary>
    public static class YutRewards
    {
        // TODO(부적 완주 보상): 3차 기획 기준 완주 보상은 정화수가 아니라 부적 1개(6종 중 랜덤,
        // GameEconomy.CookingCharmType 재사용)로 교체됨. 종류별 확률은 플레이테스트 후 조정 —
        // 그 전까지는 6종 균등(1/6)으로 구현. 아래 FinishPurifiedWater* 는 구 모델이라 교체 대상.
        // Docs/00_기획정리.md §10, Docs/05_기획_미확정사항.md 참고.

        /// <summary>완주 보상 — 말 1마리 기준 정화수 1개(업기 스택 수만큼 배율).</summary>
        public const int FinishPurifiedWaterPerPiece = 1;

        /// <summary>이 수만큼 업고 한 번에 완주할 때만 광고 2배 선택(3마리는 해당 없음).</summary>
        public const int FinishAdBonusStackCount = 4;

        /// <summary>특수 칸 — "그냥 받기" / "광고 보고 2배".</summary>
        public const int SquareRewardBase = 1;
        public const int SquareRewardAdMultiplier = 2;
        public const float SquareRewardAdWatchSeconds = 0.8f;

        /// <summary>보물상자 윷 토큰 보상은 평소 상한(5)을 넘길 수 있되 이 값까지만.</summary>
        public const int YutTokenHardCap = 7;

        /// <summary>매 판 도전과제 — 완료 시 보물상자 개수(한 번에 개봉, 광고 2배 없음).</summary>
        public const int ChallengeChestCount = 3;
        /// <summary>연속 모/빽도 과제에 필요한 연속 횟수.</summary>
        public const int ChallengeConsecutiveNeeded = 2;
        /// <summary>미잡힘 전원 완주 과제에 필요한 최소 말 수(“넷 다”).</summary>
        public const int ChallengeFinishAllPieceCount = 4;

        public static int FinishPurifiedWaterAmount(int finishStackCount) =>
            FinishPurifiedWaterPerPiece * Mathf.Max(1, finishStackCount);

        public static bool OffersFinishAdBonus(int finishStackCount) =>
            Mathf.Max(1, finishStackCount) == FinishAdBonusStackCount;

        /// <summary>보통 3개, 가끔 4개, 드물게 5개 — 평소 상한(5)을 넘기지 않는 선에서 기본값보다
        /// 후하게. "그냥 받기/광고 2배"를 거치며 실제로는 최대 YutTokenHardCap까지 쌓일 수 있다.</summary>
        public static int RollTreasureYutTokenAmount()
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.6f) return 3;
            if (roll < 0.85f) return 4;
            return 5;
        }

        /// <summary>보물상자 — 향/공양물/광고보상권/엽전/윷토큰 중 하나를 균등 확률로 뽑는다.</summary>
        public static YutSquareReward RollTreasure(IReadOnlyList<OfferingData> offeringPool)
        {
            int pick = UnityEngine.Random.Range(0, 5);
            switch (pick)
            {
                case 0:
                    return new YutSquareReward(YutSquareRewardKind.Hyang, null, 1);
                case 1:
                    var offering = offeringPool != null && offeringPool.Count > 0
                        ? offeringPool[UnityEngine.Random.Range(0, offeringPool.Count)]
                        : null;
                    return new YutSquareReward(YutSquareRewardKind.Offering, offering, 1);
                case 2:
                    return new YutSquareReward(YutSquareRewardKind.AdTicket, null, 1);
                case 3:
                    return new YutSquareReward(YutSquareRewardKind.Yeopjeon, null, 1);
                default:
                    return new YutSquareReward(YutSquareRewardKind.YutToken, null, RollTreasureYutTokenAmount());
            }
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
                default: return "보상";
            }
        }
    }
}
