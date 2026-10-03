using NUnit.Framework;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>상점 진열 리셋 비용 = 떡절구 현재 분당 산출 × 5 (12장).</summary>
    public class ShopRerollTests
    {
        [TestCase(100.0, 500.0)]   // Lv1
        [TestCase(110.0, 550.0)]   // Lv2 (100 × 1.1)
        [TestCase(121.0, 605.0)]   // Lv3
        [TestCase(133.1, 666.0)]   // Lv4 — 665.5 올림
        public void RerollCost_IsFiveMinutesOfMortar(double perMinute, double expected)
        {
            Assert.AreEqual(expected, ShopStock.RerollCost(perMinute).ToDouble(), 0.001);
        }

        [Test]
        public void RerollCost_ZeroWithoutMortar()
        {
            Assert.AreEqual(0, ShopStock.RerollCost(0).Mantissa);
        }
    }
}
