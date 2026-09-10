using NUnit.Framework;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Data;

namespace Yoegoe.Tests.EditMode
{
    /// <summary>
    /// CharacterAgent 상태 전환(6-2) 중 공개 API로 직접 유도 가능한 부분만 검증한다.
    ///
    /// EditMode에서는 AddComponent 시 Awake/Start가 보장되지 않으므로(GameEconomyTests 참고),
    /// Stats(공개 필드)를 직접 세팅해 픽스처를 만들고 실제 전환은 전부 공개 API
    /// (TrySitOnProp / EnterPlaying / ReceiveOffering)로만 유도한다.
    ///
    /// 커버 못 하는 부분: 자연 기력 감소로 인한 자동 주저앉기/기절, 도착 시 자동 착석,
    /// 5분 놀기 자동 종료 — 전부 Update()의 private Tick* 메서드가 Time.deltaTime을 직접
    /// 참조해서 EditMode에서 결정론적으로 재현할 수 없다. (PlayMode 테스트나 internal 테스트
    /// 시드 없이는 검증 불가 — 필요하면 별도로 논의.)
    /// </summary>
    public class CharacterAgentStateTests
    {
        GameObject agentGO;
        CharacterAgent agent;
        CharacterData data;

        GameObject propGO;
        PropSlot prop;
        PropData propData;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance<CharacterData>();
            data.id = CharacterId.Rabbit;

            agentGO = new GameObject("Agent_Test");
            agent = agentGO.AddComponent<CharacterAgent>();
            agent.Data = data;
            agent.Stats.Stage = GrowthStage.Hon;
            agent.Stats.State = ActionState.Walking;
            agent.Stats.Stamina = 100f;
            agent.Stats.Intimacy = 0f;

            propData = ScriptableObject.CreateInstance<PropData>();
            propData.propId = "well";
            propData.baseProductionPerMinute = 100;

            propGO = new GameObject("Prop_Test");
            prop = propGO.AddComponent<PropSlot>();
            prop.data = propData;
            prop.ConfigureBuiltState(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (agentGO != null) Object.DestroyImmediate(agentGO);
            if (data != null) Object.DestroyImmediate(data);
            if (propGO != null) Object.DestroyImmediate(propGO);
            if (propData != null) Object.DestroyImmediate(propData);
        }

        // ---------------- Walking/Playing -> Staying (TrySitOnProp) ----------------

        [Test]
        public void TrySitOnProp_OnEmptyBuiltProp_EntersStayingAndOccupies()
        {
            bool ok = agent.TrySitOnProp(prop);

            Assert.IsTrue(ok);
            Assert.AreEqual(ActionState.Staying, agent.Stats.State);
            Assert.AreSame(agent, prop.Occupant);
        }

        [Test]
        public void TrySitOnProp_WhenPropAlreadyOccupied_FailsWithoutChangingState()
        {
            var otherGO = new GameObject("Agent_Other");
            var other = otherGO.AddComponent<CharacterAgent>();
            try
            {
                prop.TryOccupy(other);

                bool ok = agent.TrySitOnProp(prop);

                Assert.IsFalse(ok);
                Assert.AreEqual(ActionState.Walking, agent.Stats.State);
                Assert.AreSame(other, prop.Occupant);
            }
            finally
            {
                Object.DestroyImmediate(otherGO);
            }
        }

        [Test]
        public void TrySitOnProp_WhenNeok_AlwaysFails()
        {
            agent.Stats.Stage = GrowthStage.Neok;

            Assert.IsFalse(agent.TrySitOnProp(prop));
            Assert.IsFalse(prop.IsOccupied);
        }

        [Test]
        public void TrySitOnProp_WhenFainted_Fails()
        {
            agent.Stats.State = ActionState.Fainted;
            Assert.IsFalse(agent.TrySitOnProp(prop));
        }

        [Test]
        public void TrySitOnProp_WhenSlumped_Fails()
        {
            // 6-2: 주저앉기 드래그는 별도 경로(SettleSlumpedAfterDrag) — TrySitOnProp으론 못 앉힌다.
            agent.Stats.State = ActionState.Slumped;
            Assert.IsFalse(agent.TrySitOnProp(prop));
        }

        [Test]
        public void TrySitOnProp_EndingProp_OnlyOwnerCanSit()
        {
            propData.isEndingProp = true;
            propData.owner = CharacterId.Rabbit;
            data.id = CharacterId.Gumiho; // 다른 요괴

            Assert.IsFalse(agent.TrySitOnProp(prop));
            Assert.IsFalse(prop.IsOccupied);

            data.id = CharacterId.Rabbit; // 주인
            Assert.IsTrue(agent.TrySitOnProp(prop));
        }

