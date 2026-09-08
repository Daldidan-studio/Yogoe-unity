using System;
using Yoegoe.Core;
using Yoegoe.Data;

namespace Yoegoe.Save
{
    /// <summary>
    /// 저장 시각~현재까지 경과 시간을 GameSaveData 위에서 따라잡는다.
    /// 씬을 직접 건드리지 않고 DTO만 수정 → Apply 단계에서 월드에 반영.
    /// 공식은 CharacterAgent / 기획 6·7장과 동일(오프라인=동일 속도).
    /// </summary>
    public static class OfflineSimulator
    {
        public static float MaxOfflineSeconds = 12f * 60f * 60f;

        private const float StaminaDrainPerSecond = 1f / 20f; // 기획: 20초당 1
        private const float StayDurationSeconds = 5f * 60f;
        private const float FaintThresholdSeconds = 12f * 60f * 60f;
        private const float PlayDurationSeconds = 5f * 60f;

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
                    if (agent.stage == GrowthStage.Neok) continue; // 넋: 자연 기력 감소·생산 없음
                    SimulateAgent(agent, data, elapsed);
                }
            }

            data.savedAtUtcTicks = utcNow.Ticks;
            result.Applied = true;
            return result;
        }

        private static void SimulateAgent(AgentSave agent, GameSaveData data, float remaining)
        {
            // 상태 전환이 있을 수 있어 구간을 나눠 소진한다.
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
                        used = SimulateSlumped(agent, remaining);
                        break;
                    case ActionState.Fainted:
                        return; // 더 이상 진행 없음
                    case ActionState.Playing:
                        used = SimulatePlaying(agent, remaining);
                        break;
                    case ActionState.Walking:
                    default:
                        // 오프라인 걷기는 생산 0. 타이머만 진행(방황 재추첨 등은 생략).
                        agent.stateTimer += remaining;
                        return;
                }
                remaining -= used;
            }
        }

        /// <summary>머물기: 기력 있는 동안·5분 한도 안에서 기물 더미에 생산.</summary>
        private static float SimulateStaying(AgentSave agent, GameSaveData data, float dt)
        {
            float drain = StaminaDrainPerSecond;
            if (drain <= 0f) drain = 1f / 20f;

            float timeToZero = agent.stamina > 0f ? agent.stamina / drain : 0f;
            float timeToStayEnd = Math.Max(0f, StayDurationSeconds - agent.stateTimer);
            float slice = Math.Min(dt, Math.Min(timeToZero, timeToStayEnd));

            if (slice <= 0f)
            {
                // 이미 기력 0이거나 머물기 시간 초과
                if (agent.stamina <= 0f)
                {
                    agent.stamina = 0f;
                    EnterSlumped(agent);
                }
                else
                {
                    LeavePropKeepPile(agent);
                    EnterWalking(agent);
                }
                return 0.0001f; // 진행 보장
            }

            agent.stateTimer += slice;
            agent.stamina -= slice * drain;
            if (agent.stamina < 0f) agent.stamina = 0f;

            AddProduction(agent, data, slice);

            if (agent.stamina <= 0f)
            {
                agent.stamina = 0f;
                EnterSlumped(agent); // 기물 점유 유지
            }
            else if (agent.stateTimer >= StayDurationSeconds)
            {
                LeavePropKeepPile(agent);
                EnterWalking(agent);
            }

            return slice;
        }

        private static float SimulateSlumped(AgentSave agent, float dt)
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

        private static float SimulatePlaying(AgentSave agent, float dt)
        {
            float timeToEnd = Math.Max(0f, PlayDurationSeconds - agent.stateTimer);
            float slice = Math.Min(dt, timeToEnd > 0f ? timeToEnd : dt);
            agent.stateTimer += slice;
            if (agent.stateTimer >= PlayDurationSeconds)
                EnterWalking(agent);
            return slice <= 0f ? dt : slice;
        }

        private static void AddProduction(AgentSave agent, GameSaveData data, float dt)
        {
            if (string.IsNullOrEmpty(agent.occupiedPropId) || data.props == null) return;
            var prop = FindProp(data, agent.occupiedPropId);
            if (prop == null) return;

            double basePerMin = prop.baseProductionPerMinute;
            if (basePerMin <= 0) basePerMin = 100;
            int level = Math.Max(1, prop.level);
            double perMinute = basePerMin * Math.Pow(1.1, level - 1)
                               * (1.0 + agent.intimacy / 100.0)
                               * EndingMultiplier(agent, prop);

            var add = BigNumberSave.From((BigNumber)(perMinute / 60.0 * dt));
            var cur = prop.pendingMerit.ToBigNumber() + add.ToBigNumber();
            prop.pendingMerit = BigNumberSave.From(cur);
        }

        private static double EndingMultiplier(AgentSave agent, PropSave prop)
        {
            if (!prop.isEndingProp) return 1.0;
            if (string.IsNullOrEmpty(prop.ownerCharacterId)) return 1.0;
            return prop.ownerCharacterId == agent.characterId ? 2.0 : 1.0;
        }

        private static PropSave FindProp(GameSaveData data, string propId)
        {
            foreach (var p in data.props)
            {
                if (p != null && p.propId == propId) return p;
            }
            return null;
        }

        private static void EnterSlumped(AgentSave agent)
        {
            agent.state = ActionState.Slumped;
            agent.stateTimer = 0f;
            // occupiedPropId 유지 (6-2)
        }

        private static void EnterWalking(AgentSave agent)
        {
            agent.state = ActionState.Walking;
            agent.stateTimer = 0f;
        }

        private static void LeavePropKeepPile(AgentSave agent)
        {
            agent.occupiedPropId = "";
        }
    }
}
