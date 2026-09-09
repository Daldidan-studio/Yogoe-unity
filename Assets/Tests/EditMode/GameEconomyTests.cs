using NUnit.Framework;
using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// GameEconomy는 static 전역 상태라 테스트끼리 서로 오염된다 — 매 테스트 전에
    /// ApplyStartingState로 명시적으로 리셋한다. (item 4 정적 상태 문제의 실제 사례)
    /// </summary>
    public class GameEconomyTests
    {
        StartingStateSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<StartingStateSettings>();
            settings.startingIntimacy = 50f;
            settings.startingStamina = 100f;
            settings.startingMerit = 1000;
            settings.startingYeopjeon = 0;
            settings.startingHyang = 3;
            settings.startingPurifiedWater = 10;
            settings.startingYutToken = 5;
            settings.yutTokenMax = 5;
            settings.startingOfferings = null;
            GameEconomy.ApplyStartingState(settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (settings != null) Object.DestroyImmediate(settings);
        }

        [Test]
        public void ApplyStartingState_SetsConfiguredValues()
        {
            Assert.AreEqual(1000, GameEconomy.MeritPile.ToDouble(), 0.01);
            Assert.AreEqual(0, GameEconomy.Yeopjeon);
            Assert.AreEqual(3, GameEconomy.Hyang);
            Assert.AreEqual(10, GameEconomy.PurifiedWater);
            Assert.AreEqual(5, GameEconomy.YutToken);
            Assert.AreEqual(5, GameEconomy.YutTokenMax);
            Assert.AreEqual(0, GameEconomy.PropsPurchasedCount);
            Assert.IsFalse(GameEconomy.HasPendingBatchMerit);
        }

        [Test]
        public void AddMerit_IncreasesPileAndRaisesEvent()
        {
            double? seen = null;
            System.Action<Yoegoe.Core.BigNumber> handler = v => seen = v.ToDouble();
            GameEconomy.OnMeritChanged += handler;
            try
            {
                GameEconomy.AddMerit(500);
                Assert.AreEqual(1500, GameEconomy.MeritPile.ToDouble(), 0.01);
                Assert.IsTrue(seen.HasValue);
                Assert.AreEqual(1500, seen.Value, 0.01);
            }
            finally
            {
                GameEconomy.OnMeritChanged -= handler;
            }
        }

        [Test]
        public void TrySpendMerit_SucceedsWhenEnough()
        {
            bool ok = GameEconomy.TrySpendMerit(300);
            Assert.IsTrue(ok);
            Assert.AreEqual(700, GameEconomy.MeritPile.ToDouble(), 0.01);
        }

        [Test]
        public void TrySpendMerit_FailsWhenNotEnough_PileUnchanged()
        {
            bool ok = GameEconomy.TrySpendMerit(5000);
            Assert.IsFalse(ok);
            Assert.AreEqual(1000, GameEconomy.MeritPile.ToDouble(), 0.01);
        }

        [Test]
        public void TrySpendMerit_FailsForNegativeAmount()
        {
            Assert.IsFalse(GameEconomy.TrySpendMerit(-1));
        }

        [Test]
        public void YutToken_AddCapsAtMax()
        {
            GameEconomy.TrySpendYutToken(5);
            GameEconomy.AddYutToken(10);
            Assert.AreEqual(GameEconomy.YutTokenMax, GameEconomy.YutToken);
        }

        [Test]
        public void TrySpendYutToken_FailsWhenInsufficient_LeavesTokenUnchanged()
        {
            bool ok = GameEconomy.TrySpendYutToken(999);
            Assert.IsFalse(ok);
            Assert.AreEqual(5, GameEconomy.YutToken);
        }

        [Test]
        public void Yeopjeon_AddAndSpendRoundTrip()
        {
            GameEconomy.AddYeopjeon(100);
            Assert.AreEqual(100, GameEconomy.Yeopjeon);
            Assert.IsTrue(GameEconomy.TrySpendYeopjeon(40));
            Assert.AreEqual(60, GameEconomy.Yeopjeon);
            Assert.IsFalse(GameEconomy.TrySpendYeopjeon(1000));
        }

        [Test]
        public void PropsPurchasedCount_IncrementsAndClampsAtZeroWhenSetNegative()
        {
            GameEconomy.IncrementPropsPurchasedCount();
            GameEconomy.IncrementPropsPurchasedCount();
            Assert.AreEqual(2, GameEconomy.PropsPurchasedCount);

            GameEconomy.SetPropsPurchasedCount(-5);
            Assert.AreEqual(0, GameEconomy.PropsPurchasedCount);
        }

        [Test]
        public void BatchMerit_ClaimAppliesMultiplierAndClearsPending()
        {
            GameEconomy.AddPendingBatchMerit(100);
            Assert.IsTrue(GameEconomy.HasPendingBatchMerit);

            bool claimed = GameEconomy.TryClaimBatchMerit(multiplier: 3);

            Assert.IsTrue(claimed);
            Assert.IsFalse(GameEconomy.HasPendingBatchMerit);
            Assert.AreEqual(1300, GameEconomy.MeritPile.ToDouble(), 0.01); // 시작 1000 + 100*3
        }

        [Test]
        public void TryClaimBatchMerit_FailsWhenNothingPending()
        {
            Assert.IsFalse(GameEconomy.TryClaimBatchMerit());
        }
    }
}