        // ---------------- Staying -> Playing (EnterPlaying) ----------------

        [Test]
        public void EnterPlaying_FromStaying_VacatesPropButKeepsPile()
        {
            agent.TrySitOnProp(prop);
            prop.AddToMeritPile(10);

            agent.EnterPlaying();

            Assert.AreEqual(ActionState.Playing, agent.Stats.State);
            Assert.IsFalse(prop.IsOccupied);
            Assert.IsTrue(prop.HasPendingMerit); // 요괴가 떠나도 더미는 기물에 남음 (7-2)
        }

        [Test]
        public void EnterPlaying_WhenNeok_NoOp()
        {
            agent.Stats.Stage = GrowthStage.Neok;

            agent.EnterPlaying();

            Assert.AreEqual(ActionState.Walking, agent.Stats.State);
        }

        [Test]
        public void EnterPlaying_WhenFainted_NoOp()
        {
            agent.Stats.State = ActionState.Fainted;

            agent.EnterPlaying();

            Assert.AreEqual(ActionState.Fainted, agent.Stats.State);
        }

        // ---------------- Slumped/Fainted -> Walking (ReceiveOffering) ----------------

        [Test]
        public void ReceiveOffering_WakesSlumpedAgent_AndVacatesProp()
        {
            agent.TrySitOnProp(prop);
            agent.Stats.State = ActionState.Slumped; // 6-2: 주저앉기 중에도 점유는 유지된다
            agent.Stats.Stamina = 0f;

            agent.ReceiveOffering(staminaGain: 30, intimacyGain: 0.25f);

            Assert.AreEqual(ActionState.Walking, agent.Stats.State);
            Assert.AreEqual(30f, agent.Stats.Stamina, 0.01f);
            Assert.IsFalse(prop.IsOccupied); // 상세 공양으로 기상 → 기물에서 일어남
        }

        [Test]
        public void ReceiveOffering_WakesFaintedAgent()
        {
            agent.Stats.State = ActionState.Fainted;
            agent.Stats.Stamina = 0f;

            agent.ReceiveOffering(staminaGain: 30, intimacyGain: 0.25f);

            Assert.AreEqual(ActionState.Walking, agent.Stats.State);
        }

        [Test]
        public void ReceiveOffering_ClampsStaminaAndIntimacyAt100()
        {
            agent.Stats.Stamina = 90f;
            agent.Stats.Intimacy = 99.9f;

            agent.ReceiveOffering(staminaGain: 30, intimacyGain: 5f);

            Assert.AreEqual(100f, agent.Stats.Stamina, 0.0001f);
            Assert.AreEqual(100f, agent.Stats.Intimacy, 0.0001f);
        }

        [Test]
        public void ReceiveOffering_Neok_IgnoresGeneralOffering()
        {
            agent.Stats.Stage = GrowthStage.Neok;
            agent.Stats.Stamina = 0f;

            agent.ReceiveOffering(30, 0.25f, OfferingKind.General);

            Assert.AreEqual(0f, agent.Stats.Stamina); // 넋은 정화수만 기력을 채운다
        }

        [Test]
        public void ReceiveOffering_Neok_PurifiedWaterFillsStamina()
        {
            agent.Stats.Stage = GrowthStage.Neok;
            agent.Stats.Stamina = 0f;

            agent.ReceiveOffering(30, 0f, OfferingKind.PurifiedWater);

            Assert.AreEqual(30f, agent.Stats.Stamina, 0.01f);
        }

        // ---------------- 생산량 조회 (온라인 경로가 ProductionFormula를 실제로 쓰는지) ----------------

        [Test]
        public void GetProductionPerMinuteIfStaying_ZeroWhenNotStaying()
        {
            Assert.AreEqual(0, agent.GetProductionPerMinuteIfStaying());
        }

        [Test]
        public void GetProductionPerMinuteIfStaying_CombinesLevelAndIntimacy()
        {
            prop.level = 2;
            agent.Stats.Intimacy = 50f;
            agent.TrySitOnProp(prop);

            // 100 * 1.1(레벨2) * 1.5(친밀도50)
            Assert.AreEqual(100.0 * 1.1 * 1.5, agent.GetProductionPerMinuteIfStaying(), 0.0001);
        }
    }
}
