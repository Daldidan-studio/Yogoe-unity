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

        GameObject root;
        YutMiniGame miniGame;
        YutMatch match;
        YutThrowOutcome? pendingOutcome;

        GameObject noticeRoot;
        Text noticeText;
        Action pendingNoticeAction;

        void Awake() => Instance = this;

        void Start()
        {
            EnsureBuilt();
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

            root.SetActive(true);
            miniGame.Show();
            miniGame.SetLeaveVisible(true);
            miniGame.SetThrowVisible(true);
            miniGame.SetTurnLabel("내 턴");
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

        void HandleThrowPressed()
        {
            if (match == null || match.IsEnded) return;
            var outcome = match.ThrowForPlayer();
            StartCoroutine(PlayerThrowRoutine(outcome));
        }

        IEnumerator PlayerThrowRoutine(YutThrowOutcome outcome)
        {
            miniGame.SetThrowVisible(false);
            yield return miniGame.PlayThrowAnim(outcome.Result);
            if (match == null || match.IsEnded) yield break;

            var candidates = match.GetPlayerCandidates(outcome.Result);
            if (candidates.Count == 0)
            {
                // 이동할 말이 없음(예: 대기 말뿐인데 빽도) — 턴을 그냥 넘긴다.
                yield return RunOpponentTurnRoutine();
                yield break;
            }

            pendingOutcome = outcome;
            var uiCandidates = candidates
                .Select(c => new YutMiniGame.YokaiMoveCandidate(c.PieceId, NameFor(c.PieceId), c.DestinationNode))
                .ToList();
            miniGame.FlashCandidates(uiCandidates);
        }

        void HandleCandidateTapped(string pieceId)
        {
            if (match == null || match.IsEnded || pendingOutcome == null) return;
            miniGame.ClearCandidates();
            var outcome = pendingOutcome.Value;
            pendingOutcome = null;

            bool bonusTurn = match.ApplyPlayerMove(pieceId, outcome);
            if (match.IsEnded) return; // HandleMatchEnded가 이미 결과 처리

            if (bonusTurn)
                miniGame.SetThrowVisible(true);
            else
                StartCoroutine(RunOpponentTurnRoutine());
        }

        /// <summary>
        /// 이무기 턴 전체(보너스 턴 포함)를 한 번씩 던지기 애니메이션까지 보여주며 진행한다.
        /// 플레이어 턴과 대칭으로 ThrowForOpponent/ApplyOpponentMove를 한 스텝씩 돌려서,
        /// 이무기도 실제로 던지는 모습이 보이고 지금 누구 턴인지 라벨로 알 수 있게 한다.
        /// </summary>
        IEnumerator RunOpponentTurnRoutine()
        {
            miniGame.SetThrowVisible(false);
            miniGame.SetTurnLabel("이무기 턴");
            yield return new WaitForSecondsRealtime(0.4f);

            bool bonus;
            int guard = 0;
            do
            {
                guard++;
                var outcome = match.ThrowForOpponent();
                yield return miniGame.PlayThrowAnim(outcome.Result);
                if (match == null || match.IsEnded) yield break;

                bonus = match.ApplyOpponentMove(outcome);
                if (match.IsEnded) yield break;

                if (bonus)
                    yield return new WaitForSecondsRealtime(0.4f);
            } while (bonus && guard < 20);

            miniGame.SetTurnLabel("내 턴");
            if (match != null && !match.IsEnded)
                miniGame.SetThrowVisible(true);
        }

        void HandleLeavePressed() => Close();

        void HandlePiecesChanged()
        {
            if (match == null) return;

            var infos = match.PlayerPieces
                .Where(p => !p.Finished)
                .Select(p => new YutMiniGame.YokaiPieceInfo(p.Id, p.DisplayName, Mathf.Max(0, p.NodeId)))
                .ToList();
            miniGame.ShowYokaiPieces(infos);

            var opp = match.OpponentPiece;
            miniGame.ShowOpponentPiece(opp.OnBoard);
            if (opp.OnBoard) miniGame.SetOpponentPieceIndex(opp.NodeId);
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
            if (root != null) return;
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
            miniGame.OnThrowPressed += HandleThrowPressed;
            miniGame.OnLeavePressed += HandleLeavePressed;
            miniGame.OnCandidateTapped += HandleCandidateTapped;
            miniGame.Hide();

            BuildNoticePanel(rootRt);

            root.SetActive(false);
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
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(OnNoticeOk);

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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
