using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Cooking;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Minigames.Yut;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>윷놀이 보상 — 특수 칸 배치·지급, 판에서 얻은 물건 기록·완주 정산, 획득 표시. (YutScreen 분할 — 본체는 YutScreen.cs)</summary>
    public partial class YutScreen
    {
        /// <summary>완주 후 Close — 이미 지급된 미연출 획득이 있으면 팀 캐릭터 만세·수거 연출.</summary>
        void TryPresentPostYutLoot()
        {
            if (presentableLoot.Count == 0) return;

            var loot = new List<PostYutLootEntry>(presentableLoot);
            presentableLoot.Clear();

            var team = new List<CharacterAgent>();
            foreach (var kv in teamById)
            {
                if (kv.Value != null) team.Add(kv.Value);
            }
            if (team.Count == 0)
            {
                // 세이브 복원만 된 경우 teamById가 비어 있을 수 있음 — 맵의 요괴로 대체
                for (int i = 0; i < CharacterAgent.All.Count; i++)
                {
                    var a = CharacterAgent.All[i];
                    if (a != null && a.Stats != null)
                        team.Add(a);
                }
            }
            if (team.Count == 0) return;

            PostYutLootPresenter.Ensure().Begin(team, loot);
        }

        /// <summary>매치 중 획득 — 완주 전에는 경제에 넣지 않고 pending에만 쌓는다.
        /// 완주 정산 구간(grantRewardsToEconomyNow)에는 바로 지급하고 연출 목록에 넣는다.</summary>
        void TrackMatchLoot(YutSquareRewardKind kind, OfferingData offering, int amount,
            CookingIngredientId ingredient = default, CookingCharmType charm = CookingCharmType.None)
        {
            if (amount <= 0) return;
            if (grantRewardsToEconomyNow)
            {
                GrantLootToEconomy(kind, offering, amount, ingredient, charm);
                MergeLootEntry(presentableLoot, kind, offering, amount, ingredient, charm);
            }
            else
            {
                MergeLootEntry(pendingLoot, kind, offering, amount, ingredient, charm);
            }
        }

        /// <summary>완주 시 pending → 경제 지급 + 연출 목록으로 이동.</summary>
        void CommitPendingLootOnFinish()
        {
            for (int i = 0; i < pendingLoot.Count; i++)
            {
                var e = pendingLoot[i];
                GrantLootToEconomy(e.Kind, e.Offering, e.Amount, e.Ingredient, e.Charm);
                MergeLootEntry(presentableLoot, e.Kind, e.Offering, e.Amount, e.Ingredient, e.Charm);
            }
            pendingLoot.Clear();
        }

        void GrantLootToEconomy(YutSquareRewardKind kind, OfferingData offering, int amount,
            CookingIngredientId ingredient = default, CookingCharmType charm = CookingCharmType.None)
        {
            if (amount <= 0 || GameEconomy.Instance == null) return;
            switch (kind)
            {
                case YutSquareRewardKind.Yeopjeon:
                    GameEconomy.Instance.AddYeopjeon(amount);
                    break;
                case YutSquareRewardKind.Water:
                    GameEconomy.Instance.AddWater(amount);
                    break;
                case YutSquareRewardKind.Hyang:
                    GameEconomy.Instance.AddHyang(amount);
                    break;
                case YutSquareRewardKind.AdTicket:
                    GiftBundle.AddAdTickets(amount);
                    break;
                case YutSquareRewardKind.Offering:
                    if (offering != null) GameEconomy.Instance.AddOffering(offering, amount);
                    break;
                case YutSquareRewardKind.YutToken:
                    GameEconomy.Instance.AddYutTokenOverflow(amount, YutRewards.YutTokenHardCap);
                    break;
                case YutSquareRewardKind.IngredientBundle:
                    GameEconomy.Instance.AddMaterial(ingredient, amount);
                    break;
                case YutSquareRewardKind.Charm:
                    GameEconomy.Instance.AddCharm(charm, amount);
                    break;
            }
        }

        void MergeLootEntry(List<PostYutLootEntry> list, YutSquareRewardKind kind, OfferingData offering, int amount,
            CookingIngredientId ingredient = default, CookingCharmType charm = CookingCharmType.None)
        {
            if (amount <= 0 || list == null) return;

            Sprite icon = null;
            string label;
            switch (kind)
            {
                case YutSquareRewardKind.Yeopjeon:
                    icon = YutMiniGame.YeopjeonIcon();
                    label = "엽전";
                    break;
                case YutSquareRewardKind.Water:
                    icon = YutMiniGame.WaterIcon();
                    label = "물";
                    break;
                case YutSquareRewardKind.Hyang:
                    label = "향";
                    break;
                case YutSquareRewardKind.AdTicket:
                    label = "광고보상권";
                    break;
                case YutSquareRewardKind.YutToken:
                    label = "윷 토큰";
                    break;
                case YutSquareRewardKind.Offering:
                    if (offering == null) return;
                    icon = offering.icon;
                    label = !string.IsNullOrEmpty(offering.displayName) ? offering.displayName : "공양물";
                    break;
                case YutSquareRewardKind.IngredientBundle:
                    icon = YutMiniGame.IngredientBagIcon();
                    label = CookingRecipeCatalog.DisplayName(ingredient);
                    break;
                case YutSquareRewardKind.Charm:
                    label = YutRewards.CharmDisplayName(charm);
                    break;
                default:
                    return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                bool sameOffering = kind != YutSquareRewardKind.Offering
                    || ReferenceEquals(e.Offering, offering)
                    || (e.Offering != null && offering != null
                        && string.Equals(e.Offering.offeringId, offering.offeringId, StringComparison.OrdinalIgnoreCase));
                bool sameIngredient = kind != YutSquareRewardKind.IngredientBundle || e.Ingredient == ingredient;
                bool sameCharm = kind != YutSquareRewardKind.Charm || e.Charm == charm;
                if (e.Kind == kind && sameOffering && sameIngredient && sameCharm)
                {
                    list[i] = new PostYutLootEntry(kind, e.Offering ?? offering, e.Amount + amount, e.Icon ?? icon, e.Label,
                        ingredient, charm);
                    return;
                }
            }

            list.Add(new PostYutLootEntry(kind, offering, amount, icon, label, ingredient, charm));
        }

        struct SpecialSquareSnap
        {
            public int NodeId;
            public YutBoardLayout.SpecialSquareKind Kind;
            public Sprite Icon;
        }

        List<SpecialSquareSnap> SnapshotSpecialSquareVisuals()
        {
            var list = new List<SpecialSquareSnap>();
            var icons = BuildSpecialSquareIcons();
            for (int nodeId = 0; nodeId < YutBoardLayout.NodeCount; nodeId++)
            {
                var kind = YutBoardLayout.GetSpecialKind(nodeId);
                if (kind == YutBoardLayout.SpecialSquareKind.None) continue;
                icons.TryGetValue(nodeId, out var icon);
                list.Add(new SpecialSquareSnap { NodeId = nodeId, Kind = kind, Icon = icon });
            }
            return list;
        }

        /// <summary>같은 종류끼리 노드 번호 순으로 짝지어 from→to 비행 경로를 만든다.</summary>
        static List<YutMiniGame.SpecialSquareFlight> BuildSpecialSquareFlights(
            List<SpecialSquareSnap> before, List<SpecialSquareSnap> after)
        {
            var flights = new List<YutMiniGame.SpecialSquareFlight>();
            var kinds = new[]
            {
                YutBoardLayout.SpecialSquareKind.Coin,
                YutBoardLayout.SpecialSquareKind.IngredientBag,
                YutBoardLayout.SpecialSquareKind.Treasure,
                YutBoardLayout.SpecialSquareKind.Water,
            };
            for (int k = 0; k < kinds.Length; k++)
            {
                var kind = kinds[k];
                var olds = new List<SpecialSquareSnap>();
                var news = new List<SpecialSquareSnap>();
                for (int i = 0; i < before.Count; i++)
                    if (before[i].Kind == kind) olds.Add(before[i]);
                for (int i = 0; i < after.Count; i++)
                    if (after[i].Kind == kind) news.Add(after[i]);
                olds.Sort((a, b) => a.NodeId.CompareTo(b.NodeId));
                news.Sort((a, b) => a.NodeId.CompareTo(b.NodeId));
                int n = Mathf.Min(olds.Count, news.Count);
                for (int i = 0; i < n; i++)
                {
                    Sprite icon = olds[i].Icon != null ? olds[i].Icon : news[i].Icon;
                    flights.Add(new YutMiniGame.SpecialSquareFlight(olds[i].NodeId, news[i].NodeId, icon));
                }
            }
            return flights;
        }

        /// <summary>소진되지 않은 특수 칸 종류를 유지하고 노드 위치만 랜덤 재배치.</summary>
        void ReshuffleRemainingSpecialSquares(bool applyVisuals = true)
        {
            YutBoardLayout.ReshuffleRemainingSpecialSquares();
            if (applyVisuals)
                ApplySpecialSquareVisuals();
        }

        /// <summary>보드 위 특수 칸마다 무슨 보상인지 아이콘을 입힌다 — 엽전·재료보따리·물은
        /// 내용물을, 보물상자 칸은 안이 뭔지 숨기고 상자 아이콘만 보여준다.</summary>
        void ApplySpecialSquareVisuals()
        {
            if (miniGame == null) return;
            miniGame.RefreshSpecialSquareVisuals(BuildSpecialSquareIcons());
        }

        /// <summary>공양물 칸에 배정할 후보 — 물 제외 전체 공양물 목록. 수동 루프로 필터링한다
        /// (LINQ .Where/.ToList를 새 조합에 처음 쓰면 IL2CPP WebGL에서 "null function"이 나던
        /// 문제 때문에 — 오늘 이미 두 번 겪었다).</summary>
        /// <summary>공양물 칸·보물상자 풀 = 3차 공양물 24종 (OfferingCatalog.RandomPool).
        /// 카탈로그가 아직 없으면(테스트 등) StartingState 목록으로 폴백.</summary>
        List<OfferingData> GetOfferingPool()
        {
            var pool = new List<OfferingData>();
            if (OfferingCatalog.RandomPool.Count > 0)
            {
                pool.AddRange(OfferingCatalog.RandomPool);
                return pool;
            }
            var settings = StartingStateSettings.Get();
            if (settings.startingOfferings == null) return pool;
            foreach (var o in settings.startingOfferings)
                if (o != null && o.kind != OfferingKind.Water) pool.Add(o);
            return pool;
        }

        /// <summary>세이브 복원용 — 풀에 없는(예전) 공양물 id도 전체 카탈로그에서 찾는다.</summary>
        static OfferingData FindSavedOffering(List<OfferingData> pool, string id) =>
            pool.FirstOrDefault(o => o != null && o.offeringId == id) ?? OfferingCatalog.Find(id);

        /// <summary>구 공양물 칸 배정 — 재료보따리로 바뀐 뒤엔 비운다.</summary>
        void AssignSpecialOfferings() => specialOfferingByNode.Clear();

        /// <summary>pool에서 count개를 뽑되, 가능하면 offeringId가 겹치지 않게 한다.</summary>
        static List<OfferingData> PickDistinctOfferings(IReadOnlyList<OfferingData> pool, int count)
        {
            var result = new List<OfferingData>(count);
            if (pool == null || pool.Count == 0 || count <= 0) return result;

            var remaining = new List<OfferingData>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) remaining.Add(pool[i]);

            for (int n = 0; n < count; n++)
            {
                if (remaining.Count == 0)
                {
                    // 종류가 칸 수보다 적으면 전체 풀에서 다시 채워 중복 허용.
                    for (int i = 0; i < pool.Count; i++)
                        if (pool[i] != null) remaining.Add(pool[i]);
                    if (remaining.Count == 0) break;
                }

                int pick = UnityEngine.Random.Range(0, remaining.Count);
                result.Add(remaining[pick]);
                string takenId = remaining[pick].offeringId;
                // 같은 id는 더 이상 후보에 두지 않는다.
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(remaining[i].offeringId, takenId, StringComparison.OrdinalIgnoreCase))
                        remaining.RemoveAt(i);
                }
            }

            return result;
        }

        /// <summary>
        /// 말이 특수 칸에 도착했을 때 — 칸 종류에 맞는 보상 팝업은 Presenter가 띄운다.
        /// </summary>
        void HandleSpecialSquareReached(int nodeId, IReadOnlyList<string> pieceIds) =>
            squareRewards.OnReached(nodeId, this);

        /// <summary>보상을 받은 특수 칸은 보드·세이브에서 제거한다(아이콘/색도 일반 칸으로 되돌림).</summary>
        void ClearConsumedSpecialSquareAt(int nodeId)
        {
            if (nodeId < 0) return;
            YutBoardLayout.ClearSpecialSquare(nodeId);
            specialOfferingByNode.Remove(nodeId);
            ApplySpecialSquareVisuals();
        }

        /// <summary>동(東) 구역에 이번 매치에서 특수 칸으로 모은 것들을 아이콘으로 보여준다.</summary>
        void RefreshCollectedItemsDisplay()
        {
            if (miniGame == null) return;
            var items = new List<YutMiniGame.CollectedItemView>();
            foreach (var kv in matchOfferingCounts)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Icon = kv.Key.icon,
                    Count = kv.Value,
                    Label = kv.Key.displayName,
                });
            }
            if (matchYeopjeonTotal > 0)
            {
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Icon = YutMiniGame.YeopjeonIcon(),
                    Count = matchYeopjeonTotal,
                    Label = "엽전",
                });
            }
            if (matchWaterTotal > 0)
            {
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Icon = YutMiniGame.WaterIcon(),
                    Count = matchWaterTotal,
                    Label = "물",
                });
            }
            if (matchHyangTotal > 0)
            {
                items.Add(new YutMiniGame.CollectedItemView { Count = matchHyangTotal, Label = "향" });
            }
            if (matchAdTicketTotal > 0)
            {
                items.Add(new YutMiniGame.CollectedItemView { Count = matchAdTicketTotal, Label = "광고보상권" });
            }
            if (matchYutTokenTotal > 0)
            {
                // 평소 상한(5)을 넘겨 받은 적이 있으면(보물상자 보너스) 눈에 띄게 다른 색으로.
                // 완주 전엔 경제에 안 넣으므로, 보유+이번 판 미지급분을 합쳐 상한 초과 여부를 본다.
                int held = GameEconomy.Instance != null ? GameEconomy.Instance.YutToken : 0;
                int max = GameEconomy.Instance != null ? GameEconomy.Instance.YutTokenMax : 5;
                bool overflowed = held + matchYutTokenTotal > max;
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Count = matchYutTokenTotal,
                    Label = "윷 토큰",
                    Tint = overflowed ? new Color(1f, 0.55f, 0.85f, 1f) : (Color?)null,
                });
            }
            foreach (var kv in matchIngredientCounts)
            {
                if (kv.Value <= 0) continue;
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Icon = YutMiniGame.IngredientBagIcon(),
                    Count = kv.Value,
                    Label = CookingRecipeCatalog.DisplayName(kv.Key),
                });
            }
            foreach (var kv in matchCharmCounts)
            {
                if (kv.Value <= 0) continue;
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Count = kv.Value,
                    Label = YutRewards.CharmDisplayName(kv.Key),
                });
            }
            miniGame.ShowCollectedItems(items);
        }

        /// <summary>매치 종료 다이얼로그에 덧붙일 "이번 판에 얻은 것" 한 줄 요약. 없으면 null.</summary>
        string BuildCollectedItemsSummary()
        {
            var parts = new List<string>();
            foreach (var kv in matchOfferingCounts)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                parts.Add($"{kv.Key.displayName} {kv.Value}개");
            }
            if (matchYeopjeonTotal > 0) parts.Add($"엽전 {matchYeopjeonTotal}개");
            if (matchWaterTotal > 0) parts.Add($"물 {matchWaterTotal}개");
            if (matchHyangTotal > 0) parts.Add($"향 {matchHyangTotal}개");
            if (matchAdTicketTotal > 0) parts.Add($"광고보상권 {matchAdTicketTotal}개");
            if (matchYutTokenTotal > 0) parts.Add($"윷 토큰 {matchYutTokenTotal}개");
            foreach (var kv in matchIngredientCounts)
            {
                if (kv.Value <= 0) continue;
                parts.Add($"{CookingRecipeCatalog.DisplayName(kv.Key)} {kv.Value}개");
            }
            foreach (var kv in matchCharmCounts)
            {
                if (kv.Value <= 0) continue;
                parts.Add($"{YutRewards.CharmDisplayName(kv.Key)} {kv.Value}개");
            }
            return parts.Count > 0 ? string.Join(", ", parts) + "를 얻었다" : null;
        }

        void OnSquareRewardFlowEnded(bool pendingBonusThrow)
        {
            if (pendingBonusThrow)
                miniGame.SetThrowVisible(true);
            else
                StartCoroutine(RunOpponentTurnRoutine());
        }

        /// <summary>완주 부적 지급 후 안내.
        /// 안내를 닫으면(도전 보상 있으면 이어서) 전원 완주이므로 판을 강제로 새 판으로 리셋한다.</summary>
        void GrantFinishRewardAndNotice()
        {
            var charmNames = new List<string>(pendingFinishCharmCount);
            for (int i = 0; i < pendingFinishCharmCount; i++)
            {
                var charm = YutRewards.RollFinishCharm();
                matchCharmCounts.TryGetValue(charm, out int cur);
                matchCharmCounts[charm] = cur + 1;
                TrackMatchLoot(YutSquareRewardKind.Charm, null, 1, default, charm);
                charmNames.Add(YutRewards.CharmDisplayName(charm));
            }

            string charmsLine = string.Join(", ", charmNames);
            string message = pendingFinishStack > 1
                ? $"완주! {pendingFinishStack}마리 업고 ×{pendingFinishStack}\n부적: {charmsLine}"
                : $"완주! 부적 획득: {charmsLine}";

            string collected = BuildCollectedItemsSummary();
            if (!string.IsNullOrEmpty(collected))
                message += $"\n{collected}";

            ShowNotice(message, OnMatchEndedNoticeOk);
            GameSaveBridge.SaveFromWorld();
        }

        /// <summary>특수 칸/도전 공용 — 매치 집계·로그. 경제 지급은 완주 정산 구간에만.</summary>
        string ApplySquareRewardToEconomy(YutSquareReward reward, int multiplier)
        {
            int amount = reward.Amount * multiplier;
            switch (reward.Kind)
            {
                case YutSquareRewardKind.Yeopjeon:
                    matchYeopjeonTotal += amount;
                    TrackMatchLoot(YutSquareRewardKind.Yeopjeon, null, amount);
                    miniGame.AddPlayLogEntry($"엽전 {amount}개 획득.");
                    return $"엽전 {amount}개";
                case YutSquareRewardKind.Water:
                    matchWaterTotal += amount;
                    TrackMatchLoot(YutSquareRewardKind.Water, null, amount);
                    miniGame.AddPlayLogEntry($"물 {amount}개 획득.");
                    return $"물 {amount}개";
                case YutSquareRewardKind.Hyang:
                    matchHyangTotal += amount;
                    TrackMatchLoot(YutSquareRewardKind.Hyang, null, amount);
                    miniGame.AddPlayLogEntry($"향 {amount}개 획득.");
                    return $"향 {amount}개";
                case YutSquareRewardKind.AdTicket:
                    matchAdTicketTotal += amount;
                    TrackMatchLoot(YutSquareRewardKind.AdTicket, null, amount);
                    miniGame.AddPlayLogEntry($"광고보상권 {amount}개 획득.");
                    return $"광고보상권 {amount}개";
                case YutSquareRewardKind.Offering:
                    if (reward.Offering == null) return null;
                    matchOfferingCounts.TryGetValue(reward.Offering, out int cur);
                    matchOfferingCounts[reward.Offering] = cur + amount;
                    TrackMatchLoot(YutSquareRewardKind.Offering, reward.Offering, amount);
                    miniGame.AddPlayLogEntry($"{reward.Offering.displayName} {amount}개 획득.");
                    return $"{reward.Offering.displayName} {amount}개";
                case YutSquareRewardKind.YutToken:
                    matchYutTokenTotal += amount;
                    TrackMatchLoot(YutSquareRewardKind.YutToken, null, amount);
                    miniGame.AddPlayLogEntry($"윷 토큰 {amount}개 획득.");
                    return $"윷 토큰 {amount}개";
                case YutSquareRewardKind.IngredientBundle:
                    {
                        if (reward.Ingredients == null || reward.Ingredients.Length == 0)
                            return null;
                        var parts = new List<string>();
                        for (int i = 0; i < reward.Ingredients.Length; i++)
                        {
                            var id = reward.Ingredients[i];
                            matchIngredientCounts.TryGetValue(id, out int curIngredient);
                            matchIngredientCounts[id] = curIngredient + amount;
                            TrackMatchLoot(YutSquareRewardKind.IngredientBundle, null, amount, id);
                            parts.Add(CookingRecipeCatalog.DisplayName(id));
                        }
                        string desc = string.Join(", ", parts);
                        if (amount > 1) desc += $" ×{amount}";
                        miniGame.AddPlayLogEntry($"재료보따리 획득: {desc}");
                        return $"재료 {desc}";
                    }
                case YutSquareRewardKind.Charm:
                    {
                        var charm = reward.Charm;
                        matchCharmCounts.TryGetValue(charm, out int curCharm);
                        matchCharmCounts[charm] = curCharm + amount;
                        TrackMatchLoot(YutSquareRewardKind.Charm, null, amount, default, charm);
                        string name = YutRewards.CharmDisplayName(charm);
                        miniGame.AddPlayLogEntry($"{name} {amount}개 획득.");
                        return $"{name} {amount}개";
                    }
                default:
                    return null;
            }
        }
    }
}
