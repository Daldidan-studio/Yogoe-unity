using NUnit.Framework;
using Yoegoe.Minigames.Yut;

namespace Yoegoe.Tests.EditMode
{
    public class YutChallengeTests
    {
        [Test]
        public void ConsecutiveMo_CompletesAtNeededStreak()
        {
            var c = YutChallengeState.Restore(YutChallengeKind.ConsecutiveMo, false, false, 0);
            Assert.IsFalse(c.OnPlayerThrow(YutThrowResult.Do));
            Assert.AreEqual(0, c.Streak);
            Assert.IsFalse(c.OnPlayerThrow(YutThrowResult.Mo));
            Assert.AreEqual(1, c.Streak);
            Assert.IsTrue(c.OnPlayerThrow(YutThrowResult.Mo));
            Assert.IsTrue(c.Completed);
        }

        [Test]
        public void ConsecutiveBaekdo_ResetsOnOtherResult()
        {
            var c = YutChallengeState.Restore(YutChallengeKind.ConsecutiveBaekdo, false, false, 0);
            Assert.IsFalse(c.OnPlayerThrow(YutThrowResult.Baekdo));
            Assert.IsFalse(c.OnPlayerThrow(YutThrowResult.Do));
            Assert.AreEqual(0, c.Streak);
            Assert.IsFalse(c.Completed);
        }

        [Test]
        public void FinishAllUncaptured_FailsOnCapture_CompletesWhenAllFinished()
        {
            var c = YutChallengeState.Restore(YutChallengeKind.FinishAllUncaptured, false, false, 0);
            Assert.IsFalse(c.TryCompleteAllFinished(4, 3));
            c.OnPlayerCaptured();
            Assert.IsTrue(c.Failed);
            Assert.IsFalse(c.TryCompleteAllFinished(4, 4));

            var ok = YutChallengeState.Restore(YutChallengeKind.FinishAllUncaptured, false, false, 0);
            Assert.IsTrue(ok.TryCompleteAllFinished(4, 4));
            Assert.IsTrue(ok.Completed);
        }

        [Test]
        public void Pick_WithFewerThanFour_ExcludesFinishAll()
        {
            for (int i = 0; i < 40; i++)
            {
                var c = YutChallengeState.Pick(3);
                Assert.AreNotEqual(YutChallengeKind.FinishAllUncaptured, c.Kind);
            }
        }
    }
}
