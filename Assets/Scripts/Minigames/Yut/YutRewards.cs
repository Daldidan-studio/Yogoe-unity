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
    /// 윷놀이 보상 수치·판정. 지급(GameEconomy)·팝업은 YutScreen 책임.
    /// </summary>
    public static class YutRewards
    {
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
    }
}
