using System.Collections.Generic;
using NUnit.Framework;
using Yoegoe.Minigames.Yut;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// 완주 보상 배율의 근거는 "이번 골인에 같이 들어온 업기 스택 수"다.
    /// YutMatch가 OnMatchEnded(finishStackCount) / OnPlayerPieceFinished(ids)로
    /// 그 수를 넘기는 계약을 고정한다 — UI(YutScreen)는 이 값만 믿고 ×보상·광고 분기를 탄다.
    /// </summary>
    public class YutMatchFinishStackTests
    {
        static YutMatch MatchWith(params string[] ids)
        {
            var team = new List<(string id, string name)>();
            foreach (var id in ids)
                team.Add((id, id));
            return new YutMatch(team);
        }

        /// <summary>참(0)에 서 있으면 다음 던지기(빽도 제외)로 즉시 완주.</summary>
        static void PlaceOnStart(YutMatch match, params string[] ids)
        {
            var want = new HashSet<string>(ids);
            foreach (var p in match.PlayerPieces)
            {
                if (!want.Contains(p.Id)) continue;
                p.NodeId = YutBoardLayout.Start;
                p.Finished = false;
                p.History.Clear();
            }
        }

        static YutThrowOutcome DoThrow() =>
            new YutThrowOutcome(YutThrowResult.Do, grantsBonusThrow: false);

        [Test]
        public void EndAsFinished_PassesStackCountToOnMatchEnded()
        {
            var match = MatchWith("a", "b", "c");
            int? endedStack = null;
            match.OnMatchEnded += stack => endedStack = stack;

            match.EndAsFinished(2);

            Assert.IsTrue(match.IsEnded);
            Assert.AreEqual(2, endedStack);
        }

        [Test]
        public void EndAsFinished_ZeroOrNegative_BecomesOne()
        {
            var match = MatchWith("a");
            int? endedStack = null;
            match.OnMatchEnded += stack => endedStack = stack;

            match.EndAsFinished(0);

            Assert.AreEqual(1, endedStack);
        }

        [Test]
        public void PartialFinish_RaisesFinishedWithStack_DoesNotEndMatch()
        {
            // 4마리 중 2마리만 참에 업혀 골인 → 매치는 안 끝나고 Finished(2)만.
            var match = MatchWith("a", "b", "c", "d");
            PlaceOnStart(match, "a", "b");

            IReadOnlyList<string> finished = null;
            int? endedStack = null;
            match.OnPlayerPieceFinished += ids => finished = ids;
            match.OnMatchEnded += stack => endedStack = stack;

            match.ApplyPlayerMove("a", useShortcut: false, DoThrow());

            Assert.IsFalse(match.IsEnded);
            Assert.IsNull(endedStack);
            Assert.IsNotNull(finished);
            Assert.AreEqual(2, finished.Count);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, finished);
        }

        [Test]
        public void AllFourStackedFinish_EndsMatchWithStackFour()
        {
            var match = MatchWith("a", "b", "c", "d");
            PlaceOnStart(match, "a", "b", "c", "d");

            IReadOnlyList<string> finished = null;
            int? endedStack = null;
            match.OnPlayerPieceFinished += ids => finished = ids;
            match.OnMatchEnded += stack => endedStack = stack;

            match.ApplyPlayerMove("a", useShortcut: false, DoThrow());

            Assert.IsTrue(match.IsEnded);
            Assert.AreEqual(4, endedStack);
            // 전원 완주 시 Finished 이벤트는 안 타고 Ended만 탄다(순서 계약).
            Assert.IsNull(finished);
            Assert.IsTrue(match.PlayerPieces[0].Finished);
            Assert.IsTrue(match.PlayerPieces[1].Finished);
            Assert.IsTrue(match.PlayerPieces[2].Finished);
            Assert.IsTrue(match.PlayerPieces[3].Finished);
        }

        [Test]
        public void ThreeStackedFinish_WhenTeamIsThree_EndsWithStackThree()
        {
            // 광고 2배(4마리) 해당 없음 — 스택 3만 넘기면 UI가 광고를 안 띄운다.
            var match = MatchWith("a", "b", "c");
            PlaceOnStart(match, "a", "b", "c");

            int? endedStack = null;
            match.OnMatchEnded += stack => endedStack = stack;

            match.ApplyPlayerMove("b", useShortcut: false, DoThrow());

            Assert.IsTrue(match.IsEnded);
            Assert.AreEqual(3, endedStack);
        }

        [Test]
        public void FinishRewardAmounts_MatchScreenContract()
        {
            // YutRewards: 정화수 1/마리, 배율 = stack, 광고 시 ×2.
            Assert.AreEqual(1, YutRewards.FinishPurifiedWaterAmount(1));
            Assert.AreEqual(2, YutRewards.FinishPurifiedWaterAmount(2));
            Assert.AreEqual(4, YutRewards.FinishPurifiedWaterAmount(YutRewards.FinishAdBonusStackCount));
            Assert.AreEqual(8, YutRewards.FinishPurifiedWaterAmount(YutRewards.FinishAdBonusStackCount)
                * YutRewards.SquareRewardAdMultiplier);
            Assert.AreEqual(3, YutRewards.FinishPurifiedWaterAmount(3));
            Assert.IsFalse(YutRewards.OffersFinishAdBonus(3));
            Assert.IsTrue(YutRewards.OffersFinishAdBonus(4));
        }
    }
}
