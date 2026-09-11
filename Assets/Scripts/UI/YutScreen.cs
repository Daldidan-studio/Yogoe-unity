using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
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

        /// <summary>특수 칸(도개걸윷모 밟았을 때 정화수·공양물·엽전 확정 수급) 보상 배율.</summary>
        const int SquareRewardBase = 1;
        const int SquareRewardAdMultiplier = 2;
        const float SquareRewardAdWatchSeconds = 0.8f; // BatchCollectPopup과 동일한 "광고 시청" 연출용 지연

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

        Action pendingNoticeAction;
        Action pendingChoiceContinue;
        Action pendingChoiceStop;
        Action pendingRewardPlain;
        Action pendingRewardAd;

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
            match.OnPiecesChanged += HandlePiecesChanged;
            match.OnMatchEnded += HandleMatchEnded;
            match.OnPlayerPiecesMoved += HandlePlayerPiecesMoved;
            match.OnPlayerPiecesCaptured += HandlePlayerPiecesCaptured;
            match.OnOpponentCaptured += HandleOpponentCaptured;
            match.OnPlayerPieceFinished += HandlePlayerPieceFinished;
            match.OnSpecialSquareReached += HandleSpecialSquareReached;

            awaitingFinishChoice = false;
            awaitingSquareReward = false;
            root.SetActive(true);
            miniGame.Show();
            miniGame.SetLeaveVisible(true);
            miniGame.SetThrowVisible(true);
            miniGame.ShowLogLine("Imugi", "이무기 : 좋다, 한번 놀아보자꾸나.");
            miniGame.RefreshHearts(GameEconomy.Instance.YutToken);
            HandlePiecesChanged();
            GameSaveBridge.SaveFromWorld();
        }

        public void Close()
        {
            if (match != null)
            {
                match.OnPiecesChanged -= HandlePiecesChanged;
                match.OnMatchEnded -= HandleMatchEnded;
                match.OnPlayerPiecesMoved -= HandlePlayerPiecesMoved;
                match.OnPlayerPiecesCaptured -= HandlePlayerPiecesCaptured;
                match.OnOpponentCaptured -= HandleOpponentCaptured;
                match.OnPlayerPieceFinished -= HandlePlayerPieceFinished;
                match.OnSpecialSquareReached -= HandleSpecialSquareReached;
                match = null;
            }
            pendingOutcome = null;
            if (miniGame != null) miniGame.Hide();
            if (root != null) root.SetActive(false);
            GameSaveBridge.SaveFromWorld();
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
            GameEconomy.Instance.AddPurifiedWater(SquareRewardBase * multiplier);
            GameEconomy.Instance.AddYeopjeon(SquareRewardBase * multiplier);
            if (pendingSquareOffering != null)
                GameEconomy.Instance.AddOffering(pendingSquareOffering, SquareRewardBase * multiplier);
            pendingSquareOffering = null;
            GameSaveBridge.SaveFromWorld();
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

                if (bonus)
                    yield return new WaitForSecondsRealtime(0.4f);
            } while (bonus && guard < 20);

            if (match != null && !match.IsEnded)
                miniGame.SetThrowVisible(true);
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

            var roster = match.PlayerPieces
                .Select(p =>
                {
                    teamById.TryGetValue(p.Id, out var agent);
                    int stamina = agent != null && agent.Stats != null ? Mathf.RoundToInt(agent.Stats.Stamina) : 0;
                    int intimacy = agent != null && agent.Stats != null ? Mathf.RoundToInt(agent.Stats.Intimacy) : 0;
                    return new YutMiniGame.RosterEntry(p.Id, p.DisplayName, stamina, intimacy, PositionLabelFor(p));
                })
                .ToList();
            miniGame.ShowRoster(roster);
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

            if (playerWon)
            {
                GameEconomy.Instance.AddHyang(WinHyangReward);
                GameEconomy.Instance.AddYeopjeon(WinYeopjeonReward);
                ShowNotice($"승리! 향 {WinHyangReward}개 + 엽전 {WinYeopjeonReward}개 획득", Close);
            }
            else
            {
                ShowNotice("패배했습니다.", Close);
            }
            GameSaveBridge.SaveFromWorld();
        }

        string NameFor(string pieceId) =>
            match?.PlayerPieces.FirstOrDefault(p => p.Id == pieceId)?.DisplayName ?? "?";

        void EnsureBuilt()
        {
            if (HasPrefabShell) return;
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
            BuildRewardPanel(rootRt);

            root.SetActive(false);
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
            boxRt.sizeDelta = new Vector2(600, 320);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 30);
            textRt.sizeDelta = new Vector2(520, 160);
            noticeText = textGO.AddComponent<Text>();
            noticeText.font = font;
            noticeText.fontSize = UiFonts.Size(30);
            noticeText.alignment = TextAnchor.MiddleCenter;
            noticeText.color = new Color(1f, 0.95f, 0.85f);
            noticeText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var btnGO = new GameObject("Btn_Ok", typeof(RectTransform));
            var btnRt = (RectTransform)btnGO.transform;
            btnRt.SetParent(boxRt, false);
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0, -110);
            btnRt.sizeDelta = new Vector2(220, 70);
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
            label.fontSize = UiFonts.Size(28);
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
            boxRt.sizeDelta = new Vector2(660, 380);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 70);
            textRt.sizeDelta = new Vector2(580, 200);
            choiceText = textGO.AddComponent<Text>();
            choiceText.font = font;
            choiceText.fontSize = UiFonts.Size(28);
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
            btnRt.sizeDelta = new Vector2(290, 100);
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
            labelText.fontSize = UiFonts.Size(24);
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
            boxRt.sizeDelta = new Vector2(660, 380);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGO.transform;
            textRt.SetParent(boxRt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, 70);
            textRt.sizeDelta = new Vector2(580, 200);
            rewardText = textGO.AddComponent<Text>();
            rewardText.font = font;
            rewardText.fontSize = UiFonts.Size(28);
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
