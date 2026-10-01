using System;
using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Characters;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.UI;

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

            GameSaveMigration.MigrateToCurrent(data);

            var sim = OfflineSimulator.Simulate(data, TrustedTime.UtcNow);
            if (sim.SimulatedSeconds > 1f)
            {
                Debug.Log($"[GameSaveBridge] 오프라인 따라잡기 {sim.SimulatedSeconds:F0}s " +
                          $"(실제 경과 {sim.ElapsedSeconds:F0}s, 상한 {OfflineSimulator.MaxOfflineSeconds:F0}s)");
            }

            ApplyToWorld(data);
            // 공덕 더미는 기물에 남겨 두고 버드나무에서 수거한다 (7-4). 예전의 콜드스타트 일괄 스윕은 폐지.
            RefreshAllPropPileLabels();
            return true;
        }

        /// <summary>모든 기물 PendingMerit를 일괄 수거 대기분으로 옮긴다.</summary>
        public static void SweepPropPilesIntoBatch()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<PropSlot>(FindObjectsSortMode.None))
            {
                if (p == null || !p.HasPendingMerit) continue;
                GameEconomy.Instance.AddPendingBatchMerit(p.TakePendingMerit());
            }
        }

        /// <summary>기물 더미 표시를 즉시 맞춘다 (일괄 수거·로드 직후).</summary>
        public static void RefreshAllPropPileLabels()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<PropSlot>(FindObjectsSortMode.None))
                p?.ForceRefreshPileLabel();
        }

        public static void SaveFromWorld()
        {
            // Play 중이 아니거나 Economy 부팅 전이면 OnApplicationQuit 등에서 NRE 남
            if (GameEconomy.Instance == null) return;
            var data = CaptureFromWorld();
            GameSaveService.Save(data);
        }

        public static GameSaveData CaptureFromWorld()
        {
            if (GameEconomy.Instance == null)
                throw new System.InvalidOperationException(
                    "[GameSaveBridge] GameEconomy.Instance 가 없습니다. SaveFromWorld는 Play 중에만 호출하세요.");

            var data = new GameSaveData
            {
                version = GameSaveMigration.CurrentVersion,
                savedAtUtcTicks = TrustedTime.UtcNow.Ticks,
                economy = new EconomySave
                {
                    merit = BigNumberSave.From(GameEconomy.Instance.MeritPile),
                    pendingBatchMerit = BigNumberSave.From(GameEconomy.Instance.PendingBatchMerit),
                    yeopjeon = GameEconomy.Instance.Yeopjeon,
                    hyang = GameEconomy.Instance.Hyang,
                    water = GameEconomy.Instance.Water,
                    yutToken = GameEconomy.Instance.YutToken,
                    yutTokenMax = GameEconomy.Instance.YutTokenMax,
                    yutTokenRegenNextUtcTicks = GameEconomy.Instance.YutTokenRegenNextUtcTicks,
                    propsPurchasedCount = GameEconomy.Instance.PropsPurchasedCount,
                    giftMissStreak = 0,
                    giftFirstGrantDone = false,
                    adRewardTickets = 0
                }
            };
            GiftBundle.CaptureToSave(out data.economy.giftMissStreak, out data.economy.giftFirstGrantDone, out data.economy.adRewardTickets);
            ShopStock.CaptureToSave(out data.economy.shopLeftOfferingId, out data.economy.shopRightOfferingId, out data.economy.shopNextRefreshUtcTicks);
            data.economy.offerings = CaptureOfferings(GameEconomy.Instance);
            data.economy.materials = GameEconomy.Instance.CaptureMaterialCounts();
            data.economy.specialItems = GameEconomy.Instance.CaptureSpecialItemCounts();
            data.economy.charms = GameEconomy.Instance.CaptureCharmCounts();
            Attendance.CaptureToSave(out data.economy.attendanceNextDayIndex, out data.economy.attendanceLastHandledDayKey);
            data.economy.lockedSlotUnlocked = CharacterSummon.LockedSlotUnlocked;

            // Props
            var props = UnityEngine.Object.FindObjectsByType<PropSlot>(FindObjectsSortMode.None);
            data.props = new PropSave[props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                string id = p.data != null ? p.data.propId : p.name;
                p.CaptureStorage(out int stored, out float cycleProgress, out bool judged, out int[] ingredients);
                data.props[i] = new PropSave
                {
                    storedResources = stored,
                    cycleProgressSeconds = cycleProgress,
                    overflowJudged = judged,
                    pendingIngredients = ingredients,
                    propId = id,
                    level = p.level,
                    isBuilt = p.IsBuilt,
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
                    intimacy = a.Stats.Intimacy,
                    stamina = a.Stats.Stamina,
                    state = a.Stats.State,
                    stateTimer = a.Stats.StateTimer,
                    posX = a.transform.position.x,
                    posY = a.transform.position.y,
                    occupiedPropId = propId,
                    revealedPreferredOfferingIds = a.Stats.RevealedPreferredOfferingIds.ToArray(),
                    goldenBuffEndsUtcTicks = a.Stats.GoldenBuffEndsUtcTicks
                };
            }

            if (YutScreen.Instance != null)
                data.yutMatch = YutScreen.Instance.CaptureForSave();

            return data;
        }

        public static void ApplyToWorld(GameSaveData data)
        {
            if (data == null) return;

            // Economy — StartingState를 덮어쓴다
            ApplyEconomy(data.economy);

            // Props — 점유 초기화 후 더미·레벨 반영
            var props = UnityEngine.Object.FindObjectsByType<PropSlot>(FindObjectsSortMode.None);
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
                        p.ApplySaveBuiltState(ps.isBuilt, ps.level);
                        p.SetPendingMeritFromSave(ps.pendingMerit.ToBigNumber());
                        p.RestoreStorage(ps.storedResources, ps.cycleProgressSeconds, ps.overflowJudged, ps.pendingIngredients);
                        break;
                    }
                }
            }

            // Agents — 세이브에만 있는 고라니 등 먼저 스폰
            EnsureMissingAgentsFromSave(data.agents);

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
                            ags.intimacy,
                            ags.stamina,
                            ags.state,
                            ags.stateTimer,
                            new Vector3(ags.posX, ags.posY, a.transform.position.z),
                            occupy);
                        a.Stats.SetRevealedPreferences(ags.revealedPreferredOfferingIds);
                        a.Stats.GoldenBuffEndsUtcTicks = ags.goldenBuffEndsUtcTicks;
                        break;
                    }
                }
            }

            // 진행 중이던 윷놀이 매치 — 있으면 조용히 복원(화면은 유저가 윷놀이를 열 때 이어짐).
            if (YutScreen.Instance != null)
                YutScreen.Instance.ApplyFromSave(data.yutMatch);
        }

        /// <summary>콜드스타트 시 세이브에 고라니가 있으면 월드에 스폰 (향 소모 없음).</summary>
        /// <summary>콜드스타트 시 세이브에 있는 소환 요괴(고라니·구미호)를 월드에 스폰 (향 소모 없음).</summary>
        private static void EnsureMissingAgentsFromSave(AgentSave[] agents)
        {
            if (agents == null) return;
            foreach (var ags in agents)
            {
                if (ags == null || string.IsNullOrEmpty(ags.characterId)) continue;
                CharacterId id;
                if (ags.characterId == nameof(CharacterId.Gorani) || ags.characterId == "고라니") id = CharacterId.Gorani;
                else if (ags.characterId == nameof(CharacterId.Gumiho) || ags.characterId == "구미호") id = CharacterId.Gumiho;
                else continue;
                if (CharacterSummon.IsPresent(id)) continue;
                CharacterSummon.SpawnForSaveRestore(id, null, new Vector3(ags.posX, ags.posY, 0f));
            }
        }

        private static void ApplyEconomy(EconomySave e)
        {
            if (e == null) return;
            GameEconomy.Instance.ApplySaveSnapshot(
                e.merit.ToBigNumber(),
                e.pendingBatchMerit != null ? e.pendingBatchMerit.ToBigNumber() : BigNumber.Zero,
                e.yeopjeon,
                e.hyang,
                e.water,
                e.yutToken,
                e.yutTokenMax,
                e.propsPurchasedCount,
                e.yutTokenRegenNextUtcTicks);
            // null = 구세이브(필드 없음) → StartingState 인벤 유지. 배열 있으면(빈 배열 포함) 통째 교체.
            if (e.offerings != null)
                ApplyOfferings(GameEconomy.Instance, e.offerings);
            if (e.materials != null && e.materials.Length > 0)
                GameEconomy.Instance.ReplaceMaterialCounts(e.materials);
            if (e.specialItems != null && e.specialItems.Length > 0)
                GameEconomy.Instance.ReplaceSpecialItemCounts(e.specialItems);
            if (e.charms != null && e.charms.Length > 0)
                GameEconomy.Instance.ReplaceCharmCounts(e.charms);
            else
                GameEconomy.Instance.ReplaceCharmCounts(null);
            GiftBundle.ResetFromSave(e.giftMissStreak, e.giftFirstGrantDone, e.adRewardTickets);
            ShopStock.ResetFromSave(e.shopLeftOfferingId, e.shopRightOfferingId, e.shopNextRefreshUtcTicks);
            Attendance.ResetFromSave(e.attendanceNextDayIndex, e.attendanceLastHandledDayKey);
            CharacterSummon.ResetFromSave(e.lockedSlotUnlocked);
        }

        static OfferingCountSave[] CaptureOfferings(GameEconomy eco)
        {
            var buf = new List<KeyValuePair<string, int>>(8);
            eco.CaptureOfferingCounts(buf);
            if (buf.Count == 0) return Array.Empty<OfferingCountSave>();
            var arr = new OfferingCountSave[buf.Count];
            for (int i = 0; i < buf.Count; i++)
            {
                arr[i] = new OfferingCountSave
                {
                    offeringId = buf[i].Key,
                    count = buf[i].Value
                };
            }
            return arr;
        }

        static void ApplyOfferings(GameEconomy eco, OfferingCountSave[] offerings)
        {
            // 구세이브의 조합별 id(saenggogi_bbb 등)는 하나로 합친다
            var merged = new Dictionary<string, int>();
            var order = new List<string>(offerings.Length);
            for (int i = 0; i < offerings.Length; i++)
            {
                var o = offerings[i];
                if (o == null || string.IsNullOrEmpty(o.offeringId) || o.count <= 0) continue;
                string id = Yoegoe.Cooking.CookingRecipeCatalog.CanonicalProductId(o.offeringId);
                if (!merged.ContainsKey(id)) { merged[id] = 0; order.Add(id); }
                merged[id] += o.count;
            }
            var buf = new List<KeyValuePair<string, int>>(order.Count);
            foreach (var id in order)
                buf.Add(new KeyValuePair<string, int>(id, merged[id]));
            eco.ReplaceOfferingCounts(buf);
        }

        private static string FindOccupiedPropId(CharacterAgent agent)
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<PropSlot>(FindObjectsSortMode.None))
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
