using System;
using System.Collections.Generic;
using System.Linq;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 윷놀이 한 판의 규칙(잡기·업기·보너스턴·완주)만 담당하는 순수 로직.
    /// 이무기는 결승점이 없어서 패배 조건이 아니라 그냥 계속 도는 상대다 — 플레이어가 말을 완주시켜
    /// 승리하거나(전원 완주 시 자동 종료, 일부만 완주해도 EndAsPlayerWin으로 원하는 시점에 종료 가능)
    /// 직접 나가기 전까진 매치가 안 끝난다.
    /// 화면(YutMiniGame)이나 재화(GameEconomy)는 모르고, 결과만 이벤트로 알린다.
    /// 소유는 UI 쪽(YutScreen)이 한다 — CharacterAgent가 CharacterRequestState를 들고 있는 것과 같은 패턴.
    ///
    /// 완주 규칙: 참(0)에 "멈추는" 것만으로는 완주가 아니다 — 참을 실제로 지나가야(또는 이미 참에
    /// 있는 말이 다음 던지기를 할 때) 완주로 친다. 그래서 참에 정확히 도착한 말은 그 자리에
    /// 대기하다가, 다음 차례에 뭘 던지든 즉시 완주한다(빽도만 예외 — 빽도는 항상 뒤로 간다).
    /// </summary>
    public class YutMatch
    {
        public readonly struct YutMoveCandidate
        {
            public readonly string PieceId;
            public readonly int DestinationNode;
            /// <summary>모/뒷모/방 갈림길에서 지름길 쪽 후보인지. 갈림길이 아니면 의미 없음(둘 다 false).</summary>
            public readonly bool UseShortcut;

            public YutMoveCandidate(string pieceId, int destinationNode, bool useShortcut)
            {
                PieceId = pieceId;
                DestinationNode = destinationNode;
                UseShortcut = useShortcut;
            }
        }

        readonly List<YutPiece> playerPieces;
        readonly YutPiece opponentPiece;

        public IReadOnlyList<YutPiece> PlayerPieces => playerPieces;
        public YutPiece OpponentPiece => opponentPiece;
        public bool IsEnded { get; private set; }

        /// <summary>말 이동·잡기·완주가 있을 때마다 (화면 갱신 신호 — 페이로드 없이 최신 상태를 다시 읽으면 됨).</summary>
        public event Action OnPiecesChanged;
        /// <summary>매치 종료. true면 플레이어 승리.</summary>
        public event Action<bool> OnMatchEnded;
        /// <summary>
        /// 내 말(스택이면 전원)을 실제로 옮겼을 때 그 말들의 id. 기획 11장 "말을 움직일 때마다
        /// 그 요괴 친밀도 +0.25" — 캐릭터 스탯은 YutMatch가 몰라서 호출부(YutScreen)가 처리한다.
        /// </summary>
        public event Action<IReadOnlyList<string>> OnPlayerPiecesMoved;
        /// <summary>이무기가 내 말(스택이면 전원)을 잡았을 때 — 잡힌 말들. 게임로그 대사용.</summary>
        public event Action<IReadOnlyList<YutPiece>> OnPlayerPiecesCaptured;
        /// <summary>내가 이무기를 잡았을 때. 게임로그 대사용.</summary>
        public event Action OnOpponentCaptured;
        /// <summary>
        /// 내 말(스택이면 전원)이 골인했는데 아직 안 끝난(안 들어온) 말이 남아있을 때 — 그 말들의 id.
        /// 매치는 안 끝난다(전원 골인 전까지는 EndAsPlayerWin을 호출해야 끝남) — 호출부(YutScreen)가
        /// "여기서 그만 받을지, 남은 말로 계속할지" 물어보는 용도.
        /// </summary>
        public event Action<IReadOnlyList<string>> OnPlayerPieceFinished;

        public YutMatch(IEnumerable<(string id, string displayName)> playerTeam)
        {
            playerPieces = playerTeam.Select(t => new YutPiece(t.id, t.displayName, isPlayer: true)).ToList();
            opponentPiece = new YutPiece("imugi", "이무기", isPlayer: false);
        }

        /// <summary>대기 말이 빽도로 들어올 때 서는 자리 — 참 바로 뒤(19번).</summary>
        const int BaekdoEntryNode = 19;

        public YutThrowOutcome ThrowForPlayer() => YutThrowRoller.Roll();

        /// <summary>
        /// 던진 결과로 지금 움직일 수 있는 내 말(또는 스택 대표) 후보 목록. 말이 모/뒷모/방 갈림길에
        /// 정확히 멈춰 있으면 지름길로 가는 후보와 바깥길로 가는 후보를 둘 다 내놓는다 — 어느 쪽으로
        /// 갈지는 유저가 고른다. 빽도가 나오면 대기 말 중 하나를 참 뒤(BaekdoEntryNode)로 보내는
        /// 것도 후보로 내놓는다(대기 말이 빽도로 들어오는 변형 규칙).
        /// </summary>
        public IReadOnlyList<YutMoveCandidate> GetPlayerCandidates(YutThrowResult result)
        {
            var list = new List<YutMoveCandidate>();
            var seenBoardNodes = new HashSet<int>();
            bool isBaekdo = result == YutThrowResult.Baekdo;

            foreach (var p in playerPieces)
            {
                if (p.Finished) continue;

                if (!p.OnBoard)
                {
                    if (isBaekdo)
                    {
                        list.Add(new YutMoveCandidate(p.Id, BaekdoEntryNode, false));
                        continue;
                    }
                    var path = YutMoveResolver.GetPath(YutBoardLayout.Start, result);
                    list.Add(new YutMoveCandidate(p.Id, ResolveDisplayDestination(false, path), false));
                    continue;
                }

                if (!seenBoardNodes.Add(p.NodeId)) continue; // 같은 칸(스택)은 대표 한 명만 후보로

                if (isBaekdo)
                {
                    int back = YutMoveResolver.PeekBackwardDestination(p.NodeId, p.History);
                    list.Add(new YutMoveCandidate(p.Id, back, false));
                    continue;
                }

                if (YutMoveResolver.IsForkNode(p.NodeId))
                {
                    var shortcutPath = YutMoveResolver.GetPath(p.NodeId, result, takeShortcut: true);
                    var outerPath = YutMoveResolver.GetPath(p.NodeId, result, takeShortcut: false);
                    list.Add(new YutMoveCandidate(p.Id, ResolveDisplayDestination(true, shortcutPath), true));
                    list.Add(new YutMoveCandidate(p.Id, ResolveDisplayDestination(true, outerPath), false));
                    continue;
                }

                var boardPath = YutMoveResolver.GetPath(p.NodeId, result);
                bool atStart = p.NodeId == YutBoardLayout.Start;
                list.Add(new YutMoveCandidate(p.Id, ResolveDisplayDestination(atStart, boardPath), false));
            }

            return list;
        }

        /// <summary>
        /// pieceId가 속한 칸(스택이면 전원)을 결과만큼 이동시킨다. useShortcut은 모/뒷모/방 갈림길에
        /// 멈춰 있던 말일 때만 의미 있음(GetPlayerCandidates가 준 후보의 UseShortcut을 그대로 넘기면 됨).
        /// 완주해도 매치가 바로 끝나지는 않는다 — 안 끝난 말이 남아있으면 OnPlayerPieceFinished만 쏘고,
        /// 호출부가 EndAsPlayerWin을 불러야 실제로 끝난다(전원 골인이면 자동으로 끝남).
        /// 반환값이 true면 보너스 턴(윷/모 또는 잡기).
        /// </summary>
        public bool ApplyPlayerMove(string pieceId, bool useShortcut, YutThrowOutcome outcome)
        {
            if (IsEnded) return false;
            var piece = playerPieces.FirstOrDefault(p => p.Id == pieceId && !p.Finished);
            if (piece == null) return false;

            bool wasOnBoard = piece.OnBoard;
            int fromNode = piece.NodeId;
            var group = wasOnBoard
                ? playerPieces.Where(p => !p.Finished && p.NodeId == fromNode).ToList()
                : new List<YutPiece> { piece };
            var movedIds = group.Select(p => p.Id).ToList();
            bool isBaekdo = outcome.Result == YutThrowResult.Baekdo;

            // 빽도 — 완주 판정 없이 항상 뒤로 간다(대기 말이면 참 뒤에 새로 서는 것도 포함).
            if (isBaekdo)
            {
                int dest;
                List<int> newHistory;
                if (!wasOnBoard)
                {
                    dest = BaekdoEntryNode;
                    newHistory = new List<int> { BaekdoEntryNode };
                }
                else
                {
                    newHistory = new List<int>(piece.History);
                    dest = YutMoveResolver.PeekBackwardDestination(fromNode, newHistory);
                    if (newHistory.Count > 0) newHistory.RemoveAt(newHistory.Count - 1);
                }

                foreach (var p in group)
                {
                    p.NodeId = dest;
                    p.History.Clear();
                    p.History.AddRange(newHistory);
                }

                bool capturedByEntry = opponentPiece.OnBoard && opponentPiece.NodeId == dest;
                if (capturedByEntry)
                {
                    opponentPiece.NodeId = -1;
                    opponentPiece.History.Clear();
                    OnOpponentCaptured?.Invoke();
                }

                OnPlayerPiecesMoved?.Invoke(movedIds);
                OnPiecesChanged?.Invoke();
                return outcome.GrantsBonusThrow || capturedByEntry;
            }

            // 이미 참에 서 있던 말은 뭘 던지든 이번 던지기로 바로 완주(참을 "지나는" 셈).
            bool alreadyAtStart = wasOnBoard && fromNode == YutBoardLayout.Start;
            var path = YutMoveResolver.GetPath(wasOnBoard ? fromNode : YutBoardLayout.Start, outcome.Result, useShortcut);

            if (ResolvesToFinish(alreadyAtStart, path))
            {
                foreach (var p in group) { p.Finished = true; p.NodeId = -1; p.History.Clear(); }
                OnPlayerPiecesMoved?.Invoke(movedIds);
                OnPiecesChanged?.Invoke();

                if (playerPieces.All(p => p.Finished))
                {
                    IsEnded = true;
                    OnMatchEnded?.Invoke(true);
                    return false;
                }

                OnPlayerPieceFinished?.Invoke(movedIds);
                return outcome.GrantsBonusThrow;
            }

            int dest2 = path[path.Length - 1];
            var visited = new List<int>(piece.History);
            visited.AddRange(path.Take(path.Length - 1));
            foreach (var p in group)
            {
                p.NodeId = dest2;
                p.History.Clear();
                p.History.AddRange(visited);
            }

            bool captured = opponentPiece.OnBoard && opponentPiece.NodeId == dest2;
            if (captured)
            {
                opponentPiece.NodeId = -1;
                opponentPiece.History.Clear();
                OnOpponentCaptured?.Invoke();
            }

            OnPlayerPiecesMoved?.Invoke(movedIds);
            OnPiecesChanged?.Invoke();
            return outcome.GrantsBonusThrow || captured;
        }

        /// <summary>이무기 턴의 던지기 한 번. 플레이어 쪽 ThrowForPlayer와 대칭 — 호출부가 던지기
        /// 애니메이션을 보여줄 수 있게 굴림과 적용(ApplyOpponentMove)을 분리해 둔다.</summary>
        public YutThrowOutcome ThrowForOpponent() => YutThrowRoller.Roll();

        /// <summary>
        /// 이무기 던지기 결과 하나를 적용한다. 말이 하나뿐이라 "어느 말을 움직일지" 선택이 없어서
        /// 이동·잡기 판정까지 바로 진행한다. 이무기는 결승점이 없어서(참을 지나도) 완주로 안 끝나고
        /// 그냥 계속 판을 돈다 — 반환값이 true면 보너스 턴(윷/모 또는 잡기).
        /// </summary>
        public bool ApplyOpponentMove(YutThrowOutcome outcome)
        {
            if (IsEnded) return false;
            bool isBaekdo = outcome.Result == YutThrowResult.Baekdo;

            if (isBaekdo && !opponentPiece.OnBoard)
                return false; // 대기 중에 빽도 — 움직일 게 없어 턴 소모

            int dest;
            if (isBaekdo)
            {
                var history = new List<int>(opponentPiece.History);
                dest = YutMoveResolver.PeekBackwardDestination(opponentPiece.NodeId, history);
                if (history.Count > 0) history.RemoveAt(history.Count - 1);
                opponentPiece.History.Clear();
                opponentPiece.History.AddRange(history);
            }
            else
            {
                int fromNode = opponentPiece.OnBoard ? opponentPiece.NodeId : YutBoardLayout.Start;
                var path = YutMoveResolver.GetPath(fromNode, outcome.Result);
                dest = path[path.Length - 1];
                var visited = new List<int>(opponentPiece.History);
                visited.AddRange(path.Take(path.Length - 1));
                opponentPiece.History.Clear();
                opponentPiece.History.AddRange(visited);
            }
            opponentPiece.NodeId = dest;

            var captured = playerPieces.Where(p => !p.Finished && p.NodeId == dest).ToList();
            foreach (var p in captured) { p.NodeId = -1; p.History.Clear(); }
            if (captured.Count > 0) OnPlayerPiecesCaptured?.Invoke(captured);

            OnPiecesChanged?.Invoke();
            return outcome.GrantsBonusThrow || captured.Count > 0;
        }

        /// <summary>
        /// 완주한 말이 남아있는(그런데 아직 안 들어온 말도 있는) 상태에서, 유저가 "여기서 그만"을
        /// 선택했을 때 호출 — 즉시 플레이어 승리로 매치를 끝낸다.
        /// </summary>
        public void EndAsPlayerWin()
        {
            if (IsEnded) return;
            IsEnded = true;
            OnMatchEnded?.Invoke(true);
        }

        /// <summary>
        /// 완주 판정: 이미 참에 서 있던 말이 던졌다면(alreadyAtStart) 무조건 완주. 아니면 이번 이동
        /// 경로가 참을 "지나가는"(도착 칸 제외한 중간에 참이 나오는) 경우만 완주 — 참에 딱 멈추는
        /// 건 완주가 아니다.
        /// </summary>
        static bool ResolvesToFinish(bool alreadyAtStart, int[] path)
        {
            if (alreadyAtStart) return true;
            for (int i = 1; i < path.Length - 1; i++)
                if (path[i] == YutBoardLayout.Start) return true;
            return false;
        }

        static int ResolveDisplayDestination(bool alreadyAtStart, int[] path) =>
            ResolvesToFinish(alreadyAtStart, path) ? YutBoardLayout.Start : path[path.Length - 1];
    }
}
