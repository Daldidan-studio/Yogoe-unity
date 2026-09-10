using System;
using NUnit.Framework;
using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    public class GameEconomyTests
    {
        GameObject economyGO;
        GameEconomy economy;
        StartingStateSettings settings;

        [SetUp]
        public void SetUp()
        {
            economyGO = new GameObject("GameEconomy_Test");
            economy = economyGO.AddComponent<GameEconomy>();
            // EditMode: Awake 미호출 → Instance가 null로 남음
            economy.BecomeInstance();

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
            economy.ApplyStartingState(settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (settings != null) Object.DestroyImmediate(settings);
            // GameEconomy.Instance는 static이라 다음 테스트가 새 인스턴스를 만들기 전에
            // 반드시 즉시(지연 아님) 파괴해야 싱글턴 가드에 걸리지 않는다.
            if (economyGO != null) Object.DestroyImmediate(economyGO);
        }

        [Test]
        public void ApplyStartingState_SetsConfiguredValues()
        {
            Assert.AreEqual(1000, economy.MeritPile.ToDouble(), 0.01);
            Assert.AreEqual(0, economy.Yeopjeon);
            Assert.AreEqual(3, economy.Hyang);
            Assert.AreEqual(10, economy.PurifiedWater);
            Assert.AreEqual(5, economy.YutToken);
            Assert.AreEqual(5, economy.YutTokenMax);
            Assert.AreEqual(0, economy.PropsPurchasedCount);
            Assert.IsFalse(economy.HasPendingBatchMerit);
        }

        [Test]
        public void Instance_PointsToTheCreatedComponent()
        {
            Assert.AreSame(economy, GameEconomy.Instance);
        }

        [Test]
        public void AddMerit_IncreasesPileAndRaisesEvent()
        {
            double? seen = null;
            System.Action<Yoegoe.Core.BigNumber> handler = v => seen = v.ToDouble();
            economy.OnMeritChanged += handler;
            try
            {
                economy.AddMerit(500);
                Assert.AreEqual(1500, economy.MeritPile.ToDouble(), 0.01);
                Assert.IsTrue(seen.HasValue);
                Assert.AreEqual(1500, seen.Value, 0.01);
            }
            finally
            {
                economy.OnMeritChanged -= handler;
            }
        }

        [Test]
        public void TrySpendMerit_SucceedsWhenEnough()
        {
            bool ok = economy.TrySpendMerit(300);
            Assert.IsTrue(ok);
            Assert.AreEqual(700, economy.MeritPile.ToDouble(), 0.01);
        }

        [Test]
        public void TrySpendMerit_FailsWhenNotEnough_PileUnchanged()
        {
            bool ok = economy.TrySpendMerit(5000);
            Assert.IsFalse(ok);
            Assert.AreEqual(1000, economy.MeritPile.ToDouble(), 0.01);
        }

        [Test]
        public void TrySpendMerit_FailsForNegativeAmount()
        {
            Assert.IsFalse(economy.TrySpendMerit(-1));
        }

        [Test]
        public void YutToken_AddCapsAtMax()
        {
            economy.TrySpendYutToken(5);
            economy.AddYutToken(10);
            Assert.AreEqual(economy.YutTokenMax, economy.YutToken);
        }

        [Test]
        public void TrySpendYutToken_FailsWhenInsufficient_LeavesTokenUnchanged()
        {
            bool ok = economy.TrySpendYutToken(999);
            Assert.IsFalse(ok);
            Assert.AreEqual(5, economy.YutToken);
        }

        [Test]
        public void EnsureYutTokenFresh_WhileFull_NeverStartsCountdown()
        {
            var now = DateTime.UtcNow;
            economy.EnsureYutTokenFresh(now);
            Assert.AreEqual(0, economy.YutTokenRegenNextUtcTicks);

            economy.EnsureYutTokenFresh(now.AddDays(1));
            Assert.AreEqual(5, economy.YutToken); // 가득 찬 동안은 그냥 대기 없이 유지
        }

        [Test]
        public void TrySpendYutToken_FromFull_StartsThirtyMinuteCountdown()
        {
            var before = DateTime.UtcNow;
            economy.TrySpendYutToken(1);
            var expectedNoEarlierThan = before.Add(GameEconomy.YutTokenRegenInterval).Ticks;

            Assert.GreaterOrEqual(economy.YutTokenRegenNextUtcTicks, expectedNoEarlierThan);
        }

        [Test]
        public void EnsureYutTokenFresh_AfterIntervalElapses_GrantsOneToken()
        {
            var now = DateTime.UtcNow;
            economy.TrySpendYutToken(1); // 5 -> 4, 카운트다운 시작

            economy.EnsureYutTokenFresh(now.Add(GameEconomy.YutTokenRegenInterval).AddSeconds(1));

            Assert.AreEqual(5, economy.YutToken);
            Assert.AreEqual(0, economy.YutTokenRegenNextUtcTicks); // 다시 가득 참 -> 대기 없음
        }

        [Test]
        public void EnsureYutTokenFresh_LongOfflineGap_CatchesUpMultipleIntervalsButCapsAtMax()
        {
            var now = DateTime.UtcNow;
            economy.TrySpendYutToken(5); // 5 -> 0

            // 30분 x 10만큼 지났다고 가정 — 최대치(5)를 넘길 수 없어야 한다.
            var muchLater = now.Add(TimeSpan.FromTicks(GameEconomy.YutTokenRegenInterval.Ticks * 10));
            economy.EnsureYutTokenFresh(muchLater);

            Assert.AreEqual(5, economy.YutToken);
            Assert.AreEqual(0, economy.YutTokenRegenNextUtcTicks);
        }

        [Test]
        public void EnsureYutTokenFresh_BeforeIntervalElapses_GrantsNothing()
        {
            var now = DateTime.UtcNow;
            economy.TrySpendYutToken(1); // 5 -> 4

            economy.EnsureYutTokenFresh(now.Add(GameEconomy.YutTokenRegenInterval).AddSeconds(-1));

            Assert.AreEqual(4, economy.YutToken);
        }

        [Test]
        public void Yeopjeon_AddAndSpendRoundTrip()
        {
            economy.AddYeopjeon(100);
            Assert.AreEqual(100, economy.Yeopjeon);
            Assert.IsTrue(economy.TrySpendYeopjeon(40));
            Assert.AreEqual(60, economy.Yeopjeon);
            Assert.IsFalse(economy.TrySpendYeopjeon(1000));
        }

        [Test]
        public void PropsPurchasedCount_IncrementsAndClampsAtZeroWhenSetNegative()
        {
            economy.IncrementPropsPurchasedCount();
            economy.IncrementPropsPurchasedCount();
            Assert.AreEqual(2, economy.PropsPurchasedCount);

            economy.SetPropsPurchasedCount(-5);
            Assert.AreEqual(0, economy.PropsPurchasedCount);
        }

        [Test]
        public void BatchMerit_ClaimAppliesMultiplierAndClearsPending()
        {
            economy.AddPendingBatchMerit(100);
            Assert.IsTrue(economy.HasPendingBatchMerit);

            bool claimed = economy.TryClaimBatchMerit(multiplier: 3);

            Assert.IsTrue(claimed);
            Assert.IsFalse(economy.HasPendingBatchMerit);
            Assert.AreEqual(1300, economy.MeritPile.ToDouble(), 0.01); // 시작 1000 + 100*3
        }

        [Test]
        public void TryClaimBatchMerit_FailsWhenNothingPending()
        {
            Assert.IsFalse(economy.TryClaimBatchMerit());
        }

        [Test]
        public void OfferingInventory_CaptureReplaceRoundTrip()
        {
            var carrot = ScriptableObject.CreateInstance<OfferingData>();
            carrot.offeringId = "carrot";
            carrot.kind = OfferingKind.General;

            var peach = ScriptableObject.CreateInstance<OfferingData>();
            peach.offeringId = "peach";
            peach.kind = OfferingKind.Preferred;

            try
            {
                economy.AddOffering(carrot, 3);
                economy.AddOffering(peach, 1);
                Assert.AreEqual(3, economy.GetOfferingCount("carrot"));
                Assert.AreEqual(1, economy.GetOfferingCount(peach));

                var buf = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
                economy.CaptureOfferingCounts(buf);
                Assert.AreEqual(2, buf.Count);

                economy.ReplaceOfferingCounts(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, int>("carrot", 7),
                });
                Assert.AreEqual(7, economy.GetOfferingCount("carrot"));
                Assert.AreEqual(0, economy.GetOfferingCount("peach"));
            }
            finally
            {
                Object.DestroyImmediate(carrot);
                Object.DestroyImmediate(peach);
            }
        }
    }
}
