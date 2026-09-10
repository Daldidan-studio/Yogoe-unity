using System;
using System.Collections.Generic;
using System.Linq;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 윷놀이 한 판의 규칙(승패·잡기·업기·보너스턴)만 담당하는 순수 로직.
    /// 화면(YutMiniGame)이나 재화(GameEconomy)는 모르고, 결과만 이벤트로 알린다.
    /// 소유는 UI 쪽(YutScreen)이 한다 — CharacterAgent가 CharacterRequestState를 들고 있는 것과 같은 패턴.
    /// </summary>
    public class YutMatch
    {
        public readonly struct YutMoveCandidate
        {
            public readonly string PieceId;
            public readonly int DestinationNode;

            public YutMoveCandidate(string pieceId, int destinationNode)
            {
                PieceId = pieceId;
                DestinationNode = destinationNode;
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

        public YutMatch(IEnumerable<(string id, string displayName)> playerTeam)
        {
            playerPieces = playerTeam.Select(t => new YutPiece(t.id, t.displayName, isPlayer: true)).ToList();
            opponentPiece = new YutPiece("imugi", "이무기", isPlayer: false);
        }

        public YutThrowOutcome ThrowForPlayer() => YutThrowRoller.Roll();

        /// <summary>던진 결과로 지금 움직일 수 있는 내 말(또는 스택 대표) 후보 목록.</summary>
        public IReadOnlyList<YutMoveCandidate> GetPlayerCandidates(YutThrowResult result)
        {
            var list = new List<YutMoveCandidate>();
            var seenBoardNodes = new HashSet<int>();

            foreach (var p in playerPieces)
            {
                if (p.Finished) continue;

                if (!p.OnBoard)
                {
                    if (result == YutThrowResult.Baekdo) continue; // 대기 말은 빽도로 못 움직임
                    var path = YutMoveResolver.GetPath(YutBoardLayout.Start, result);
                    list.Add(new YutMoveCandidate(p.Id, ResolveDisplayDestination(path)));
                    continue;
                }

                if (!seenBoardNodes.Add(p.NodeId)) continue; // 같은 칸(스택)은 대표 한 명만 후보로
                var boardPath = YutMoveResolver.GetPath(p.NodeId, result);
                list.Add(new YutMoveCandidate(p.Id, ResolveDisplayDestination(boardPath)));
            }

            return list;
        }

        /// <summary>
        /// pieceId가 속한 칸(스택이면 전원)을 결과만큼 이동시킨다.
        /// 완주하면 매치가 끝난다. 반환값이 true면 보너스 턴(윷/모 또는 잡기) — 플레이어가 한 번 더 던진다.
        /// </summary>
        public bool ApplyPlayerMove(string pieceId, YutThrowOutcome outcome)
        {
            if (IsEnded) return false;
            var piece = playerPieces.FirstOrDefault(p => p.Id == pieceId && !p.Finished);
            if (piece == null) return false;

            bool wasOnBoard = piece.OnBoard;
            int fromNode = piece.NodeId;
            var group = wasOnBoard
                ? playerPieces.Where(p => !p.Finished && p.NodeId == fromNode).ToList()
                : new List<YutPiece> { piece };

            var path = YutMoveResolver.GetPath(wasOnBoard ? fromNode : YutBoardLayout.Start, outcome.Result);
            var movedIds = group.Select(p => p.Id).ToList();

            if (ResolvesToFinish(path))
            {
                foreach (var p in group) { p.Finished = true; p.NodeId = -1; }
                OnPlayerPiecesMoved?.Invoke(movedIds);
                OnPiecesChanged?.Invoke();
                IsEnded = true;
                OnMatchEnded?.Invoke(true);
                return false;
            }

            int dest = path[path.Length - 1];
            foreach (var p in group) p.NodeId = dest;

            bool captured = opponentPiece.OnBoard && opponentPiece.NodeId == dest;
            if (captured) opponentPiece.NodeId = -1;

            OnPlayerPiecesMoved?.Invoke(movedIds);
            OnPiecesChanged?.Invoke();
            return outcome.GrantsBonusThrow || captured;
        }

        /// <summary>이무기 턴의 던지기 한 번. 플레이어 쪽 ThrowForPlayer와 대칭 — 호출부가 던지기
        /// 애니메이션을 보여줄 수 있게 굴림과 적용(ApplyOpponentMove)을 분리해 둔다.</summary>
        public YutThrowOutcome ThrowForOpponent() => YutThrowRoller.Roll();

        /// <summary>
        /// 이무기 던지기 결과 하나를 적용한다. 말이 하나뿐이라 "어느 말을 움직일지" 선택이 없어서
        /// 이동·잡기·완주 판정까지 바로 진행한다. 반환값이 true면 보너스 턴(윷/모 또는 잡기) —
        /// 호출부가 한 번 더 ThrowForOpponent/ApplyOpponentMove를 돌려야 한다.
        /// </summary>
        public bool ApplyOpponentMove(YutThrowOutcome outcome)
        {
            if (IsEnded) return false;

            if (outcome.Result == YutThrowResult.Baekdo && !opponentPiece.OnBoard)
                return false; // 대기 중에 빽도 — 움직일 게 없어 턴 소모

            int fromNode = opponentPiece.OnBoard ? opponentPiece.NodeId : YutBoardLayout.Start;
            var path = YutMoveResolver.GetPath(fromNode, outcome.Result);

            if (ResolvesToFinish(path))
            {
                opponentPiece.Finished = true;
                opponentPiece.NodeId = -1;
                OnPiecesChanged?.Invoke();
                IsEnded = true;
                OnMatchEnded?.Invoke(false);
                return false;
            }

            int dest = path[path.Length - 1];
            opponentPiece.NodeId = dest;

            var captured = playerPieces.Where(p => !p.Finished && p.NodeId == dest).ToList();
            foreach (var p in captured) p.NodeId = -1;

            OnPiecesChanged?.Invoke();
            return outcome.GrantsBonusThrow || captured.Count > 0;
        }

        /// <summary>path[1..] 안에 출발점(0)이 다시 나오면 이번 이동으로 완주.</summary>
        static bool ResolvesToFinish(int[] path)
        {
            for (int i = 1; i < path.Length; i++)
                if (path[i] == YutBoardLayout.Start) return true;
            return false;
        }

        static int ResolveDisplayDestination(int[] path) =>
            ResolvesToFinish(path) ? YutBoardLayout.Start : path[path.Length - 1];
    }
}
