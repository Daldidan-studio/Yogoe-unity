using NUnit.Framework;
using Yoegoe.Core;

namespace Yoegoe.Tests.EditMode
{
    public class BigNumberTests
    {
        [Test]
        public void Zero_HasZeroMantissaAndExponent()
        {
            Assert.AreEqual(0, BigNumber.Zero.Mantissa);
            Assert.AreEqual(0, BigNumber.Zero.Exponent);
        }

        [TestCase(5, 0)]
        [TestCase(999, 2)]
        [TestCase(1000, 3)]
        [TestCase(1500, 3)]
        [TestCase(999999, 5)]
        [TestCase(-2500, 3)]
        public void FromDouble_NormalizesMantissaBetween1And10(double value, int expectedExponent)
        {
            var n = BigNumber.FromDouble(value);
            Assert.AreEqual(expectedExponent, n.Exponent);
            Assert.GreaterOrEqual(System.Math.Abs(n.Mantissa), 1.0);
            Assert.Less(System.Math.Abs(n.Mantissa), 10.0);
        }

        [Test]
        public void FromDouble_Zero_StaysZero()
        {
            var n = BigNumber.FromDouble(0);
            Assert.AreEqual(BigNumber.Zero, n);
        }

        [Test]
        public void ToDouble_RoundTripsThroughFromDouble()
        {
            var n = BigNumber.FromDouble(123456.789);
            Assert.AreEqual(123456.789, n.ToDouble(), 0.01);
        }

        [Test]
        public void Addition_SameExponent_SumsMantissas()
        {
            var a = new BigNumber(1.5, 3); // 1500
            var b = new BigNumber(2.5, 3); // 2500
            var sum = a + b;
            Assert.AreEqual(4000, sum.ToDouble(), 0.01);
        }

        [Test]
        public void Addition_WithZero_ReturnsOtherOperandUnchanged()
        {
            var a = BigNumber.FromDouble(500);
            Assert.AreEqual(a, a + BigNumber.Zero);
            Assert.AreEqual(a, BigNumber.Zero + a);
        }

        [Test]
        public void Addition_HugeExponentGap_SmallerOperandIsIgnored()
        {
            // double 정밀도(약 15자리)를 넘는 지수 차이는 더해봐야 사라지므로 큰 쪽만 남는다.
            var huge = new BigNumber(1, 20);
            var tiny = new BigNumber(1, 2);
            Assert.AreEqual(huge, huge + tiny);
        }

        [Test]
        public void Subtraction_ProducesExpectedResult()
        {
            var a = BigNumber.FromDouble(1000);
            var b = BigNumber.FromDouble(300);
            Assert.AreEqual(700, (a - b).ToDouble(), 0.01);
        }

        [Test]
        public void Multiplication_ByScalar_ScalesValue()
        {
            var a = BigNumber.FromDouble(200);
            Assert.AreEqual(600, (a * 3.0).ToDouble(), 0.01);
        }

        [Test]
        public void Multiplication_ByBigNumber_MultipliesMantissasAndAddsExponents()
        {
            var a = BigNumber.FromDouble(200); // 2 * 10^2
            var b = BigNumber.FromDouble(30);  // 3 * 10^1
            Assert.AreEqual(6000, (a * b).ToDouble(), 0.5);
        }

        [Test]
        public void Comparison_OrdersByExponentThenMantissa()
        {
            var small = BigNumber.FromDouble(999);
            var big = BigNumber.FromDouble(1000);
            Assert.IsTrue(big > small);
            Assert.IsTrue(small < big);
            Assert.IsTrue(big >= big);
            Assert.IsTrue(small <= small);
        }

        [Test]
        public void Comparison_NegativeIsLessThanPositive()
        {
            var neg = BigNumber.FromDouble(-5);
            var pos = BigNumber.FromDouble(5);
            Assert.IsTrue(neg < pos);
        }

        [TestCase(0, "0")]
        [TestCase(999, "999")]
        public void ToDisplayString_BelowFirstUnit_ShowsPlainNumber(double value, string expected)
        {
            Assert.AreEqual(expected, BigNumber.FromDouble(value).ToDisplayString());
        }

        [Test]
        public void ToDisplayString_AtFirstUnit_ShowsGiyeokWithTwoDecimals()
        {
            // 1,000 = 지수 3 = 첫 단위(ㄱ). 값이 10 미만이면 소수점 2자리까지 보여준다.
            Assert.AreEqual("1.00ㄱ", BigNumber.FromDouble(1000).ToDisplayString());
        }

        [Test]
        public void ToDisplayString_SecondUnit_UsesNieun()
        {
            StringAssert.Contains("ㄴ", BigNumber.FromDouble(1_000_000).ToDisplayString());
        }

        [Test]
        public void ToDisplayString_After14thUnit_RepeatsLetter()
        {
            // 단위 레벨 15 = 지수 45 = ㄱ을 두 번 겹쳐 ㄱㄱ (기획서 3장 확장 규칙)
            var n = new BigNumber(1, 45);
            StringAssert.Contains("ㄱㄱ", n.ToDisplayString());
        }

        [Test]
        public void ToDisplayString_NegativeValue_KeepsMinusSign()
        {
            Assert.IsTrue(BigNumber.FromDouble(-5000).ToDisplayString().StartsWith("-"));
        }
    }
}
