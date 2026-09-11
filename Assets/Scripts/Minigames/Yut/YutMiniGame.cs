using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
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

        /// <summary>탭이 아니라 아래→위 슬라이드로 던지기가 완료됐을 때. power(0~1)는 슬라이드
        /// 속도 기반 — 던지는 연출(아치 높이·회전·착지 퍼짐)에만 쓰고 결과 확률엔 영향 없다.</summary>
        public event Action<float> OnThrowPressed;
        public event Action OnLeavePressed;
        /// <summary>족보 안내 오버레이가 열리고/닫힐 때. ScrollScreenUI가 이걸로 대사 타이핑을 같이 멈춘다.</summary>
        public event Action<bool> OnRulesPanelToggled;
        /// <summary>족보 안내를 유저가 닫기 버튼으로 직접 닫았을 때.</summary>
        public event Action OnRulesClosed;
        /// <summary>FlashCandidates로 보드 위에 띄운 후보 중 하나를 유저가 탭했을 때 — 그 요괴 id와
        /// (갈림길 후보였다면) 지름길을 골랐는지 여부.</summary>
        public event Action<string, bool> OnCandidateTapped;

        Image[] _heartIcons;
        GameObject _throwZone;
        YutThrowSwipeZone _throwSwipe;
        Button _leaveButton;
        RectTransform _boardRoot;
        RectTransform _piece;
        RectTransform _opponentPiece;
        Image[] _pads;
        RectTransform[] _parkedSticks;
        RectTransform[] _quadrants; // YutBoardQuadrant 순서대로
        GameObject _rulesOverlay;
        readonly List<GameObject> _candidateMarkers = new();
        readonly List<(RectTransform rect, string pieceId, bool useShortcut)> _candidateHits = new();
        GameObject _logBar;
        Image _logBarLeftPortrait;
        Image _logBarRightPortrait;
        RectTransform _logViewport;
        RectTransform _logContent;
        ScrollRect _logScroll;
        GameObject _miniThrowContainer;
        Image[] _miniThrowSticks;
        readonly List<(bool leftSpeaking, string text)> _logHistory = new();
        /// <summary>스크롤로 다시 볼 수 있게 최근 N줄까지 보관(화면엔 뷰포트만큼만 보이고 위·아래로 민다).</summary>
        const int MaxLogHistory = 40;
        const float LogBubbleHeight = 44f;
        const float LogBubbleGap = 6f;

        // 본게임 수련장 전용 — 보유 요괴 전체를 동시에 말로 표시(id → 말 오브젝트/이니셜 라벨).
        // 튜토리얼의 _piece/_opponentPiece(각본 대결용)와는 완전히 별개.
        readonly Dictionary<string, RectTransform> _yokaiPieces = new();
        readonly Dictionary<string, Text> _yokaiPieceLabels = new();

        RectTransform _rosterPanel;
        RectTransform _rosterRow;
        Text _rosterCaption;
        readonly List<RosterChip> _rosterChips = new();

        readonly struct RosterChip
        {
            public readonly RectTransform Root;
            public readonly Image Portrait;
            public readonly Text Name;
            public readonly Text Stats;
            public readonly Text Status;

            public RosterChip(RectTransform root, Image portrait, Text name, Text stats, Text status)
            {
                Root = root;
                Portrait = portrait;
                Name = name;
                Stats = stats;
                Status = status;
            }
        }

        /// <summary>윷놀이 화면 최하단 요괴 명단 한 줄(초상·이름·기력♦친밀도♡·보드 위치) 항목.</summary>
        public readonly struct RosterEntry
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly int Stamina;
            public readonly int Intimacy;
            public readonly string StatusLabel;

            public RosterEntry(string id, string displayName, int stamina, int intimacy, string statusLabel)
            {
                Id = id;
                DisplayName = displayName;
                Stamina = stamina;
                Intimacy = intimacy;
                StatusLabel = statusLabel;
            }
        }

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
            EnsureThrowSwipeZone();

            _leaveButton = transform.Find("Leave")?.GetComponent<Button>();
            if (_leaveButton == null)
            {
                _leaveButton = CreateButton(transform, "Leave", "나가기", null);
                SetAnchor(_leaveButton.GetComponent<RectTransform>(), 0.02f, 0.93f, 0.18f, 0.99f, 0, 0, 0, 0);
            }

            WireButton(_leaveButton, () => OnLeavePressed?.Invoke());
        }

        void ForwardSwipeThrow(float power) => OnThrowPressed?.Invoke(power);

        /// <summary>탭 버튼 대신 아래→위 슬라이드로 던지는 입력 영역. 손 모양 힌트가 살짝 위아래로
        /// 통통 튀어서 "여기서 위로 밀어라"를 안내한다.</summary>
        void EnsureThrowSwipeZone()
        {
            if (_throwZone != null) return;

            var existing = transform.Find("ThrowSwipeZone");
            if (existing != null)
            {
                _throwZone = existing.gameObject;
                _throwSwipe = existing.GetComponent<YutThrowSwipeZone>();
                if (_throwSwipe == null)
                    _throwSwipe = _throwZone.AddComponent<YutThrowSwipeZone>();
                _throwSwipe.OnSwipeThrow -= ForwardSwipeThrow;
                _throwSwipe.OnSwipeThrow += ForwardSwipeThrow;
                // 예전 Bake본은 보드와 떨어진 자리/색으로 저장돼 있을 수 있어 — 매번 보드 하단에
                // 이어붙는 위치/색으로 다시 맞춘다(코드가 항상 최종 소스, 밭 배치와 동일한 원칙).
                SetAnchor((RectTransform)existing, 0.1f, 0.13f, 0.9f, 0.2f, 0, 0, 0, 0);
                var existingImg = existing.GetComponent<Image>();
                if (existingImg != null) existingImg.color = new Color(0.12f, 0.22f, 0.18f, 0.92f);
                if (existing.Find("IdleStick0") == null)
                    BuildIdleThrowSticks(existing);
                if (Application.isPlaying)
                {
                    var labelRt = existing.Find("Label") as RectTransform;
                    if (labelRt != null)
                        StartCoroutine(BounceHint(labelRt));
                }
                return;
            }

            _throwZone = new GameObject("ThrowSwipeZone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _throwZone.transform.SetParent(transform, false);
            // 보드(_boardRoot) 아래쪽 가장자리(y=0.2)에 바로 이어 붙여서 보드의 연장처럼 보이게 —
            // 폭도 보드와 동일(0.1~0.9), 배경색도 보드 배경색과 맞춘다. 화면 맨 아래(y<0.13)는
            // RosterPanel(요괴 명단) 자리로 비워둔다.
            SetAnchor((RectTransform)_throwZone.transform, 0.1f, 0.13f, 0.9f, 0.2f, 0, 0, 0, 0);
            _throwZone.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.18f, 0.92f);

            BuildIdleThrowSticks(_throwZone.transform);

            var label = CreateText(_throwZone.transform, "Label", "↑ 위로 슬라이드해서 던지기", 25, TextAnchor.LowerCenter);
            SetAnchor(label.rectTransform, 0f, 0f, 1f, 0.34f, 0, 0, 0, 0);
            label.raycastTarget = false;

            _throwSwipe = _throwZone.AddComponent<YutThrowSwipeZone>();
            _throwSwipe.OnSwipeThrow += ForwardSwipeThrow;

            if (Application.isPlaying)
                StartCoroutine(BounceHint(label.rectTransform));
        }

        /// <summary>
        /// 던지기 전 대기 상태의 윷가락 4개 — 실제로 던져질 때(PlayThrowAnim)와 같은 에셋을 써서
        /// "여기 놓인 진짜 윷을 집어 던진다"는 느낌을 준다. 던지는 순간엔 SetThrowVisible(false)로
        /// 이 존 전체가 꺼지고, PlayThrowAnim이 별도 스틱을 만들어 애니메이션하므로 서로 안 겹친다.
        /// </summary>
        void BuildIdleThrowSticks(Transform parent)
        {
            EnsureStickSprites();
            const float stickW = 20f, stickH = 86f, gap = 14f;
            float totalW = stickW * 4 + gap * 3;
            float startX = -totalW / 2f + stickW / 2f;
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"IdleStick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.72f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(stickW, stickH);
                rt.anchoredPosition = new Vector2(startX + i * (stickW + gap), 0f);
                var img = go.GetComponent<Image>();
                ApplyStickFace(img, front: true, isBaekdoStick: i == 0);
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
                img.raycastTarget = false;
            }
        }

        IEnumerator BounceHint(RectTransform rt)
        {
            while (rt != null)
            {
                float bounce = Mathf.Sin(Time.unscaledTime * 2.4f) * 6f;
                rt.anchoredPosition = new Vector2(0, bounce);
                yield return null;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureBoard();
        }

        /// <summary>에디터 Prefab Bake용 — 보드·로그바까지 만들어 Scene/Prefab에서 보이게 한다.</summary>
        public void EnsureBoardForBake()
        {
            gameObject.SetActive(true);
            EnsureBoard();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ClearParkedSticks();
            ClearCandidates();
            ClearLog();
        }

        public void SetThrowVisible(bool on)
        {
            if (_throwZone == null) return;
            _throwZone.SetActive(on);
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

        /// <summary>
        /// 보드 위쪽 게임로그에 대사 한 줄을 채팅처럼 쌓아 올린다(말풍선). speakerId가 "Imugi"면
        /// 왼쪽 정렬(이무기 초상도 살짝 키워 강조), 그 외(플레이어 쪽 요괴 id)면 오른쪽 정렬 —
        /// 실제 말한 요괴가 옥토끼가 아니어도(예: 삼족오가 잡힘) 헤더 초상은 고정, 말풍선 텍스트만
        /// 그 이름을 쓴다. Content는 ScrollRect라 예전 대사도 위로 밀어 다시 볼 수 있다.
        /// </summary>
        public void ShowLogLine(string speakerId, string text)
        {
            EnsureBoard();
            if (_logContent == null || string.IsNullOrEmpty(text)) return;

            bool leftSpeaking = speakerId == "Imugi";
            if (_logBarLeftPortrait != null)
                _logBarLeftPortrait.rectTransform.localScale = Vector3.one * (leftSpeaking ? 1.12f : 1f);
            if (_logBarRightPortrait != null)
                _logBarRightPortrait.rectTransform.localScale = Vector3.one * (!leftSpeaking ? 1.12f : 1f);

            _logHistory.Add((leftSpeaking, text));
            if (_logHistory.Count > MaxLogHistory) _logHistory.RemoveAt(0);
            RebuildLogView();
        }

        void RebuildLogView()
        {
            if (_logContent == null) return;

            for (int i = _logContent.childCount - 1; i >= 0; i--)
                Destroy(_logContent.GetChild(i).gameObject);

            int count = _logHistory.Count;
            if (count == 0)
            {
                _logContent.sizeDelta = new Vector2(0f, 0f);
                return;
            }

            float viewportH = _logViewport != null ? Mathf.Abs(_logViewport.rect.height) : 0f;
            float contentH = count * (LogBubbleHeight + LogBubbleGap) + LogBubbleGap;
            if (contentH < viewportH) contentH = viewportH;

            _logContent.anchorMin = new Vector2(0f, 1f);
            _logContent.anchorMax = new Vector2(1f, 1f);
            _logContent.pivot = new Vector2(0.5f, 1f);
            _logContent.sizeDelta = new Vector2(0f, contentH);
            _logContent.anchoredPosition = Vector2.zero;

            float viewportW = _logViewport != null ? Mathf.Abs(_logViewport.rect.width) : ((RectTransform)_logContent.parent).rect.width;
            float bubbleW = Mathf.Max(120f, viewportW * 0.74f);

            for (int i = 0; i < count; i++)
            {
                var (leftSpeaking, text) = _logHistory[i];
                float y = -(LogBubbleGap + i * (LogBubbleHeight + LogBubbleGap));

                var bubbleGo = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bubbleGo.transform.SetParent(_logContent, false);
                var bubbleRt = bubbleGo.GetComponent<RectTransform>();
                bubbleRt.anchorMin = bubbleRt.anchorMax = bubbleRt.pivot = new Vector2(leftSpeaking ? 0f : 1f, 1f);
                bubbleRt.sizeDelta = new Vector2(bubbleW, LogBubbleHeight);
                bubbleRt.anchoredPosition = new Vector2(leftSpeaking ? 4f : -4f, y);
                bubbleGo.GetComponent<Image>().color = leftSpeaking
                    ? new Color(0.22f, 0.2f, 0.3f, 0.95f)
                    : new Color(0.2f, 0.32f, 0.24f, 0.95f);
                bubbleGo.GetComponent<Image>().raycastTarget = false;

                var label = CreateText(bubbleGo.transform, "Text", text, 21,
                    leftSpeaking ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(10f, 1f);
                labelRt.offsetMax = new Vector2(-10f, -1f);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.raycastTarget = false;
            }

            Canvas.ForceUpdateCanvases();
            if (_logScroll != null)
                _logScroll.verticalNormalizedPosition = 0f;
        }

        /// <summary>매치 시작/재입장 때 이전 대화가 안 남게 게임로그를 비운다.</summary>
        public void ClearLog()
        {
            _logHistory.Clear();
            if (_logContent == null) return;
            for (int i = _logContent.childCount - 1; i >= 0; i--)
                Destroy(_logContent.GetChild(i).gameObject);
            _logContent.sizeDelta = new Vector2(0f, 0f);
            if (_logScroll != null)
                _logScroll.verticalNormalizedPosition = 1f;
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

            Vector3 fromPos = _opponentPiece.position;
            nodeId = Mathf.Clamp(nodeId, 0, _pads.Length - 1);
            var pad = _pads[nodeId].rectTransform;
            _opponentPiece.SetParent(pad, false);
            _opponentPiece.anchorMin = new Vector2(0.15f, 0.15f);
            _opponentPiece.anchorMax = new Vector2(0.85f, 0.85f);
            _opponentPiece.offsetMin = Vector2.zero;
            _opponentPiece.offsetMax = Vector2.zero;
            SlideIn(_opponentPiece, fromPos);
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
        /// 말 아이콘: 캐릭터 CharacterData 스프라이트 / 이무기는 UI/ImugiPortrait (없으면 색+이니셜 폴백).
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

            var waiting = new List<RectTransform>();
            foreach (var info in pieces)
            {
                if (!_yokaiPieces.TryGetValue(info.Id, out var piece) || piece == null)
                {
                    piece = BuildYokaiPieceVisual(info.Id, info.DisplayName, out var label);
                    _yokaiPieces[info.Id] = piece;
                    _yokaiPieceLabels[info.Id] = label;
                }

                if (info.NodeId < 0)
                    waiting.Add(piece); // 남(South) 구역에 한꺼번에 나란히 배치
                else
                    PlaceOnNode(piece, info.NodeId);
            }
            LayoutWaitingPieces(waiting);
        }

        RectTransform BuildYokaiPieceVisual(string id, string displayName, out Text label)
        {
            var go = new GameObject($"YokaiPiece_{id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_pads[0].transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.18f, 0.18f); // PlaceOnNode 기본값과 맞춰서, 새로 생긴 말도
            rt.anchorMax = new Vector2(0.82f, 0.82f); // SlideIn의 시작점이 참 중앙으로 잘 정의되게 함
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
                label = CreateText(go.transform, "Label", InitialOf(displayName), 25, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.raycastTarget = false;
            }

            // 누르고 후보 칸까지 드래그해서 이동시킬 수 있게 — 후보가 아닐 때 드롭하면 그냥 무시된다.
            var drag = go.AddComponent<YutPieceDragHandle>();
            drag.Owner = this;
            drag.PieceId = id;

            return go.GetComponent<RectTransform>();
        }

        /// <summary>남(South) 구역 — 대기말을 보유한 수만큼 가로로 나란히 배치.</summary>
        void LayoutWaitingPieces(List<RectTransform> pieces)
        {
            var south = _quadrants != null ? _quadrants[(int)YutBoardQuadrant.South] : null;
            if (south == null) return;

            int count = pieces.Count;
            for (int i = 0; i < count; i++)
            {
                var piece = pieces[i];
                Vector3 fromPos = piece.position;
                piece.SetParent(south, false);
                float slotW = 1f / count;
                float pad = slotW * 0.1f;
                piece.anchorMin = new Vector2(i * slotW + pad, 0.05f);
                piece.anchorMax = new Vector2((i + 1) * slotW - pad, 0.95f);
                piece.offsetMin = Vector2.zero;
                piece.offsetMax = Vector2.zero;
                SlideIn(piece, fromPos);
            }
        }

        void PlaceOnNode(RectTransform piece, int nodeId)
        {
            Vector3 fromPos = piece.position;
            nodeId = Mathf.Clamp(nodeId, 0, _pads.Length - 1);
            piece.SetParent(_pads[nodeId].transform, false);
            piece.anchorMin = new Vector2(0.18f, 0.18f);
            piece.anchorMax = new Vector2(0.82f, 0.82f);
            piece.offsetMin = Vector2.zero;
            piece.offsetMax = Vector2.zero;
            SlideIn(piece, fromPos);
        }

        /// <summary>
        /// 말이 순간이동하지 않고 눈에 보이게 미끄러지도록 한다 — 이미 새 위치로 배치된 rt.position을
        /// 목표로 잡고 fromPos에서 슬라이드한다. 던질 때마다("모→이동→윷→이동→도→이동") 실제로
        /// 움직이는 게 보여야 보너스 턴이 이어지는 게 자연스럽게 읽힌다.
        /// </summary>
        void SlideIn(RectTransform rt, Vector3 fromPos)
        {
            Vector3 toPos = rt.position;
            if ((toPos - fromPos).sqrMagnitude < 1f) return; // 실질적으로 제자리면 생략
            rt.position = fromPos;
            StartCoroutine(SlideRoutine(rt, fromPos, toPos));
        }

        IEnumerator SlideRoutine(RectTransform rt, Vector3 fromPos, Vector3 toPos)
        {
            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (rt == null) yield break;
                float u = Mathf.Clamp01(t / duration);
                float eased = 1f - (1f - u) * (1f - u);
                rt.position = Vector3.Lerp(fromPos, toPos, eased);
                yield return null;
            }
            if (rt != null) rt.position = toPos;
        }

        static string InitialOf(string displayName) =>
            string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1);

        static readonly Dictionary<string, Sprite> PieceSpriteCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// 이무기 → Resources/UI/ImugiPortrait.
        /// 그 외 요괴 → CharacterData(월드 에이전트 / Main 연결 / Gorani Resources) 첫 스프라이트.
        /// </summary>
        static Sprite PieceSpriteFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (PieceSpriteCache.TryGetValue(id, out var cached) && cached != null) return cached;

            Sprite sprite;
            if (id == "Imugi")
                sprite = Resources.Load<Sprite>("UI/ImugiPortrait");
            else
                sprite = CharacterSpawner.FirstSprite(FindCharacterData(id));

            if (sprite != null) PieceSpriteCache[id] = sprite;
            return sprite;
        }

        static CharacterData FindCharacterData(string id)
        {
            foreach (var agent in CharacterAgent.All)
            {
                if (agent?.Data != null && agent.Data.id.ToString() == id)
                    return agent.Data;
            }

            var main = UnityEngine.Object.FindObjectOfType<Yoegoe.Main>();
            if (main != null)
            {
                if (id == nameof(CharacterId.Rabbit)) return main.oktoData;
                if (id == nameof(CharacterId.SamjokO)) return main.samjokOData;
                if (id == nameof(CharacterId.Gumiho)) return main.gumihoData;
                if (id == nameof(CharacterId.Gorani))
                    return main.goraniData != null
                        ? main.goraniData
                        : Resources.Load<CharacterData>("Characters/Gorani");
            }

            if (id == nameof(CharacterId.Gorani))
                return Resources.Load<CharacterData>("Characters/Gorani");
            return null;
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
            public readonly bool UseShortcut;

            public YokaiMoveCandidate(string id, string displayName, int destinationNode, bool useShortcut)
            {
                Id = id;
                DisplayName = displayName;
                DestinationNode = destinationNode;
                UseShortcut = useShortcut;
            }
        }

        // 한 칸에 후보가 여럿(대기 말 여럿이 같은 결과로 동시 입장 가능 등) 겹칠 때, 그 칸을
        // 중심으로 위/아래/왼쪽/오른쪽에 하나씩 붙여서 보여준다 — 팀이 최대 4마리라 방향 4개면 충분.
        static readonly Vector2[] CrossDirs = { new(0, 1), new(0, -1), new(-1, 0), new(1, 0) };

        /// <summary>던진 결과로 움직일 수 있는 말 후보를 보드 위에 직접 보여준다. 후보가 한 마리면
        /// 그 칸 위에 바로, 여럿이 같은 칸으로 겹치면 그 칸 둘레(상하좌우)에 하나씩 붙여서 —
        /// 다이얼로그 없이 보드만 보고 원하는 말을 탭해서 고르게 한다.</summary>
        public void FlashCandidates(IReadOnlyList<YokaiMoveCandidate> candidates)
        {
            ClearCandidates();
            EnsureBoard();
            if (_pads == null || _pads.Length == 0 || candidates == null || candidates.Count == 0) return;

            var byNode = new Dictionary<int, List<YokaiMoveCandidate>>();
            foreach (var c in candidates)
            {
                int node = Mathf.Clamp(c.DestinationNode, 0, _pads.Length - 1);
                if (!byNode.TryGetValue(node, out var list))
                {
                    list = new List<YokaiMoveCandidate>();
                    byNode[node] = list;
                }
                list.Add(c);
            }

            foreach (var kv in byNode)
            {
                if (kv.Value.Count == 1)
                    _candidateMarkers.Add(BuildCandidateOnNode(kv.Key, kv.Value[0]));
                else
                    _candidateMarkers.AddRange(BuildCandidateCross(kv.Key, kv.Value));
            }
        }

        /// <summary>FlashCandidates로 띄운 후보 마커를 전부 지운다.</summary>
        public void ClearCandidates()
        {
            foreach (var go in _candidateMarkers)
                if (go != null) Destroy(go);
            _candidateMarkers.Clear();
            _candidateHits.Clear();
        }

        /// <summary>
        /// 말 아이콘을 드래그해서 놓았을 때(YutPieceDragHandle) 호출된다. 지금 보드 위에 떠 있는
        /// 후보 마커 중 이 말 것이면서 놓은 지점과 겹치는 게 있으면 그 후보를 고른 것으로 처리한다
        /// (탭했을 때와 동일하게 OnCandidateTapped를 쏨). 겹치는 후보가 없으면 조용히 무시.
        /// </summary>
        public void ResolveDrop(string pieceId, Vector2 screenPos)
        {
            foreach (var hit in _candidateHits)
            {
                if (hit.pieceId != pieceId || hit.rect == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(hit.rect, screenPos, null))
                {
                    OnCandidateTapped?.Invoke(hit.pieceId, hit.useShortcut);
                    return;
                }
            }
        }

        GameObject BuildCandidateOnNode(int nodeId, YokaiMoveCandidate candidate)
        {
            var go = new GameObject($"Candidate_{nodeId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_pads[nodeId].transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            ApplyCandidateVisual(img, candidate);
            WireCandidateButton(go, img, candidate.Id, candidate.UseShortcut);
            StartCoroutine(PulseScale(rt));
            return go;
        }

        /// <summary>경합 밭(같은 칸으로 갈 수 있는 후보 2마리 이상)을 홀드 없이 처음부터 사방에
        /// 펼쳐서 보여준다 — 각 아이콘은 탭도 되고(WireCandidateButton) 말을 드래그해서 놓아도
        /// 된다(ResolveDrop이 _candidateHits로 판정).</summary>
        List<GameObject> BuildCandidateCross(int nodeId, List<YokaiMoveCandidate> group)
        {
            var result = new List<GameObject>();
            var pad = _pads[nodeId].rectTransform;
            Vector2 size = pad.anchorMax - pad.anchorMin;

            for (int i = 0; i < group.Count && i < CrossDirs.Length; i++)
            {
                var candidate = group[i];
                var dir = CrossDirs[i];
                var go = new GameObject($"Candidate_{nodeId}_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_boardRoot, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = pad.anchorMin + Vector2.Scale(dir, size);
                rt.anchorMax = pad.anchorMax + Vector2.Scale(dir, size);
                rt.offsetMin = pad.offsetMin;
                rt.offsetMax = pad.offsetMax;
                var img = go.GetComponent<Image>();
                ApplyCandidateVisual(img, candidate);
                WireCandidateButton(go, img, candidate.Id, candidate.UseShortcut);
                StartCoroutine(PulseScale(rt));
                result.Add(go);
            }
            return result;
        }

        void ApplyCandidateVisual(Image img, YokaiMoveCandidate candidate)
        {
            var sprite = PieceSpriteFor(candidate.Id);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = ColorForYokai(candidate.Id);
                var label = CreateText(img.transform, "Label", InitialOf(candidate.DisplayName), 23, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.raycastTarget = false;
            }
        }

        void WireCandidateButton(GameObject go, Image img, string tappedId, bool useShortcut)
        {
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnCandidateTapped?.Invoke(tappedId, useShortcut));
            _candidateHits.Add((go.GetComponent<RectTransform>(), tappedId, useShortcut));
        }

        IEnumerator PulseScale(RectTransform rt)
        {
            while (rt != null)
            {
                float s = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 4.5f);
                rt.localScale = Vector3.one * s;
                yield return null;
            }
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
            if (_boardRoot != null && _pads != null) return;

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

            BindOrCreateHearts();
            if (!TryBindPads())
                CreatePads();
            BindOrCreatePieces();

            EnsureQuadrants();
            EnsureRulesOverlay();
            EnsureLogBar();
            EnsureRosterPanel();
        }

        /// <summary>
        /// 화면 최하단(y 0~0.13) — 참가 요괴 전체를 초상화·이름·기력(♦)·친밀도(♡)·보드 위치와 함께
        /// 한 줄로 보여준다. ThrowSwipeZone(0.13~0.2)과 겹치지 않게 그 아래 자리에 둔다.
        /// </summary>
        void EnsureRosterPanel()
        {
            if (_rosterPanel != null) return;

            var existing = transform.Find("RosterPanel");
            if (existing != null)
            {
                _rosterPanel = existing as RectTransform;
            }
            else
            {
                var go = new GameObject("RosterPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                _rosterPanel = go.GetComponent<RectTransform>();
                SetAnchor(_rosterPanel, 0.02f, 0f, 0.98f, 0.13f, 0, 0, 0, 0);
                go.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.07f, 0.85f);
            }

            var rowT = _rosterPanel.Find("Row") as RectTransform;
            if (rowT == null)
            {
                var rowGo = new GameObject("Row", typeof(RectTransform));
                rowT = rowGo.GetComponent<RectTransform>();
                rowT.SetParent(_rosterPanel, false);
                SetAnchor(rowT, 0f, 0.3f, 1f, 1f, 0, 0, 0, 0);
            }
            _rosterRow = rowT;

            var captionT = _rosterPanel.Find("Caption") as RectTransform;
            Text captionText = captionT != null ? captionT.GetComponent<Text>() : null;
            if (captionText == null)
            {
                captionText = CreateText(_rosterPanel, "Caption", "", 21, TextAnchor.MiddleCenter);
                SetAnchor(captionText.rectTransform, 0f, 0f, 1f, 0.3f, 0, 0, 0, 0);
                captionText.color = new Color(0.85f, 0.8f, 0.7f, 0.85f);
                captionText.raycastTarget = false;
            }
            _rosterCaption = captionText;
        }

        /// <summary>참가 요괴 명단을 최신 상태로 다시 그린다. 인원 수가 바뀔 때만 칩을 새로 만들고,
        /// 그 외엔 이미 만든 칩의 초상·이름·스탯·상태 텍스트만 갱신한다.</summary>
        public void ShowRoster(IReadOnlyList<RosterEntry> entries)
        {
            EnsureBoard();
            if (_rosterRow == null || entries == null) return;

            if (_rosterChips.Count != entries.Count)
            {
                foreach (var chip in _rosterChips)
                    if (chip.Root != null) Destroy(chip.Root.gameObject);
                _rosterChips.Clear();

                for (int i = 0; i < entries.Count; i++)
                    _rosterChips.Add(BuildRosterChip(_rosterRow, i, entries.Count));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var chip = _rosterChips[i];

                var sprite = PieceSpriteFor(entry.Id);
                if (sprite != null)
                {
                    chip.Portrait.sprite = sprite;
                    chip.Portrait.color = Color.white;
                    chip.Portrait.preserveAspect = true;
                }
                else
                {
                    chip.Portrait.sprite = null;
                    chip.Portrait.color = ColorForYokai(entry.Id);
                }

                chip.Name.text = entry.DisplayName;
                chip.Stats.text = $"♦{entry.Stamina} ♡{entry.Intimacy}";
                chip.Status.text = entry.StatusLabel;
            }

            _rosterCaption.text = "이동한 요괴마다 친밀도 +0.25";
        }

        RosterChip BuildRosterChip(RectTransform parent, int index, int count)
        {
            var go = new GameObject($"Chip{index}", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            float slotW = 1f / count;
            const float pad = 0.03f;
            rt.anchorMin = new Vector2(index * slotW + pad, 0f);
            rt.anchorMax = new Vector2((index + 1) * slotW - pad, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            // 위에서부터 고정 픽셀로 쌓는다(초상 60px → 이름 22px → 스탯 20px → 상태 18px) —
            // Row 실제 높이와 상관없이 서로 겹치지 않게.
            const float portraitSize = 68f, nameH = 27f, statsH = 25f, statusH = 23f, gap = 4f;

            var portraitRt = (RectTransform)portraitGo.transform;
            portraitRt.SetParent(rt, false);
            portraitRt.anchorMin = portraitRt.anchorMax = new Vector2(0.5f, 1f);
            portraitRt.pivot = new Vector2(0.5f, 1f);
            portraitRt.sizeDelta = new Vector2(portraitSize, portraitSize);
            portraitRt.anchoredPosition = new Vector2(0f, 0f);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;

            float y = portraitSize + gap;
            var nameText = CreateText(rt, "Name", "", 23, TextAnchor.MiddleCenter);
            AnchorTopStrip(nameText.rectTransform, y, nameH);
            nameText.raycastTarget = false;
            y += nameH + gap;

            var statsText = CreateText(rt, "Stats", "", 20, TextAnchor.MiddleCenter);
            AnchorTopStrip(statsText.rectTransform, y, statsH);
            statsText.color = new Color(0.8f, 0.9f, 0.95f);
            statsText.raycastTarget = false;
            y += statsH + gap;

            var statusText = CreateText(rt, "Status", "", 19, TextAnchor.MiddleCenter);
            AnchorTopStrip(statusText.rectTransform, y, statusH);
            statusText.color = new Color(0.7f, 0.65f, 0.55f);
            statusText.raycastTarget = false;

            return new RosterChip(rt, portrait, nameText, statsText, statusText);
        }

        /// <summary>부모 위쪽 기준 y(px) 지점부터 height(px)만큼의 가로 전체 폭 띠를 앵커한다.</summary>
        static void AnchorTopStrip(RectTransform rt, float yFromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
            rt.sizeDelta = new Vector2(0f, height);
        }

        void BindOrCreateHearts()
        {
            if (_heartIcons != null) return;

            const int heartCount = 5 /* 구 KSpirits.Core.GameConstants.HeartMax, 새 설계에 맞게 나중에 재조정 */;
            if (transform.Find("Heart0") != null)
            {
                _heartIcons = new Image[heartCount];
                for (int i = 0; i < heartCount; i++)
                {
                    var t = transform.Find($"Heart{i}");
                    if (t == null) { _heartIcons = null; break; }
                    _heartIcons[i] = t.GetComponent<Image>();
                }
                if (_heartIcons != null) return;
            }

            _heartIcons = new Image[heartCount];
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

        bool TryBindPads()
        {
            if (_boardRoot == null || _boardRoot.Find("Node0") == null) return false;

            _pads = new Image[YutBoardLayout.NodeCount];
            for (int i = 0; i < YutBoardLayout.NodeCount; i++)
            {
                var t = _boardRoot.Find($"Node{i}");
                if (t == null)
                {
                    _pads = null;
                    return false;
                }
                _pads[i] = t.GetComponent<Image>();
            }
            return true;
        }

        void CreatePads()
        {
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
                padImg.color = YutBoardLayout.IsSpecialReward(i)
                    ? new Color(0.35f, 0.55f, 0.85f, 0.9f) // 특수 칸 — 공양물·정화수·엽전 확정 수급
                    : IsWaypoint(i)
                        ? new Color(0.7f, 0.55f, 0.3f, 0.85f)
                        : new Color(0.35f, 0.32f, 0.28f, 0.9f);
                _pads[i] = padImg;
            }
        }

        void BindOrCreatePieces()
        {
            if (_pads == null || _pads.Length == 0) return;

            var pieceT = _pads[0].transform.Find("YutPiece");
            if (pieceT != null)
                _piece = pieceT as RectTransform;
            else
            {
                var pieceGo = new GameObject("YutPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pieceGo.transform.SetParent(_pads[0].transform, false);
                _piece = pieceGo.GetComponent<RectTransform>();
                _piece.anchorMin = new Vector2(0.15f, 0.15f);
                _piece.anchorMax = new Vector2(0.85f, 0.85f);
                _piece.offsetMin = Vector2.zero;
                _piece.offsetMax = Vector2.zero;
                pieceGo.GetComponent<Image>().color = new Color(0.95f, 0.9f, 0.85f, 1f);
                pieceGo.SetActive(false); // 튜토리얼 각본 대결 전용(SetPieceIndex) — 본게임은 안 씀
            }

            var opponentT = _pads[0].transform.Find("ImugiPiece");
            if (opponentT != null)
                _opponentPiece = opponentT as RectTransform;
            else
            {
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
                    opponentImg.color = new Color(0.25f, 0.55f, 0.85f, 1f);
                }
                opponentGo.SetActive(false);
            }
        }

        /// <summary>
        /// 보드 위쪽 게임로그 — 위쪽은 이무기(왼쪽)·옥토끼(팀 대표, 오른쪽) 초상이 고정으로 있고,
        /// 그 아래 Viewport/Content는 ScrollRect로 말풍선이 카톡처럼 쌓인다. 넘치면 손가락으로
        /// 위·아래 스크롤. VerticalLayoutGroup 없이 말풍선 좌표만 직접 잡고 ScrollRect만 붙인다.
        /// </summary>
        void EnsureLogBar()
        {
            if (_logBar != null) return;

            var existing = transform.Find("LogBar");
            if (existing != null)
            {
                _logBar = existing.gameObject;
                _logBarLeftPortrait = existing.Find("LeftHeader/Icon")?.GetComponent<Image>();
                _logBarRightPortrait = existing.Find("RightHeader/Icon")?.GetComponent<Image>();
                _miniThrowContainer = existing.Find("MiniThrow")?.gameObject;
                if (_miniThrowContainer != null)
                {
                    _miniThrowSticks = new Image[4];
                    for (int i = 0; i < 4; i++)
                        _miniThrowSticks[i] = _miniThrowContainer.transform.Find($"Stick{i}")?.GetComponent<Image>();
                }
                EnsureLogScrollArea();
                return;
            }

            _logBar = new GameObject("LogBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _logBar.transform.SetParent(transform, false);
            SetAnchor((RectTransform)_logBar.transform, 0.06f, 0.68f, 0.94f, 0.9f, 0, 0, 0, 0);
            _logBar.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.92f);

            _logBarLeftPortrait = BuildLogHeaderPortrait(_logBar.transform, "Imugi", "이무기", left: true, out _);
            _logBarRightPortrait = BuildLogHeaderPortrait(_logBar.transform, "Rabbit", "옥토끼", left: false, out _);
            BuildOpponentMiniThrow(_logBar.transform);
            EnsureLogScrollArea();
        }

        /// <summary>
        /// Prefab에 Viewport가 없으면 예전 Content를 Viewport로 올리고 안쪽에 Content를 새로 둔다.
        /// ScrollRect는 LogBar에 붙여 헤더는 고정·대사만 스크롤되게 한다.
        /// </summary>
        void EnsureLogScrollArea()
        {
            if (_logBar == null) return;
            if (_logScroll != null && _logContent != null && _logViewport != null) return;

            var root = _logBar.transform;
            var viewportT = root.Find("Viewport") as RectTransform;
            var contentT = root.Find("Viewport/Content") as RectTransform
                           ?? root.Find("Content") as RectTransform;

            // 예전 Prefab: Content가 곧 뷰포트 자리 → Viewport로 이름 바꾸고 자식 Content 생성
            if (viewportT == null && contentT != null && contentT.parent == root)
            {
                contentT.gameObject.name = "Viewport";
                viewportT = contentT;
                contentT = null;
            }

            if (viewportT == null)
            {
                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                viewportGo.transform.SetParent(root, false);
                viewportT = viewportGo.GetComponent<RectTransform>();
                SetAnchor(viewportT, 0.02f, 0.02f, 0.98f, 0.72f, 0, 0, 0, 0);
            }

            var viewportImg = viewportT.GetComponent<Image>();
            if (viewportImg == null) viewportImg = viewportT.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.01f); // 레이캐스트용(거의 투명)
            viewportImg.raycastTarget = true;

            if (viewportT.GetComponent<RectMask2D>() == null)
                viewportT.gameObject.AddComponent<RectMask2D>();

            if (contentT == null)
                contentT = viewportT.Find("Content") as RectTransform;
            if (contentT == null)
            {
                var contentGo = new GameObject("Content", typeof(RectTransform));
                contentGo.transform.SetParent(viewportT, false);
                contentT = contentGo.GetComponent<RectTransform>();
            }

            contentT.anchorMin = new Vector2(0f, 1f);
            contentT.anchorMax = new Vector2(1f, 1f);
            contentT.pivot = new Vector2(0.5f, 1f);
            contentT.anchoredPosition = Vector2.zero;
            contentT.sizeDelta = new Vector2(0f, 0f);

            _logViewport = viewportT;
            _logContent = contentT;

            _logScroll = _logBar.GetComponent<ScrollRect>();
            if (_logScroll == null) _logScroll = _logBar.AddComponent<ScrollRect>();
            _logScroll.viewport = _logViewport;
            _logScroll.content = _logContent;
            _logScroll.horizontal = false;
            _logScroll.vertical = true;
            _logScroll.movementType = ScrollRect.MovementType.Clamped;
            _logScroll.inertia = true;
            _logScroll.decelerationRate = 0.135f;
            _logScroll.scrollSensitivity = 40f;
            _logScroll.verticalScrollbar = null;
            _logScroll.horizontalScrollbar = null;
        }

        Image BuildLogHeaderPortrait(Transform parent, string spriteId, string displayLabel, bool left, out Text nameTextOut)
        {
            var go = new GameObject(left ? "LeftHeader" : "RightHeader", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(left ? 0.02f : 0.82f, 0.74f);
            rt.anchorMax = new Vector2(left ? 0.18f : 0.98f, 0.98f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.28f);
            iconRt.anchorMax = new Vector2(1f, 1f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var img = iconGo.GetComponent<Image>();
            img.raycastTarget = false;
            var sprite = PieceSpriteFor(spriteId);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = spriteId == "Imugi" ? new Color(0.25f, 0.55f, 0.85f) : new Color(0.9f, 0.85f, 0.75f);
            }

            var nameText = CreateText(go.transform, "Name", displayLabel, 18, TextAnchor.MiddleCenter);
            nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 0.28f);
            nameText.rectTransform.offsetMin = Vector2.zero;
            nameText.rectTransform.offsetMax = Vector2.zero;
            nameText.raycastTarget = false;
            nameTextOut = nameText;

            return img;
        }

        /// <summary>
        /// 이무기 초상 밑, 이름표보다 더 아래(로그바의 이무기 쪽 아래 공간)에 윷가락 4개를 숨겨둔다.
        /// 이름표 자리를 같이 쓰면 너무 좁아서(세로 30px 안팎) 막대가 뭉개져 네모로 보였다 —
        /// 로그바 안에서 따로 자리를 만들어 훨씬 넉넉하게 뒀다.
        /// </summary>
        void BuildOpponentMiniThrow(Transform logBar)
        {
            var container = new GameObject("MiniThrow", typeof(RectTransform));
            container.transform.SetParent(logBar, false);
            var rt = (RectTransform)container.transform;
            rt.anchorMin = new Vector2(0.02f, 0.48f);
            rt.anchorMax = new Vector2(0.2f, 0.7f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _miniThrowContainer = container;

            _miniThrowSticks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Stick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(container.transform, false);
                var srt = go.GetComponent<RectTransform>();
                float slotW = 1f / 4;
                srt.anchorMin = new Vector2(i * slotW + slotW * 0.12f, 0.05f);
                srt.anchorMax = new Vector2((i + 1) * slotW - slotW * 0.12f, 0.95f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                ApplyStickFace(img, front: true, isBaekdoStick: i == 0);
                _miniThrowSticks[i] = img;
            }
            container.SetActive(false);
        }

        /// <summary>
        /// 이무기가 던질 때, 보드 한가운데 큰 연출 대신 초상 밑 작은 자리에서 결과를 보여준다.
        /// 윷가락 4개가 잠깐 흔들리다 결과에 맞는 앞/뒷면을 드러낸다.
        /// </summary>
        public IEnumerator PlayOpponentMiniThrowAnim(YutThrowResult result)
        {
            EnsureBoard();
            if (_miniThrowContainer == null) yield break;

            var frontStates = DetermineFrontStates(result);
            _miniThrowContainer.SetActive(true);
            for (int i = 0; i < 4; i++)
                ApplyStickFace(_miniThrowSticks[i], front: true, isBaekdoStick: i == 0);

            const float shakeDuration = 0.35f;
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < 4; i++)
                {
                    var rt = _miniThrowSticks[i].rectTransform;
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin((Time.unscaledTime + i) * 28f) * 12f);
                }
                yield return null;
            }

            for (int i = 0; i < 4; i++)
            {
                ApplyStickFace(_miniThrowSticks[i], frontStates[i], isBaekdoStick: i == 0);
                _miniThrowSticks[i].rectTransform.localRotation = Quaternion.identity;
            }
            yield return new WaitForSecondsRealtime(0.5f);

            _miniThrowContainer.SetActive(false);
        }

        void EnsureRulesOverlay()
        {
            if (_rulesOverlay != null) return;

            var existing = transform.Find("RulesOverlay");
            if (existing != null)
            {
                _rulesOverlay = existing.gameObject;
                var closeBtn = existing.Find("Close")?.GetComponent<Button>();
                WireButton(closeBtn, () => ShowRulesOverlay(false));
                return;
            }

            _rulesOverlay = new GameObject("RulesOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _rulesOverlay.transform.SetParent(transform, false);
            SetAnchor((RectTransform)_rulesOverlay.transform, 0.14f, 0.32f, 0.86f, 0.64f, 0, 0, 0, 0);
            _rulesOverlay.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

            var rulesText = CreateText(_rulesOverlay.transform, "RulesText",
                "윷놀이 족보 (16분의)\n\n빽도 -1\n도 1\n개 2\n걸 3\n윷 4 (한 번 더)\n모 5 (한 번 더)",
                27, TextAnchor.MiddleCenter);
            SetAnchor(rulesText.rectTransform, 0.05f, 0.2f, 0.95f, 0.95f, 0, 0, 0, 0);

            var closeBtnNew = CreateButton(_rulesOverlay.transform, "Close", "닫기", () => ShowRulesOverlay(false));
            SetAnchor(closeBtnNew.GetComponent<RectTransform>(), 0.32f, 0.04f, 0.68f, 0.16f, 0, 0, 0, 0);

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
            _quadrants[(int)YutBoardQuadrant.North] =
                FindOrCreateQuadrant("Quadrant_North", 0.42f, 0.49f, 0.63f, 0.575f);
            _quadrants[(int)YutBoardQuadrant.West] =
                FindOrCreateQuadrant("Quadrant_West", 0.13f, 0.32f, 0.33f, 0.53f);
            _quadrants[(int)YutBoardQuadrant.East] =
                FindOrCreateQuadrant("Quadrant_East", 0.67f, 0.32f, 0.87f, 0.53f);
            _quadrants[(int)YutBoardQuadrant.South] =
                FindOrCreateQuadrant("Quadrant_South", 0.3f, 0.22f, 0.7f, 0.33f);
        }

        RectTransform FindOrCreateQuadrant(string name, float xmin, float ymin, float xmax, float ymax)
        {
            var existing = transform.Find(name) as RectTransform;
            if (existing != null) return existing;
            return CreateQuadrantContainer(name, xmin, ymin, xmax, ymax);
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
        /// power(0~1)는 슬라이드 던지기 속도 — 아치 높이·회전·착지 퍼짐만 키우고 줄인다.
        /// 결과(result)와는 무관(호출부가 이미 확률표로 정해서 넘겨준다).
        /// </summary>
        public IEnumerator PlayThrowAnim(YutThrowResult result, float power = 1f)
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
                routines[i] = StartCoroutine(ThrowOneStick(sticks[i], origin, ToLocal, i * 0.05f, frontStates[i], isBaekdoStick: i == 0, power));
            for (int i = 0; i < 4; i++)
                yield return routines[i];

            yield return new WaitForSecondsRealtime(0.35f);

            // 다음 던지기 전까지 방금 던진 윷을 보드 북쪽(North 구역)에 계속 보이게 둔다
            yield return ParkSticks(sticks, ToLocal);
            var thrownZone = GetQuadrant(YutBoardQuadrant.North);
            foreach (var rt in sticks)
                rt.SetParent(thrownZone, true);
            _parkedSticks = sticks;
        }

        // North 구역(0.42~0.63, 0.49~0.575) 안쪽에만 딱 맞게, 보드 노드와 겹치지 않게 촘촘히 배치
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
            bool targetFront, bool isBaekdoStick, float power)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            // power(슬라이드 속도, 0~1)가 클수록 더 멀리·높이·세게 날아간다 — 확률과는 무관, 연출 전용.
            float spreadX = Mathf.Lerp(0.08f, 0.24f, power);
            float yMin = Mathf.Lerp(0.3f, 0.34f, power);
            float yMax = Mathf.Lerp(0.36f, 0.58f, power);
            var landNorm = new Vector2(
                0.5f + UnityEngine.Random.Range(-spreadX, spreadX),
                UnityEngine.Random.Range(yMin, yMax));
            float arcHeight = Mathf.Lerp(90f, 280f, power) + UnityEngine.Random.Range(-15f, 15f);
            float spin = (Mathf.Lerp(480f, 1300f, power) + UnityEngine.Random.Range(-60f, 60f))
                * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            float duration = Mathf.Lerp(0.65f, 0.42f, power);

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
                text = CreateText(button.transform, "Label", label, 33, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
                text.raycastTarget = false;
            }
            text.text = label;
            text.font = ResolveFont();
            text.fontSize = UiFonts.Size(34);
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

            var text = CreateText(go.transform, "Label", label, 29, TextAnchor.MiddleCenter);
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
