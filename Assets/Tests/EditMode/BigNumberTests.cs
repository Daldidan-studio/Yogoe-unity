using NUnit.Framework;
using Yoegoe.Core;

namespace Yoegoe.Tests
{
    public class BigNumberTests
    {
        [Test]
        public void Add_SmallValues()
        {
            BigNumber a = 100;
            BigNumber b = 50;
            Assert.AreEqual(150.0, (a + b).ToDouble(), 0.01);
        }

        [Test]
        public void ImplicitFromInt()
        {
            BigNumber n = 800;
            Assert.AreEqual(800.0, n.ToDouble(), 0.01);
        }
    }
}
