using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>도전과제 UI·보상 지급에 필요한 화면 측 훅.</summary>
    public interface IYutChallengeHost
    {
        YutMiniGame MiniGame { get; }
        void ShowNotice(string message, Action onOk);
        YutSquareReward RollTreasureReward();
        string ApplySquareRewardToEconomy(YutSquareReward reward, int multiplier);
        void RefreshCollectedItemsDisplay();
        void SaveFromWorld();
    }

    /// <summary>
    /// 도전과제 배너·완료 보상(보물상자 ×3) 진행.
    /// 규칙/상태 추적의 소유는 <see cref="YutChallengeState"/>, 이 클래스는 화면 연동만.
    /// </summary>
    public sealed class YutChallengePresenter
    {
        public YutChallengeState State { get; private set; }
        public bool AwaitingReward { get; private set; }
        public bool PendingRewardAfterFinish { get; set; }

        public void Clear()
        {
            State = null;
            AwaitingReward = false;
            PendingRewardAfterFinish = false;
        }

        /// <summary>팝업/대기 플래그만 내린다(과제 진행 상태는 유지).</summary>
        public void ClearBlockingFlags()
        {
            AwaitingReward = false;
            PendingRewardAfterFinish = false;
        }

        public void StartNew(int playerPieceCount, IYutChallengeHost host)
        {
            State = YutChallengeState.Pick(playerPieceCount);
            AwaitingReward = false;
            PendingRewardAfterFinish = false;
            RefreshBanner(host);
        }

        public void Restore(YutChallengeKind kind, bool completed, bool failed, int streak, IYutChallengeHost host)
        {
            State = YutChallengeState.Restore(kind, completed, failed, streak);
            AwaitingReward = false;
            PendingRewardAfterFinish = false;
            RefreshBanner(host);
        }

        public void WriteSave(out int kind, out bool completed, out bool failed, out int streak)
        {
            if (State == null)
            {
                kind = -1;
                completed = false;
                failed = false;
                streak = 0;
                return;
            }
            kind = (int)State.Kind;
            completed = State.Completed;
            failed = State.Failed;
            streak = State.Streak;
        }

        public void RefreshBanner(IYutChallengeHost host)
        {
            if (host?.MiniGame == null) return;
            host.MiniGame.SetChallengeBanner(State != null ? State.BannerText : null);
        }

        /// <summary>플레이어 잡힘. 배너 갱신.</summary>
        public void NotifyPlayerCaptured(IYutChallengeHost host)
        {
            if (State == null) return;
            State.OnPlayerCaptured();
            RefreshBanner(host);
        }

        /// <summary>플레이어 던지기. true면 방금 과제 완료(보상 루틴 시작).</summary>
        public bool NotifyPlayerThrow(YutThrowResult result, IYutChallengeHost host)
        {
            if (State == null) return false;
            bool justCompleted = State.OnPlayerThrow(result);
            RefreshBanner(host);
            return justCompleted;
        }

        /// <summary>매치 종료 전원 완주 판정. true면 완주 후 보상 예약.</summary>
        public bool TryMarkAllFinishedComplete(int totalPieces, int finishedCount, IYutChallengeHost host)
        {
            if (State == null) return false;
            if (!State.TryCompleteAllFinished(totalPieces, finishedCount)) return false;
            PendingRewardAfterFinish = true;
            RefreshBanner(host);
            return true;
        }

        /// <summary>도전 완료 — 보물상자 3개 즉시 지급(광고 2배 없음).</summary>
        public IEnumerator PlayRewardRoutine(IYutChallengeHost host)
        {
            if (host == null) yield break;

            AwaitingReward = true;
            host.MiniGame?.SetThrowVisible(false);

            var lines = new List<string>(YutRewards.ChallengeChestCount);
            for (int i = 0; i < YutRewards.ChallengeChestCount; i++)
            {
                var reward = host.RollTreasureReward();
                string desc = host.ApplySquareRewardToEconomy(reward, 1);
                if (!string.IsNullOrEmpty(desc))
                    lines.Add("· " + desc);
            }

            host.RefreshCollectedItemsDisplay();
            RefreshBanner(host);
            host.SaveFromWorld();

            bool closed = false;
            string body = lines.Count > 0
                ? $"도전 성공!\n보물상자 ×{YutRewards.ChallengeChestCount}\n{string.Join("\n", lines)}"
                : $"도전 성공!\n보물상자 ×{YutRewards.ChallengeChestCount}";
            host.ShowNotice(body, () => closed = true);
            yield return new WaitUntil(() => closed);
            AwaitingReward = false;
        }
    }
}
