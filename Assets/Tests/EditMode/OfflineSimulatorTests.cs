using System;
using NUnit.Framework;
using Yoegoe.Data;
using Yoegoe.Save;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// OfflineSimulator는 씬·MonoBehaviour 없이 GameSaveData DTO만으로 동작해서
    /// EditMode 테스트로 바로 검증 가능하다 (이 코드베이스에서 드문 순수 로직 클래스).
    /// </summary>
    public class OfflineSimulatorTests
    {
        static GameSaveData MakeSave(DateTime savedAtUtc)
        {
            return new GameSaveData
            {
                savedAtUtcTicks = savedAtUtc.Ticks,
                props = new[]
                {
                    new PropSave { propId = "prop1", isBuilt = true, baseProductionPerMinute = 60 }
                },
                agents = new[]
                {
                    new AgentSave
                    {
                        characterId = "rabbit",
                        stage = GrowthStage.Hon,
                        stamina = 100f,
                        intimacy = 0f,
                        state = ActionState.Staying,
                        occupiedPropId = "prop1"
                    }
                }
            };
        }

        [Test]
        public void Simulate_NoSavedTimestamp_ReturnsNotApplied()
        {
            var data = new GameSaveData { savedAtUtcTicks = 0 };
            var result = OfflineSimulator.Simulate(data, DateTime.UtcNow);
            Assert.IsFalse(result.Applied);
        }

        [Test]
        public void Simulate_SavedTimeInFuture_DoesNothing()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddMinutes(5)); // 시계가 되감긴 것처럼 저장 시각이 미래
            var result = OfflineSimulator.Simulate(data, now);
            Assert.AreEqual(0f, result.ElapsedSeconds);
            Assert.IsFalse(result.Applied);
        }

        [Test]
        public void Simulate_CapsSimulatedSecondsAtMaxOffline()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddSeconds(-OfflineSimulator.MaxOfflineSeconds * 2));
            var result = OfflineSimulator.Simulate(data, now);
            Assert.AreEqual(OfflineSimulator.MaxOfflineSeconds, result.SimulatedSeconds, 0.01f);
        }

        [Test]
        public void Simulate_StayingAgent_DrainsStaminaAndProducesMerit()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddSeconds(-100)); // 100초 경과, 기력 100 -> 20초당 1 소모

            var result = OfflineSimulator.Simulate(data, now);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(95f, data.agents[0].stamina, 0.01f);
            Assert.Greater(data.props[0].pendingMerit.ToBigNumber().ToDouble(), 0);
        }

        [Test]
        public void Simulate_StaminaReachesZero_EntersSlumpedButKeepsProp()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddHours(-1)); // 기력 100 -> 2000초만에 0, 남는 시간은 Slumped

            OfflineSimulator.Simulate(data, now);

            var agent = data.agents[0];
            Assert.AreEqual(ActionState.Slumped, agent.state);
            Assert.AreEqual(0f, agent.stamina, 0.01f);
            // 6-2 규칙: 주저앉기 중에도 점유 기물은 유지한다.
            Assert.AreEqual("prop1", agent.occupiedPropId);
        }

        [Test]
        public void Simulate_SlumpedPastThreshold_Faints()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddSeconds(-100));
            data.agents[0].state = ActionState.Slumped;
            data.agents[0].stamina = 0f;
            data.agents[0].stateTimer = 12f * 60f * 60f - 10f; // 기절까지 10초 남음

            OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(ActionState.Fainted, data.agents[0].state);
        }

        [Test]
        public void Simulate_FaintedAgent_NeverRevivesOffline()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddHours(-6));
            data.agents[0].state = ActionState.Fainted;
            data.agents[0].stamina = 0f;

            OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(ActionState.Fainted, data.agents[0].state);
        }

        [Test]
        public void Simulate_NeokAgent_IsUnaffected()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddSeconds(-100));
            data.agents[0].stage = GrowthStage.Neok;

            OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(100f, data.agents[0].stamina); // 넋은 자연 기력 변화 없음
        }

        [Test]
        public void Simulate_WalkingAgent_OnlyAdvancesTimerWithNoProduction()
        {
            var now = DateTime.UtcNow;
            var data = MakeSave(now.AddSeconds(-100));
            data.agents[0].state = ActionState.Walking;
            data.agents[0].stamina = 50f;

            OfflineSimulator.Simulate(data, now);

            var agent = data.agents[0];
            Assert.AreEqual(50f, agent.stamina); // 걷기는 기력 소모·생산 없음
            Assert.AreEqual(100f, agent.stateTimer, 0.01f);
            Assert.AreEqual(0.0, data.props[0].pendingMerit.ToBigNumber().ToDouble());
        }
    }
}
