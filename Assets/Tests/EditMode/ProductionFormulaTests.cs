using NUnit.Framework;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// CharacterAgent(온라인 틱)와 OfflineSimulator(오프라인 정산)가 똑같이
    /// ProductionFormula만 호출하게 만든 이유: 예전엔 두 곳에 공식이 복붙돼 있어서
    /// 한쪽만 밸런스 조정하면 온라인/오프라인 생산량이 갈라질 수 있었다.
    /// 이 테스트는 그 회귀를 막는 용도 — 값 자체보다 "이 클래스가 유일한 소스"라는 계약을 고정한다.
    /// </summary>
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
        public void IntimacyMultiplier_NeokIsAlwaysOne()
        {
            Assert.AreEqual(1.0, ProductionFormula.IntimacyMultiplier(GrowthStage.Neok, 80f), 0.0001);
        }

        [Test]
        public void IntimacyMultiplier_HonScalesWithIntimacy()
        {
            Assert.AreEqual(1.0, ProductionFormula.IntimacyMultiplier(GrowthStage.Hon, 0f), 0.0001);
            Assert.AreEqual(1.5, ProductionFormula.IntimacyMultiplier(GrowthStage.Hon, 50f), 0.0001);
            Assert.AreEqual(2.0, ProductionFormula.IntimacyMultiplier(GrowthStage.Hon, 100f), 0.0001);
        }

        [Test]
        public void EndingMultiplier_OnlyDoublesForSameOwner()
        {
            Assert.AreEqual(1.0, ProductionFormula.EndingMultiplier(isEndingProp: false, sameOwner: true), 0.0001);
            Assert.AreEqual(1.0, ProductionFormula.EndingMultiplier(isEndingProp: true, sameOwner: false), 0.0001);
            Assert.AreEqual(2.0, ProductionFormula.EndingMultiplier(isEndingProp: true, sameOwner: true), 0.0001);
        }

        [Test]
        public void PerMinute_CombinesAllThreeMultipliers()
        {
            double result = ProductionFormula.PerMinute(
                baseProductionPerMinute: 100,
                level: 2,
                stage: GrowthStage.Hon,
                intimacy: 50f,
                isEndingProp: true,
                sameOwner: true);

            // 100 * 1.1(레벨2) * 1.5(친밀도50) * 2(엔딩기물)
            Assert.AreEqual(100.0 * 1.1 * 1.5 * 2.0, result, 0.0001);
        }
    }
}
