using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yoegoe.Minigames.Yut;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// 최근 윷 룰 회귀: 대기말 참 홉, 완주 후보 WillFinish, 이무기 한 바퀴·특수칸 재배치,
    /// 이무기 갈림길(유저 말 추적). UI(동 반짝임/드래그 비활성)는 EditMode 밖.
    /// </summary>
    public class YutRecentRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            YutBoardLayout.RestoreSpecialSquares(null);
        }

        static YutMatch MatchWith(params string[] ids)
        {
            var team = new List<(string id, string name)>();
            foreach (var id in ids)
                team.Add((id, id));
            return new YutMatch(team);
        }

        static YutThrowOutcome Outcome(YutThrowResult result, bool bonus = false) =>
            new YutThrowOutcome(result, bonus);

        static void PlacePlayer(YutMatch match, string id, int nodeId)
        {
            foreach (var p in match.PlayerPieces)
            {
                if (p.Id != id) continue;
                p.NodeId = nodeId;
                p.Finished = false;
                p.History.Clear();
                if (nodeId >= 0) p.History.Add(nodeId);
            }
        }

        static void PlaceOpponent(YutMatch match, int nodeId)
        {
            match.OpponentPiece.NodeId = nodeId;
            match.OpponentPiece.Finished = false;
            match.OpponentPiece.History.Clear();
            if (nodeId >= 0) match.OpponentPiece.History.Add(nodeId);
        }

        static Dictionary<YutBoardLayout.SpecialSquareKind, int> CountKinds()
        {
            var counts = new Dictionary<YutBoardLayout.SpecialSquareKind, int>();
            for (int i = 0; i < YutBoardLayout.NodeCount; i++)
            {
                var kind = YutBoardLayout.GetSpecialKind(i);
                if (kind == YutBoardLayout.SpecialSquareKind.None) continue;
                if (!counts.ContainsKey(kind)) counts[kind] = 0;
                counts[kind]++;
            }
            return counts;
        }

        static HashSet<int> OccupiedSpecialNodes()
        {
            var set = new HashSet<int>();
            for (int i = 0; i < YutBoardLayout.NodeCount; i++)
                if (YutBoardLayout.IsSpecialReward(i)) set.Add(i);
            return set;
        }

        // ---------- 대기말 입장 홉: 참먹이 먼저 ----------

        [Test]
        public void WaitingEntry_Do_HopStartsAtCham()
        {
            var match = MatchWith("a");
            // NodeId -1 = 대기
            var preview = match.PreviewPlayerHop("a", useShortcut: false, Outcome(YutThrowResult.Do));

            Assert.IsTrue(preview.Ok);
            Assert.GreaterOrEqual(preview.HopNodes.Length, 1);
            Assert.AreEqual(YutBoardLayout.Start, preview.HopNodes[0]);
            Assert.AreEqual(1, preview.HopNodes[preview.HopNodes.Length - 1]);
        }

        [Test]
        public void WaitingEntry_Baekdo_HopsChamThen19()
        {
            var match = MatchWith("a");
            var preview = match.PreviewPlayerHop("a", useShortcut: false, Outcome(YutThrowResult.Baekdo));

            Assert.IsTrue(preview.Ok);
            CollectionAssert.AreEqual(new[] { YutBoardLayout.Start, 19 }, preview.HopNodes);
        }

        [Test]
        public void BoardPiece_Do_DoesNotPrependCham()
        {
            var match = MatchWith("a");
            PlacePlayer(match, "a", 3);
            var preview = match.PreviewPlayerHop("a", useShortcut: false, Outcome(YutThrowResult.Do));

            Assert.IsTrue(preview.Ok);
            Assert.AreEqual(4, preview.HopNodes[0]);
            Assert.AreNotEqual(YutBoardLayout.Start, preview.HopNodes[0]);
        }

        // ---------- 완주 후보: WillFinish / DestinationNode ----------

        [Test]
        public void Candidate_AlreadyAtStart_WillFinish_DestinationIsStart()
        {
            var match = MatchWith("a");
            PlacePlayer(match, "a", YutBoardLayout.Start);

            var c = match.GetPlayerCandidates(YutThrowResult.Do).First(x => x.PieceId == "a");

            Assert.IsTrue(c.WillFinish);
            Assert.AreEqual(YutBoardLayout.Start, c.DestinationNode);
        }

        [Test]
        public void Candidate_LandExactlyOnStart_WillFinishFalse()
        {
            // 28에서 도 → 참에 딱 멈춤(완주 아님).
            var match = MatchWith("a");
            PlacePlayer(match, "a", 28);

            var c = match.GetPlayerCandidates(YutThrowResult.Do).First(x => x.PieceId == "a");

            Assert.IsFalse(c.WillFinish);
            Assert.AreEqual(YutBoardLayout.Start, c.DestinationNode);
        }

        [Test]
        public void Preview_AlreadyAtStart_WillFinish_EmptyHops()
        {
            var match = MatchWith("a");
            PlacePlayer(match, "a", YutBoardLayout.Start);
            var preview = match.PreviewPlayerHop("a", false, Outcome(YutThrowResult.Gae));

            Assert.IsTrue(preview.WillFinish);
            Assert.AreEqual(0, preview.HopNodes.Length);
        }

        // ---------- 특수 칸 재배치 ----------

        [Test]
        public void ReshuffleRemaining_KeepsKinds_DoesNotReviveCleared()
        {
            YutBoardLayout.RestoreSpecialSquares(new Dictionary<int, YutBoardLayout.SpecialSquareKind>
            {
                { 1, YutBoardLayout.SpecialSquareKind.Coin },
                { 2, YutBoardLayout.SpecialSquareKind.Coin },
                { 3, YutBoardLayout.SpecialSquareKind.Offering },
                { 4, YutBoardLayout.SpecialSquareKind.Treasure },
            });
            YutBoardLayout.ClearSpecialSquare(1); // 소진

            var beforeKinds = CountKinds();
            Assert.AreEqual(1, beforeKinds[YutBoardLayout.SpecialSquareKind.Coin]);
            Assert.AreEqual(1, beforeKinds[YutBoardLayout.SpecialSquareKind.Offering]);
            Assert.AreEqual(1, beforeKinds[YutBoardLayout.SpecialSquareKind.Treasure]);

            Random.InitState(7);
            YutBoardLayout.ReshuffleRemainingSpecialSquares();

            var afterKinds = CountKinds();
            Assert.AreEqual(beforeKinds[YutBoardLayout.SpecialSquareKind.Coin], afterKinds[YutBoardLayout.SpecialSquareKind.Coin]);
            Assert.AreEqual(beforeKinds[YutBoardLayout.SpecialSquareKind.Offering], afterKinds[YutBoardLayout.SpecialSquareKind.Offering]);
            Assert.AreEqual(beforeKinds[YutBoardLayout.SpecialSquareKind.Treasure], afterKinds[YutBoardLayout.SpecialSquareKind.Treasure]);
            Assert.AreEqual(3, OccupiedSpecialNodes().Count);

            foreach (int node in OccupiedSpecialNodes())
            {
                Assert.AreNotEqual(YutBoardLayout.Start, node);
                Assert.AreNotEqual(YutBoardLayout.Mo, node);
                Assert.AreNotEqual(YutBoardLayout.DwitMo, node);
                Assert.AreNotEqual(YutBoardLayout.JjiMo, node);
                Assert.AreNotEqual(YutBoardLayout.Bang, node);
            }
        }

        [Test]
        public void ReshuffleRemaining_CanMoveNodes()
        {
            YutBoardLayout.RestoreSpecialSquares(new Dictionary<int, YutBoardLayout.SpecialSquareKind>
            {
                { 1, YutBoardLayout.SpecialSquareKind.Coin },
                { 2, YutBoardLayout.SpecialSquareKind.Offering },
            });
            var before = OccupiedSpecialNodes();

            bool moved = false;
            for (int seed = 0; seed < 40; seed++)
            {
                YutBoardLayout.RestoreSpecialSquares(new Dictionary<int, YutBoardLayout.SpecialSquareKind>
                {
                    { 1, YutBoardLayout.SpecialSquareKind.Coin },
                    { 2, YutBoardLayout.SpecialSquareKind.Offering },
                });
                Random.InitState(seed);
                YutBoardLayout.ReshuffleRemainingSpecialSquares();
                if (!before.SetEquals(OccupiedSpecialNodes()))
                {
                    moved = true;
                    break;
                }
            }

            Assert.IsTrue(moved, "여러 시드 중 적어도 한 번은 위치가 바뀌어야 한다");
        }

        // ---------- 이무기 한 바퀴 ----------

        [Test]
        public void Opponent_AtStart_ThrowDo_RaisesOnOpponentLapped()
        {
            var match = MatchWith("a");
            PlacePlayer(match, "a", 5);
            PlaceOpponent(match, YutBoardLayout.Start);

            bool lapped = false;
            match.OnOpponentLapped += () => lapped = true;

            match.ApplyOpponentMove(Outcome(YutThrowResult.Do));

            Assert.IsTrue(lapped);
            Assert.AreEqual(1, match.OpponentPiece.NodeId);
        }

        [Test]
        public void Opponent_PassThroughStart_RaisesOnOpponentLapped()
        {
            // 28에서 개(2) → 28→참→1, 참을 지나감.
            var match = MatchWith("a");
            PlacePlayer(match, "a", 5);
            PlaceOpponent(match, 28);

            bool lapped = false;
            match.OnOpponentLapped += () => lapped = true;

            match.ApplyOpponentMove(Outcome(YutThrowResult.Gae));

            Assert.IsTrue(lapped);
            Assert.AreEqual(1, match.OpponentPiece.NodeId);
        }

        [Test]
        public void Opponent_FirstEntry_DoesNotCountAsLap()
        {
            var match = MatchWith("a");
            PlacePlayer(match, "a", 5);
            // 이무기 대기(NodeId -1)
            Assert.IsFalse(match.OpponentPiece.OnBoard);

            bool lapped = false;
            match.OnOpponentLapped += () => lapped = true;

            match.ApplyOpponentMove(Outcome(YutThrowResult.Do));

            Assert.IsFalse(lapped);
            Assert.AreEqual(1, match.OpponentPiece.NodeId);
        }

        [Test]
        public void Opponent_LandExactlyOnStart_DoesNotLapYet()
        {
            var match = MatchWith("a");
            PlacePlayer(match, "a", 5);
            PlaceOpponent(match, 28);

            bool lapped = false;
            match.OnOpponentLapped += () => lapped = true;

            match.ApplyOpponentMove(Outcome(YutThrowResult.Do)); // 28→참 착지

            Assert.IsFalse(lapped);
            Assert.AreEqual(YutBoardLayout.Start, match.OpponentPiece.NodeId);
        }

        // ---------- 이무기 갈림길: 유저 말에 가까운 쪽 ----------

        [Test]
        public void OpponentAtMo_ChoosesShortcut_WhenPlayerOnShortcut()
        {
            // 모에서 도: 지름길→20, 바깥→6. 유저가 20이면 지름길.
            var match = MatchWith("a");
            PlacePlayer(match, "a", 20);
            PlaceOpponent(match, YutBoardLayout.Mo);

            var preview = match.PreviewOpponentHop(Outcome(YutThrowResult.Do));
            Assert.IsTrue(preview.Ok);
            Assert.AreEqual(20, preview.HopNodes[preview.HopNodes.Length - 1]);

            match.ApplyOpponentMove(Outcome(YutThrowResult.Do));
            Assert.AreEqual(20, match.OpponentPiece.NodeId);
        }

        [Test]
        public void OpponentAtMo_ChoosesOuter_WhenPlayerOnOuter()
        {
            var match = MatchWith("a");
            PlacePlayer(match, "a", 6);
            PlaceOpponent(match, YutBoardLayout.Mo);

            var preview = match.PreviewOpponentHop(Outcome(YutThrowResult.Do));
            Assert.IsTrue(preview.Ok);
            Assert.AreEqual(6, preview.HopNodes[preview.HopNodes.Length - 1]);

            match.ApplyOpponentMove(Outcome(YutThrowResult.Do));
            Assert.AreEqual(6, match.OpponentPiece.NodeId);
        }

        [Test]
        public void OpponentAtMo_Geol_ChoosesPathCloserToPlayer()
        {
            // 모+걸: 지름 20→21→22, 바깥 6→7→8.
            var match = MatchWith("a");
            PlacePlayer(match, "a", 22);
            PlaceOpponent(match, YutBoardLayout.Mo);

            match.ApplyOpponentMove(Outcome(YutThrowResult.Geol));
            Assert.AreEqual(22, match.OpponentPiece.NodeId);

            var match2 = MatchWith("a");
            PlacePlayer(match2, "a", 8);
            PlaceOpponent(match2, YutBoardLayout.Mo);
            match2.ApplyOpponentMove(Outcome(YutThrowResult.Geol));
            Assert.AreEqual(8, match2.OpponentPiece.NodeId);
        }
    }
}
