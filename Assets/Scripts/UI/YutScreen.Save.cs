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
    /// <summary>진행 중 매치 세이브·불러오기 (말 위치·특수 칸·미정산 획득). (YutScreen 분할 — 본체는 YutScreen.cs)</summary>
    public partial class YutScreen
    {
        /// <summary>세이브용 스냅샷 — 진행 중(승패 안 난) 매치가 없으면 null. 특수 칸 배치도 같이
        /// 담아야 나갔다 들어오거나 앱을 재시작해도 말 위치·특수 칸이 둘 다 그대로 이어진다.</summary>
        public YutMatchSave CaptureForSave()
        {
            if (match == null || match.IsEnded) return null;

            var squareNodeIds = new List<int>();
            var squareKinds = new List<int>();
            for (int nodeId = 0; nodeId < YutBoardLayout.NodeCount; nodeId++)
            {
                var kind = YutBoardLayout.GetSpecialKind(nodeId);
                if (kind == YutBoardLayout.SpecialSquareKind.None) continue;
                squareNodeIds.Add(nodeId);
                squareKinds.Add((int)kind);
            }

            challenge.WriteSave(out int challengeKind, out bool challengeCompleted, out bool challengeFailed, out int challengeStreak);

            var pendingOfferingIds = new List<string>();
            var pendingOfferingCounts = new List<int>();
            foreach (var kv in matchOfferingCounts)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                pendingOfferingIds.Add(kv.Key.offeringId);
                pendingOfferingCounts.Add(kv.Value);
            }

            var pendingIngredientIds = new List<int>();
            var pendingIngredientCounts = new List<int>();
            foreach (var kv in matchIngredientCounts)
            {
                if (kv.Value <= 0) continue;
                pendingIngredientIds.Add((int)kv.Key);
                pendingIngredientCounts.Add(kv.Value);
            }

            var pendingCharmTypes = new List<int>();
            var pendingCharmCounts = new List<int>();
            foreach (var kv in matchCharmCounts)
            {
                if (kv.Value <= 0) continue;
                pendingCharmTypes.Add((int)kv.Key);
                pendingCharmCounts.Add(kv.Value);
            }

            return new YutMatchSave
            {
                playerPieces = match.PlayerPieces.Select(ToPieceSave).ToArray(),
                opponentPiece = ToPieceSave(match.OpponentPiece),
                specialSquareNodeIds = squareNodeIds.ToArray(),
                specialSquareKinds = squareKinds.ToArray(),
                challengeKind = challengeKind,
                challengeCompleted = challengeCompleted,
                challengeFailed = challengeFailed,
                challengeStreak = challengeStreak,
                pendingYeopjeon = matchYeopjeonTotal,
                pendingWater = matchWaterTotal,
                pendingHyang = matchHyangTotal,
                pendingAdTicket = matchAdTicketTotal,
                pendingYutToken = matchYutTokenTotal,
                pendingOfferingIds = pendingOfferingIds.ToArray(),
                pendingOfferingCounts = pendingOfferingCounts.ToArray(),
                pendingIngredientIds = pendingIngredientIds.ToArray(),
                pendingIngredientCounts = pendingIngredientCounts.ToArray(),
                pendingCharmTypes = pendingCharmTypes.ToArray(),
                pendingCharmCounts = pendingCharmCounts.ToArray(),
            };
        }

        static YutPieceSave ToPieceSave(YutPiece p) => new YutPieceSave
        {
            id = p.Id,
            displayName = p.DisplayName,
            nodeId = p.NodeId,
            finished = p.Finished,
            history = p.History.ToArray()
        };

        /// <summary>부팅 시 세이브에 진행 중이던 매치가 있으면 조용히(화면은 안 열고) 복원해서,
        /// 유저가 윷놀이를 다시 열면 ResumeExistingMatch로 바로 이어지게 해 둔다.</summary>
        public void ApplyFromSave(YutMatchSave saved)
        {
            if (saved?.playerPieces == null || saved.playerPieces.Length == 0) return;

            EnsureBuilt();
            if (match != null) UnsubscribeMatchEvents();

            teamById.Clear();
            var agents = CharacterAgent.All.Where(a => a != null && a.Stats != null).ToList();
            var team = new List<(string id, string name)>();
            foreach (var ps in saved.playerPieces)
            {
                var agent = agents.FirstOrDefault(a =>
                    (a.Data != null ? a.Data.id.ToString() : a.name) == ps.id);
                if (agent != null) teamById[ps.id] = agent;
                team.Add((ps.id, ps.displayName));
            }

            match = new YutMatch(team);
            for (int i = 0; i < saved.playerPieces.Length && i < match.PlayerPieces.Count; i++)
                ApplyPieceSave(match.PlayerPieces[i], saved.playerPieces[i]);
            if (saved.opponentPiece != null)
                ApplyPieceSave(match.OpponentPiece, saved.opponentPiece);

            RestoreSpecialSquares(saved);

            if (saved.challengeKind >= 0)
            {
                challenge.Restore(
                    (YutChallengeKind)saved.challengeKind,
                    saved.challengeCompleted,
                    saved.challengeFailed,
                    saved.challengeStreak,
                    this);
            }
            else
            {
                challenge.StartNew(match.PlayerPieces.Count, this);
            }

            SubscribeMatchEvents();
            awaitingFinishChoice = false;
            squareRewards.Clear();
            awaitingReviveChoice = false;
            pendingOpponentLappedFx = false;
            RestorePendingLootFromSave(saved);
        }

        /// <summary>중도 저장분 — 완주 전 미지급 재화를 보드 집계·pendingLoot로 되살린다.</summary>
        void RestorePendingLootFromSave(YutMatchSave saved)
        {
            pendingLoot.Clear();
            matchOfferingCounts.Clear();
            matchIngredientCounts.Clear();
            matchCharmCounts.Clear();
            matchYeopjeonTotal = Mathf.Max(0, saved.pendingYeopjeon);
            matchWaterTotal = Mathf.Max(0, saved.pendingWater);
            matchHyangTotal = Mathf.Max(0, saved.pendingHyang);
            matchAdTicketTotal = Mathf.Max(0, saved.pendingAdTicket);
            matchYutTokenTotal = Mathf.Max(0, saved.pendingYutToken);

            if (matchYeopjeonTotal > 0)
                MergeLootEntry(pendingLoot, YutSquareRewardKind.Yeopjeon, null, matchYeopjeonTotal);
            if (matchWaterTotal > 0)
                MergeLootEntry(pendingLoot, YutSquareRewardKind.Water, null, matchWaterTotal);
            if (matchHyangTotal > 0)
                MergeLootEntry(pendingLoot, YutSquareRewardKind.Hyang, null, matchHyangTotal);
            if (matchAdTicketTotal > 0)
                MergeLootEntry(pendingLoot, YutSquareRewardKind.AdTicket, null, matchAdTicketTotal);
            if (matchYutTokenTotal > 0)
                MergeLootEntry(pendingLoot, YutSquareRewardKind.YutToken, null, matchYutTokenTotal);

            if (saved.pendingOfferingIds != null && saved.pendingOfferingCounts != null)
            {
                var pool = GetOfferingPool();
                for (int i = 0; i < saved.pendingOfferingIds.Length && i < saved.pendingOfferingCounts.Length; i++)
                {
                    int count = saved.pendingOfferingCounts[i];
                    if (count <= 0) continue;
                    var offering = FindSavedOffering(pool, saved.pendingOfferingIds[i]);
                    if (offering == null) continue;
                    matchOfferingCounts[offering] = count;
                    MergeLootEntry(pendingLoot, YutSquareRewardKind.Offering, offering, count);
                }
            }

            if (saved.pendingIngredientIds != null && saved.pendingIngredientCounts != null)
            {
                for (int i = 0; i < saved.pendingIngredientIds.Length && i < saved.pendingIngredientCounts.Length; i++)
                {
                    int count = saved.pendingIngredientCounts[i];
                    if (count <= 0) continue;
                    var id = (CookingIngredientId)saved.pendingIngredientIds[i];
                    matchIngredientCounts.TryGetValue(id, out int cur);
                    matchIngredientCounts[id] = cur + count;
                    MergeLootEntry(pendingLoot, YutSquareRewardKind.IngredientBundle, null, count, id);
                }
            }

            if (saved.pendingCharmTypes != null && saved.pendingCharmCounts != null)
            {
                for (int i = 0; i < saved.pendingCharmTypes.Length && i < saved.pendingCharmCounts.Length; i++)
                {
                    int count = saved.pendingCharmCounts[i];
                    if (count <= 0) continue;
                    var charm = (CookingCharmType)saved.pendingCharmTypes[i];
                    matchCharmCounts.TryGetValue(charm, out int cur);
                    matchCharmCounts[charm] = cur + count;
                    MergeLootEntry(pendingLoot, YutSquareRewardKind.Charm, null, count, default, charm);
                }
            }
        }

        /// <summary>말 위치는 그대로 복원되는데 특수 칸만 새로 섞이면 안 되니, 저장된 배치가
        /// 있으면 그대로 되살리고 — 그 배치 자체가 없는 옛 세이브일 때만 어쩔 수 없이 새로 뽑는다.</summary>
        void RestoreSpecialSquares(YutMatchSave saved)
        {
            if (saved.specialSquareNodeIds == null || saved.specialSquareNodeIds.Length == 0)
            {
                YutBoardLayout.RegenerateSpecialSquares();
                return;
            }

            var kinds = new Dictionary<int, YutBoardLayout.SpecialSquareKind>();
            for (int i = 0; i < saved.specialSquareNodeIds.Length && i < saved.specialSquareKinds.Length; i++)
                kinds[saved.specialSquareNodeIds[i]] = (YutBoardLayout.SpecialSquareKind)saved.specialSquareKinds[i];
            YutBoardLayout.RestoreSpecialSquares(kinds);

        }

        static void ApplyPieceSave(YutPiece piece, YutPieceSave save)
        {
            piece.NodeId = save.nodeId;
            piece.Finished = save.finished;
            piece.History.Clear();
            if (save.history != null) piece.History.AddRange(save.history);
        }
    }
}
