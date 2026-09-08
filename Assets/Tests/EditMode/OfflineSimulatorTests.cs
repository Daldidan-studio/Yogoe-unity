using System;
using NUnit.Framework;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Save;

namespace Yoegoe.Tests
{
    public class OfflineSimulatorTests
    {
        const double BasePerMinute = 100.0;

        [Test]
        public void StayFiveMinutes_GoesToPlaying_AndClearsOccupy()
        {
            var data = MakeStaySave(stamina: 100f, stateTimer: 0f);
            var now = SavedAt(data).AddMinutes(5);

            var result = OfflineSimulator.Simulate(data, now);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(ActionState.Playing, data.agents[0].state);
            Assert.AreEqual(0f, data.agents[0].stateTimer, 0.01f);
            Assert.AreEqual("", data.agents[0].occupiedPropId);
        }

        [Test]
        public void PlayingFiveMinutes_GoesToWalking()
        {
            var data = MakeSave(
                new AgentSave
                {
                    characterId = "gumiho",
                    stage = GrowthStage.Hon,
                    intimacy = 0f,
                    stamina = 80f,
                    state = ActionState.Playing,
                    stateTimer = 0f,
                    occupiedPropId = ""
                },
                MakeProp("well"));

            var now = SavedAt(data).AddMinutes(5);
            OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(ActionState.Walking, data.agents[0].state);
        }

        [Test]
        public void StayThenPlay_FullLoop_EndsWalking()
        {
            // 머물기 5분 + 놀기 5분 = 걷기
            var data = MakeStaySave(stamina: 100f, stateTimer: 0f);
            var now = SavedAt(data).AddMinutes(10);

            OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(ActionState.Walking, data.agents[0].state);
            Assert.AreEqual("", data.agents[0].occupiedPropId);
        }

        [Test]
        public void Staying_AddsMeritToPropPile()
        {
            var data = MakeStaySave(stamina: 100f, stateTimer: 0f, intimacy: 0f);
            var now = SavedAt(data).AddSeconds(60); // 1분

            OfflineSimulator.Simulate(data, now);

            // 분당 100, 보정 1.0 → 1분에 100
            Assert.AreEqual(100.0, data.props[0].pendingMerit.ToBigNumber().ToDouble(), 0.5);
            Assert.AreEqual(ActionState.Staying, data.agents[0].state);
        }

        [Test]
        public void StaminaDepletes_GoesSlumped_KeepsOccupy()
        {
            // 기력 1 → 20초면 0
            var data = MakeStaySave(stamina: 1f, stateTimer: 0f);
            var now = SavedAt(data).AddSeconds(30);

            OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(ActionState.Slumped, data.agents[0].state);
            Assert.AreEqual("well", data.agents[0].occupiedPropId);
            Assert.AreEqual(0f, data.agents[0].stamina, 0.01f);
        }

        [Test]
        public void CapsAtMaxOfflineSeconds()
        {
            var data = MakeStaySave(stamina: 100f, stateTimer: 0f);
            var now = SavedAt(data).AddHours(48);

            var result = OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(48f * 3600f, result.ElapsedSeconds, 1f);
            Assert.AreEqual(OfflineSimulator.MaxOfflineSeconds, result.SimulatedSeconds, 1f);
        }

        [Test]
        public void Neok_IsSkipped()
        {
            var data = MakeSave(
                new AgentSave
                {
                    characterId = "gorani",
                    stage = GrowthStage.Neok,
                    stamina = 50f,
                    state = ActionState.Staying,
                    stateTimer = 0f,
                    occupiedPropId = "well"
                },
                MakeProp("well"));

            var now = SavedAt(data).AddMinutes(10);
            OfflineSimulator.Simulate(data, now);

            Assert.AreEqual(ActionState.Staying, data.agents[0].state);
            Assert.AreEqual(0.0, data.props[0].pendingMerit.ToBigNumber().ToDouble(), 0.01);
        }

        [Test]
        public void OwnEndingProp_DoublesProduction()
        {
            var prop = MakeProp("mortar");
            prop.isEndingProp = true;
            prop.ownerCharacterId = "rabbit";
            var data = MakeSave(
                new AgentSave
                {
                    characterId = "rabbit",
                    stage = GrowthStage.Hon,
                    intimacy = 0f,
                    stamina = 100f,
                    state = ActionState.Staying,
                    stateTimer = 0f,
                    occupiedPropId = "mortar"
                },
                prop);

            OfflineSimulator.Simulate(data, SavedAt(data).AddSeconds(60));

            Assert.AreEqual(200.0, data.props[0].pendingMerit.ToBigNumber().ToDouble(), 0.5);
        }

        static GameSaveData MakeStaySave(float stamina, float stateTimer, float intimacy = 0f)
        {
            return MakeSave(
                new AgentSave
                {
                    characterId = "rabbit",
                    stage = GrowthStage.Hon,
                    intimacy = intimacy,
                    stamina = stamina,
                    state = ActionState.Staying,
                    stateTimer = stateTimer,
                    occupiedPropId = "well"
                },
                MakeProp("well"));
        }

        static GameSaveData MakeSave(AgentSave agent, PropSave prop)
        {
            var savedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return new GameSaveData
            {
                savedAtUtcTicks = savedAt.Ticks,
                agents = new[] { agent },
                props = new[] { prop }
            };
        }

        static PropSave MakeProp(string id) => new PropSave
        {
            propId = id,
            level = 1,
            isBuilt = true,
            pendingMerit = BigNumberSave.From(BigNumber.Zero),
            baseProductionPerMinute = BasePerMinute
        };

        static DateTime SavedAt(GameSaveData data) =>
            new DateTime(data.savedAtUtcTicks, DateTimeKind.Utc);
    }
}
