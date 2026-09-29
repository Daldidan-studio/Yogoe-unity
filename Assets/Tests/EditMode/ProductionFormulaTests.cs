using NUnit.Framework;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>수식 조각만 검증. 규칙 조합(주인 배율·만창)은 PropProductionTests.</summary>
    public class ProductionFormulaTests
    {
        [Test]
        public void LevelMultiplier_GrowsByOnePointOnePerLevel()
        {
            Assert.AreEqual(1.0, ProductionFormula.LevelMultiplier(1), 0.0001);
            Assert.AreEqual(1.1, ProductionFormula.LevelMultiplier(2), 0.0001);
            Assert.AreEqual(1.1 * 1.1, ProductionFormula.LevelMultiplier(3), 0.0001);
        }

        [Test]
        public void IntimacyMultiplier_ScalesWithIntimacy()
        {
            Assert.AreEqual(1.0, ProductionFormula.IntimacyMultiplier(0f), 0.0001);
            Assert.AreEqual(1.5, ProductionFormula.IntimacyMultiplier(50f), 0.0001);
            Assert.AreEqual(2.0, ProductionFormula.IntimacyMultiplier(100f), 0.0001);
        }
    }
}
