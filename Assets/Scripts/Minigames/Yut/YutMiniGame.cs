using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.UI;

namespace Yoegoe.Minigames.Yut
{
    /// <summary>
    /// 윷놀이 화면(전체화면 TrainingPanel)을 소유하는 독립 모듈.
    /// TrainingPanel GameObject에 런타임에 자동 부착된다 (ScrollScreenUI.EnsureWired 참고).
    /// 결과 판정·재화 소모 같은 게임 로직은 호출부(TutorialController, 추후 본게임 컨트롤러)의
    /// 책임이고, 이 컴포넌트는 화면 표시와 입력 이벤트만 담당한다.
    /// </summary>
    public class YutMiniGame : MonoBehaviour
    {
        /// <summary>
        /// 한글 표시용 폰트. 비워두면 유니티 기본 폰트로 나오는데, WebGL에선 한글 글리프가 없어서
        /// 글씨가 아예 안 보인다 — 호출부(YutScreen)가 프로젝트 한글 폰트(DOSGothic 등)를 넣어준다.
        /// </summary>
        public Font font;

        public event Action OnThrowPressed;
        public event Action OnLeavePressed;
        /// <summary>족보 안내 오버레이가 열리고/닫힐 때. ScrollScreenUI가 이걸로 대사 타이핑을 같이 멈춘다.</summary>
        public event Action<bool> OnRulesPanelToggled;
        /// <summary>족보 안내를 유저가 닫기 버튼으로 직접 닫았을 때.</summary>
        public event Action OnRulesClosed;
        /// <summary>FlashCandidates로 띄운 선택 다이얼로그에서 후보 하나를 유저가 탭했을 때 — 그 요괴 id.</summary>
        public event Action<string> OnCandidateTapped;

        Image[] _heartIcons;
        Button _throwButton;
        Button _leaveButton;
        RectTransform _boardRoot;
        RectTransform _piece;
        RectTransform _opponentPiece;
        Image[] _pads;
        RectTransform[] _parkedSticks;
        RectTransform[] _quadrants; // YutBoardQuadrant 순서대로
        GameObject _rulesOverlay;
        GameObject _candidateDialog;
        Transform _candidateDialogList;

        // 본게임 수련장 전용 — 보유 요괴 전체를 동시에 말로 표시(id → 말 오브젝트/이니셜 라벨).
        // 튜토리얼의 _piece/_opponentPiece(각본 대결용)와는 완전히 별개.
        readonly Dictionary<string, RectTransform> _yokaiPieces = new();
        readonly Dictionary<string, Text> _yokaiPieceLabels = new();

        static readonly Color YutStickFront = new(0.92f, 0.88f, 0.78f);
        static readonly Color YutStickBack = new(0.35f, 0.3f, 0.26f);
        static readonly Color BaekdoMarkColor = new(0.85f, 0.25f, 0.3f);

        static Sprite _stickFront;
        static Sprite _stickFrontBaekdo;
        static Sprite _stickBack;

        static void EnsureStickSprites()
        {
            if (_stickFront == null)
                _stickFront = Resources.Load<Sprite>("UI/YutPieces/YutStick_Front");
            if (_stickFrontBaekdo == null)
                _stickFrontBaekdo = Resources.Load<Sprite>("UI/YutPieces/YutStick_FrontBaekdo");
            if (_stickBack == null)
                _stickBack = Resources.Load<Sprite>("UI/YutPieces/YutStick_Back");
        }

        static void ApplyStickFace(Image img, bool front, bool isBaekdoStick)
        {
            EnsureStickSprites();
            Sprite sprite = null;
            if (front)
                sprite = isBaekdoStick && _stickFrontBaekdo != null ? _stickFrontBaekdo : _stickFront;
            else
                sprite = _stickBack;

            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.sprite = null;
                img.color = front ? YutStickFront : YutStickBack;
            }

            // 자식 빽도 점(에셋 폴백용)은 앞면일 때만
            var mark = img.transform.Find("BaekdoMark");
            if (mark != null) mark.gameObject.SetActive(front);
        }
        static readonly Color HeartOn = new(0.95f, 0.25f, 0.35f);
        static readonly Color HeartOff = new(0.3f, 0.15f, 0.18f, 0.6f);

