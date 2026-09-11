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
    /// 윷놀이 진입점(11장). 윷 토큰 소모 → 옥토끼+현재 슬롯의 혼 전원 vs 이무기(말 1개) 대결 →
    /// 승리 시 향·엽전 지급까지의 최소 완결 루프. 화면/입력은 YutMiniGame, 규칙은 YutMatch가 담당.
    /// </summary>
    public class YutScreen : MonoBehaviour
    {
        public static YutScreen Instance { get; private set; }

        public Font font;

        /// <summary>승리(완주) 보상 — 기획서 11장 확정: 향 1개 + 엽전 1개.</summary>
        const int WinHyangReward = 1;
        const int WinYeopjeonReward = 1;

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

        /// <summary>특수 칸(도개걸윷모 밟았을 때 정화수·공양물·엽전 확정 수급) 보상 배율.</summary>
        const int SquareRewardBase = 1;
        const int SquareRewardAdMultiplier = 2;
        const float SquareRewardAdWatchSeconds = 0.8f; // BatchCollectPopup과 동일한 "광고 시청" 연출용 지연
        const float ReviveAdWatchSeconds = 0.8f;

        public bool HasPrefabShell => root != null && miniGame != null;

        YutMatch match;
        YutThrowOutcome? pendingOutcome;

        /// <summary>말 한 마리가 골인해서 "계속할지/그만할지" 다이얼로그가 떠 있는 동안, 방금 던진
        /// 결과가 보너스였는지 기억해뒀다가 '계속하기'를 고르면 그대로 이어서 써야 한다.</summary>
        bool awaitingFinishChoice;
        bool pendingBonusAfterContinue;

        /// <summary>특수 칸 보상 팝업("그냥 받기"/"광고 보고 2배")이 떠 있는 동안 다음 턴 진행을 멈춘다.</summary>
        bool awaitingSquareReward;
        bool pendingBonusAfterSquareReward;
        OfferingData pendingSquareOffering;

        /// <summary>"광고 보고 말 되살리기" 팝업이 떠 있는 동안 이무기 보너스 턴 진행을 멈춘다.</summary>
        bool awaitingReviveChoice;
        List<YutMatch.CapturedPieceSnapshot> pendingReviveSnapshots;

        /// <summary>이번 매치에서 특수 칸으로 모은 것들 — 동(東) 구역에 표시, 매치가 끝나면 요약
        /// 다이얼로그로도 보여준다. 새 매치 시작할 때 비운다(재시작 복원 시엔 다시 0부터).</summary>
        readonly Dictionary<OfferingData, int> matchOfferingCounts = new Dictionary<OfferingData, int>();
        int matchPurifiedWaterTotal;
        int matchYeopjeonTotal;

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
            SubscribeMatchEvents();

            awaitingFinishChoice = false;
            awaitingSquareReward = false;
            awaitingReviveChoice = false;
            matchOfferingCounts.Clear();
            matchPurifiedWaterTotal = 0;
            matchYeopjeonTotal = 0;
            root.SetActive(true);
            miniGame.Show();
            miniGame.SetLeaveVisible(true);
            miniGame.SetThrowVisible(true);
            miniGame.ShowLogLine("Imugi", "이무기 : 좋다, 한번 놀아보자꾸나.");
            miniGame.RefreshHearts(GameEconomy.Instance.YutToken);
            HandlePiecesChanged();
            HandleTurnTrackerChanged();
            RefreshCollectedItemsDisplay();
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
            HandleTurnTrackerChanged();
            RefreshCollectedItemsDisplay(); // 나갔다 왔거나 재시작 복원 — 이번 매치에서 모은 건 추적 안 해서 빈 채로 시작
        }

        void HandleTurnTrackerChanged()
        {
            if (match == null) return;
            miniGame.ShowTurnTracker(match.PlayerTurnNumber, match.CurrentTurnResults);
        }

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
            match.OnTurnTrackerChanged += HandleTurnTrackerChanged;
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
            match.OnTurnTrackerChanged -= HandleTurnTrackerChanged;
        }

        /// <summary>
        /// "나가기"를 눌러도 승패가 안 난 매치는 메모리에 그대로 둔다 — 다시 열면 이어서 하고,
        /// 세이브에도 매번 담겨서 앱을 껐다 켜도 이어진다. 승패가 이미 난 매치만 완전히 정리한다.
        /// </summary>
        public void Close()
        {
            if (match != null && match.IsEnded)
            {
                UnsubscribeMatchEvents();
                match = null;
            }
            // 팝업이 떠 있던 채로 나가면 그 선택은 그냥 흘려보낸다(다음에 열면 던지기 대기 상태로).
            pendingOutcome = null;
            awaitingFinishChoice = false;
            pendingBonusAfterContinue = false;
            awaitingSquareReward = false;
            pendingBonusAfterSquareReward = false;
            pendingSquareOffering = null;
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

        /// <summary>세이브용 스냅샷 — 진행 중(승패 안 난) 매치가 없으면 null.</summary>
        public YutMatchSave CaptureForSave()
        {
            if (match == null || match.IsEnded) return null;
            return new YutMatchSave
            {
                playerPieces = match.PlayerPieces.Select(ToPieceSave).ToArray(),
                opponentPiece = ToPieceSave(match.OpponentPiece)
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

            SubscribeMatchEvents();
            awaitingFinishChoice = false;
            awaitingSquareReward = false;
            awaitingReviveChoice = false;
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

        /// <summary>이무기한테 내 말이 잡혔을 때 게임로그 대사. 잡힌 말 자기 이름으로 반응한다.</summary>
        void HandlePlayerPiecesCaptured(IReadOnlyList<YutPiece> captured)
        {
            foreach (var p in captured)
                miniGame.ShowLogLine(p.Id, $"{p.DisplayName} : 으악, 잡혀버렸어요!! 이무기 님 한번 더...!");
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
                miniGame.ShowLogLine(pendingReviveSnapshots[0].PieceId, $"{names} 되살아났어요!");
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

        /// <summary>내가 이무기를 잡았을 때 게임로그 대사.</summary>
        void HandleOpponentCaptured() =>
            miniGame.ShowLogLine("Imugi", "이무기 : 크윽...! 방심했다, 한 번 더 던지거라!");

        /// <summary>power(0~1)는 슬라이드 속도 기반 — 던지는 연출에만 쓰고 결과 확률엔 영향 없다.</summary>
        void HandleThrowPressed(float power)
        {
            if (match == null || match.IsEnded) return;
            var outcome = match.ThrowForPlayer();
            StartCoroutine(PlayerThrowRoutine(outcome, power));
        }

        IEnumerator PlayerThrowRoutine(YutThrowOutcome outcome, float power)
        {
            miniGame.SetThrowVisible(false);
            yield return miniGame.PlayThrowAnim(outcome.Result, power);
            if (match == null || match.IsEnded) yield break;
            miniGame.ShowLogLine("Rabbit", $"옥토끼 : {outcome.Result.DisplayName()}.");

            var candidates = match.GetPlayerCandidates(outcome.Result);
            if (candidates.Count == 0)
            {
                // 이동할 말이 없음(예: 대기 말뿐인데 빽도) — 턴을 그냥 넘긴다.
                yield return RunOpponentTurnRoutine();
                yield break;
            }

            pendingOutcome = outcome;
            var uiCandidates = candidates
                .Select(c => new YutMiniGame.YokaiMoveCandidate(c.PieceId, NameFor(c.PieceId), c.DestinationNode, c.UseShortcut))
                .ToList();
            miniGame.FlashCandidates(uiCandidates);
        }

        void HandleCandidateTapped(string pieceId, bool useShortcut)
        {
            if (match == null || match.IsEnded || pendingOutcome == null) return;
            miniGame.ClearCandidates();
            var outcome = pendingOutcome.Value;
            pendingOutcome = null;

            bool bonusTurn = match.ApplyPlayerMove(pieceId, useShortcut, outcome);
            if (match.IsEnded) return; // HandleMatchEnded가 이미 결과 처리

            if (awaitingSquareReward)
            {
                // 특수 칸 보상 팝업("그냥 받기"/"광고 보고 2배")이 이미 떴다 — 그 선택이 끝나야 다음이 진행된다.
                pendingBonusAfterSquareReward = bonusTurn;
                return;
            }

            if (awaitingFinishChoice)
            {
                // 골인 다이얼로그("계속하기"/"그만하기")가 이미 떴다 — 그 선택이 끝나야 다음이 진행된다.
                pendingBonusAfterContinue = bonusTurn;
                return;
            }

            if (bonusTurn)
                miniGame.SetThrowVisible(true);
            else
                StartCoroutine(RunOpponentTurnRoutine());
        }

        /// <summary>
        /// 말이 특수 칸(YutBoardLayout.IsSpecialReward)에 도착했을 때 — 공양물 하나를 무작위로
        /// 골라 정화수·엽전과 함께 "그냥 받기(1개씩 확정)"/"광고 보고 2배" 팝업을 띄운다. 완주와
        /// 달리 매치를 막지 않고, 선택 즉시 재화를 지급한다(칸에서 얻은 건 패배해도 유지).
        /// </summary>
        void HandleSpecialSquareReached(IReadOnlyList<string> pieceIds)
        {
            var settings = StartingStateSettings.Get();
            var pool = settings.startingOfferings?
                .Where(o => o != null && o.kind != OfferingKind.PurifiedWater)
                .ToList();
            pendingSquareOffering = pool != null && pool.Count > 0
                ? pool[UnityEngine.Random.Range(0, pool.Count)]
                : null;

            awaitingSquareReward = true;
            miniGame.SetThrowVisible(false);
            string offeringName = pendingSquareOffering != null ? pendingSquareOffering.displayName : "공양물";
            ShowRewardChoice($"특수 칸 발견!\n{offeringName} · 정화수 · 엽전을 얻을 수 있어요.",
                onPlain: HandleSquareRewardPlain,
                onAd: HandleSquareRewardAd);
        }

        void HandleSquareRewardPlain()
        {
            GrantSquareReward(SquareRewardBase);
            ResumeAfterSquareReward();
        }

        void HandleSquareRewardAd() => StartCoroutine(SquareRewardAdRoutine());

        IEnumerator SquareRewardAdRoutine()
        {
            // BatchCollectPopup과 같은 패턴 — 실제 광고 SDK가 붙기 전까지 짧은 지연으로 "시청 중"을 흉내낸다.
            yield return new WaitForSecondsRealtime(SquareRewardAdWatchSeconds);
            GrantSquareReward(SquareRewardAdMultiplier);
            ResumeAfterSquareReward();
        }

        void GrantSquareReward(int multiplier)
        {
            int purifiedAmount = SquareRewardBase * multiplier;
            int yeopjeonAmount = SquareRewardBase * multiplier;
            GameEconomy.Instance.AddPurifiedWater(purifiedAmount);
            GameEconomy.Instance.AddYeopjeon(yeopjeonAmount);
            matchPurifiedWaterTotal += purifiedAmount;
            matchYeopjeonTotal += yeopjeonAmount;

            if (pendingSquareOffering != null)
            {
                int offeringAmount = SquareRewardBase * multiplier;
                GameEconomy.Instance.AddOffering(pendingSquareOffering, offeringAmount);
                matchOfferingCounts.TryGetValue(pendingSquareOffering, out int cur);
                matchOfferingCounts[pendingSquareOffering] = cur + offeringAmount;
            }
            pendingSquareOffering = null;

            RefreshCollectedItemsDisplay();
            GameSaveBridge.SaveFromWorld();
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
            if (matchPurifiedWaterTotal > 0)
            {
                var water = CharacterCatalog.FindOffering("purifiedwater");
                items.Add(new YutMiniGame.CollectedItemView
                {
                    Icon = water != null ? water.icon : null,
                    Count = matchPurifiedWaterTotal,
                    Label = "정화수",
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
            if (matchPurifiedWaterTotal > 0) parts.Add($"정화수 {matchPurifiedWaterTotal}개");
            if (matchYeopjeonTotal > 0) parts.Add($"엽전 {matchYeopjeonTotal}개");
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
            awaitingFinishChoice = true;
            miniGame.SetThrowVisible(false);
            string names = string.Join(", ", finishedIds.Select(NameFor));
            ShowChoice($"{names} 골인!\n여기서 그만 받을까요, 남은 말로 계속할까요?",
                onContinue: HandleContinueAfterFinish,
                onStop: HandleStopAfterFinish);
        }

        void HandleContinueAfterFinish()
        {
            awaitingFinishChoice = false;
            if (pendingBonusAfterContinue)
                miniGame.SetThrowVisible(true);
            else
                StartCoroutine(RunOpponentTurnRoutine());
        }

        void HandleStopAfterFinish()
        {
            awaitingFinishChoice = false;
            match?.EndAsPlayerWin();
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
            yield return new WaitForSecondsRealtime(0.4f);

            bool bonus;
            int guard = 0;
            do
            {
                guard++;
                var outcome = match.ThrowForOpponent();
                yield return miniGame.PlayOpponentMiniThrowAnim(outcome.Result);
                if (match == null || match.IsEnded) yield break;
                miniGame.ShowLogLine("Imugi", $"이무기 : {outcome.Result.DisplayName()}.");

                bonus = match.ApplyOpponentMove(outcome);
                if (match.IsEnded) yield break;

                // 말이 잡혔으면 "광고 보고 되살리기" 팝업이 뜬다 — 선택이 끝날 때까지 다음 던지기를 멈춘다.
                if (awaitingReviveChoice)
                    yield return new WaitUntil(() => !awaitingReviveChoice);
                if (match == null || match.IsEnded) yield break;

                if (bonus)
                    yield return new WaitForSecondsRealtime(0.4f);
            } while (bonus && guard < 20);

            if (match != null && !match.IsEnded)
            {
                match.StartNewPlayerTurn();
                miniGame.SetThrowVisible(true);
            }
        }

        void HandleLeavePressed() => Close();

        void HandlePiecesChanged()
        {
            if (match == null) return;

            var infos = match.PlayerPieces
                .Where(p => !p.Finished)
                .Select(p => new YutMiniGame.YokaiPieceInfo(p.Id, p.DisplayName, p.NodeId))
                .ToList();
            miniGame.ShowYokaiPieces(infos);

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
                }
            }
            miniGame.ShowRoster(roster, showExtraSlot, extraLabel, extraAction);
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
            var sprite = miniGame != null ? miniGame.GetGoraniSprite() : null;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.45f, 0.85f, 1f, 1f); // 고라니 넋 플레이스홀더 색과 맞춤
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

        void HandleMatchEnded(bool playerWon)
        {
            miniGame.SetThrowVisible(false);
            miniGame.ClearCandidates();

            string message = playerWon
                ? $"승리! 향 {WinHyangReward}개 + 엽전 {WinYeopjeonReward}개 획득"
                : "패배했습니다.";
            if (playerWon)
            {
                GameEconomy.Instance.AddHyang(WinHyangReward);
                GameEconomy.Instance.AddYeopjeon(WinYeopjeonReward);
            }

            string collected = BuildCollectedItemsSummary();
            if (!string.IsNullOrEmpty(collected))
                message += $"\n{collected}";

            ShowNotice(message, Close);
            GameSaveBridge.SaveFromWorld();
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
            miniGame.OnThrowPressed += HandleThrowPressed;
            miniGame.OnLeavePressed += HandleLeavePressed;
            miniGame.OnCandidateTapped += HandleCandidateTapped;

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
            var action = pendingNoticeAction;
            pendingNoticeAction = null;
            action?.Invoke();
            // 매치 시작 전 안내(토큰 부족 등)였다면 화면 자체를 다시 닫는다.
            if (match == null && root != null) root.SetActive(false);
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
    }
}
