using System;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.Save
{
    /// <summary>
    /// 씬(Agent/Prop/Economy) ↔ GameSaveData 스냅샷.
    /// IO는 GameSaveService, 시간 따라잡기는 OfflineSimulator.
    /// </summary>
    public static class GameSaveBridge
    {
        /// <summary>
        /// 부팅 직후(월드 생성 뒤) 호출.
        /// 세이브 있으면 로드 → 오프라인 시뮬 → 월드 반영.
        /// 없으면 false (기존 StartingState 유지).
        /// </summary>
        public static bool TryLoadSimulateAndApply()
        {
            if (!GameSaveService.TryLoad(out var data)) return false;

            var sim = OfflineSimulator.Simulate(data, DateTime.UtcNow);
            if (sim.SimulatedSeconds > 1f)
            {
                Debug.Log($"[GameSaveBridge] 오프라인 따라잡기 {sim.SimulatedSeconds:F0}s " +
                          $"(실제 경과 {sim.ElapsedSeconds:F0}s, 상한 {OfflineSimulator.MaxOfflineSeconds:F0}s)");
            }

            ApplyToWorld(data);
            return true;
        }

        public static void SaveFromWorld()
        {
            var data = CaptureFromWorld();
            GameSaveService.Save(data);
        }

        public static GameSaveData CaptureFromWorld()
        {
            var data = new GameSaveData
            {
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                economy = new EconomySave
                {
                    merit = BigNumberSave.From(GameEconomy.MeritPile),
                    yeopjeon = GameEconomy.Yeopjeon,
                    hyang = GameEconomy.Hyang,
                    purifiedWater = GameEconomy.PurifiedWater,
                    yutToken = GameEconomy.YutToken,
                    yutTokenMax = GameEconomy.YutTokenMax
                }
            };

            // Props
            var props = UnityEngine.Object.FindObjectsOfType<PropSlot>();
            data.props = new PropSave[props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                string id = p.data != null ? p.data.propId : p.name;
                data.props[i] = new PropSave
                {
                    propId = id,
                    level = p.level,
                    pendingMerit = BigNumberSave.From(p.PendingMerit),
                    baseProductionPerMinute = p.data != null ? p.data.baseProductionPerMinute : 100,
                    isEndingProp = p.data != null && p.data.isEndingProp,
                    ownerCharacterId = p.data != null ? p.data.owner.ToString() : ""
                };
            }

            // Agents
            var agents = CharacterAgent.All;
            data.agents = new AgentSave[agents.Count];
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                string cid = a.Data != null ? a.Data.id.ToString() : a.name;
                string propId = FindOccupiedPropId(a);

                data.agents[i] = new AgentSave
                {
                    characterId = cid,
                    stage = a.Stats.Stage,
                    intimacy = a.Stats.Intimacy,
                    stamina = a.Stats.Stamina,
                    state = a.Stats.State,
                    stateTimer = a.Stats.StateTimer,
                    posX = a.transform.position.x,
                    posY = a.transform.position.y,
                    occupiedPropId = propId
                };
            }

            return data;
        }

        public static void ApplyToWorld(GameSaveData data)
        {
            if (data == null) return;

            // Economy — StartingState를 덮어쓴다
            ApplyEconomy(data.economy);

            // Props — 점유 초기화 후 더미·레벨 반영
            var props = UnityEngine.Object.FindObjectsOfType<PropSlot>();
            foreach (var p in props)
                p.ClearOccupantForSaveRestore();

            if (data.props != null)
            {
                foreach (var ps in data.props)
                {
                    if (ps == null || string.IsNullOrEmpty(ps.propId)) continue;
                    foreach (var p in props)
                    {
                        string id = p.data != null ? p.data.propId : p.name;
                        if (id != ps.propId) continue;
                        p.level = Mathf.Max(1, ps.level);
                        p.SetPendingMeritFromSave(ps.pendingMerit.ToBigNumber());
                        break;
                    }
                }
            }

            // Agents + 기물 점유 복원
            if (data.agents != null)
            {
                foreach (var ags in data.agents)
                {
                    if (ags == null) continue;
                    foreach (var a in CharacterAgent.All)
                    {
                        if (a == null || a.Data == null) continue;
                        string cid = a.Data.id.ToString();
                        if (cid != ags.characterId && a.Data.displayName != ags.characterId) continue;

                        PropSlot occupy = null;
                        if (!string.IsNullOrEmpty(ags.occupiedPropId))
                        {
                            foreach (var p in props)
                            {
                                string id = p.data != null ? p.data.propId : p.name;
                                if (id == ags.occupiedPropId) { occupy = p; break; }
                            }
                        }

                        a.ApplySaveSnapshot(
                            ags.stage,
                            ags.intimacy,
                            ags.stamina,
                            ags.state,
                            ags.stateTimer,
                            new Vector3(ags.posX, ags.posY, a.transform.position.z),
                            occupy);
                        break;
                    }
                }
            }
        }

        private static void ApplyEconomy(EconomySave e)
        {
            if (e == null) return;
            // GameEconomy에 일괄 Set API가 없어 리플렉션 대신 공개 API 확장 필요 — 골격용 최소 반영
            GameEconomy.ApplySaveSnapshot(
                e.merit.ToBigNumber(),
                e.yeopjeon,
                e.hyang,
                e.purifiedWater,
                e.yutToken,
                e.yutTokenMax);
        }

        private static string FindOccupiedPropId(CharacterAgent agent)
        {
            foreach (var p in UnityEngine.Object.FindObjectsOfType<PropSlot>())
            {
                if (p.Occupant == agent)
                {
                    return p.data != null ? p.data.propId : p.name;
                }
            }
            return "";
        }
    }
}