        public void BindFromHierarchy()
        {
            _throwButton = transform.Find("Throw")?.GetComponent<Button>();
            if (_throwButton == null)
            {
                _throwButton = CreateButton(transform, "Throw", "윷 던지기", null);
                SetAnchor(_throwButton.GetComponent<RectTransform>(), 0.32f, 0.04f, 0.68f, 0.14f, 0, 0, 0, 0);
            }

            _leaveButton = transform.Find("Leave")?.GetComponent<Button>();
            if (_leaveButton == null)
            {
                _leaveButton = CreateButton(transform, "Leave", "나가기", null);
                SetAnchor(_leaveButton.GetComponent<RectTransform>(), 0.02f, 0.93f, 0.18f, 0.99f, 0, 0, 0, 0);
            }

            WireButton(_throwButton, () => OnThrowPressed?.Invoke());
            WireButton(_leaveButton, () => OnLeavePressed?.Invoke());
        }

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureBoard();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ClearParkedSticks();
            ClearCandidates();
        }

        public void SetThrowVisible(bool on)
        {
            if (_throwButton == null) return;
            _throwButton.gameObject.SetActive(on);
            if (on) EnsureButtonLabel(_throwButton, "윷 던지기");
        }

        public void SetLeaveVisible(bool on)
        {
            if (_leaveButton == null) return;
            _leaveButton.gameObject.SetActive(on);
            if (on) EnsureButtonLabel(_leaveButton, "나가기");
        }

        public void RefreshHearts(int hearts)
        {
            if (_heartIcons == null) return;
            for (int i = 0; i < _heartIcons.Length; i++)
                _heartIcons[i].color = i < hearts ? HeartOn : HeartOff;
        }

        /// <summary>상대(이무기 등) 말 표시를 켜고 끈다. 켜기 전까지는 판 위에 안 보인다.</summary>
        public void ShowOpponentPiece(bool on)
        {
            EnsureBoard();
            if (_opponentPiece != null) _opponentPiece.gameObject.SetActive(on);
        }

        public void SetOpponentPieceIndex(int nodeId)
        {
            EnsureBoard();
            if (_pads == null || _pads.Length == 0 || _opponentPiece == null) return;

            nodeId = Mathf.Clamp(nodeId, 0, _pads.Length - 1);
            var pad = _pads[nodeId].rectTransform;
            _opponentPiece.SetParent(pad, false);
            _opponentPiece.anchorMin = new Vector2(0.15f, 0.15f);
            _opponentPiece.anchorMax = new Vector2(0.85f, 0.85f);
            _opponentPiece.offsetMin = Vector2.zero;
            _opponentPiece.offsetMax = Vector2.zero;
        }

        /// <summary>특정 칸을 잠깐 밝게 강조(다음 이동 위치 예고 등). 다음 SetPieceIndex 호출 때 정상 복구된다.</summary>
        public void FlashNode(int nodeId)
        {
            EnsureBoard();
            if (_pads == null || nodeId < 0 || nodeId >= _pads.Length) return;
            _pads[nodeId].color = new Color(1f, 0.95f, 0.4f, 1f);
        }

        public readonly struct YokaiPieceInfo
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly int NodeId;

            public YokaiPieceInfo(string id, string displayName, int nodeId)
            {
                Id = id;
                DisplayName = displayName;
                NodeId = nodeId;
            }
        }

        /// <summary>
        /// 본게임 수련장 전용 — 보유 요괴 전체를 각자 위치에 동시에 말로 표시한다.
        /// 목록에 없는 요괴의 말은 정리된다.
        /// 말 아이콘: Resources/UI/YutPieces/{id} (없으면 색+이니셜 폴백).
        /// </summary>
        public void ShowYokaiPieces(IReadOnlyList<YokaiPieceInfo> pieces)
        {
            EnsureBoard();
            if (_pads == null || _pads.Length == 0 || pieces == null) return;

            var keep = new HashSet<string>();
            foreach (var info in pieces) keep.Add(info.Id);
            var stale = new List<string>();
            foreach (var kv in _yokaiPieces)
                if (!keep.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var id in stale)
            {
                if (_yokaiPieces.TryGetValue(id, out var rt) && rt != null) Destroy(rt.gameObject);
                _yokaiPieces.Remove(id);
                _yokaiPieceLabels.Remove(id);
            }

            foreach (var info in pieces)
            {
                if (!_yokaiPieces.TryGetValue(info.Id, out var piece) || piece == null)
                {
                    piece = BuildYokaiPieceVisual(info.Id, info.DisplayName, out var label);
                    _yokaiPieces[info.Id] = piece;
                    _yokaiPieceLabels[info.Id] = label;
                }
                PlaceOnNode(piece, info.NodeId);
            }
        }

        RectTransform BuildYokaiPieceVisual(string id, string displayName, out Text label)
        {
            var go = new GameObject($"YokaiPiece_{id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_pads[0].transform, false);
            var img = go.GetComponent<Image>();
            var sprite = PieceSpriteFor(id);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
                label = null;
            }
            else
            {
                img.color = ColorForYokai(id);
                label = CreateText(go.transform, "Label", InitialOf(displayName), 20, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.raycastTarget = false;
            }

            return go.GetComponent<RectTransform>();
        }

        void PlaceOnNode(RectTransform piece, int nodeId)
        {
            nodeId = Mathf.Clamp(nodeId, 0, _pads.Length - 1);
            piece.SetParent(_pads[nodeId].transform, false);
            piece.anchorMin = new Vector2(0.18f, 0.18f);
            piece.anchorMax = new Vector2(0.82f, 0.82f);
            piece.offsetMin = Vector2.zero;
            piece.offsetMax = Vector2.zero;
        }

        static string InitialOf(string displayName) =>
            string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1);

        static readonly Dictionary<string, Sprite> PieceSpriteCache = new Dictionary<string, Sprite>();

        /// <summary>Resources/UI/YutPieces/{id} — Rabbit/SamjokO/Gumiho/Gorani/Imugi.</summary>
        static Sprite PieceSpriteFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (PieceSpriteCache.TryGetValue(id, out var cached) && cached != null) return cached;
            var sprite = Resources.Load<Sprite>("UI/YutPieces/" + id);
            if (sprite != null) PieceSpriteCache[id] = sprite;
            return sprite;
        }

        static Color ColorForYokai(string id)
        {
            int hash = 0;
            if (!string.IsNullOrEmpty(id))
                foreach (char c in id) hash = hash * 31 + c;
            float hue = (Mathf.Abs(hash) % 360) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.9f);
        }

        public readonly struct YokaiMoveCandidate
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly int DestinationNode;

            public YokaiMoveCandidate(string id, string displayName, int destinationNode)
            {
                Id = id;
                DisplayName = displayName;
                DestinationNode = destinationNode;
            }
        }

        /// <summary>
        /// 던진 결과로 움직일 수 있는 말 후보를 모달 다이얼로그로 띄워서 유저가 직접 고르게 한다.
        /// 예전엔 보드 칸 위에 작은 마커로 표시했는데(같은 칸으로 가는 후보가 여럿이면 칸 하나에
        /// 몰아넣고 자동으로 라벨만 순환) 탭 대상이 뭔지 불명확해서 "누굴 골랐는지도 모르게 여러
        /// 마리가 같이 움직인다"는 혼란이 있었다 — 다이얼로그에 이름 있는 버튼으로 명확히 고르게 함.
        /// </summary>
        public void FlashCandidates(IReadOnlyList<YokaiMoveCandidate> candidates)
        {
            ClearCandidates();
            EnsureBoard();
            if (_candidateDialogList == null || candidates == null || candidates.Count == 0) return;

            int count = candidates.Count;
            for (int i = 0; i < count; i++)
            {
                var candidate = candidates[i];
                float yTop = 1f - (float)i / count;
                float yBottom = 1f - (float)(i + 1) / count;

                var rowGo = new GameObject($"Row_{candidate.Id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rowGo.transform.SetParent(_candidateDialogList, false);
                var rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, yBottom);
                rowRt.anchorMax = new Vector2(1f, yTop);
                rowRt.offsetMin = new Vector2(0, 5);
                rowRt.offsetMax = new Vector2(0, -5);
                var rowImg = rowGo.GetComponent<Image>();
                rowImg.color = new Color(0.25f, 0.22f, 0.18f, 0.95f);

                // 클로저가 반복문 변수를 그대로 캡처하지 않도록 지역 복사본을 만든다.
                string tappedId = candidate.Id;
                var btn = rowGo.AddComponent<Button>();
                btn.targetGraphic = rowImg;
                btn.onClick.AddListener(() => OnCandidateTapped?.Invoke(tappedId));

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(rowGo.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.03f, 0.1f);
                iconRt.anchorMax = new Vector2(0.22f, 0.9f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.raycastTarget = false;
                var pieceSprite = PieceSpriteFor(candidate.Id);
                if (pieceSprite != null)
                {
                    iconImg.sprite = pieceSprite;
                    iconImg.color = Color.white;
                    iconImg.preserveAspect = true;
                }
                else
                {
                    iconImg.color = ColorForYokai(candidate.Id);
                }

                var label = CreateText(rowGo.transform, "Label", candidate.DisplayName, 26, TextAnchor.MiddleLeft);
                label.rectTransform.anchorMin = new Vector2(0.28f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.95f, 1f);
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.raycastTarget = false;
            }

            _candidateDialog.SetActive(true);
        }

        /// <summary>FlashCandidates로 띄운 선택 다이얼로그를 닫고 후보 버튼을 전부 지운다.</summary>
        public void ClearCandidates()
        {
            if (_candidateDialog != null) _candidateDialog.SetActive(false);
            if (_candidateDialogList == null) return;
            for (int i = _candidateDialogList.childCount - 1; i >= 0; i--)
                Destroy(_candidateDialogList.GetChild(i).gameObject);
        }

        void EnsureCandidateDialog()
        {
            if (_candidateDialog != null) return;

            _candidateDialog = new GameObject("CandidateDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _candidateDialog.transform.SetParent(transform, false);
            SetAnchor((RectTransform)_candidateDialog.transform, 0.16f, 0.24f, 0.84f, 0.7f, 0, 0, 0, 0);
            _candidateDialog.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

            var title = CreateText(_candidateDialog.transform, "Title", "이동할 말을 골라주세요", 24, TextAnchor.MiddleCenter);
            SetAnchor(title.rectTransform, 0.05f, 0.86f, 0.95f, 0.98f, 0, 0, 0, 0);

            var listGo = new GameObject("List", typeof(RectTransform));
            listGo.transform.SetParent(_candidateDialog.transform, false);
            SetAnchor(listGo.GetComponent<RectTransform>(), 0.06f, 0.04f, 0.94f, 0.82f, 0, 0, 0, 0);
            _candidateDialogList = listGo.transform;

            _candidateDialog.SetActive(false);
        }

        /// <summary>
        /// 족보(빽도~모 6종) 안내 오버레이. 딱 한 번 보여주고, 유저가 닫기 버튼을 눌러야 닫힌다
        /// (자동으로 안 없어짐). 열려있는 동안엔 OnRulesPanelToggled(true)로 대사 타이핑도
        /// 같이 멈춰서, 안내 보는 동안 다른 진행이 몰래 같이 흐르지 않게 한다.
        /// 닫히는 순간 OnRulesClosed를 쏴서, 호출부가 "닫을 때까지 대기"를 걸 수 있다.
        /// </summary>
        public void ShowRulesOverlay(bool on)
        {
            EnsureBoard();
            if (_rulesOverlay == null) return;

            bool wasOpen = _rulesOverlay.activeSelf;
            _rulesOverlay.SetActive(on);
            OnRulesPanelToggled?.Invoke(on);

            if (wasOpen && !on)
                OnRulesClosed?.Invoke();
        }

        static bool IsWaypoint(int nodeId) =>
            nodeId == YutBoardLayout.Start || nodeId == YutBoardLayout.Mo ||
            nodeId == YutBoardLayout.DwitMo || nodeId == YutBoardLayout.JjiMo ||
            nodeId == YutBoardLayout.Bang;

        public void SetPieceIndex(int nodeId)
        {
            EnsureBoard();
            if (_pads == null || _pads.Length == 0 || _piece == null) return;

            nodeId = Mathf.Clamp(nodeId, 0, _pads.Length - 1);
            _piece.gameObject.SetActive(true);
            for (int i = 0; i < _pads.Length; i++)
            {
                bool here = i == nodeId;
                _pads[i].color = here
                    ? new Color(1f, 0.85f, 0.35f, 1f)
                    : IsWaypoint(i)
                        ? new Color(0.7f, 0.55f, 0.3f, 0.85f)
                        : new Color(0.35f, 0.32f, 0.28f, 0.9f);
            }

            var pad = _pads[nodeId].rectTransform;
            _piece.SetParent(pad, false);
            _piece.anchorMin = new Vector2(0.15f, 0.15f);
            _piece.anchorMax = new Vector2(0.85f, 0.85f);
            _piece.offsetMin = Vector2.zero;
            _piece.offsetMax = Vector2.zero;
        }

        void EnsureBoard()
        {
            if (_boardRoot != null) return;

            var existing = transform.Find("YutBoard");
            if (existing != null)
            {
                _boardRoot = existing as RectTransform;
            }
            else
            {
                var boardGo = new GameObject("YutBoard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                boardGo.transform.SetParent(transform, false);
                _boardRoot = boardGo.GetComponent<RectTransform>();
                SetAnchor(_boardRoot, 0.1f, 0.2f, 0.9f, 0.65f, 0, 0, 0, 0);
                boardGo.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.18f, 0.92f);
            }

            if (_heartIcons == null)
            {
                _heartIcons = new Image[5 /* 구 KSpirits.Core.GameConstants.HeartMax, 새 설계에 맞게 나중에 재조정 */];
                const float iconW = 0.035f;
                const float gap = 0.008f;
                float totalW = _heartIcons.Length * iconW + (_heartIcons.Length - 1) * gap;
                float startX = 0.95f - totalW;
                for (int i = 0; i < _heartIcons.Length; i++)
                {
                    var heartGo = new GameObject($"Heart{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    heartGo.transform.SetParent(transform, false);
                    float x = startX + i * (iconW + gap);
                    SetAnchor(heartGo.GetComponent<RectTransform>(), x, 0.9f, x + iconW, 0.97f, 0, 0, 0, 0);
                    _heartIcons[i] = heartGo.GetComponent<Image>();
                }
            }

            _pads = new Image[YutBoardLayout.NodeCount];
            for (int i = 0; i < YutBoardLayout.NodeCount; i++)
            {
                var pos = YutBoardLayout.Normalized(i);
                float half = IsWaypoint(i) ? 0.05f : 0.032f;
                var padGo = new GameObject($"Node{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                padGo.transform.SetParent(_boardRoot, false);
                var padRt = padGo.GetComponent<RectTransform>();
                SetAnchor(padRt, pos.x - half, pos.y - half, pos.x + half, pos.y + half, 0, 0, 0, 0);
                var padImg = padGo.GetComponent<Image>();
                padImg.color = IsWaypoint(i)
                    ? new Color(0.7f, 0.55f, 0.3f, 0.85f)
                    : new Color(0.35f, 0.32f, 0.28f, 0.9f);
                _pads[i] = padImg;
            }

            var pieceGo = new GameObject("YutPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pieceGo.transform.SetParent(_pads[0].transform, false);
            _piece = pieceGo.GetComponent<RectTransform>();
            _piece.anchorMin = new Vector2(0.15f, 0.15f);
            _piece.anchorMax = new Vector2(0.85f, 0.85f);
            _piece.offsetMin = Vector2.zero;
            _piece.offsetMax = Vector2.zero;
            pieceGo.GetComponent<Image>().color = new Color(0.95f, 0.9f, 0.85f, 1f);
            pieceGo.SetActive(false); // 튜토리얼 각본 대결 전용(SetPieceIndex) — 본게임(YutScreen)은 안 씀, 기본으로 숨겨둠

            var opponentGo = new GameObject("ImugiPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            opponentGo.transform.SetParent(_pads[0].transform, false);
            _opponentPiece = opponentGo.GetComponent<RectTransform>();
            _opponentPiece.anchorMin = new Vector2(0.15f, 0.15f);
            _opponentPiece.anchorMax = new Vector2(0.85f, 0.85f);
            _opponentPiece.offsetMin = Vector2.zero;
            _opponentPiece.offsetMax = Vector2.zero;
            var opponentImg = opponentGo.GetComponent<Image>();
            var imugiSprite = PieceSpriteFor("Imugi");
            if (imugiSprite != null)
            {
                opponentImg.sprite = imugiSprite;
                opponentImg.color = Color.white;
                opponentImg.preserveAspect = true;
            }
            else
            {
                opponentImg.color = new Color(0.25f, 0.55f, 0.85f, 1f); // 에셋 못 찾을 때 폴백
            }
            opponentGo.SetActive(false);

            EnsureQuadrants();
            EnsureRulesOverlay();
            EnsureCandidateDialog();
        }

        void EnsureRulesOverlay()
        {
            if (_rulesOverlay != null) return;

            _rulesOverlay = new GameObject("RulesOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _rulesOverlay.transform.SetParent(transform, false);
            SetAnchor((RectTransform)_rulesOverlay.transform, 0.14f, 0.32f, 0.86f, 0.64f, 0, 0, 0, 0);
            _rulesOverlay.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

            var rulesText = CreateText(_rulesOverlay.transform, "RulesText",
                "윷놀이 족보 (16분의)\n\n빽도 -1\n도 1\n개 2\n걸 3\n윷 4 (한 번 더)\n모 5 (한 번 더)",
                22, TextAnchor.MiddleCenter);
            SetAnchor(rulesText.rectTransform, 0.05f, 0.2f, 0.95f, 0.95f, 0, 0, 0, 0);

            var closeBtn = CreateButton(_rulesOverlay.transform, "Close", "닫기", () => ShowRulesOverlay(false));
            SetAnchor(closeBtn.GetComponent<RectTransform>(), 0.32f, 0.04f, 0.68f, 0.16f, 0, 0, 0, 0);

            _rulesOverlay.SetActive(false);
        }

        /// <summary>
        /// 두 대각선이 나누는 4개 삼각형 구역의 컨테이너를 만든다. 아직 내용은 비어 있고,
        /// 각 구역을 담당할 기능이 GetQuadrant()로 받아서 자기 UI를 채워 넣는 자리(베이스)다.
        /// </summary>
        void EnsureQuadrants()
        {
            if (_quadrants != null) return;

            _quadrants = new RectTransform[4];
            _quadrants[(int)YutBoardQuadrant.ThrownSticks] =
                CreateQuadrantContainer("Quadrant_ThrownSticks", 0.42f, 0.49f, 0.63f, 0.575f);
            _quadrants[(int)YutBoardQuadrant.SpecialAbility] =
                CreateQuadrantContainer("Quadrant_SpecialAbility", 0.13f, 0.32f, 0.33f, 0.53f);
            _quadrants[(int)YutBoardQuadrant.WaitingPieces] =
                CreateQuadrantContainer("Quadrant_WaitingPieces", 0.67f, 0.32f, 0.87f, 0.53f);
            _quadrants[(int)YutBoardQuadrant.FinishedAndLoot] =
                CreateQuadrantContainer("Quadrant_FinishedAndLoot", 0.3f, 0.22f, 0.7f, 0.33f);
        }

        RectTransform CreateQuadrantContainer(string name, float xmin, float ymin, float xmax, float ymax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchor(rt, xmin, ymin, xmax, ymax, 0, 0, 0, 0);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            return rt;
        }

        /// <summary>다른 기능(대기말/특수능력/완주말+보물 등)이 자기 UI를 붙일 구역 컨테이너.</summary>
        public RectTransform GetQuadrant(YutBoardQuadrant quadrant)
        {
            EnsureBoard();
            return _quadrants[(int)quadrant];
        }

        /// <summary>
        /// 윷가락 4개를 던져서 흩뿌리는 연출. 결과(result)에 맞는 앞/뒤 패턴으로 착지한다 —
        /// 뒤집힌 가락 개수 = 0(모)/1(도·빽도)/2(개)/3(걸)/4(윷). 0번 가락은 빨간 점으로
        /// 표시된 "빽도 가락"이라, 1개만 뒤집혔을 때 그게 0번이면 빽도, 다른 가락이면 도로
        /// 갈린다(기획서 7-4 "빽도 가락만 엎어진 경우" 기준). 어느 가락이 뒤집힐지는 개/걸에서만
        /// 랜덤이고 개수는 항상 결과와 일치한다.
        /// </summary>
        public IEnumerator PlayThrowAnim(YutThrowResult result)
        {
            EnsureBoard();
            ClearParkedSticks();
            EnsureStickSprites();

            var panelRect = ((RectTransform)transform).rect;
            Vector2 ToLocal(Vector2 norm) =>
                new((norm.x - 0.5f) * panelRect.width, (norm.y - 0.5f) * panelRect.height);

            var frontStates = DetermineFrontStates(result);

            var origin = new Vector2(0.5f, 0.19f);
            var sticks = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"YutStick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(22f, 110f);
                rt.anchoredPosition = ToLocal(origin);
                var img = go.GetComponent<Image>();
                ApplyStickFace(img, front: true, isBaekdoStick: i == 0);
                // 에셋에 빽도 점이 없으면 예전처럼 빨간 점 자식
                if (i == 0 && (_stickFrontBaekdo == null || img.sprite != _stickFrontBaekdo))
                {
                    var markGo = new GameObject("BaekdoMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    markGo.transform.SetParent(rt, false);
                    var markRt = markGo.GetComponent<RectTransform>();
                    markRt.anchorMin = new Vector2(0.5f, 0.85f);
                    markRt.anchorMax = new Vector2(0.5f, 0.85f);
                    markRt.sizeDelta = new Vector2(8f, 8f);
                    markGo.GetComponent<Image>().color = BaekdoMarkColor;
                }
                go.transform.SetAsLastSibling();
                sticks[i] = rt;
            }

            var routines = new Coroutine[4];
            for (int i = 0; i < 4; i++)
                routines[i] = StartCoroutine(ThrowOneStick(sticks[i], origin, ToLocal, i * 0.05f, frontStates[i], isBaekdoStick: i == 0));
            for (int i = 0; i < 4; i++)
                yield return routines[i];

            yield return new WaitForSecondsRealtime(0.35f);

            // 다음 던지기 전까지 방금 던진 윷을 보드 상단(ThrownSticks 구역)에 계속 보이게 둔다
            yield return ParkSticks(sticks, ToLocal);
            var thrownZone = GetQuadrant(YutBoardQuadrant.ThrownSticks);
            foreach (var rt in sticks)
                rt.SetParent(thrownZone, true);
            _parkedSticks = sticks;
        }

        // ThrownSticks 구역(0.42~0.63, 0.49~0.575) 안쪽에만 딱 맞게, 보드 노드와 겹치지 않게 촘촘히 배치
        static readonly Vector2[] ParkSpots =
        {
            new(0.455f, 0.5325f), new(0.505f, 0.5325f), new(0.545f, 0.5325f), new(0.595f, 0.5325f),
        };
        static readonly Vector2 ParkedStickSize = new(10f, 42f);

        IEnumerator ParkSticks(RectTransform[] sticks, Func<Vector2, Vector2> toLocal)
        {
            var starts = new Vector2[sticks.Length];
            var startRotations = new Quaternion[sticks.Length];
            var startSizes = new Vector2[sticks.Length];
            for (int i = 0; i < sticks.Length; i++)
            {
                starts[i] = sticks[i].anchoredPosition;
                startRotations[i] = sticks[i].localRotation;
                startSizes[i] = sticks[i].sizeDelta;
            }

            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                for (int i = 0; i < sticks.Length; i++)
                {
                    sticks[i].anchoredPosition = Vector2.Lerp(starts[i], toLocal(ParkSpots[i]), u);
                    sticks[i].localRotation = Quaternion.Slerp(startRotations[i], Quaternion.identity, u);
                    sticks[i].sizeDelta = Vector2.Lerp(startSizes[i], ParkedStickSize, u);
                }
                yield return null;
            }
            for (int i = 0; i < sticks.Length; i++)
            {
                sticks[i].sizeDelta = ParkedStickSize;
                sticks[i].anchoredPosition = toLocal(ParkSpots[i]);
                sticks[i].localRotation = Quaternion.identity;
            }
        }

        void ClearParkedSticks()
        {
            if (_parkedSticks == null) return;
            foreach (var rt in _parkedSticks)
                if (rt != null) Destroy(rt.gameObject);
            _parkedSticks = null;
        }

        // 뒤집힌(등 보임) 가락 개수 = 0(모)/1(도·빽도)/2(개)/3(걸)/4(윷) — 확률표(1/4/6/4/1)와 일치.
        // 1개만 뒤집혔을 때, 그게 0번 "빽도 가락"이면 빽도, 다른 가락이면 도로 갈린다.
        static bool[] DetermineFrontStates(YutThrowResult result)
        {
            var front = new[] { true, true, true, true }; // 기본: 4개 다 정상면(뒤집히지 않음)
            switch (result)
            {
                case YutThrowResult.Mo:
                    break; // 0개 뒤집힘
                case YutThrowResult.Baekdo:
                    front[0] = false; // 빽도 가락(0번)만 뒤집힘
                    break;
                case YutThrowResult.Do:
                    front[1 + UnityEngine.Random.Range(0, 3)] = false; // 빽도 가락 제외, 나머지 중 1개만
                    break;
                case YutThrowResult.Gae:
                    FlipRandom(front, 2);
                    break;
                case YutThrowResult.Geol:
                    FlipRandom(front, 3);
                    break;
                case YutThrowResult.Yut:
                    for (int i = 0; i < front.Length; i++) front[i] = false; // 4개 다 뒤집힘
                    break;
            }
            return front;
        }

        static void FlipRandom(bool[] front, int count)
        {
            var indices = new List<int> { 0, 1, 2, 3 };
            for (int i = 0; i < count; i++)
            {
                int pick = UnityEngine.Random.Range(0, indices.Count);
                front[indices[pick]] = false;
                indices.RemoveAt(pick);
            }
        }

        IEnumerator ThrowOneStick(RectTransform rt, Vector2 originNorm, Func<Vector2, Vector2> toLocal, float delay,
            bool targetFront, bool isBaekdoStick)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            var landNorm = new Vector2(
                UnityEngine.Random.Range(0.28f, 0.72f),
                UnityEngine.Random.Range(0.32f, 0.55f));
            float arcHeight = UnityEngine.Random.Range(160f, 260f);
            float spin = UnityEngine.Random.Range(720f, 1260f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            float duration = UnityEngine.Random.Range(0.45f, 0.6f);

            Vector2 start = toLocal(originNorm);
            Vector2 end = toLocal(landNorm);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eu = 1f - (1f - u) * (1f - u);
                var pos = Vector2.Lerp(start, end, eu);
                pos.y += arcHeight * 4f * u * (1f - u);
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, spin, u));
                yield return null;
            }
            rt.anchoredPosition = end;

            bool front = targetFront;
            var img = rt.GetComponent<Image>();
            const float flipDuration = 0.12f;
            float flipT = 0f;
            while (flipT < flipDuration)
            {
                flipT += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(flipT / flipDuration);
                rt.localScale = new Vector3(Mathf.Abs(Mathf.Cos(u * Mathf.PI)), 1f, 1f);
                if (u >= 0.5f)
                    ApplyStickFace(img, front, isBaekdoStick);
                yield return null;
            }
            rt.localScale = Vector3.one;
            ApplyStickFace(img, front, isBaekdoStick);

            const float settleDuration = 0.18f;
            float settleT = 0f;
            Vector2 settled = rt.anchoredPosition;
            while (settleT < settleDuration)
            {
                settleT += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(settleT / settleDuration);
                float bounce = Mathf.Sin(u * Mathf.PI) * 10f * (1f - u);
                rt.anchoredPosition = settled + new Vector2(0, bounce);
                yield return null;
            }
            rt.anchoredPosition = settled;
        }

        void EnsureButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<Text>(true);
            if (text == null)
            {
                text = CreateText(button.transform, "Label", label, 28, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
                text.raycastTarget = false;
            }
            text.text = label;
            text.font = ResolveFont();
            text.fontSize = UiFonts.Size(29);
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
        }

        static void WireButton(Button btn, Action action)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action?.Invoke());
        }

        Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.22f, 0.18f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText(go.transform, "Label", label, 24, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
            return btn;
        }

        Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = ResolveFont();
            text.fontSize = UiFonts.Size(size + 1);
            text.color = Color.white;
            text.alignment = anchor;
            return text;
        }

        Font ResolveFont() => font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static void SetAnchor(RectTransform rt, float xmin, float ymin, float xmax, float ymax,
            float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(xmin, ymin);
            rt.anchorMax = new Vector2(xmax, ymax);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
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
