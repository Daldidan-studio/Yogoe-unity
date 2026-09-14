using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>특수 칸 보상 UI·지급에 필요한 화면 측 훅.</summary>
    public interface IYutSquareRewardHost
    {
        YutMiniGame MiniGame { get; }
        void ShowNotice(string message, Action onOk);
        void ShowRewardChoice(string message, Action onPlain, Action onAd);
        Coroutine StartRoutine(IEnumerator routine);
        OfferingData TryGetOfferingForNode(int nodeId);
        IReadOnlyList<OfferingData> GetTreasureOfferingPool();
        string ApplySquareRewardToEconomy(YutSquareReward reward, int multiplier);
        void ClearConsumedSpecialSquareAt(int nodeId);
        void RefreshCollectedItemsDisplay();
        void SaveFromWorld();
        /// <summary>보상 선택·비행이 끝난 뒤 — true면 보너스 던지기, false면 상대 턴.</summary>
        void OnSquareRewardFlowEnded(bool pendingBonusThrow);
    }

    /// <summary>
    /// 특수 칸(엽전/공양물/보물상자/정화수) 발견 → 확인/2배 선택 → 지급·비행.
    /// 재화 적용·보드 칸 소모는 Host, 이 클래스는 대기 상태와 팝업 흐름만.
    /// </summary>
    public sealed class YutSquareRewardPresenter
    {
        public bool Awaiting { get; private set; }
        public bool PendingBonusThrow { get; set; }

        YutSquareReward? pendingReward;
        int pendingNodeId = -1;

        public void Clear()
        {
            Awaiting = false;
            PendingBonusThrow = false;
            pendingReward = null;
            pendingNodeId = -1;
        }

        public YutSquareReward RollTreasure(IYutSquareRewardHost host) =>
            YutRewards.RollTreasure(host?.GetTreasureOfferingPool());

        /// <summary>말이 특수 칸에 도착 — 종류별 팝업을 띄운다.</summary>
        public void OnReached(int nodeId, IYutSquareRewardHost host)
        {
            if (host == null) return;

            pendingNodeId = nodeId;
            string message;
            switch (YutBoardLayout.GetSpecialKind(nodeId))
            {
                case YutBoardLayout.SpecialSquareKind.Coin:
                    pendingReward = new YutSquareReward(YutSquareRewardKind.Yeopjeon, null, 1);
                    message = "엽전 칸 발견!\n엽전을 얻을 수 있어요.";
                    break;

                case YutBoardLayout.SpecialSquareKind.Offering:
                    {
                        var offering = host.TryGetOfferingForNode(nodeId);
                        pendingReward = new YutSquareReward(YutSquareRewardKind.Offering, offering, 1);
                        string offeringName = offering != null ? offering.displayName : "공양물";
                        message = $"공양물 칸 발견!\n{offeringName}을(를) 얻을 수 있어요.";
                        break;
                    }

                case YutBoardLayout.SpecialSquareKind.Treasure:
                    pendingReward = RollTreasure(host);
                    BeginAwaiting(host);
                    host.ShowNotice(
                        $"보물상자 발견!\n{YutRewards.DescribeSquareReward(pendingReward.Value)}이(가) 들어있어요.",
                        () => ShowTreasureChoiceAfterReveal(host));
                    return;

                case YutBoardLayout.SpecialSquareKind.PurifiedWater:
                    pendingReward = new YutSquareReward(YutSquareRewardKind.PurifiedWater, null, 1);
                    message = "정화수 칸 발견!\n정화수를 얻을 수 있어요.";
                    break;

                default:
                    pendingNodeId = -1;
                    return;
            }

            BeginAwaiting(host);
            host.ShowRewardChoice(message, onPlain: () => GrantPlain(host), onAd: () => GrantAd(host));
        }

        void BeginAwaiting(IYutSquareRewardHost host)
        {
            Awaiting = true;
            host.MiniGame?.SetThrowVisible(false);
        }

        void ShowTreasureChoiceAfterReveal(IYutSquareRewardHost host)
        {
            if (pendingReward == null)
            {
                EndFlow(host);
                return;
            }
            string desc = YutRewards.DescribeSquareReward(pendingReward.Value);
            host.ShowRewardChoice(
                $"보물상자!\n{desc}\n그냥 받을까요, 광고 보고 2배 받을까요?",
                onPlain: () => GrantPlain(host),
                onAd: () => GrantAd(host));
        }

        public void GrantPlain(IYutSquareRewardHost host) =>
            host?.StartRoutine(GrantRoutine(host, YutRewards.SquareRewardBase));

        public void GrantAd(IYutSquareRewardHost host) =>
            host?.StartRoutine(AdRoutine(host));

        IEnumerator AdRoutine(IYutSquareRewardHost host)
        {
            yield return new WaitForSecondsRealtime(YutRewards.SquareRewardAdWatchSeconds);
            yield return GrantRoutine(host, YutRewards.SquareRewardAdMultiplier);
        }

        IEnumerator GrantRoutine(IYutSquareRewardHost host, int multiplier)
        {
            if (host == null || pendingReward == null)
            {
                EndFlow(host);
                yield break;
            }

            var reward = pendingReward.Value;
            int fromNode = pendingNodeId;
            Sprite flyIcon = IconFor(reward);
            host.ApplySquareRewardToEconomy(reward, multiplier);
            pendingReward = null;

            if (fromNode >= 0 && host.MiniGame != null)
            {
                pendingNodeId = -1;
                host.ClearConsumedSpecialSquareAt(fromNode);
                yield return host.MiniGame.PlayCollectRewardFly(fromNode, flyIcon);
            }
            else
            {
                ClearConsumed(host);
            }

            host.RefreshCollectedItemsDisplay();
            host.SaveFromWorld();
            EndFlow(host);
        }

        void ClearConsumed(IYutSquareRewardHost host)
        {
            if (pendingNodeId < 0) return;
            int nodeId = pendingNodeId;
            pendingNodeId = -1;
            host.ClearConsumedSpecialSquareAt(nodeId);
        }

        void EndFlow(IYutSquareRewardHost host)
        {
            bool bonus = PendingBonusThrow;
            Awaiting = false;
            host?.OnSquareRewardFlowEnded(bonus);
        }

        static Sprite IconFor(YutSquareReward reward)
        {
            switch (reward.Kind)
            {
                case YutSquareRewardKind.Yeopjeon:
                    return YutMiniGame.YeopjeonIcon();
                case YutSquareRewardKind.PurifiedWater:
                    return YutMiniGame.PurifiedWaterIcon();
                case YutSquareRewardKind.Offering:
                    return reward.Offering != null ? reward.Offering.icon : null;
                default:
                    return null;
            }
        }
    }
}
