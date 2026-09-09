using System;
using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Core;
using Yoegoe.Data;

namespace Yoegoe.Economy
{
    /// <summary>12장 고가구점 재고: 2시간 랜덤 공양 2칸 + 향 + 5분치 공덕 리셋.</summary>
    public static class ShopStock
    {
        public const int OfferingPriceYeopjeon = 10;
        public const int HyangPriceYeopjeon = 20;
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(2);

        public enum Side { Left, Right }
        public enum BuyFail { None, NoStock, NotEnoughYeopjeon }

        public static string LeftOfferingId { get; private set; }
        public static string RightOfferingId { get; private set; }
        public static long NextRefreshUtcTicks { get; private set; }

        static OfferingData[] catalog;

        public static void SetCatalog(OfferingData[] offerings) => catalog = offerings;

        public static void CaptureToSave(out string leftId, out string rightId, out long nextTicks)
        {
            leftId = LeftOfferingId ?? "";
            rightId = RightOfferingId ?? "";
            nextTicks = NextRefreshUtcTicks;
        }

        public static void ResetFromSave(string leftId, string rightId, long nextTicks)
        {
            LeftOfferingId = leftId;
            RightOfferingId = rightId;
            NextRefreshUtcTicks = nextTicks;
            EnsureFresh(DateTime.UtcNow);
        }

        public static void EnsureFresh(DateTime utcNow)
        {
            bool missing = string.IsNullOrEmpty(LeftOfferingId) || string.IsNullOrEmpty(RightOfferingId);
            bool expired = NextRefreshUtcTicks <= 0 || utcNow.Ticks >= NextRefreshUtcTicks;
            if (!missing && !expired) return;
            RerollBoth(utcNow);
        }

        public static void ForceReroll(DateTime utcNow) => RerollBoth(utcNow);

        static void RerollBoth(DateTime utcNow)
        {
            LeftOfferingId = PickRandomOfferingId(exclude: null);
            RightOfferingId = PickRandomOfferingId(exclude: LeftOfferingId);
            // 후보가 1개뿐이면 양쪽 동일 허용
            if (string.IsNullOrEmpty(RightOfferingId))
                RightOfferingId = LeftOfferingId;
            NextRefreshUtcTicks = utcNow.Add(RefreshInterval).Ticks;
        }

        static string PickRandomOfferingId(string exclude)
        {
            var list = BuildCandidates(exclude);
            if (list.Count == 0)
            {
                list = BuildCandidates(null);
                if (list.Count == 0) return "";
            }
            return list[UnityEngine.Random.Range(0, list.Count)].offeringId;
        }

        static List<OfferingData> BuildCandidates(string excludeId)
        {
            var list = new List<OfferingData>();
            var src = catalog;
            if (src == null || src.Length == 0)
            {
                // CharacterCatalog 테이블 폴백은 id만으로는 전체 목록이 없어 catalog 필수
                return list;
            }
            for (int i = 0; i < src.Length; i++)
            {
                var o = src[i];
                if (o == null) continue;
                if (o.kind == OfferingKind.PurifiedWater) continue;
                if (string.IsNullOrEmpty(o.offeringId)) continue;
                if (!string.IsNullOrEmpty(excludeId)
                    && string.Equals(o.offeringId, excludeId, StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(o);
            }
            return list;
        }

        public static OfferingData GetOffering(Side side)
        {
            string id = side == Side.Left ? LeftOfferingId : RightOfferingId;
            if (string.IsNullOrEmpty(id)) return null;
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Length; i++)
                {
                    var o = catalog[i];
                    if (o == null) continue;
                    if (string.Equals(o.offeringId, id, StringComparison.OrdinalIgnoreCase))
                        return o;
                }
            }
            return CharacterCatalog.FindOffering(id);
        }

        public static bool TryBuyOffering(Side side, out BuyFail fail)
        {
            fail = BuyFail.None;
            var o = GetOffering(side);
            if (o == null)
            {
                fail = BuyFail.NoStock;
                return false;
            }
            if (!GameEconomy.Instance.TrySpendYeopjeon(OfferingPriceYeopjeon))
            {
                fail = BuyFail.NotEnoughYeopjeon;
                return false;
            }
            GameEconomy.Instance.AddOffering(o, 1);
            return true;
        }

        public static bool TryBuyHyang(out BuyFail fail)
        {
            fail = BuyFail.None;
            if (!GameEconomy.Instance.TrySpendYeopjeon(HyangPriceYeopjeon))
            {
                fail = BuyFail.NotEnoughYeopjeon;
                return false;
            }
            GameEconomy.Instance.AddHyang(1);
            return true;
        }

        /// <summary>현재 생산속도(분당) × 5. 생산 0이면 건립 기물 기본합 × 5, 최소 1.</summary>
        public static BigNumber GetResetCostMerit()
        {
            double perMin = GetCurrentProductionPerMinute();
            if (perMin <= 0.0001)
                perMin = GetBuiltPropsBaseProductionSum();
            double cost = Math.Max(1.0, perMin * 5.0);
            return BigNumber.FromDouble(cost);
        }

        public static bool TryResetWithMerit(out BigNumber cost, out bool notEnoughMerit)
        {
            cost = GetResetCostMerit();
            notEnoughMerit = false;
            if (!GameEconomy.Instance.TrySpendMerit(cost))
            {
                notEnoughMerit = true;
                return false;
            }
            ForceReroll(DateTime.UtcNow);
            return true;
        }

        public static double GetCurrentProductionPerMinute()
        {
            double sum = 0;
            foreach (var agent in CharacterAgent.All)
            {
                if (agent == null) continue;
                sum += agent.GetProductionPerMinuteIfStaying();
            }
            return sum;
        }

        static double GetBuiltPropsBaseProductionSum()
        {
            double sum = 0;
            if (PropManager.Instance == null) return sum;
            var all = PropManager.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null || !p.IsBuilt) continue;
                sum += p.GetBaseProductionThisLevel();
            }
            return sum;
        }
    }
}
