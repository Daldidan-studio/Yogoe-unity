using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Yoegoe.Cooking;
using Yoegoe.Economy;
using Yoegoe.Minigames.Yut;

namespace Yoegoe.Tests.EditMode
{
    public class YutRewardsTests
    {
        static int CountOf(Dictionary<YutSquareRewardKind, int> counts, YutSquareRewardKind kind) =>
            counts.TryGetValue(kind, out int n) ? n : 0;

        [Test]
        public void RollTreasure_FollowsPlannedWeights()
        {
            var pool = new List<Yoegoe.Data.OfferingData>();
            var counts = new Dictionary<YutSquareRewardKind, int>();
            const int n = 10000;
            Random.InitState(123);
            for (int i = 0; i < n; i++)
            {
                var r = YutRewards.RollTreasure(pool);
                if (!counts.ContainsKey(r.Kind)) counts[r.Kind] = 0;
                counts[r.Kind]++;
                if (r.Kind == YutSquareRewardKind.Yeopjeon)
                    Assert.AreEqual(3, r.Amount);
            }

            Assert.AreEqual(0, CountOf(counts, YutSquareRewardKind.YutToken));

            float offering = CountOf(counts, YutSquareRewardKind.Offering) / (float)n;
            float ad = CountOf(counts, YutSquareRewardKind.AdTicket) / (float)n;
            float hyang = CountOf(counts, YutSquareRewardKind.Hyang) / (float)n;
            float yeop = CountOf(counts, YutSquareRewardKind.Yeopjeon) / (float)n;

            Assert.That(offering, Is.InRange(0.45f, 0.55f));
            Assert.That(ad, Is.InRange(0.25f, 0.35f));
            Assert.That(hyang, Is.InRange(0.03f, 0.08f));
            Assert.That(yeop, Is.InRange(0.10f, 0.20f));
        }

        [Test]
        public void RollIngredientBundle_ExcludesWater_HasThree()
        {
            Random.InitState(9);
            for (int t = 0; t < 50; t++)
            {
                var bag = YutRewards.RollIngredientBundle();
                Assert.AreEqual(3, bag.Length);
                for (int i = 0; i < bag.Length; i++)
                    Assert.AreNotEqual(CookingIngredientId.Water, bag[i]);
            }
        }

        [Test]
        public void RollFinishCharm_IsInventoryCharm()
        {
            // 완주 보상 6종 — 나가리도 소모품이라 나올 수 있다 (시트 charms 가중치)
            Random.InitState(1);
            for (int i = 0; i < 40; i++)
            {
                var c = YutRewards.RollFinishCharm();
                Assert.IsTrue(GameEconomy.IsInventoryCharm(c));
                Assert.AreNotEqual(CookingCharmType.None, c);
            }
        }
    }
}
