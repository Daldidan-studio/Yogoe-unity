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

        private const float StaminaDrainPerSecond = 1f / 600f; // 10분당 1
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
            var prop = FindOccupiedProp(agent, data);
            // 만창: 앉아만 있고 생산·기력소모 정지 (수거는 복귀 후 플레이어가)
            if (prop != null && IsHalted(prop))
            {
                agent.stateTimer += dt;
                return dt;
            }

            float drain = StaminaDrainPerSecond;
            float timeToZero = agent.stamina > 0f ? agent.stamina / drain : 0f;
            float slice = Math.Min(dt, timeToZero);

            if (slice <= 0f)
            {
                agent.stamina = 0f;
                EnterPlayingExhausted(agent);
                return 0.0001f;
            }

            float worked = prop != null ? Produce(agent, prop, slice) : slice;
            if (worked <= 0f && prop != null && IsHalted(prop))
                return 0.0001f;

            agent.stateTimer += worked;
            agent.stamina -= worked * drain;
            if (agent.stamina < 0f) agent.stamina = 0f;

            if (agent.stamina <= 0f)
            {
                agent.stamina = 0f;
                EnterPlayingExhausted(agent);
            }

            return worked > 0f ? worked : slice;
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

        static PropSave FindOccupiedProp(AgentSave agent, GameSaveData data)
        {
            if (string.IsNullOrEmpty(agent.occupiedPropId) || data.props == null) return null;
            var prop = FindProp(data, agent.occupiedPropId);
            return prop != null && prop.isBuilt ? prop : null;
        }

        /// <summary>시트(props.json) 설정. 없으면 구세이브 호환: 분당 산출이 있으면 공덕 기물.</summary>
        static PropCatalog.Entry ConfigFor(PropSave prop)
        {
            if (PropCatalog.TryGet(prop.propId, out var e)) return e;
            return new PropCatalog.Entry
            {
                propId = prop.propId,
                resourceType = prop.baseProductionPerMinute > 0 ? "Merit" : "None",
                meritPerMinute = prop.baseProductionPerMinute,
                levelGrowth = ProductionFormula.LevelGrowth,
                intimacyBonus = true,
                ownerMultiplier = 2.0
            };
        }

        static bool IsResource(PropResourceType t) =>
            t == PropResourceType.PurifiedWater || t == PropResourceType.Yeopjeon
            || t == PropResourceType.Hunt || t == PropResourceType.Gather;

        static bool IsHalted(PropSave prop)
        {
            var cfg = ConfigFor(prop);
            var type = cfg.ResourceType;
            int level = Math.Max(1, prop.level);
            if (IsResource(type))
            {
                var st = ToState(prop);
                return PropStorage.IsHalted(st, PropStorage.Capacity(cfg.baseCapacity, level));
            }
            if (type == PropResourceType.Merit)
            {
                double cap = ProductionFormula.MeritCapacity(cfg.meritPerMinute, level, cfg.levelGrowth, cfg.meritCapacityMinutes);
                return !double.IsInfinity(cap) && prop.pendingMerit.ToBigNumber().ToDouble() >= cap - 0.0001;
            }
            return false;
        }

        static PropStorage.State ToState(PropSave prop) => new PropStorage.State
        {
            Stored = prop.storedResources,
            CycleProgressSeconds = prop.cycleProgressSeconds,
            OverflowJudged = prop.overflowJudged
        };

        /// <summary>온라인 PropSlot.ProduceWhileStaying와 같은 규칙. 반환 = 일한 초.</summary>
        private static float Produce(AgentSave agent, PropSave prop, float dt)
        {
            var cfg = ConfigFor(prop);
            var type = cfg.ResourceType;
            int level = Math.Max(1, prop.level);

            if (type == PropResourceType.Merit)
            {
                bool sameOwner = !string.IsNullOrEmpty(prop.ownerCharacterId)
                                 && prop.ownerCharacterId == agent.characterId;
                double perMinute = ProductionFormula.PerMinute(
                    cfg.meritPerMinute, level, agent.intimacy, prop.isEndingProp, sameOwner,
                    cfg.levelGrowth > 0 ? cfg.levelGrowth : ProductionFormula.LevelGrowth,
                    cfg.intimacyBonus, cfg.ownerMultiplier > 0 ? cfg.ownerMultiplier : 1.0);
                if (perMinute <= 0) return dt;

                double cap = ProductionFormula.MeritCapacity(cfg.meritPerMinute, level, cfg.levelGrowth, cfg.meritCapacityMinutes);
                double pile = prop.pendingMerit.ToBigNumber().ToDouble();
                double room = double.IsInfinity(cap) ? double.MaxValue : cap - pile;
                if (room <= 0.0001) return 0f;
                float worked = (float)Math.Min(dt, room / perMinute * 60.0);
                var cur = prop.pendingMerit.ToBigNumber() + (BigNumber)(perMinute / 60.0 * worked);
                prop.pendingMerit = BigNumberSave.From(cur);
                return worked;
            }

            if (IsResource(type))
            {
                var st = ToState(prop);
                var ingredients = new System.Collections.Generic.List<int>(prop.pendingIngredients ?? Array.Empty<int>());
                // 오프라인 오버플로우 판정도 온라인과 동일하게 적용 (Docs/05 7항 미확정 — 바뀌면 여기만)
                float worked = PropStorage.Advance(ref st, cfg.cycleMinutes * 60f,
                    PropStorage.Capacity(cfg.baseCapacity, level), PropStorage.OverflowChance(level), dt,
                    () => UnityEngine.Random.value,
                    () =>
                    {
                        if (type == PropResourceType.Hunt || type == PropResourceType.Gather)
                            ingredients.Add(PropCatalog.RollDrop(type, UnityEngine.Random.value));
                    });
                prop.storedResources = st.Stored;
                prop.cycleProgressSeconds = st.CycleProgressSeconds;
                prop.overflowJudged = st.OverflowJudged;
                prop.pendingIngredients = ingredients.ToArray();
                return worked;
            }

            return dt; // 화덕 등 산출 없음
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
