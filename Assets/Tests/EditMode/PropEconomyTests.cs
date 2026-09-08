using NUnit.Framework;
using Yoegoe.Economy;

namespace Yoegoe.Tests
{
    public class PropEconomyTests
    {
        [Test]
        public void GetPurchaseCost_FirstIs800()
        {
            Assert.AreEqual(800.0, PropEconomy.GetPurchaseCost(1).ToDouble(), 0.01);
        }

        [Test]
        public void GetPurchaseCost_GrowsBy1_4()
        {
            Assert.AreEqual(800.0 * 1.4, PropEconomy.GetPurchaseCost(2).ToDouble(), 0.01);
            Assert.AreEqual(800.0 * 1.4 * 1.4, PropEconomy.GetPurchaseCost(3).ToDouble(), 0.01);
        }

        [Test]
        public void GetPurchaseCost_ClampsOrderToAtLeastOne()
        {
            Assert.AreEqual(PropEconomy.GetPurchaseCost(1).ToDouble(), PropEconomy.GetPurchaseCost(0).ToDouble(), 0.01);
            Assert.AreEqual(PropEconomy.GetPurchaseCost(1).ToDouble(), PropEconomy.GetPurchaseCost(-3).ToDouble(), 0.01);
        }

        [Test]
        public void GetUpgradeCost_Level1Is500()
        {
            Assert.AreEqual(500.0, PropEconomy.GetUpgradeCost(1).ToDouble(), 0.01);
        }

        [Test]
        public void GetUpgradeCost_GrowsBy1_15()
        {
            Assert.AreEqual(500.0 * 1.15, PropEconomy.GetUpgradeCost(2).ToDouble(), 0.01);
        }
    }
}
