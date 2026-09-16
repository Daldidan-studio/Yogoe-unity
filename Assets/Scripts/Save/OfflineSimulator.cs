using System;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Save
{
    /// <summary>
    /// 저장 시각~현재까지 경과 시간을 GameSaveData 위에서 따라잡는다.
    /// 씬을 직접 건드리지 않고 DTO만 수정 → Apply 단계에서 월드에 반영.
    /// </summary>
    public static class OfflineSimulator
    {
        public static float MaxOfflineSeconds = 18f * 60f * 60f;

        private const float StaminaDrainPerSecond = 1f / 120f; // 2분당 1
        private const float FaintThresholdSeconds = 18f * 60f * 60f;
        private const float PlayDurationSeconds = 1f * 60f;

        public struct Result
        {
            public float ElapsedSeconds;
            public float SimulatedSeconds;
            public bool Applied;
        }

        public static Result Simulate(GameSaveData data, DateTime utcNow)
        {
            var result = new Result { Applied = false };
            if (data == null || data.savedAtUtcTicks <= 0) return result;

            var savedAt = new DateTime(data.savedAtUtcTicks, DateTimeKind.Utc);
            double raw = (utcNow - savedAt).TotalSeconds;
            if (raw <= 0)
            {
                result.ElapsedSeconds = 0f;
                return result;
            }

            float elapsed = (float)Math.Min(raw, MaxOfflineSeconds);
            result.ElapsedSeconds = (float)raw;
            result.SimulatedSeconds = elapsed;

            if (data.agents != null)
            {
                foreach (var agent in data.agents)
                {
                    if (agent == null) continue;
                    if (agent.stage == GrowthStage.Neok) continue;
                    if (agent.state == ActionState.Slumped)
                        MigrateSlumped(agent);
                    SimulateAgent(agent, data, elapsed);
                }
            }

            data.savedAtUtcTicks = utcNow.Ticks;
            result.Applied = true;
            return result;
        }

        private static void SimulateAgent(AgentSave agent, GameSaveData data, float remaining)
        {
            int guard = 0;
            while (remaining > 0.0001f && guard++ < 64)
            {
                float used = remaining;
                switch (agent.state)
                {
                    case ActionState.Staying:
                        used = SimulateStaying(agent, data, remaining);
                        break;
                    case ActionState.Slumped:
                        MigrateSlumped(agent);
                        used = SimulatePlaying(agent, remaining);
                        break;
                    case ActionState.Fainted:
                        return;
                    case ActionState.Playing:
                        used = SimulatePlaying(agent, remaining);
                        break;
                    case ActionState.Walking:
                    default:
                        agent.stateTimer += remaining;
                        return;
                }
                remaining -= used;
            }
        }

        private static float SimulateStaying(AgentSave agent, GameSaveData data, float dt)
        {
            float drain = StaminaDrainPerSecond;
            float timeToZero = agent.stamina > 0f ? agent.stamina / drain : 0f;
            float slice = Math.Min(dt, timeToZero);

            if (slice <= 0f)
            {
                agent.stamina = 0f;
                EnterPlayingExhausted(agent);
                return 0.0001f;
            }

            agent.stateTimer += slice;
            agent.stamina -= slice * drain;
            if (agent.stamina < 0f) agent.stamina = 0f;

            AddProduction(agent, data, slice);

            if (agent.stamina <= 0f)
            {
                agent.stamina = 0f;
                EnterPlayingExhausted(agent);
            }

            return slice;
        }

        private static float SimulatePlaying(AgentSave agent, float dt)
        {
            if (agent.stamina <= 0f)
            {
                float timeToFaint = Math.Max(0f, FaintThresholdSeconds - agent.stateTimer);
                float slice = Math.Min(dt, timeToFaint > 0f ? timeToFaint : dt);
                agent.stateTimer += slice;
                if (agent.stateTimer >= FaintThresholdSeconds)
                {
                    agent.state = ActionState.Fainted;
                    agent.stateTimer = 0f;
                }
                return slice <= 0f ? dt : slice;
            }

            float timeToEnd = Math.Max(0f, PlayDurationSeconds - agent.stateTimer);
            float slicePlay = Math.Min(dt, timeToEnd > 0f ? timeToEnd : dt);
            agent.stateTimer += slicePlay;
            if (agent.stateTimer >= PlayDurationSeconds)
                EnterWalking(agent);
            return slicePlay <= 0f ? dt : slicePlay;
        }

        private static void AddProduction(AgentSave agent, GameSaveData data, float dt)
        {
            if (string.IsNullOrEmpty(agent.occupiedPropId) || data.props == null) return;
            var prop = FindProp(data, agent.occupiedPropId);
            if (prop == null || !prop.isBuilt) return;

            double basePerMin = prop.baseProductionPerMinute;
            if (basePerMin <= 0) basePerMin = 100;
            int level = Math.Max(1, prop.level);
            bool sameOwner = !string.IsNullOrEmpty(prop.ownerCharacterId)
                             && prop.ownerCharacterId == agent.characterId;
            double perMinute = ProductionFormula.PerMinute(
                basePerMin, level, agent.stage, agent.intimacy, prop.isEndingProp, sameOwner);

            var add = BigNumberSave.From((BigNumber)(perMinute / 60.0 * dt));
            var cur = prop.pendingMerit.ToBigNumber() + add.ToBigNumber();
            prop.pendingMerit = BigNumberSave.From(cur);
        }

        private static PropSave FindProp(GameSaveData data, string propId)
        {
            foreach (var p in data.props)
            {
                if (p != null && p.propId == propId) return p;
            }
            return null;
        }

        private static void MigrateSlumped(AgentSave agent)
        {
            agent.state = ActionState.Playing;
            agent.stamina = 0f;
            agent.occupiedPropId = "";
            // stateTimer 유지 → 기절까지 이어짐
        }

        private static void EnterPlayingExhausted(AgentSave agent)
        {
            agent.occupiedPropId = "";
            agent.state = ActionState.Playing;
            agent.stateTimer = 0f;
            agent.stamina = 0f;
        }

        private static void EnterWalking(AgentSave agent)
        {
            agent.state = ActionState.Walking;
            agent.stateTimer = 0f;
        }
    }
}
