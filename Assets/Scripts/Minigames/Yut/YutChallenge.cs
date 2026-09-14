using UnityEngine;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>윷놀이 매 판 도전과제 종류.</summary>
    public enum YutChallengeKind
    {
        /// <summary>이무기에게 한 번도 잡히지 않고 말 넷(전원) 완주.</summary>
        FinishAllUncaptured = 0,
        /// <summary>플레이어 던지기 연속 모.</summary>
        ConsecutiveMo = 1,
        /// <summary>플레이어 던지기 연속 빽도.</summary>
        ConsecutiveBaekdo = 2,
    }

    /// <summary>
    /// 한 매치의 도전과제 진행. 매 판 시작 때 Pick으로 하나 고르고,
    /// 던지기/잡힘/완주 이벤트로 갱신한다. 완료되면 Completed=true(즉각 보상은 YutChallengePresenter).
    /// </summary>
    public sealed class YutChallengeState
    {
        public YutChallengeKind Kind { get; private set; }
        public bool Completed { get; private set; }
        public bool Failed { get; private set; }
        public int Streak { get; private set; }

        public string BannerText
        {
            get
            {
                if (Completed) return "도전 완료! 보물상자 ×3";
                if (Failed) return "도전 실패 — " + Description;
                if (Kind == YutChallengeKind.ConsecutiveMo || Kind == YutChallengeKind.ConsecutiveBaekdo)
                {
                    int need = YutRewards.ChallengeConsecutiveNeeded;
                    if (Streak > 0)
                        return $"도전: {Description} ({Streak}/{need})";
                }
                return "도전: " + Description;
            }
        }

        public string Description => Kind switch
        {
            YutChallengeKind.FinishAllUncaptured =>
                "이무기에게 한 번도 잡히지 않고 넷 다 완주",
            YutChallengeKind.ConsecutiveMo =>
                $"모 {YutRewards.ChallengeConsecutiveNeeded}연속",
            YutChallengeKind.ConsecutiveBaekdo =>
                $"빽도 {YutRewards.ChallengeConsecutiveNeeded}연속",
            _ => "도전",
        };

        public static YutChallengeState Pick(int playerPieceCount)
        {
            var pool = new System.Collections.Generic.List<YutChallengeKind>(3)
            {
                YutChallengeKind.ConsecutiveMo,
                YutChallengeKind.ConsecutiveBaekdo,
            };
            if (playerPieceCount >= YutRewards.ChallengeFinishAllPieceCount)
                pool.Add(YutChallengeKind.FinishAllUncaptured);

            var kind = pool[UnityEngine.Random.Range(0, pool.Count)];
            return new YutChallengeState { Kind = kind };
        }

        public static YutChallengeState Restore(YutChallengeKind kind, bool completed, bool failed, int streak)
        {
            return new YutChallengeState
            {
                Kind = kind,
                Completed = completed,
                Failed = failed,
                Streak = Mathf.Max(0, streak),
            };
        }

        /// <summary>플레이어 던지기 1회. true면 방금 완료.</summary>
        public bool OnPlayerThrow(YutThrowResult result)
        {
            if (Completed || Failed) return false;
            if (Kind == YutChallengeKind.ConsecutiveMo)
                return TickStreak(result == YutThrowResult.Mo);
            if (Kind == YutChallengeKind.ConsecutiveBaekdo)
                return TickStreak(result == YutThrowResult.Baekdo);
            return false;
        }

        bool TickStreak(bool matched)
        {
            if (!matched)
            {
                Streak = 0;
                return false;
            }
            Streak++;
            if (Streak < YutRewards.ChallengeConsecutiveNeeded) return false;
            Completed = true;
            return true;
        }

        /// <summary>이무기에게 잡힘 — 미잡힘 완주 과제는 실패.</summary>
        public void OnPlayerCaptured()
        {
            if (Completed) return;
            if (Kind != YutChallengeKind.FinishAllUncaptured) return;
            Failed = true;
            Streak = 0;
        }

        /// <summary>매치 종료 시 전원 완주 판정. true면 방금 완료.</summary>
        public bool TryCompleteAllFinished(int totalPieces, int finishedCount)
        {
            if (Completed || Failed) return false;
            if (Kind != YutChallengeKind.FinishAllUncaptured) return false;
            if (totalPieces < YutRewards.ChallengeFinishAllPieceCount) return false;
            if (finishedCount < totalPieces) return false;
            Completed = true;
            return true;
        }
    }
}
