using System;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Core;

namespace Yoegoe.Economy
{
    /// <summary>기물 구매·업그레이드 비용 (기획 8장).</summary>
    public static class PropEconomy
    {
        public const double PurchaseBaseCost = 800;
        public const double PurchaseCostGrowth = 1.4;
        public const double UpgradeBaseCost = 500;
        public const double UpgradeCostGrowth = 1.15;

        /// <summary>n번째 구매 비용. n = PropsPurchasedCount + 1. 자릿수 반올림 적용.</summary>
        public static BigNumber GetPurchaseCost(int purchaseOrderN)
        {
            int n = Math.Max(1, purchaseOrderN);
            return RoundCost(PurchaseBaseCost * Math.Pow(PurchaseCostGrowth, n - 1));
        }

        public static BigNumber GetNextPurchaseCost() =>
            GetPurchaseCost(GameEconomy.PropsPurchasedCount + 1);

        /// <summary>현재 레벨 L → L+1 업그레이드 비용: 500 × 1.15^(L−1). 자릿수 반올림 적용.</summary>
        public static BigNumber GetUpgradeCost(int level)
        {
            int l = Math.Max(1, level);
            return RoundCost(UpgradeBaseCost * Math.Pow(UpgradeCostGrowth, l - 1));
        }

        /// <summary>
        /// 비용 표시용 자릿수 반올림.
        /// 10 미만은 그대로, 그 이상은 단위 = 10^⌊log₁₀⌋ (100미만→10, 1000미만→100, …).
        /// </summary>
        public static BigNumber RoundCost(BigNumber amount)
        {
            if (amount.Mantissa == 0) return BigNumber.Zero;
            // 정규화 후 Exponent < 1 → |값| < 10
            if (amount.Exponent < 1) return amount;

            double sign = amount.Mantissa < 0 ? -1 : 1;
            double roundedMant = Math.Round(Math.Abs(amount.Mantissa), MidpointRounding.AwayFromZero);
            if (roundedMant >= 10)
                return new BigNumber(sign, amount.Exponent + 1);
            return new BigNumber(sign * roundedMant, amount.Exponent);
        }

        public static BigNumber GetUpgradeCost(PropSlot prop)
        {
            if (prop == null || !prop.IsBuilt) return BigNumber.Zero;
            return GetUpgradeCost(prop.level);
        }

        /// <summary>건립된 기물 중 업글 비용이 가장 싼 대상. 없으면 null.</summary>
        public static PropSlot FindCheapestUpgradeTarget()
        {
            PropSlot best = null;
            BigNumber bestCost = BigNumber.Zero;

            if (PropManager.Instance != null)
            {
                var props = PropManager.Instance.All;
                for (int i = 0; i < props.Count; i++)
                    best = ConsiderUpgradeCandidate(props[i], best, ref bestCost);
                return best;
            }

            // 폴백 (테스트 씬 등)
            var found = UnityEngine.Object.FindObjectsByType<PropSlot>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
                best = ConsiderUpgradeCandidate(found[i], best, ref bestCost);
            return best;
        }

        private static PropSlot ConsiderUpgradeCandidate(PropSlot p, PropSlot best, ref BigNumber bestCost)
        {
            if (p == null || !p.IsBuilt) return best;
            var cost = GetUpgradeCost(p);
            if (best == null || cost < bestCost)
            {
                bestCost = cost;
                return p;
            }
            return best;
        }

        public static bool TryPurchase(PropSlot prop)
        {
            if (prop == null || prop.IsBuilt) return false;
            var cost = GetNextPurchaseCost();
            if (!GameEconomy.TrySpendMerit(cost)) return false;
            prop.Build();
            GameEconomy.IncrementPropsPurchasedCount();
            return true;
        }

        public static bool TryUpgrade(PropSlot prop)
        {
            if (prop == null || !prop.IsBuilt) return false;
            var cost = GetUpgradeCost(prop);
            if (!GameEconomy.TrySpendMerit(cost)) return false;
            prop.level += 1;
            prop.NotifyLevelUp();
            return true;
        }

        /// <summary>최저가 기물 1회 업글. 성공한 기물 반환.</summary>
        public static PropSlot TryUpgradeCheapest()
        {
            var target = FindCheapestUpgradeTarget();
            if (target == null) return null;
            if (!TryUpgrade(target)) return null;
            return target;
        }
    }
}
