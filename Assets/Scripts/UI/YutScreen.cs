using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Minigames.Yut;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>
    /// 윷놀이 진입점(11장). 윷 토큰 소모 → 옥토끼+현재 슬롯의 혼 전원 vs 이무기(말 1개) →
    /// 완주 시 향·엽전 지급까지의 최소 완결 루프. 화면/입력은 YutMiniGame, 규칙은 YutMatch가 담당.
    /// </summary>
    public class YutScreen : MonoBehaviour
    {
        public static YutScreen Instance { get; private set; }

        public Font font;

        /// <summary>완주 보상 — 기획서 11장 확정: 향 1개 + 엽전 1개.</summary>
        const int FinishHyangReward = 1;
        const int FinishYeopjeonReward = 1;
        /// <summary>이 수만큼 업고 한 번에 완주할 때만 광고 2배 선택(3마리는 해당 없음).</summary>
        const int FinishAdBonusStackCount = 4;

        /// <summary>말 이동 시 친밀도 +0.25(11장) 적용을 위한 piece id → 캐릭터 매핑.</summary>
        readonly Dictionary<string, CharacterAgent> teamById = new Dictionary<string, CharacterAgent>();

        [Header("셸 (Prefab — 비어 있으면 Play 시 코드 조립)")]
        [SerializeField] GameObject root;
        [SerializeField] YutMiniGame miniGame;
        [SerializeField] GameObject noticeRoot;
        [SerializeField] Text noticeText;
        [SerializeField] Button noticeOkButton;
        [SerializeField] GameObject choiceRoot;
        [SerializeField] Text choiceText;
        [SerializeField] Button choiceContinueButton;
        [SerializeField] Button choiceStopButton;
        [SerializeField] GameObject rewardRoot;
        [SerializeField] Text rewardText;
        [SerializeField] Button rewardPlainButton;
        [SerializeField] Button rewardAdButton;
        [SerializeField] GameObject reviveRoot;
        [SerializeField] Text reviveText;
        [SerializeField] Button reviveYesButton;
        [SerializeField] Button reviveNoButton;
        [SerializeField] GameObject confirmRoot;
        [SerializeField] Text confirmText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        const int EvolveWithPurifiedWaterCost = 5;

        /// <summary>특수 칸(엽전/공양물/보물상자) 보상 배율 — "그냥 받기"면 1배, "광고 보고"면 2배.</summary>
        const int SquareRewardBase = 1;
        const int SquareRewardAdMultiplier = 2;
        const float SquareRewardAdWatchSeconds = 0.8f; // BatchCollectPopup과 동일한 "광고 시청" 연출용 지연
        const float ReviveAdWatchSeconds = 0.8f;

        /// <summary>보물상자 윷 토큰 보상은 평소 상한(5)을 넘길 수 있되 이 값까지만.</summary>
        const int YutTokenHardCap = 7;

        public bool HasPrefabShell => root != null && miniGame != null;

        /// <summary>윷 화면이 떠 있는 동안 본맵 핀치/휠 줌·드래그가 새면 안 된다.</summary>
        public bool IsOpen => root != null && root.activeInHierarchy;

        YutMatch match;
        YutThrowOutcome? pendingOutcome;
        /// <summary>칸 홉 연출 중 — 연타로 Apply가 두 번 돌지 않게.</summary>
        bool moveInProgress;

        /// <summary>말 한 마리가 골인해서 "계속할지/그만할지" 다이얼로그가 떠 있는 동안, 방금 던진
        /// 결과가 보너스였는지 기억해뒀다가 '계속하기'를 고르면 그대로 이어서 써야 한다.</summary>
        bool awaitingFinishChoice;
        bool pendingBonusAfterContinue;

        /// <summary>4마리 동시 완주 광고 2배 팝업용 — 배율 적용 전(스택 반영된) 향/엽전.</summary>
        int pendingFinishHyang;
        int pendingFinishYeopjeon;
        int pendingFinishStack;

        /// <summary>직전 OnPlayerPieceFinished 스택(자동 플레이가 그만/계속 판단용).</summary>
        int lastFinishEventStack;
        readonly List<string> lastFinishedIds = new List<string>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Coroutine autoPlayRoutine;
        bool autoPlayRunning;
        /// <summary>0이면 매치 종료까지. N이면 골인 스택이 N 이상일 때 그만하기.</summary>
        int autoPlayStopAtStack;
#endif

        enum SquareRewardKind { Offering, Yeopjeon, Hyang, AdTicket, YutToken }

        readonly struct PendingSquareReward
        {
            public readonly SquareRewardKind Kind;
            public readonly OfferingData Offering; // Kind == Offering일 때만
            public readonly int Amount; // 배율 적용 전 기본 개수

            public PendingSquareReward(SquareRewardKind kind, OfferingData offering, int amount)
            {
                Kind = kind;
                Offering = offering;
                Amount = amount;
            }
        }

        /// <summary>특수 칸 보상 팝업("그냥 받기"/"광고 보고 2배")이 떠 있는 동안 다음 턴 진행을 멈춘다.</summary>
        bool awaitingSquareReward;
        bool pendingBonusAfterSquareReward;
        PendingSquareReward? pendingSquareReward;
        /// <summary>보물상자 칸에서 굴린 보상인지 — Kind는 실제 지급 재화라서 출처는 따로 기억한다.</summary>
        bool pendingSquareRewardFromTreasure;
        /// <summary>지급이 끝나면 지울 특수 칸 노드. -1이면 없음.</summary>
        int pendingSquareRewardNodeId = -1;

        /// <summary>매치 시작 때 한 번 뽑는다 — 공양물 칸(YutBoardLayout.SpecialSquareKind.Offering)
        /// 노드마다 어떤 공양물을 줄지. 매치 내내 고정(같은 칸을 다시 밟아도 같은 공양물).</summary>
        readonly Dictionary<int, OfferingData> specialOfferingByNode = new Dictionary<int, OfferingData>();

        /// <summary>"광고 보고 말 되살리기" 팝업이 떠 있는 동안 이무기 보너스 턴 진행을 멈춘다.</summary>
        bool awaitingReviveChoice;
        List<YutMatch.CapturedPieceSnapshot> pendingReviveSnapshots;

        /// <summary>이번 매치에서 특수 칸으로 모은 것들 — 동(東) 구역에 표시, 매치가 끝나면 요약
        /// 다이얼로그로도 보여준다. 새 매치 시작할 때 비운다(재시작 복원 시엔 다시 0부터).</summary>
        readonly Dictionary<OfferingData, int> matchOfferingCounts = new Dictionary<OfferingData, int>();
        int matchYeopjeonTotal;
        int matchHyangTotal;
        int matchAdTicketTotal;
        int matchYutTokenTotal;

        Action pendingNoticeAction;
        Action pendingChoiceContinue;
        Action pendingChoiceStop;
        Action pendingRewardPlain;
        Action pendingRewardAd;
        Action pendingReviveYes;
        Action pendingReviveNo;
        Action pendingConfirmYes;
        Action pendingConfirmNo;

        void Awake() => Instance = this;

        void Start()
        {
            EnsureBuilt();
            WireRuntimeListeners();
            if (root != null) root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>에디터 Bake용. 보드까지 만들어 Prefab에서 레이아웃을 볼 수 있게 한다.</summary>
        public void EnsureBuiltForBake()
        {
            EnsureBuilt();
            if (miniGame != null)
            {
                miniGame.font = font;
                miniGame.BindFromHierarchy();
                miniGame.EnsureBoardForBake();
            }
            if (root != null) root.SetActive(true);
        }

        public void Open()
        {
            EnsureBuilt();

            // 나갔다 왔거나(Close) 앱을 껐다 켜서(ApplyFromSave) 이어할 매치가 이미 있으면
            // 토큰을 새로 안 쓰고 그대로 이어서 보여준다.
            if (match != null && !match.IsEnded)
            {
                ResumeExistingMatch();
                return;
            }

            if (GameEconomy.Instance == null || !GameEconomy.Instance.TrySpendYutToken(1))
            {
                ShowNotice("윷 토큰이 부족합니다.", null);
                return;
            }

            var agents = CharacterAgent.All
                .Where(a => a != null && a.Stats != null && a.Stats.Stage == GrowthStage.Hon)
                .ToList();

            if (agents.Count == 0)
            {
                GameEconomy.Instance.AddYutToken(1); // 참가할 요괴가 없으면 토큰 환불
                ShowNotice("참가할 요괴가 없습니다.", null);
                return;
            }

            teamById.Clear();
            var team = new List<(string id, string name)>();
            foreach (var a in agents)
            {
                string id = a.Data != null ? a.Data.id.ToString() : a.name;
                string name = a.Data != null && !string.IsNullOrEmpty(a.Data.displayName) ? a.Data.displayName : a.name;
                teamById[id] = a;
                team.Add((id, name));
            }

            BeginMatch(team);
        }

        void BeginMatch(List<(string id, string name)> team)
        {
            match = new YutMatch(team);
            YutBoardLayout.RegenerateSpecialSquares(); // 진짜 새 매치일 때만 새로 뽑는다
            SubscribeMatchEvents();

            awaitingFinishChoice = false;
            awaitingSquareReward = false;
            awaitingReviveChoice = false;
            matchOfferingCounts.Clear();
            matchYeopjeonTotal = 0;
            matchHyangTotal = 0;
            matchAdTicketTotal = 0;
            matchYutTokenTotal = 0;
            AssignSpecialOfferings();
            root.SetActive(true);
            miniGame.Show();
            miniGame.SetLeaveVisible(true);
            miniGame.SetThrowVisible(true);
            // 말풍선 규칙 1번: 던지기 전에는 어떤 요괴도(이무기 포함) 말하지 않는다.
            miniGame.RefreshHearts(GameEconomy.Instance.YutToken);
            HandlePiecesChanged();
            RefreshCollectedItemsDisplay();
            ApplySpecialSquareVisuals();
            GameSaveBridge.SaveFromWorld();
        }

        /// <summary>이미 진행 중이던(나갔다 왔거나 앱 재시작으로 복원된) 매치를 그대로 보여준다 —
        /// 토큰 소모·팀 재구성 없이 화면만 다시 연다.</summary>
        void ResumeExistingMatch()
        {
            root.SetActive(true);
            miniGame.Show();
            miniGame.SetLeaveVisible(true);
            miniGame.SetThrowVisible(true);
            miniGame.RefreshHearts(GameEconomy.Instance != null ? GameEconomy.Instance.YutToken : 0);
            HandlePiecesChanged();
            RefreshCollectedItemsDisplay(); // 나갔다 왔거나 재시작 복원 — 이번 매치에서 모은 건 추적 안 해서 빈 채로 시작
            ApplySpecialSquareVisuals(); // 셸이 다시 만들어졌을 수도 있어 매번 다시 입힌다
        }

        void HandleYutTokenChanged(int token)
        {
            if (miniGame != null) miniGame.RefreshHearts(token);
        }
        /// <summary>
        /// 윷놀이 이벤트 구독
        /// </summary>
        void SubscribeMatchEvents()
        {
            if (match == null) return;
            UnsubscribeMatchEvents();
            match.OnPiecesChanged += HandlePiecesChanged;
            match.OnMatchEnded += HandleMatchEnded;
            match.OnPlayerPiecesMoved += HandlePlayerPiecesMoved;
            match.OnPlayerPiecesCaptured += HandlePlayerPiecesCaptured;
            match.OnPlayerPiecesCapturedRevivable += HandlePlayerPiecesCapturedRevivable;
            match.OnOpponentCaptured += HandleOpponentCaptured;
            match.OnPlayerPieceFinished += HandlePlayerPieceFinished;
            match.OnSpecialSquareReached += HandleSpecialSquareReached;
        }

        void UnsubscribeMatchEvents()
        {
            if (match == null) return;
            match.OnPiecesChanged -= HandlePiecesChanged;
            match.OnMatchEnded -= HandleMatchEnded;
            match.OnPlayerPiecesMoved -= HandlePlayerPiecesMoved;
            match.OnPlayerPiecesCaptured -= HandlePlayerPiecesCaptured;
            match.OnPlayerPiecesCapturedRevivable -= HandlePlayerPiecesCapturedRevivable;
            match.OnOpponentCaptured -= HandleOpponentCaptured;
            match.OnPlayerPieceFinished -= HandlePlayerPieceFinished;
            match.OnSpecialSquareReached -= HandleSpecialSquareReached;
        }

        /// <summary>
        /// "나가기"를 눌러도 승패가 안 난 매치는 메모리에 그대로 둔다 — 다시 열면 이어서 하고,
        /// 세이브에도 매번 담겨서 앱을 껐다 켜도 이어진다. 승패가 이미 난 매치만 완전히 정리한다.
        /// </summary>
        public void Close()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugStopAutoPlay();
#endif
            if (match != null && match.IsEnded)
            {
                UnsubscribeMatchEvents();
                match = null;
            }
            // 팝업이 떠 있던 채로 나가면 그 선택은 그냥 흘려보낸다(다음에 열면 던지기 대기 상태로).
            pendingOutcome = null;
            moveInProgress = false;
            awaitingFinishChoice = false;
            pendingBonusAfterContinue = false;
            awaitingSquareReward = false;
            pendingBonusAfterSquareReward = false;
            pendingSquareReward = null;
            pendingSquareRewardFromTreasure = false;
            pendingSquareRewardNodeId = -1;
            awaitingReviveChoice = false;
            pendingReviveSnapshots = null;
            // root만 꺼두면 팝업 자신의 activeSelf는 그대로 남아있어서, 다음에 다시 열 때
            // (ResumeExistingMatch) 엉뚱하게 같이 떠버린다 — 하나씩 확실히 내려둔다.
            if (noticeRoot != null) noticeRoot.SetActive(false);
            if (choiceRoot != null) choiceRoot.SetActive(false);
            if (rewardRoot != null) rewardRoot.SetActive(false);
            if (reviveRoot != null) reviveRoot.SetActive(false);
            if (miniGame != null) miniGame.Hide();
            if (root != null) root.SetActive(false);
            GameSaveBridge.SaveFromWorld();
        }

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

            var offeringNodeIds = new List<int>();
            var offeringIds = new List<string>();
            foreach (var kv in specialOfferingByNode)
            {
                if (kv.Value == null) continue;
                offeringNodeIds.Add(kv.Key);
                offeringIds.Add(kv.Value.offeringId);
            }

            return new YutMatchSave
            {
                playerPieces = match.PlayerPieces.Select(ToPieceSave).ToArray(),
                opponentPiece = ToPieceSave(match.OpponentPiece),
                specialSquareNodeIds = squareNodeIds.ToArray(),
                specialSquareKinds = squareKinds.ToArray(),
                specialOfferingNodeIds = offeringNodeIds.ToArray(),
                specialOfferingIds = offeringIds.ToArray(),
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

            SubscribeMatchEvents();
            awaitingFinishChoice = false;
            awaitingSquareReward = false;
            awaitingReviveChoice = false;
        }

        /// <summary>말 위치는 그대로 복원되는데 특수 칸만 새로 섞이면 안 되니, 저장된 배치가
        /// 있으면 그대로 되살리고 — 그 배치 자체가 없는 옛 세이브일 때만 어쩔 수 없이 새로 뽑는다.</summary>
        void RestoreSpecialSquares(YutMatchSave saved)
        {
            if (saved.specialSquareNodeIds == null || saved.specialSquareNodeIds.Length == 0)
            {
                YutBoardLayout.RegenerateSpecialSquares();
                AssignSpecialOfferings();
                return;
            }

            var kinds = new Dictionary<int, YutBoardLayout.SpecialSquareKind>();
            for (int i = 0; i < saved.specialSquareNodeIds.Length && i < saved.specialSquareKinds.Length; i++)
                kinds[saved.specialSquareNodeIds[i]] = (YutBoardLayout.SpecialSquareKind)saved.specialSquareKinds[i];
            YutBoardLayout.RestoreSpecialSquares(kinds);

            specialOfferingByNode.Clear();
            if (saved.specialOfferingNodeIds != null && saved.specialOfferingIds != null)
            {
                var pool = GetOfferingPool();
                for (int i = 0; i < saved.specialOfferingNodeIds.Length && i < saved.specialOfferingIds.Length; i++)
                {
                    var offering = pool.FirstOrDefault(o => o.offeringId == saved.specialOfferingIds[i]);
                    if (offering != null) specialOfferingByNode[saved.specialOfferingNodeIds[i]] = offering;
                }
            }
        }

        static void ApplyPieceSave(YutPiece piece, YutPieceSave save)
        {
            piece.NodeId = save.nodeId;
            piece.Finished = save.finished;
            piece.History.Clear();
            if (save.history != null) piece.History.AddRange(save.history);
        }

        /// <summary>기획 11장: 말을 움직일 때마다 그 요괴 친밀도 +0.25.</summary>
        void HandlePlayerPiecesMoved(IReadOnlyList<string> pieceIds)
        {
            foreach (var id in pieceIds)
                if (teamById.TryGetValue(id, out var agent) && agent != null)
                    agent.AddIntimacy(0.25f);
        }

        /// <summary>이무기한테 내 말이 잡혔을 때 — 말풍선 규칙: 반드시 먼저 "으악"이라고 말한
        /// 뒤에 출발점으로 돌아간다. 이 시점엔 모델 위치(NodeId)는 이미 -1이지만 화면상 말은
        /// 아직 잡힌 자리에 있다(OnPiecesChanged가 이 다음에 따로 발행되어야 실제로 이동한다) —
        /// 그래서 여기서 말풍선을 먼저 띄우면 자연히 "말하고 나서 이동"이 된다.</summary>
        void HandlePlayerPiecesCaptured(IReadOnlyList<YutPiece> captured)
        {
            foreach (var p in captured)
            {
                miniGame.ShowPieceBubble(p.Id, YutBubbleCatalog.Get(YutBubbleCatalog.Ids.EventCaptured));
                miniGame.AddPlayLogEntry($"{p.DisplayName} 잡힘.");
            }
        }

        /// <summary>같은 시점 — "광고 보고 되살리기" 팝업. 선택이 끝날 때까지 이무기 보너스 턴
        /// 진행을 멈춘다(RunOpponentTurnRoutine의 awaitingReviveChoice 대기).</summary>
        void HandlePlayerPiecesCapturedRevivable(List<YutMatch.CapturedPieceSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0) return;
            pendingReviveSnapshots = snapshots;
            awaitingReviveChoice = true;
            string names = JoinPieceNames(snapshots);
            ShowReviveChoice($"{names} 잡혔어요!\n광고 보고 되살릴까요?",
                onYes: HandleReviveYes,
                onNo: HandleReviveNo);
        }

        // LINQ(.Select)를 새 struct(CapturedPieceSnapshot)에 처음 쓰면 IL2CPP WebGL 빌드에서
        // "RuntimeError: null function"이 나는 경우가 있어(제네릭 인스턴스 누락) — 수동 루프로 우회.
        string JoinPieceNames(List<YutMatch.CapturedPieceSnapshot> snapshots)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(NameFor(snapshots[i].PieceId));
            }
            return sb.ToString();
        }

        void HandleReviveYes() => StartCoroutine(ReviveAdRoutine());

        IEnumerator ReviveAdRoutine()
        {
            // 실제 광고 SDK가 붙기 전까지 BatchCollectPopup과 동일한 짧은 지연으로 시청을 흉내낸다.
            yield return new WaitForSecondsRealtime(ReviveAdWatchSeconds);

            if (match != null && pendingReviveSnapshots != null && match.ReviveCapturedPieces(pendingReviveSnapshots))
            {
                string names = JoinPieceNames(pendingReviveSnapshots);
                miniGame.ShowPieceBubble(
                    pendingReviveSnapshots[0].PieceId,
                    YutBubbleCatalog.Get(YutBubbleCatalog.Ids.EventRevived));
                miniGame.AddPlayLogEntry($"{names} 되살아남.");
                GameSaveBridge.SaveFromWorld();
            }
            pendingReviveSnapshots = null;
            awaitingReviveChoice = false;
        }

        void HandleReviveNo()
        {
            pendingReviveSnapshots = null;
            awaitingReviveChoice = false;
        }

        /// <summary>내가 이무기를 잡았을 때.</summary>
        void HandleOpponentCaptured()
        {
            miniGame.ShowOpponentBubble(YutBubbleCatalog.Get(YutBubbleCatalog.Ids.EventOpponentCaught));
            miniGame.AddPlayLogEntry("이무기 잡음.");
        }

        /// <summary>power(0~1)는 슬라이드 속도 기반 — 던지는 연출에만 쓰고 결과 확률엔 영향 없다.</summary>
        void HandleThrowPressed(float power)
        {
            if (match == null || match.IsEnded) return;
            var outcome = match.ThrowForPlayer();
            StartCoroutine(PlayerThrowRoutine(outcome, power));
        }

        /// <summary>말풍선 규칙 2번 — 옥토끼가 결과를 말한다. 문구는 YutBubbleCatalog(시트→JSON).
        /// 윷/모는 "다시"만 알리고 칸수는 말하지 않는다(보너스라 이번 결과로는 안 움직일 수도 있어서).
        /// 옥토끼가 이미 완주해 동에 있으면 해설 톤(cheer)으로 바꾼다.</summary>
        string RabbitThrowLine(YutThrowResult result)
        {
            if (IsRabbitFinished())
            {
                return YutBubbleCatalog.Format(
                    YutBubbleCatalog.Ids.RabbitCheer,
                    ("result", result.DisplayName()));
            }

            switch (result)
            {
                case YutThrowResult.Yut: return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.RabbitYut);
                case YutThrowResult.Mo: return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.RabbitMo);
                case YutThrowResult.Baekdo: return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.RabbitBaekdo);
                default:
                    int steps = result switch
                    {
                        YutThrowResult.Do => 1,
                        YutThrowResult.Gae => 2,
                        YutThrowResult.Geol => 3,
                        _ => 0,
                    };
                    return YutBubbleCatalog.Format(
                        YutBubbleCatalog.Ids.RabbitSteps,
                        ("result", result.DisplayName()),
                        ("steps", steps.ToString()));
            }
        }

        bool IsRabbitFinished()
        {
            if (match == null) return false;
            string rabbitId = nameof(CharacterId.Rabbit);
            for (int i = 0; i < match.PlayerPieces.Count; i++)
            {
                var p = match.PlayerPieces[i];
                if (p.Id == rabbitId && p.Finished) return true;
            }
            return false;
        }

        /// <summary>말풍선 규칙 2번 — 각 요괴는 아래 조건 중 하나에 해당할 때만(우선순위 순으로
        /// 하나만) 말한다. 일반 칸으로만 이동 가능하거나 아예 이동할 수 없으면 말하지 않는다.
        /// 문구는 YutBubbleCatalog, 우선순위/조건은 여기 코드.</summary>
        string BubbleLineFor(YutMatch.YutMoveCandidate c)
        {
            switch (YutBoardLayout.GetSpecialKind(c.DestinationNode))
            {
                case YutBoardLayout.SpecialSquareKind.Treasure:
                    return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.CandidateTreasure);
                case YutBoardLayout.SpecialSquareKind.Offering:
                    return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.CandidateOffering);
                case YutBoardLayout.SpecialSquareKind.Coin:
                    return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.CandidateCoin);
            }

            if (match.OpponentPiece.OnBoard && match.OpponentPiece.NodeId == c.DestinationNode)
                return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.CandidateCapture);

            var ally = match.PlayerPieces.FirstOrDefault(
                p => !p.Finished && p.Id != c.PieceId && p.OnBoard && p.NodeId == c.DestinationNode);
            if (ally != null)
                return YutBubbleCatalog.Format(
                    YutBubbleCatalog.Ids.CandidateStack,
                    ("ally", ally.DisplayName));

            if (c.WillFinish) return YutBubbleCatalog.Get(YutBubbleCatalog.Ids.CandidateFinish);

            return null;
        }

        void ShowMoveCandidateBubbles(IReadOnlyList<YutMatch.YutMoveCandidate> candidates)
        {
            foreach (var c in candidates)
            {
                string line = BubbleLineFor(c);
                if (line != null) miniGame.ShowPieceBubble(c.PieceId, line);
            }

            // 옥토끼가 동에 있으면, 남은 말이 완주 가능할 때 옆에서 한 마디.
            if (!IsRabbitFinished() || candidates == null) return;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].WillFinish) continue;
                miniGame.ShowPieceBubble(
                    nameof(CharacterId.Rabbit),
                    YutBubbleCatalog.Get(YutBubbleCatalog.Ids.RabbitUrgeFinish));
                break;
            }
        }

        /// <summary>말이 실제로 움직인 뒤 — 놀이기록(짧은 사건형 문장)에 한 줄 남긴다. 말풍선
        /// 대사와는 분리된 별개 기록이라 여기 텍스트는 대화체가 아니라 사건 요약체로 쓴다.</summary>
        void LogPlayerMoveOutcome(string pieceId, string moverName, YutThrowOutcome outcome, bool bonusTurn)
        {
            var movedPiece = match.PlayerPieces.FirstOrDefault(p => p.Id == pieceId);
            if (movedPiece != null)
            {
                if (movedPiece.Finished)
                {
                    miniGame.AddPlayLogEntry($"{moverName} 완주.");
                }
                else
                {
                    miniGame.AddPlayLogEntry(
                        $"{outcome.Result.DisplayName()}. {moverName} {PositionLabelFor(movedPiece)}(으)로 이동.");
                    var partners = match.PlayerPieces
                        .Where(p => !p.Finished && p.Id != pieceId && p.NodeId == movedPiece.NodeId)
                        .Select(p => p.DisplayName)
                        .ToList();
                    if (partners.Count > 0)
                        miniGame.AddPlayLogEntry($"{string.Join(", ", partners)}와 업음.");
                }
            }
            if (bonusTurn)
                miniGame.AddPlayLogEntry("다시 던짐.");
        }

        IEnumerator PlayerThrowRoutine(YutThrowOutcome outcome, float power)
        {
            miniGame.SetThrowVisible(false);
            miniGame.ClearBubbles();
            yield return miniGame.PlayThrowAnim(outcome.Result, power);
            if (match == null || match.IsEnded) yield break;
            miniGame.ShowPieceBubble("Rabbit", RabbitThrowLine(outcome.Result));

            var candidates = match.GetPlayerCandidates(outcome.Result);
            if (candidates.Count == 0)
            {
                // 이동할 말이 없음(예: 대기 말뿐인데 빽도) — 턴을 그냥 넘긴다.
                yield return RunOpponentTurnRoutine();
                yield break;
            }

            ShowMoveCandidateBubbles(candidates);

            pendingOutcome = outcome;
            var uiCandidates = new List<YutMiniGame.YokaiMoveCandidate>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                var memberIds = StackMemberIdsFor(c.PieceId);
                var memberNames = new string[memberIds.Length];
                for (int m = 0; m < memberIds.Length; m++)
                    memberNames[m] = NameFor(memberIds[m]);
                uiCandidates.Add(new YutMiniGame.YokaiMoveCandidate(
                    c.PieceId,
                    NameFor(c.PieceId),
                    c.DestinationNode,
                    c.UseShortcut,
                    memberIds,
                    memberNames));
            }
            miniGame.FlashCandidates(uiCandidates);
        }

        /// <summary>보드에 업혀 있으면 같은 칸 전원, 대기 말이면 본인만 — 후보 칸에 초상을 전부 그리기 위함.</summary>
        string[] StackMemberIdsFor(string pieceId)
        {
            if (match == null) return new[] { pieceId };
            YutPiece piece = null;
            for (int i = 0; i < match.PlayerPieces.Count; i++)
            {
                var p = match.PlayerPieces[i];
                if (p.Id == pieceId && !p.Finished) { piece = p; break; }
            }
            if (piece == null || !piece.OnBoard) return new[] { pieceId };

            var ids = new List<string>();
            for (int i = 0; i < match.PlayerPieces.Count; i++)
            {
                var p = match.PlayerPieces[i];
                if (!p.Finished && p.NodeId == piece.NodeId) ids.Add(p.Id);
            }
            return ids.Count > 0 ? ids.ToArray() : new[] { pieceId };
        }

        void HandleCandidateTapped(string pieceId, bool useShortcut)
        {
            if (match == null || match.IsEnded || pendingOutcome == null || moveInProgress) return;
            StartCoroutine(PlayerMoveRoutine(pieceId, useShortcut, pendingOutcome.Value));
        }

        IEnumerator PlayerMoveRoutine(string pieceId, bool useShortcut, YutThrowOutcome outcome)
        {
            moveInProgress = true;
            miniGame.ClearCandidates();
            miniGame.ClearBubbles();
            pendingOutcome = null;
            string moverName = NameFor(pieceId);

            var preview = match.PreviewPlayerHop(pieceId, useShortcut, outcome);
            if (preview.Ok)
                yield return miniGame.PlayHopAlongPath(preview.PieceIds, preview.HopNodes, preview.WillFinish);

            if (match == null || match.IsEnded)
            {
                moveInProgress = false;
                yield break;
            }

            // 홉으로 이미 도착해 있으니 ShowYokaiPieces의 미끄러짐은 생략
            miniGame.SetSuppressPieceSlide(true);
            bool bonusTurn = match.ApplyPlayerMove(pieceId, useShortcut, outcome);
            miniGame.SetSuppressPieceSlide(false);

            LogPlayerMoveOutcome(pieceId, moverName, outcome, bonusTurn);

            if (match == null || match.IsEnded)
            {
                moveInProgress = false;
                yield break; // HandleMatchEnded가 이미 결과 처리
            }

            if (awaitingSquareReward)
            {
                // 특수 칸 보상 팝업("그냥 받기"/"광고 보고 2배")이 이미 떴다 — 그 선택이 끝나야 다음이 진행된다.
                pendingBonusAfterSquareReward = bonusTurn;
                moveInProgress = false;
                yield break;
            }

            if (awaitingFinishChoice)
            {
                // 골인 다이얼로그("계속하기"/"그만하기")가 이미 떴다 — 그 선택이 끝나야 다음이 진행된다.
                pendingBonusAfterContinue = bonusTurn;
                moveInProgress = false;
                yield break;
            }

            if (bonusTurn)
                miniGame.SetThrowVisible(true);
            else
                yield return RunOpponentTurnRoutine();

            moveInProgress = false;
        }

        /// <summary>공양물 칸에 배정할 후보 — 정화수 제외 전체 공양물 목록. 수동 루프로 필터링한다
        /// (LINQ .Where/.ToList를 새 조합에 처음 쓰면 IL2CPP WebGL에서 "null function"이 나던
        /// 문제 때문에 — 오늘 이미 두 번 겪었다).</summary>
        List<OfferingData> GetOfferingPool()
        {
            var settings = StartingStateSettings.Get();
            var pool = new List<OfferingData>();
            if (settings.startingOfferings == null) return pool;
            foreach (var o in settings.startingOfferings)
                if (o != null && o.kind != OfferingKind.PurifiedWater) pool.Add(o);
            return pool;
        }

        /// <summary>매치 시작 때 한 번 — 공양물 칸(YutBoardLayout.SpecialSquareKind.Offering)마다
        /// 어떤 공양물을 줄지 미리 뽑아 고정한다(맵에 그대로 노출되니 매번 랜덤이면 안 됨).</summary>
        void AssignSpecialOfferings()
        {
            specialOfferingByNode.Clear();
            var pool = GetOfferingPool();
            if (pool.Count == 0) return;

            for (int nodeId = 0; nodeId < YutBoardLayout.NodeCount; nodeId++)
            {
                if (YutBoardLayout.GetSpecialKind(nodeId) != YutBoardLayout.SpecialSquareKind.Offering) continue;
                specialOfferingByNode[nodeId] = pool[UnityEngine.Random.Range(0, pool.Count)];
            }
        }

        /// <summary>보드 위 특수 칸마다 무슨 보상인지 아이콘을 입힌다 — 엽전 칸/공양물 칸은 실제
        /// 내용물을, 보물상자 칸은 안이 뭔지 숨기고 상자 아이콘만 보여준다.</summary>
        void ApplySpecialSquareVisuals()
        {
            if (miniGame == null) return;
            var icons = new Dictionary<int, Sprite>();
            for (int nodeId = 0; nodeId < YutBoardLayout.NodeCount; nodeId++)
            {
                var kind = YutBoardLayout.GetSpecialKind(nodeId);
                switch (kind)
                {
                    case YutBoardLayout.SpecialSquareKind.Coin:
                        icons[nodeId] = YutMiniGame.YeopjeonIcon();
                        break;
                    case YutBoardLayout.SpecialSquareKind.Offering:
                        if (specialOfferingByNode.TryGetValue(nodeId, out var offering) && offering != null)
                            icons[nodeId] = offering.icon;
                        break;
                    case YutBoardLayout.SpecialSquareKind.Treasure:
                        icons[nodeId] = Resources.Load<Sprite>("UI/GiftChest_Closed");
                        break;
                }
            }
            miniGame.RefreshSpecialSquareVisuals(icons);
        }

        /// <summary>
        /// 말이 특수 칸에 도착했을 때 — 칸 종류(엽전/공양물/보물상자)에 맞는 보상을 정해서
        /// "그냥 받기(1배)"/"광고 보고 2배" 팝업을 띄운다. 완주와 달리 매치를 막지 않고, 선택
        /// 즉시 재화를 지급한다(칸에서 얻은 건 매치 중에도 유지).
        /// </summary>
        void HandleSpecialSquareReached(int nodeId, IReadOnlyList<string> pieceIds)
        {
            string message;
            pendingSquareRewardFromTreasure = false;
            pendingSquareRewardNodeId = nodeId;
            switch (YutBoardLayout.GetSpecialKind(nodeId))
            {
                case YutBoardLayout.SpecialSquareKind.Coin:
                    pendingSquareReward = new PendingSquareReward(SquareRewardKind.Yeopjeon, null, 1);
                    message = "엽전 칸 발견!\n엽전을 얻을 수 있어요.";
                    break;

                case YutBoardLayout.SpecialSquareKind.Offering:
                    {
                        specialOfferingByNode.TryGetValue(nodeId, out var offering);
                        pendingSquareReward = new PendingSquareReward(SquareRewardKind.Offering, offering, 1);
                        string offeringName = offering != null ? offering.displayName : "공양물";
                        message = $"공양물 칸 발견!\n{offeringName}을(를) 얻을 수 있어요.";
                        break;
                    }

                case YutBoardLayout.SpecialSquareKind.Treasure:
                    pendingSquareReward = RollTreasureReward();
                    pendingSquareRewardFromTreasure = true;
                    message = "보물상자 발견!\n무엇이 들어있을까요?";
                    break;

                default:
                    pendingSquareRewardNodeId = -1;
                    return;
            }

            awaitingSquareReward = true;
            miniGame.SetThrowVisible(false);
            ShowRewardChoice(message, onPlain: HandleSquareRewardPlain, onAd: HandleSquareRewardAd);
        }

        /// <summary>보물상자 — 향/공양물/광고보상권/엽전/윷토큰 중 하나를 균등 확률로 뽑는다.</summary>
        PendingSquareReward RollTreasureReward()
        {
            int pick = UnityEngine.Random.Range(0, 5);
            switch (pick)
            {
                case 0:
                    return new PendingSquareReward(SquareRewardKind.Hyang, null, 1);
                case 1:
                    var pool = GetOfferingPool();
                    var offering = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : null;
                    return new PendingSquareReward(SquareRewardKind.Offering, offering, 1);
                case 2:
                    return new PendingSquareReward(SquareRewardKind.AdTicket, null, 1);
                case 3:
                    return new PendingSquareReward(SquareRewardKind.Yeopjeon, null, 1);
                default:
                    return new PendingSquareReward(SquareRewardKind.YutToken, null, RollTreasureYutTokenAmount());
            }
        }

        /// <summary>보통 3개, 가끔 4개, 드물게 5개 — 평소 상한(5)을 넘기지 않는 선에서 기본값보다
        /// 후하게. "그냥 받기/광고 2배"를 거치며 실제로는 최대 YutTokenHardCap(7)까지 쌓일 수 있다.</summary>
        static int RollTreasureYutTokenAmount()
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.6f) return 3;
            if (roll < 0.85f) return 4;
            return 5;
        }

        void HandleSquareRewardPlain()
        {
            bool wasTreasure = pendingSquareRewardFromTreasure;
            string desc = GrantSquareReward(SquareRewardBase);
            if (wasTreasure && desc != null)
                ShowNotice($"보물상자에서 {desc} 나왔다!", ResumeAfterSquareReward);
            else
                ResumeAfterSquareReward();
        }

        void HandleSquareRewardAd() => StartCoroutine(SquareRewardAdRoutine());

        IEnumerator SquareRewardAdRoutine()
        {
            // BatchCollectPopup과 같은 패턴 — 실제 광고 SDK가 붙기 전까지 짧은 지연으로 "시청 중"을 흉내낸다.
            yield return new WaitForSecondsRealtime(SquareRewardAdWatchSeconds);
            bool wasTreasure = pendingSquareRewardFromTreasure;
            string desc = GrantSquareReward(SquareRewardAdMultiplier);
            if (wasTreasure && desc != null)
                ShowNotice($"보물상자에서 {desc} 나왔다!", ResumeAfterSquareReward);
            else
                ResumeAfterSquareReward();
        }

        /// <summary>실제로 재화를 지급하고 놀이기록에 남긴다. 엽전/공양물 칸은 밟기 전에 이미
        /// 뭐가 나올지 안내했지만, 보물상자는 안이 안 보이는 채로 골랐으니 반환값(뭘 받았는지
        /// 설명)을 호출부가 팝업으로 따로 알려준다.</summary>
        string GrantSquareReward(int multiplier)
        {
            if (pendingSquareReward == null) return null;
            var reward = pendingSquareReward.Value;
            int amount = reward.Amount * multiplier;
            string description = null;

            switch (reward.Kind)
            {
                case SquareRewardKind.Yeopjeon:
                    GameEconomy.Instance.AddYeopjeon(amount);
                    matchYeopjeonTotal += amount;
                    miniGame.AddPlayLogEntry($"엽전 {amount}개 획득.");
                    description = $"엽전 {amount}개";
                    break;

                case SquareRewardKind.Hyang:
                    GameEconomy.Instance.AddHyang(amount);
                    matchHyangTotal += amount;
                    miniGame.AddPlayLogEntry($"향 {amount}개 획득.");
                    description = $"향 {amount}개";
                    break;

                case SquareRewardKind.AdTicket:
                    GiftBundle.AddAdTickets(amount);
                    matchAdTicketTotal += amount;
                    miniGame.AddPlayLogEntry($"광고보상권 {amount}개 획득.");
                    description = $"광고보상권 {amount}개";
                    break;

                case SquareRewardKind.Offering:
                    if (reward.Offering != null)
                    {
                        GameEconomy.Instance.AddOffering(reward.Offering, amount);
                        matchOfferingCounts.TryGetValue(reward.Offering, out int cur);
                        matchOfferingCounts[reward.Offering] = cur + amount;
                        miniGame.AddPlayLogEntry($"{reward.Offering.displayName} {amount}개 획득.");
                        description = $"{reward.Offering.displayName} {amount}개";
                    }
                    break;

                case SquareRewardKind.YutToken:
                    GameEconomy.Instance.AddYutTokenOverflow(amount, YutTokenHardCap);
                    matchYutTokenTotal += amount;
                    miniGame.AddPlayLogEntry($"윷 토큰 {amount}개 획득.");
                    description = $"윷 토큰 {amount}개";
                    break;
            }

            pendingSquareReward = null;
            pendingSquareRewardFromTreasure = false;
            ClearConsumedSpecialSquare();
            RefreshCollectedItemsDisplay();
            GameSaveBridge.SaveFromWorld();
            return description;
        }

        /// <summary>보상을 받은 특수 칸은 보드·세이브에서 제거한다(아이콘/색도 일반 칸으로 되돌림).</summary>
        void ClearConsumedSpecialSquare()
        {
            if (pendingSquareRewardNodeId < 0) return;
            int nodeId = pendingSquareRewardNodeId;
            pendingSquareRewardNodeId = -1;
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
                bool overflowed = GameEconomy.Instance != null && GameEconomy.Instance.YutToken > GameEconomy.Instance.YutTokenMax;
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Count = matchYutTokenTotal,
                    Label = "윷 토큰",
                    Tint = overflowed ? new Color(1f, 0.55f, 0.85f, 1f) : (Color?)null,
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
            if (matchHyangTotal > 0) parts.Add($"향 {matchHyangTotal}개");
            if (matchAdTicketTotal > 0) parts.Add($"광고보상권 {matchAdTicketTotal}개");
            if (matchYutTokenTotal > 0) parts.Add($"윷 토큰 {matchYutTokenTotal}개");
            return parts.Count > 0 ? string.Join(", ", parts) + "를 얻었다" : null;
        }

        void ResumeAfterSquareReward()
        {
            awaitingSquareReward = false;
            if (pendingBonusAfterSquareReward)
                miniGame.SetThrowVisible(true);
            else
                StartCoroutine(RunOpponentTurnRoutine());
        }

        /// <summary>말이 골인했는데 아직 안 들어온 말이 남아있을 때 — 여기서 그만 받을지, 계속할지 묻는다.</summary>
        void HandlePlayerPieceFinished(IReadOnlyList<string> finishedIds)
        {
            lastFinishEventStack = finishedIds != null ? finishedIds.Count : 0;
            lastFinishedIds.Clear();
            if (finishedIds != null)
            {
                for (int i = 0; i < finishedIds.Count; i++)
                    lastFinishedIds.Add(finishedIds[i]);
            }

            awaitingFinishChoice = true;
            miniGame.SetThrowVisible(false);
            string names = string.Join(", ", finishedIds.Select(NameFor));

            // 옥토끼가 이번 골인에 포함되면 동 초상 위에서 한 마디(이미 ShowFinishedPieces로 앵커 있음).
            if (finishedIds != null)
            {
                string rabbitId = nameof(CharacterId.Rabbit);
                for (int i = 0; i < finishedIds.Count; i++)
                {
                    if (finishedIds[i] != rabbitId) continue;
                    miniGame.ShowPieceBubble(
                        rabbitId,
                        YutBubbleCatalog.Get(YutBubbleCatalog.Ids.RabbitFinished));
                    break;
                }
            }

            ShowChoice($"{names} 골인!\n여기서 그만 받을까요, 남은 말로 계속할까요?",
                onContinue: HandleContinueAfterFinish,
                onStop: () => HandleStopAfterFinish(finishedIds));
        }

        /// <summary>말이 하나 골인해서 계속하기로 했으면 — 기존 말은 그대로 두고 게임 이어가기
        void HandleContinueAfterFinish()
        {
            awaitingFinishChoice = false;
            ProceedAfterFinishContinue();
        }

        /// <summary>특수 칸(엽전/공양물/보물상자) 배치만 다시 뽑는다 — 말 위치·역사는 안 건드린다.</summary>
        void ResetSpecialSquares()
        {
            YutBoardLayout.RegenerateSpecialSquares();
            AssignSpecialOfferings();
            ApplySpecialSquareVisuals();
        }

        void ProceedAfterFinishContinue()
        {
            if (pendingBonusAfterContinue)
                miniGame.SetThrowVisible(true);
            else
                StartCoroutine(RunOpponentTurnRoutine());
        }

        /// <summary>골인 후 "여기서 그만" — 완주로 매치 종료.</summary>
        void HandleStopAfterFinish(IReadOnlyList<string> finishedIds)
        {
            awaitingFinishChoice = false;
            match?.EndAsFinished(finishedIds != null ? finishedIds.Count : 1);
        }

        /// <summary>
        /// 이무기 턴 전체(보너스 턴 포함)를 한 번씩 던지기 애니메이션까지 보여주며 진행한다.
        /// 플레이어 턴과 대칭으로 ThrowForOpponent/ApplyOpponentMove를 한 스텝씩 돌려서,
        /// 이무기도 실제로 던지는 모습이 보이고 게임로그로 지금 누구 차례인지 알 수 있게 한다.
        /// 보드 한가운데 큰 연출은 플레이어 전용 — 이무기는 초상 밑에 조그맣게 던진다.
        /// </summary>
        IEnumerator RunOpponentTurnRoutine()
        {
            miniGame.SetThrowVisible(false);
            miniGame.ClearBubbles();
            yield return new WaitForSecondsRealtime(0.4f);

            bool bonus;
            int guard = 0;
            do
            {
                guard++;
                var outcome = match.ThrowForOpponent();
                yield return miniGame.PlayOpponentMiniThrowAnim(outcome.Result);
                if (match == null || match.IsEnded) yield break;
                miniGame.ClearBubbles();
                miniGame.ShowOpponentBubble(YutBubbleCatalog.Format(
                    YutBubbleCatalog.Ids.OpponentThrow,
                    ("result", outcome.Result.DisplayName())));

                bonus = false;
                var oppPreview = match.PreviewOpponentHop(outcome);
                if (oppPreview.Ok)
                {
                    // 대기에서 첫 입장: 참에 띄운 뒤 경로를 밟는다(웹도 출발→각 칸).
                    if (!match.OpponentPiece.OnBoard)
                    {
                        miniGame.SetSuppressPieceSlide(true);
                        miniGame.ShowOpponentPiece(true);
                        miniGame.SetOpponentPieceIndex(YutBoardLayout.Start);
                        miniGame.SetSuppressPieceSlide(false);
                    }
                    yield return miniGame.PlayOpponentHopAlongPath(oppPreview.HopNodes);
                }

                if (match == null || match.IsEnded) yield break;

                miniGame.SetSuppressPieceSlide(true);
                bonus = match.ApplyOpponentMove(outcome);
                miniGame.SetSuppressPieceSlide(false);
                if (match.IsEnded) yield break;
                if (match.OpponentPiece.OnBoard)
                    miniGame.AddPlayLogEntry($"이무기, {PositionLabelFor(match.OpponentPiece)}(으)로 이동.");
                if (bonus)
                    miniGame.AddPlayLogEntry("다시 던짐.");

                // 말이 잡혔으면 "광고 보고 되살리기" 팝업이 뜬다 — 선택이 끝날 때까지 다음 던지기를 멈춘다.
                if (awaitingReviveChoice)
                    yield return new WaitUntil(() => !awaitingReviveChoice);
                if (match == null || match.IsEnded) yield break;

                if (bonus)
                    yield return new WaitForSecondsRealtime(0.4f);
            } while (bonus && guard < 20);

            if (match != null && !match.IsEnded)
            {
                miniGame.SetThrowVisible(true);
            }
        }

        void HandleLeavePressed() => Close();

        /// <summary>윷 토큰(하트) 옆 [+] — 메인 HUD와 같은 팝업(엽전 구매/광고 충전)을 윷놀이
        /// 화면 안에서도 그대로 연다.</summary>
        void HandleBuyTokensPressed()
        {
            if (YutTokenShopPopup.Instance != null) YutTokenShopPopup.Instance.Open();
        }

        void HandlePiecesChanged()
        {
            if (match == null) return;

            var infos = match.PlayerPieces
                .Where(p => !p.Finished)
                .Select(p => new YutMiniGame.YokaiPieceInfo(p.Id, p.DisplayName, p.NodeId))
                .ToList();
            miniGame.ShowYokaiPieces(infos);

            // 참(시작점)에 멈춘 것과 완주(골인)한 건 구별돼야 한다 — 완주하면 보드에서 빠지는
            // 대신 동(東) 구역 하단에 작은 초상으로 표시한다.
            var finishedIds = new List<string>();
            foreach (var p in match.PlayerPieces)
                if (p.Finished) finishedIds.Add(p.Id);
            miniGame.ShowFinishedPieces(finishedIds);

            var opp = match.OpponentPiece;
            miniGame.ShowOpponentPiece(opp.OnBoard);
            if (opp.OnBoard) miniGame.SetOpponentPieceIndex(opp.NodeId);

            // LINQ(.Select/.ToList)를 새 struct(RosterEntry)에 처음 쓰면 IL2CPP WebGL 빌드에서
            // "RuntimeError: null function"이 나는 경우가 있어(제네릭 인스턴스 누락) — 수동 루프로 우회.
            var roster = new List<YutMiniGame.RosterEntry>(match.PlayerPieces.Count);
            foreach (var p in match.PlayerPieces)
            {
                teamById.TryGetValue(p.Id, out var agent);
                int stamina = agent != null && agent.Stats != null ? Mathf.RoundToInt(agent.Stats.Stamina) : 0;
                int intimacy = agent != null && agent.Stats != null ? Mathf.RoundToInt(agent.Stats.Intimacy) : 0;
                roster.Add(new YutMiniGame.RosterEntry(p.Id, p.DisplayName, stamina, intimacy, PositionLabelFor(p)));
            }
            // 지금 키우는(소환된) 요괴 수만큼만 말을 쓸 수 있다. 고라니를 아직 안 불렀으면
            // "소환하기", 불렀는데 아직 넋이라 말로 못 쓰면 "진화 필요" 슬롯을 안내한다.
            bool showExtraSlot = false;
            string extraLabel = null;
            Action extraAction = null;
            Sprite extraIcon = null;
            if (!CharacterSummon.IsPresent(CharacterId.Gorani))
            {
                showExtraSlot = true;
                extraLabel = "소환하기";
                extraAction = OnSummonSlotTapped;
            }
            else
            {
                var gorani = CharacterSummon.Find(CharacterId.Gorani);
                if (gorani != null && gorani.Stats != null && gorani.Stats.Stage == GrowthStage.Neok)
                {
                    showExtraSlot = true;
                    extraLabel = "진화 필요";
                    extraAction = OnEvolveSlotTapped;
                    // "+"가 아니라 넋 아이콘을 계속 보여줘서 이미 소환된 상태임을 알린다.
                    extraIcon = miniGame.GetNeokSprite();
                }
            }
            miniGame.ShowRoster(roster, showExtraSlot, extraLabel, extraAction, extraIcon);
        }

        void OnSummonSlotTapped()
        {
            if (CharacterSummon.IsPresent(CharacterId.Gorani)) return;

            if (!CharacterSummon.CanSummonGorani())
            {
                ShowNotice($"향이 부족합니다 (필요 {CharacterSummon.HyangCost}, 보유 {GameEconomy.Instance.Hyang}).", null);
                return;
            }

            ShowConfirm($"향 {CharacterSummon.HyangCost}개를 피워 요괴를 부르시겠습니까?", "부르기", "취소",
                () => StartCoroutine(SummonCeremonyRoutine()), null);
        }

        /// <summary>메인 화면 소환 연출(암전 → 넋 등장)과 같은 느낌을, 윷 화면 안에서 직접
        /// 재현한다 — SummonCeremony는 월드 스페이스 연출이라 윷 화면의 불투명 패널에
        /// 가려져 안 보인다(SummonPopup과 같은 문제). 대신 화면을 어둡게 했다 밝히면서 그
        /// 사이에 넋을 소환해 "슬롯에 넋이 들어오는" 느낌만 살린다.</summary>
        IEnumerator SummonCeremonyRoutine()
        {
            CeremonyGate.Begin();
            var dimGo = new GameObject("SummonDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dimGo.transform.SetParent(root.transform, false);
            Stretch((RectTransform)dimGo.transform);
            dimGo.transform.SetAsLastSibling();
            var dimImg = dimGo.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0.05f, 0f);

            float t = 0f;
            const float dimIn = 0.45f;
            while (t < dimIn)
            {
                t += Time.unscaledDeltaTime;
                // 완전히 새까맣게는 안 하고(0.72) — 소환하기 슬롯이 은은하게 비쳐서 "저기로
                // 떨어진다"는 느낌이 나게. 메인 화면 SummonCeremony와 같은 어둡기.
                dimImg.color = new Color(0f, 0f, 0.05f, Mathf.Lerp(0f, 0.72f, t / dimIn));
                yield return null;
            }

            var agent = CharacterSummon.TrySummonGorani(null, font);

            // 넋 아이콘이 화면 위에서 로스터의 "소환하기" 슬롯 자리로 떨어져 안착하는 연출 —
            // dimGo의 자식으로 붙여서 암전 위에 확실히 보이게 한다(YutMiniGame 쪽에 붙이면
            // 암전 오버레이보다 그리기 순서가 앞서서 안 보였다).
            if (agent != null)
                yield return PlaySummonDrop(dimGo.transform);
            else
                yield return new WaitForSecondsRealtime(0.4f);

            t = 0f;
            const float dimOut = 0.5f;
            while (t < dimOut)
            {
                t += Time.unscaledDeltaTime;
                dimImg.color = new Color(0f, 0f, 0.05f, Mathf.Lerp(0.72f, 0f, t / dimOut));
                yield return null;
            }
            Destroy(dimGo);
            CeremonyGate.End();

            if (agent == null)
            {
                ShowNotice("소환에 실패했습니다.", null);
                yield break;
            }

            HandlePiecesChanged();
            GameSaveBridge.SaveFromWorld();
        }

        /// <summary>넋 아이콘을 화면 위쪽에서 로스터의 "소환하기" 슬롯 위치까지 떨어뜨린다.
        /// 슬롯 위치를 못 구하면(레이아웃 준비 전 등) 화면 중앙으로 대신 떨어뜨린다.</summary>
        IEnumerator PlaySummonDrop(Transform parent)
        {
            var go = new GameObject("SummonDrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(64f, 64f);
            var img = go.GetComponent<Image>();
            var sprite = miniGame != null ? miniGame.GetNeokSprite() : CharacterSpawner.NeokFlameSprite();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.45f, 0.85f, 1f, 1f); // 불꽃 에셋 없을 때 폴백
            }

            Vector3? slotPos = miniGame != null ? miniGame.GetSummonSlotWorldPosition() : null;
            Vector3 targetPos = slotPos ?? rt.position;
            Vector3 startPos = targetPos + new Vector3(0f, 520f, 0f);
            rt.position = startPos;

            const float fall = 0.55f;
            float t = 0f;
            while (t < fall)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / fall);
                float eased = 1f - (1f - u) * (1f - u);
                rt.position = Vector3.Lerp(startPos, targetPos, eased);
                yield return null;
            }

            const float bounce = 0.2f;
            t = 0f;
            while (t < bounce)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / bounce);
                float bob = Mathf.Sin(u * Mathf.PI) * 10f * (1f - u);
                rt.position = targetPos + new Vector3(0f, bob, 0f);
                yield return null;
            }

            rt.position = targetPos;
            yield return new WaitForSecondsRealtime(0.2f);
            Destroy(go);
        }

        /// <summary>넋은 아직 윷놀이 말로 못 쓴다 — 정화수를 써서 즉시 진화시키는 지름길.</summary>
        void OnEvolveSlotTapped()
        {
            var gorani = CharacterSummon.Find(CharacterId.Gorani);
            if (gorani == null || gorani.Stats == null || gorani.Stats.Stage != GrowthStage.Neok) return;

            ShowConfirm(
                $"아직은 윷놀이를 할 수 없다.\n정화수 {EvolveWithPurifiedWaterCost}개를 써서 진화시킬까?",
                "예", "아니오",
                () => DoEvolveGorani(gorani), null);
        }

        void DoEvolveGorani(CharacterAgent gorani)
        {
            if (gorani == null || gorani.Stats == null || gorani.Stats.Stage != GrowthStage.Neok) return;

            if (!GameEconomy.Instance.TrySpendPurifiedWater(EvolveWithPurifiedWaterCost))
            {
                ShowNotice("정화수가 부족합니다.", null);
                return;
            }

            gorani.EvolveToHon(playFx: false); // 윷 화면 뒤라 월드 연출이 안 보이니 생략

            // 진화했으면 지금 이 매치에도 바로 대기 말로 합류시킨다 — 다음 판까지 안 기다리고
            // 곧장 다른 말들처럼 로스터에 뜨게.
            if (match != null && !match.IsEnded)
            {
                string id = gorani.Data != null ? gorani.Data.id.ToString() : gorani.name;
                string name = gorani.Data != null && !string.IsNullOrEmpty(gorani.Data.displayName)
                    ? gorani.Data.displayName
                    : gorani.name;
                if (match.TryAddPlayerPiece(id, name))
                    teamById[id] = gorani;
            }

            GameSaveBridge.SaveFromWorld();
            HandlePiecesChanged();
        }

        /// <summary>말 하나의 현재 보드 위치를 사람이 읽는 이름으로 — 대기/완주가 아니면 잘 알려진
        /// 이름 있는 칸(참·도·개·걸·윷·모·뒷모·찌모·방)만 그대로, 그 외는 "N칸째"로 안전하게 표기.</summary>
        static string PositionLabelFor(YutPiece p)
        {
            if (p.Finished) return "완주";
            if (p.NodeId < 0) return "출발 대기";
            return p.NodeId switch
            {
                YutBoardLayout.Start => "참",
                1 => "도",
                2 => "개",
                3 => "걸",
                4 => "윷",
                YutBoardLayout.Mo => "모",
                YutBoardLayout.DwitMo => "뒷모",
                YutBoardLayout.JjiMo => "찌모",
                YutBoardLayout.Bang => "방",
                _ => $"{p.NodeId}칸"
            };
        }

        /// <summary>매치 종료(완주) — 보상은 이번 골인 스택 수 배율. 4마리 동시 완주만 광고 2배 선택.</summary>
        void HandleMatchEnded(int finishStackCount)
        {
            miniGame.SetThrowVisible(false);
            miniGame.ClearCandidates();

            int stack = Mathf.Max(1, finishStackCount);
            pendingFinishStack = stack;
            pendingFinishHyang = FinishHyangReward * stack;
            pendingFinishYeopjeon = FinishYeopjeonReward * stack;

            // 가진 말을 FinishAdBonusStackCount마리 전부 업고 한 번에 완주할 때만 광고 2배 선택.
            if (stack == FinishAdBonusStackCount)
            {
                ShowRewardChoice(
                    $"{stack}마리 업고 완주 (×{stack})!\n향 {pendingFinishHyang}개 + 엽전 {pendingFinishYeopjeon}개",
                    onPlain: HandleFinishRewardPlain,
                    onAd: HandleFinishRewardAd);
                return;
            }

            GrantFinishRewardAndNotice(1);
        }

        void HandleFinishRewardPlain() => GrantFinishRewardAndNotice(1);

        void HandleFinishRewardAd() => StartCoroutine(FinishRewardAdRoutine());

        IEnumerator FinishRewardAdRoutine()
        {
            yield return new WaitForSecondsRealtime(SquareRewardAdWatchSeconds);
            GrantFinishRewardAndNotice(2);
        }

        /// <summary>완주 향/엽전 지급 후 안내. multiplier는 광고 2배용.
        /// 안내를 닫으면 전원 완주이므로 판을 강제로 새 판으로 리셋한다.</summary>
        void GrantFinishRewardAndNotice(int multiplier)
        {
            int hyang = pendingFinishHyang * multiplier;
            int yeop = pendingFinishYeopjeon * multiplier;
            GameEconomy.Instance.AddHyang(hyang);
            GameEconomy.Instance.AddYeopjeon(yeop);

            string message = pendingFinishStack > 1
                ? $"완주! {pendingFinishStack}마리 업고 ×{pendingFinishStack}\n향 {hyang}개 + 엽전 {yeop}개 획득"
                : $"완주! 향 {hyang}개 + 엽전 {yeop}개 획득";
            if (multiplier > 1)
                message += $"\n(광고 ×{multiplier})";

            string collected = BuildCollectedItemsSummary();
            if (!string.IsNullOrEmpty(collected))
                message += $"\n{collected}";

            ShowNotice(message, OnMatchEndedNoticeOk);
            GameSaveBridge.SaveFromWorld();
        }

        void OnMatchEndedNoticeOk()
        {
            // 전원 완주 → 보상 확인 후 판을 강제로 리셋(토큰 추가 소모 없음).
            RestartBoardAfterAllFinished();
        }

        /// <summary>같은 팀으로 새 매치를 연다. 특수칸·말 위치·획득 내역을 처음부터.</summary>
        void RestartBoardAfterAllFinished()
        {
            var team = new List<(string id, string name)>();
            if (match != null)
            {
                for (int i = 0; i < match.PlayerPieces.Count; i++)
                {
                    var p = match.PlayerPieces[i];
                    team.Add((p.Id, p.DisplayName));
                }
                UnsubscribeMatchEvents();
                match = null;
            }
            else
            {
                foreach (var kv in teamById)
                {
                    string name = kv.Value != null && kv.Value.Data != null && !string.IsNullOrEmpty(kv.Value.Data.displayName)
                        ? kv.Value.Data.displayName
                        : kv.Key;
                    team.Add((kv.Key, name));
                }
            }

            if (team.Count == 0) return;

            BeginMatch(team);
            if (miniGame != null)
            {
                miniGame.ClearPlayLog();
                miniGame.ClearBubbles();
                miniGame.AddPlayLogEntry("새 판이 열렸습니다.");
            }
        }

        string NameFor(string pieceId) =>
            match?.PlayerPieces.FirstOrDefault(p => p.Id == pieceId)?.DisplayName ?? "?";

        void EnsureBuilt()
        {
            if (!HasPrefabShell)
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var canvasGO = new GameObject("Canvas_Yut");
                canvasGO.transform.SetParent(transform, false);
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 820;
                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                canvasGO.AddComponent<GraphicRaycaster>();

                root = new GameObject("Panel", typeof(RectTransform));
                var rootRt = (RectTransform)root.transform;
                rootRt.SetParent(canvasGO.transform, false);
                Stretch(rootRt);
                var bg = root.AddComponent<Image>();
                bg.color = new Color(0.06f, 0.06f, 0.06f, 1f);

                var gameGO = new GameObject("YutMiniGame", typeof(RectTransform));
                var gameRt = (RectTransform)gameGO.transform;
                gameRt.SetParent(rootRt, false);
                Stretch(gameRt);
                miniGame = gameGO.AddComponent<YutMiniGame>();
                miniGame.font = font;
                miniGame.BindFromHierarchy();
                miniGame.Hide();

                BuildNoticePanel(rootRt);
                BuildChoicePanel(rootRt);

                root.SetActive(false);
            }

            // 예전에 구운(Bake) YutScreen.prefab엔 리워드/되살리기 팝업이 없다(그 기능이 생기기
            // 전에 구웠음) — HasPrefabShell이라 위 블록을 건너뛰어도 이 둘은 항상 있는지 보정한다.
            // 없으면 rewardText/reviveRoot 등이 계속 null이라 특수 칸을 밟거나 말이 잡히는 순간
            // NullReferenceException으로 죽는다.
            EnsureExtraPanels();
        }

        void EnsureExtraPanels()
        {
            if (root == null) return;
            var rootRt = (RectTransform)root.transform;
            if (rewardRoot == null) BuildRewardPanel(rootRt);
            if (reviveRoot == null) BuildRevivePanel(rootRt);
            if (confirmRoot == null) BuildConfirmPanel(rootRt);
        }

        void WireRuntimeListeners()
        {
            if (miniGame == null) return;

            miniGame.OnThrowPressed -= HandleThrowPressed;
            miniGame.OnLeavePressed -= HandleLeavePressed;
            miniGame.OnCandidateTapped -= HandleCandidateTapped;
            miniGame.OnBuyTokensPressed -= HandleBuyTokensPressed;
            miniGame.OnThrowPressed += HandleThrowPressed;
            miniGame.OnLeavePressed += HandleLeavePressed;
            miniGame.OnCandidateTapped += HandleCandidateTapped;
            miniGame.OnBuyTokensPressed += HandleBuyTokensPressed;

            // 윷놀이 화면 안에서 [+]로 토큰을 사도(또는 광고로 충전해도) 하트가 바로 갱신되게.
            if (GameEconomy.Instance != null)
            {
                GameEconomy.Instance.OnYutTokenChanged -= HandleYutTokenChanged;
                GameEconomy.Instance.OnYutTokenChanged += HandleYutTokenChanged;
            }

            miniGame.font = font;
            miniGame.BindFromHierarchy();

            if (noticeOkButton != null)
            {
                noticeOkButton.onClick.RemoveAllListeners();
                noticeOkButton.onClick.AddListener(OnNoticeOk);
            }

            if (choiceContinueButton != null)
            {
                choiceContinueButton.onClick.RemoveAllListeners();
                choiceContinueButton.onClick.AddListener(OnChoiceContinueClicked);
            }

            if (choiceStopButton != null)
            {
                choiceStopButton.onClick.RemoveAllListeners();
                choiceStopButton.onClick.AddListener(OnChoiceStopClicked);
            }

            if (rewardPlainButton != null)
            {
                rewardPlainButton.onClick.RemoveAllListeners();
                rewardPlainButton.onClick.AddListener(OnRewardPlainClicked);
            }

            if (rewardAdButton != null)
            {
                rewardAdButton.onClick.RemoveAllListeners();
                rewardAdButton.onClick.AddListener(OnRewardAdClicked);
            }

            if (reviveYesButton != null)
            {
                reviveYesButton.onClick.RemoveAllListeners();
                reviveYesButton.onClick.AddListener(OnReviveYesClicked);
            }

            if (reviveNoButton != null)
            {
                reviveNoButton.onClick.RemoveAllListeners();
                reviveNoButton.onClick.AddListener(OnReviveNoClicked);
            }

            if (confirmYesButton != null)
            {
                confirmYesButton.onClick.RemoveAllListeners();
                confirmYesButton.onClick.AddListener(OnConfirmYesClicked);
            }

            if (confirmNoButton != null)
            {
                confirmNoButton.onClick.RemoveAllListeners();
                confirmNoButton.onClick.AddListener(OnConfirmNoClicked);
            }
        }

        void BuildNoticePanel(Transform parent)
        {
            noticeRoot = new GameObject("Notice", typeof(RectTransform));
            var rt = (RectTransform)noticeRoot.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var dim = noticeRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform));
            var boxRt = (RectTransform)box.transform;
            boxRt.SetParent(rt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(640, 350);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 30);
            textRt.sizeDelta = new Vector2(550, 180);
            noticeText = textGO.AddComponent<Text>();
            noticeText.font = font;
            noticeText.fontSize = UiFonts.Size(40);
            noticeText.alignment = TextAnchor.MiddleCenter;
            noticeText.color = new Color(1f, 0.95f, 0.85f);
            noticeText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var btnGO = new GameObject("Btn_Ok", typeof(RectTransform));
            var btnRt = (RectTransform)btnGO.transform;
            btnRt.SetParent(boxRt, false);
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0, -110);
            btnRt.sizeDelta = new Vector2(250, 85);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.5f, 0.45f, 1f);
            noticeOkButton = btnGO.AddComponent<Button>();
            noticeOkButton.targetGraphic = btnImg;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGO.transform;
            labelRt.SetParent(btnRt, false);
            Stretch(labelRt);
            var label = labelGO.AddComponent<Text>();
            label.font = font;
            label.fontSize = UiFonts.Size(38);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "확인";
            label.raycastTarget = false;

            noticeRoot.SetActive(false);
        }

        void ShowNotice(string message, Action onOk)
        {
            noticeText.text = message;
            pendingNoticeAction = onOk;
            if (!root.activeSelf) root.SetActive(true);
            noticeRoot.SetActive(true);
        }

        void OnNoticeOk()
        {
            noticeRoot.SetActive(false);
            // 매치 종료 안내(완주)는 action(OnMatchEndedNoticeOk) 안에서 match를 null로 비운다 —
            // action 실행 "후"에 match == null을 검사하면 그 케이스까지 "매치 시작 전 안내"로
            // 오인해 화면을 닫아버린다(완주해도 윷판이 사라지던 버그). action 실행 전 상태로 판단한다.
            bool matchWasNullBeforeAction = match == null;
            var action = pendingNoticeAction;
            pendingNoticeAction = null;
            action?.Invoke();
            // 매치 시작 전 안내(토큰 부족 등)였다면 화면 자체를 다시 닫는다.
            if (matchWasNullBeforeAction && root != null) root.SetActive(false);
        }

        /// <summary>말 골인 때 "계속하기"/"그만하고 보상받기" 둘 중 하나를 고르게 하는 팝업.</summary>
        void BuildChoicePanel(Transform parent)
        {
            choiceRoot = new GameObject("Choice", typeof(RectTransform));
            var rt = (RectTransform)choiceRoot.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var dim = choiceRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform));
            var boxRt = (RectTransform)box.transform;
            boxRt.SetParent(rt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(700, 410);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 70);
            textRt.sizeDelta = new Vector2(610, 220);
            choiceText = textGO.AddComponent<Text>();
            choiceText.font = font;
            choiceText.fontSize = UiFonts.Size(38);
            choiceText.alignment = TextAnchor.MiddleCenter;
            choiceText.color = new Color(1f, 0.95f, 0.85f);
            choiceText.horizontalOverflow = HorizontalWrapMode.Wrap;

            BuildChoiceButton(boxRt, new Vector2(-165, -130), "계속하기", out choiceContinueButton);
            BuildChoiceButton(boxRt, new Vector2(165, -130), "그만하고\n보상받기", out choiceStopButton);

            choiceRoot.SetActive(false);
        }

        void BuildChoiceButton(Transform parent, Vector2 pos, string label, out Button button)
        {
            var btnGO = new GameObject($"Btn_{label}", typeof(RectTransform));
            var btnRt = (RectTransform)btnGO.transform;
            btnRt.SetParent(parent, false);
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = pos;
            btnRt.sizeDelta = new Vector2(320, 115);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.5f, 0.45f, 1f);
            button = btnGO.AddComponent<Button>();
            button.targetGraphic = btnImg;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGO.transform;
            labelRt.SetParent(btnRt, false);
            Stretch(labelRt);
            var labelText = labelGO.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = UiFonts.Size(34);
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;
        }

        void ShowChoice(string message, Action onContinue, Action onStop)
        {
            choiceText.text = message;
            pendingChoiceContinue = onContinue;
            pendingChoiceStop = onStop;
            if (!root.activeSelf) root.SetActive(true);
            choiceRoot.SetActive(true);
        }

        void OnChoiceContinueClicked()
        {
            choiceRoot.SetActive(false);
            var action = pendingChoiceContinue;
            pendingChoiceContinue = null;
            pendingChoiceStop = null;
            action?.Invoke();
        }

        void OnChoiceStopClicked()
        {
            choiceRoot.SetActive(false);
            var action = pendingChoiceStop;
            pendingChoiceContinue = null;
            pendingChoiceStop = null;
            action?.Invoke();
        }

        /// <summary>특수 칸 보상 "그냥 받기"/"광고 보고 2배" 팝업. BuildChoicePanel과 구조는 같고
        /// 버튼 라벨·핸들러만 다르다.</summary>
        void BuildRewardPanel(Transform parent)
        {
            rewardRoot = new GameObject("SquareReward", typeof(RectTransform));
            var rt = (RectTransform)rewardRoot.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var dim = rewardRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform));
            var boxRt = (RectTransform)box.transform;
            boxRt.SetParent(rt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(700, 410);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 70);
            textRt.sizeDelta = new Vector2(610, 220);
            rewardText = textGO.AddComponent<Text>();
            rewardText.font = font;
            rewardText.fontSize = UiFonts.Size(38);
            rewardText.alignment = TextAnchor.MiddleCenter;
            rewardText.color = new Color(1f, 0.95f, 0.85f);
            rewardText.horizontalOverflow = HorizontalWrapMode.Wrap;

            BuildChoiceButton(boxRt, new Vector2(-165, -130), "그냥 받기", out rewardPlainButton);
            BuildChoiceButton(boxRt, new Vector2(165, -130), "광고 보고\n2배로 받기", out rewardAdButton);

            rewardRoot.SetActive(false);
        }

        void ShowRewardChoice(string message, Action onPlain, Action onAd)
        {
            rewardText.text = message;
            pendingRewardPlain = onPlain;
            pendingRewardAd = onAd;
            if (!root.activeSelf) root.SetActive(true);
            rewardRoot.SetActive(true);
        }

        void OnRewardPlainClicked()
        {
            rewardRoot.SetActive(false);
            var action = pendingRewardPlain;
            pendingRewardPlain = null;
            pendingRewardAd = null;
            action?.Invoke();
        }

        void OnRewardAdClicked()
        {
            rewardRoot.SetActive(false);
            var action = pendingRewardAd;
            pendingRewardPlain = null;
            pendingRewardAd = null;
            action?.Invoke();
        }

        /// <summary>이무기한테 말이 잡혔을 때 "광고 보고 되살리기"/"그냥 두기" 팝업.
        /// BuildChoicePanel과 구조는 같고 버튼 라벨·핸들러만 다르다.</summary>
        void BuildRevivePanel(Transform parent)
        {
            reviveRoot = new GameObject("Revive", typeof(RectTransform));
            var rt = (RectTransform)reviveRoot.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var dim = reviveRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform));
            var boxRt = (RectTransform)box.transform;
            boxRt.SetParent(rt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(700, 410);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 70);
            textRt.sizeDelta = new Vector2(610, 220);
            reviveText = textGO.AddComponent<Text>();
            reviveText.font = font;
            reviveText.fontSize = UiFonts.Size(38);
            reviveText.alignment = TextAnchor.MiddleCenter;
            reviveText.color = new Color(1f, 0.95f, 0.85f);
            reviveText.horizontalOverflow = HorizontalWrapMode.Wrap;

            BuildChoiceButton(boxRt, new Vector2(-165, -130), "광고 보고\n되살리기", out reviveYesButton);
            BuildChoiceButton(boxRt, new Vector2(165, -130), "그냥 두기", out reviveNoButton);

            reviveRoot.SetActive(false);
        }

        void ShowReviveChoice(string message, Action onYes, Action onNo)
        {
            reviveText.text = message;
            pendingReviveYes = onYes;
            pendingReviveNo = onNo;
            if (!root.activeSelf) root.SetActive(true);
            reviveRoot.SetActive(true);
        }

        void OnReviveYesClicked()
        {
            reviveRoot.SetActive(false);
            var action = pendingReviveYes;
            pendingReviveYes = null;
            pendingReviveNo = null;
            action?.Invoke();
        }

        void OnReviveNoClicked()
        {
            reviveRoot.SetActive(false);
            var action = pendingReviveNo;
            pendingReviveYes = null;
            pendingReviveNo = null;
            action?.Invoke();
        }

        /// <summary>범용 확인 팝업(소환하기/진화하기 등) — BuildRevivePanel과 구조는 같지만
        /// 버튼 라벨을 ShowConfirm이 호출될 때마다 바꿀 수 있다.</summary>
        void BuildConfirmPanel(Transform parent)
        {
            confirmRoot = new GameObject("Confirm", typeof(RectTransform));
            var rt = (RectTransform)confirmRoot.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var dim = confirmRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform));
            var boxRt = (RectTransform)box.transform;
            boxRt.SetParent(rt, false);
            boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(700, 410);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 70);
            textRt.sizeDelta = new Vector2(610, 220);
            confirmText = textGO.AddComponent<Text>();
            confirmText.font = font;
            confirmText.fontSize = UiFonts.Size(38);
            confirmText.alignment = TextAnchor.MiddleCenter;
            confirmText.color = new Color(1f, 0.95f, 0.85f);
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;

            BuildChoiceButton(boxRt, new Vector2(-165, -130), "예", out confirmYesButton);
            BuildChoiceButton(boxRt, new Vector2(165, -130), "아니오", out confirmNoButton);

            confirmRoot.SetActive(false);
        }

        void ShowConfirm(string message, string yesLabel, string noLabel, Action onYes, Action onNo)
        {
            confirmText.text = message;
            SetButtonLabel(confirmYesButton, yesLabel);
            SetButtonLabel(confirmNoButton, noLabel);
            pendingConfirmYes = onYes;
            pendingConfirmNo = onNo;
            if (!root.activeSelf) root.SetActive(true);
            confirmRoot.SetActive(true);
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null || string.IsNullOrEmpty(label)) return;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        void OnConfirmYesClicked()
        {
            confirmRoot.SetActive(false);
            var action = pendingConfirmYes;
            pendingConfirmYes = null;
            pendingConfirmNo = null;
            action?.Invoke();
        }

        void OnConfirmNoClicked()
        {
            confirmRoot.SetActive(false);
            var action = pendingConfirmNo;
            pendingConfirmYes = null;
            pendingConfirmNo = null;
            action?.Invoke();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// QA: 실제로 윷을 던지고(연출 포함) 말을 고르며 진행한다.
        /// stopAtStack &gt; 0 이면 그 수 이상 업고 골인했을 때 "그만"으로 보상 확인.
        /// 0 이면 매치가 끝날 때까지(전원 완주 등) 계속.
        /// </summary>
        public void DebugStartAutoPlay(int stopAtStack = 0)
        {
            EnsureBuilt();
            if (root == null || miniGame == null)
            {
                Debug.LogWarning("[Yut QA] YutScreen 셸이 없습니다.");
                return;
            }

            DebugStopAutoPlay();

            // QA는 항상 새 매치에서 던지기를 본다(끝난 매치·애매한 턴 상태 방지).
            if (match != null)
            {
                UnsubscribeMatchEvents();
                match = null;
            }

            int need = Mathf.Max(1, stopAtStack);
            if (!EnsureMatchReadyForFinishSim(need))
                return;

            ClearBlockingUiForFinishSim();
            miniGame.BindFromHierarchy();
            HandlePiecesChanged();
            root.SetActive(true);
            miniGame.Show();
            miniGame.SetLeaveVisible(true);
            miniGame.SetThrowVisible(true);

            autoPlayStopAtStack = Mathf.Max(0, stopAtStack);
            autoPlayRunning = true;
            autoPlayRoutine = StartCoroutine(AutoPlayRoutine());
            Debug.Log(autoPlayStopAtStack > 0
                ? $"[Yut QA] 자동 플레이 시작 — 업고 골인 ×{autoPlayStopAtStack}이면 그만 (말 {match.PlayerPieces.Count}개)"
                : $"[Yut QA] 자동 플레이 시작 — 매치 종료까지 (말 {match.PlayerPieces.Count}개, throwVisible={miniGame.IsThrowVisible})");
        }

        public void DebugStopAutoPlay()
        {
            autoPlayRunning = false;
            if (autoPlayRoutine != null)
            {
                StopCoroutine(autoPlayRoutine);
                autoPlayRoutine = null;
            }
        }

        IEnumerator AutoPlayRoutine()
        {
            // 한 프레임 기다려 보드/던지기 UI가 켜진 뒤 시작한다.
            yield return null;

            float startedAt = Time.unscaledTime;
            int steps = 0;

            while (autoPlayRunning && match != null && steps++ < 2000)
            {
                if (Time.unscaledTime - startedAt > 600f)
                {
                    Debug.LogWarning("[Yut QA] 자동 플레이 시간 초과");
                    break;
                }

                // 안내/보상/골인선택/특수칸/되살리기/확인 — 전부 자동으로 넘긴다.
                if (TryAutoDismissPopups())
                {
                    yield return new WaitForSecondsRealtime(0.2f);
                    continue;
                }

                if (match.IsEnded)
                    break;

                if (pendingOutcome != null)
                {
                    var candidates = match.GetPlayerCandidates(pendingOutcome.Value.Result);
                    if (candidates.Count == 0)
                    {
                        pendingOutcome = null;
                        yield return null;
                        continue;
                    }

                    var best = PickAutoPlayCandidate(candidates);
                    // 홉 연출이 끝날 때까지 기다린다(연타/다음 던지기가 겹치지 않게).
                    yield return PlayerMoveRoutine(best.PieceId, best.UseShortcut, pendingOutcome.Value);
                    yield return new WaitForSecondsRealtime(0.25f);
                    continue;
                }

                // 던지기 존이 아직 없으면 바인딩 재시도.
                if (!miniGame.IsThrowVisible)
                {
                    miniGame.BindFromHierarchy();
                    miniGame.SetThrowVisible(true);
                    if (!miniGame.IsThrowVisible)
                    {
                        yield return null;
                        continue;
                    }
                }

                HandleThrowPressed(1f);

                float wait = 0f;
                while (autoPlayRunning && match != null && !match.IsEnded && wait < 25f)
                {
                    if (pendingOutcome != null) break;
                    if (HasBlockingPopup()) break;
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                    if (pendingOutcome != null || HasBlockingPopup())
                        break;
                    // 이무기 턴이 끝나고 다시 던질 수 있으면 바깥 루프로.
                    if (miniGame.IsThrowVisible && pendingOutcome == null && wait > 0.6f)
                        break;
                }
            }

            // 종료 직후 남은 완주 안내/광고 선택도 넘긴다.
            for (int i = 0; i < 10 && autoPlayRunning && TryAutoDismissPopups(); i++)
                yield return new WaitForSecondsRealtime(0.15f);

            string reason = !autoPlayRunning ? "stop"
                : match == null ? "match=null"
                : match != null && match.IsEnded ? "match ended"
                : "step limit";
            autoPlayRunning = false;
            autoPlayRoutine = null;
            Debug.Log($"[Yut QA] 자동 플레이 종료 ({reason})");
        }

        bool HasBlockingPopup()
        {
            if (awaitingSquareReward || awaitingFinishChoice || awaitingReviveChoice) return true;
            if (noticeRoot != null && noticeRoot.activeSelf) return true;
            if (rewardRoot != null && rewardRoot.activeSelf) return true;
            if (choiceRoot != null && choiceRoot.activeSelf) return true;
            if (reviveRoot != null && reviveRoot.activeSelf) return true;
            if (confirmRoot != null && confirmRoot.activeSelf) return true;
            return false;
        }

        /// <summary>자동 플레이용 — 팝업은 전부 그냥 받기/계속/확인으로 넘긴다(광고는 스킵).</summary>
        bool TryAutoDismissPopups()
        {
            if (noticeRoot != null && noticeRoot.activeSelf)
            {
                OnNoticeOk();
                return true;
            }

            if (rewardRoot != null && rewardRoot.activeSelf)
            {
                OnRewardPlainClicked();
                return true;
            }

            if (awaitingSquareReward)
            {
                // 보상 선택 패널이 떠 있으면 그냥 받기.
                if (rewardRoot != null && rewardRoot.activeSelf)
                    OnRewardPlainClicked();
                else
                    HandleSquareRewardPlain();
                return true;
            }

            if (awaitingReviveChoice || (reviveRoot != null && reviveRoot.activeSelf))
            {
                if (reviveRoot != null) reviveRoot.SetActive(false);
                pendingReviveYes = null;
                pendingReviveNo = null;
                HandleReviveNo();
                return true;
            }

            if (awaitingFinishChoice || (choiceRoot != null && choiceRoot.activeSelf))
            {
                if (choiceRoot != null) choiceRoot.SetActive(false);
                pendingChoiceContinue = null;
                pendingChoiceStop = null;
                if (autoPlayStopAtStack > 0 && lastFinishEventStack >= autoPlayStopAtStack)
                    HandleStopAfterFinish(lastFinishedIds);
                else
                    HandleContinueAfterFinish();
                return true;
            }

            if (confirmRoot != null && confirmRoot.activeSelf)
            {
                OnConfirmNoClicked();
                return true;
            }

            return false;
        }

        YutMatch.YutMoveCandidate PickAutoPlayCandidate(IReadOnlyList<YutMatch.YutMoveCandidate> candidates)
        {
            YutMatch.YutMoveCandidate best = candidates[0];
            int bestScore = int.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                int score = ScoreAutoPlayCandidate(candidates[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidates[i];
                }
            }
            return best;
        }

        int ScoreAutoPlayCandidate(YutMatch.YutMoveCandidate c)
        {
            int score = 0;
            var piece = match.PlayerPieces.FirstOrDefault(p => p.Id == c.PieceId);
            int stackHere = 0;
            if (piece != null && piece.OnBoard)
            {
                for (int i = 0; i < match.PlayerPieces.Count; i++)
                {
                    var p = match.PlayerPieces[i];
                    if (!p.Finished && p.NodeId == piece.NodeId) stackHere++;
                }
            }

            if (c.WillFinish)
            {
                score += 1000;
                score += stackHere * 50;
                if (autoPlayStopAtStack > 0 && stackHere >= autoPlayStopAtStack)
                    score += 500;
            }

            var ally = match.PlayerPieces.FirstOrDefault(
                p => !p.Finished && p.Id != c.PieceId && p.OnBoard && p.NodeId == c.DestinationNode);
            if (ally != null) score += 400;

            if (match.OpponentPiece.OnBoard && match.OpponentPiece.NodeId == c.DestinationNode)
                score += 300;

            if (c.UseShortcut) score += 80;
            if (YutBoardLayout.IsSpecialReward(c.DestinationNode)) score += 40;
            if (c.DestinationNode == YutBoardLayout.Start && !c.WillFinish) score += 60;

            return score;
        }

        bool EnsureMatchReadyForFinishSim(int stackCount)
        {
            if (match != null && match.IsEnded)
            {
                UnsubscribeMatchEvents();
                match = null;
            }

            if (match == null)
            {
                var team = new List<(string id, string name)>();
                if (teamById.Count > 0)
                {
                    foreach (var kv in teamById)
                    {
                        string name = kv.Value != null && kv.Value.Data != null && !string.IsNullOrEmpty(kv.Value.Data.displayName)
                            ? kv.Value.Data.displayName
                            : kv.Key;
                        team.Add((kv.Key, name));
                    }
                }
                else
                {
                    foreach (var a in CharacterAgent.All)
                    {
                        if (a == null || a.Stats == null || a.Stats.Stage != GrowthStage.Hon) continue;
                        string id = a.Data != null ? a.Data.id.ToString() : a.name;
                        string name = a.Data != null && !string.IsNullOrEmpty(a.Data.displayName) ? a.Data.displayName : a.name;
                        teamById[id] = a;
                        team.Add((id, name));
                    }
                }

                while (team.Count < stackCount)
                {
                    string id = $"qa_sim_{team.Count}";
                    team.Add((id, $"QA{team.Count + 1}"));
                }

                if (team.Count == 0)
                {
                    Debug.LogWarning("[Yut QA] 시뮬할 말이 없습니다.");
                    return false;
                }

                BeginMatch(team);
            }
            else
            {
                while (match.PlayerPieces.Count < stackCount)
                {
                    int i = match.PlayerPieces.Count;
                    match.TryAddPlayerPiece($"qa_sim_{i}", $"QA{i + 1}");
                }
            }

            return match != null && !match.IsEnded;
        }

        void ClearBlockingUiForFinishSim()
        {
            awaitingFinishChoice = false;
            awaitingSquareReward = false;
            awaitingReviveChoice = false;
            pendingBonusAfterContinue = false;
            pendingBonusAfterSquareReward = false;
            pendingOutcome = null;
            if (noticeRoot != null) noticeRoot.SetActive(false);
            if (choiceRoot != null) choiceRoot.SetActive(false);
            if (rewardRoot != null) rewardRoot.SetActive(false);
            if (reviveRoot != null) reviveRoot.SetActive(false);
            if (confirmRoot != null) confirmRoot.SetActive(false);
            if (miniGame != null) miniGame.ClearCandidates();
        }
#endif
    }
}
