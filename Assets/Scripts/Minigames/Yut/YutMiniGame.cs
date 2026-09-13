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
    /// 윷놀이 전체화면 UI. 보드·말·던지기 입력 표시와 이벤트를 담당하고,
    /// 규칙·재화는 호출부(YutScreen)가 처리한다.
    /// </summary>
    public class YutMiniGame : MonoBehaviour
    {
        /// <summary>
        /// 한글 표시용 폰트. 비워두면 유니티 기본 폰트로 나오는데, WebGL에선 한글 글리프가 없어서
        /// 글씨가 아예 안 보인다 — 호출부(YutScreen)가 프로젝트 한글 폰트(DOSGothic 등)를 넣어준다.
        /// </summary>
        public Font font;

        [Header("말 크기 (Inspector에서 조절)")]
        [Tooltip("보드 칸 한 변 대비 말 크기 비율. 기본 0.64")]
        [SerializeField, Range(0.2f, 1.2f)] float boardPieceFill = 0.64f;
        [Tooltip("South 대기말 — 슬롯 가로 여백 비율(슬롯 폭 대비). 키울수록 말 작아짐. 기본 0.1")]
        [SerializeField, Range(0f, 0.45f)] float waitingPieceSidePad = 0.1f;
        [Tooltip("South 대기말 — 위아래 inset(0~0.5). 키울수록 말 작아짐. 기본 0.05")]
        [SerializeField, Range(0f, 0.45f)] float waitingPieceVerticalInset = 0.05f;
        [Tooltip("한 칸에 업힌 말들의 가로 간격(말 폭 대비). 기본 0.62")]
        [SerializeField, Range(0.3f, 1f)] float stackedPieceSpread = 0.62f;

        /// <summary>탭이 아니라 아래→위 슬라이드로 던지기가 완료됐을 때. power(0~1)는 슬라이드
        /// 속도 기반 — 던지는 연출(아치 높이·회전·착지 퍼짐)에만 쓰고 결과 확률엔 영향 없다.</summary>
        public event Action<float> OnThrowPressed;
        public event Action OnLeavePressed;
        /// <summary>윷 토큰(하트) 옆 [+] 버튼 — YutScreen이 YutTokenShopPopup을 연다.</summary>
        public event Action OnBuyTokensPressed;
        /// <summary>FlashCandidates로 보드 위에 띄운 후보 중 하나를 유저가 탭했을 때 — 그 요괴 id와
        /// (갈림길 후보였다면) 지름길을 골랐는지 여부.</summary>
        public event Action<string, bool> OnCandidateTapped;

        Image[] _heartIcons;
        GameObject _throwZone;
        YutThrowSwipeZone _throwSwipe;
        Button _leaveButton;
        RectTransform _boardRoot;
        RectTransform _opponentPiece;
        Image[] _pads;
        RectTransform[] _parkedSticks;
        RectTransform[] _quadrants; // YutBoardQuadrant 순서대로
        readonly List<GameObject> _candidateMarkers = new();
        readonly List<(RectTransform rect, string pieceId, bool useShortcut, string[] stackMemberIds)> _candidateHits = new();
        GameObject _miniThrowContainer;
        Image[] _miniThrowSticks;

        // 대화 말풍선(각 요괴 말 근처)과 놀이기록(윷 토큰 옆 버튼으로 여는 팝업)은 분리 —
        // 말풍선은 타이머 없이 다음 액션(던지기/말 선택/턴 전환/화면 닫기)까지 유지한다.
        const string OpponentBubbleKey = "__opponent__";
        readonly Dictionary<string, GameObject> _activeBubbles = new();
        /// <summary>말풍선이 따라다닐 말 RectTransform — LateUpdate에서 위치를 맞춘다.</summary>
        readonly Dictionary<string, RectTransform> _bubbleAnchors = new();
        Button _playLogButton;
        GameObject _playLogPanel;
        RectTransform _playLogContent;
        RectTransform _playLogViewport;
        ScrollRect _playLogScroll;
        readonly List<string> _playLogEntries = new();
        const int MaxPlayLogEntries = 200;

        // 본게임 — 보유 요괴 전체를 동시에 말로 표시(id → 말 오브젝트/이니셜 라벨).
        readonly Dictionary<string, RectTransform> _yokaiPieces = new();
        readonly Dictionary<string, Text> _yokaiPieceLabels = new();
        /// <summary>Inspector에서 말 크기를 바꿀 때 Play 중에도 다시 깔 수 있게, 마지막 Show 입력을 기억한다.</summary>
        List<YokaiPieceInfo> _lastYokaiPieces;
        bool _suppressPieceSlide;

        RectTransform _rosterPanel;
        RectTransform _rosterRow;
        Text _rosterCaption;
        readonly List<RosterChip> _rosterChips = new();
        GameObject _summonSlotChip;

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

        // 우상단 한 줄: [기록] [+ 토큰구매] [하트(윷 토큰)] — 하트가 오른쪽 끝(HeartsRightEdge)에
        // 붙고, 그 왼쪽으로 +·기록 버튼이 이어진다. 세 자리 모두 여기 상수 하나로 관리한다.
        const float HeartsRightEdge = 0.93f;
        const float TokenPlusLeft = 0.673f;
        const float TokenPlusRight = 0.713f;
        const float PlayLogLeft = 0.533f;
        const float PlayLogRight = 0.663f;
        Button _tokenPlusButton;

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

            // 보드(_boardRoot) 아래쪽 가장자리 근처에 붙인다. Prefab에 있으면 그 레이아웃을 유지하고,
            // 없을 때만 아래 기본 좌표/색으로 새로 만든다.
            // ※ Prefab에 이미 있으면 여기 앵커 숫자는 절대 적용 안 됨 — 크기 조절은 Prefab/씬의
            //   ThrowSwipeZone RectTransform을 직접 고쳐야 한다.
            var rt = FindOrCreatePanel(transform, "ThrowSwipeZone", 0.1f, 0.08f, 0.9f, 0.30f,
                new Color(0.12f, 0.22f, 0.18f, 0.92f), out bool created);
            _throwZone = rt.gameObject;
            // 보드·로스터보다 뒤에 그려지면(형제 순서가 앞이면) 영역을 키워도 클릭/슬라이드를
            // 보드가 가로챈다 — 놀이기록 패널 바로 앞에 두어 입력 우선권을 확보한다.
            BringThrowZoneAboveBoard(rt);

            if (!created)
            {
                _throwSwipe = rt.GetComponent<YutThrowSwipeZone>();
                if (_throwSwipe == null)
                    _throwSwipe = _throwZone.AddComponent<YutThrowSwipeZone>();
                _throwSwipe.OnSwipeThrow -= ForwardSwipeThrow;
                _throwSwipe.OnSwipeThrow += ForwardSwipeThrow;
                if (rt.Find("IdleStick0") == null)
                    BuildIdleThrowSticks(rt);
                if (Application.isPlaying)
                {
                    var existingLabelRt = rt.Find("Label") as RectTransform;
                    if (existingLabelRt != null)
                        StartCoroutine(BounceHint(existingLabelRt));
                }
                return;
            }

            BuildIdleThrowSticks(_throwZone.transform);

            var label = CreateText(_throwZone.transform, "Label", "↑ 위로 슬라이드해서 던지기", 30, TextAnchor.LowerCenter);
            SetAnchor(label.rectTransform, 0f, 0f, 1f, 0.34f, 0, 0, 0, 0);
            label.raycastTarget = false;

            _throwSwipe = _throwZone.AddComponent<YutThrowSwipeZone>();
            _throwSwipe.OnSwipeThrow += ForwardSwipeThrow;

            if (Application.isPlaying)
                StartCoroutine(BounceHint(label.rectTransform));
        }

        /// <summary>ThrowSwipeZone을 YutBoard/Roster 위, PlayLog 아래에 둔다 — 영역을 키워도 입력이 먹히게.</summary>
        void BringThrowZoneAboveBoard(RectTransform throwRt)
        {
            if (throwRt == null) return;
            var playLog = transform.Find("PlayLogPanel");
            if (playLog != null)
                throwRt.SetSiblingIndex(playLog.GetSiblingIndex());
            else
                throwRt.SetAsLastSibling();
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
            ClearBubbles();
            ClearPlayLog();
        }

        public void SetThrowVisible(bool on)
        {
            if (_throwZone == null) return;
            _throwZone.SetActive(on);
        }

        public bool IsThrowVisible =>
            _throwZone != null && _throwZone.activeSelf && _throwZone.activeInHierarchy;

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

        /// <summary>플레이어 쪽 요괴 하나(pieceId) 말 위 대화 말풍선. 같은 말이면 교체,
        /// 다음 ClearBubbles(다음 액션)까지 유지. 놀이기록엔 안 남는다.
        /// 보드에 없으면 동(東) 완주 초상 위를 앵커로 쓴다(옥토끼 완주 후 해설용).</summary>
        public void ShowPieceBubble(string pieceId, string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pieceId)) return;
            if (_yokaiPieces.TryGetValue(pieceId, out var anchor) && anchor != null)
            {
                ShowBubbleAbove(pieceId, anchor, text);
                return;
            }
            if (_finishedPieceAnchors.TryGetValue(pieceId, out var finished) && finished != null)
                ShowBubbleAbove(pieceId, finished, text);
        }

        /// <summary>이무기 말 위 대화 말풍선. 다음 ClearBubbles까지 유지.</summary>
        public void ShowOpponentBubble(string text)
        {
            if (!string.IsNullOrEmpty(text) && _opponentPiece != null && _opponentPiece.gameObject.activeInHierarchy)
                ShowBubbleAbove(OpponentBubbleKey, _opponentPiece, text);
        }

        /// <summary>떠 있는 말풍선을 전부 지운다 — 던지기/말 선택/턴 전환 등 다음 액션 시점.</summary>
        public void ClearBubbles()
        {
            foreach (var kv in _activeBubbles)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            _activeBubbles.Clear();
            _bubbleAnchors.Clear();
        }

        void LateUpdate()
        {
            // 홉/슬라이드 중에도 이무기·요괴 대사가 말 위를 따라가게
            RepositionBubbles();
        }

        void RepositionBubbles()
        {
            if (_activeBubbles.Count == 0) return;
            foreach (var kv in _activeBubbles)
            {
                if (kv.Value == null) continue;
                if (!_bubbleAnchors.TryGetValue(kv.Key, out var anchor) || anchor == null || !anchor.gameObject.activeInHierarchy)
                    continue;
                var rt = (RectTransform)kv.Value.transform;
                rt.position = BubbleWorldPosAbove(anchor);
            }
        }

        static Vector3 BubbleWorldPosAbove(RectTransform anchor) =>
            anchor.position + new Vector3(0f, anchor.rect.height * 0.6f + 12f, 0f);

        void ShowBubbleAbove(string key, RectTransform anchor, string text)
        {
            if (_activeBubbles.TryGetValue(key, out var prev) && prev != null)
                Destroy(prev);
            _activeBubbles.Remove(key);
            _bubbleAnchors.Remove(key);

            var go = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            // anchor.parent(칸 하나)에 붙이면 그 칸 안에서만 맨 위로 와서, 옆 칸(형제 순서상 뒤에
            // 오는 칸)에 말풍선이 걸쳐 나올 때 그 칸의 말한테 가려졌다 — 최상위(transform)에
            // 붙여서 보드 위 어떤 칸·말보다도 항상 위에 그려지게 한다. 위치는 world position
            // (.position)으로 잡으므로 부모를 바꿔도 계산은 그대로 유효하다.
            rt.SetParent(transform, false);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220f, 56f);
            rt.position = BubbleWorldPosAbove(anchor);
            rt.SetAsLastSibling();
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.99f, 0.97f, 0.9f, 0.97f);
            bg.raycastTarget = false;

            var label = CreateText(rt, "Text", text, 20, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.color = new Color(0.15f, 0.12f, 0.08f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            _activeBubbles[key] = go;
            _bubbleAnchors[key] = anchor;
        }

        void EnsurePlayLogButton()
        {
            if (_playLogButton != null) return;

            var existing = transform.Find("PlayLogButton")?.GetComponent<Button>();
            if (existing != null)
            {
                _playLogButton = existing;
                WireButton(_playLogButton, TogglePlayLog);
                return;
            }

            var go = new GameObject("PlayLogButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            SetAnchor((RectTransform)go.transform, PlayLogLeft, 0.9f, PlayLogRight, 0.97f, 0, 0, 0, 0);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.22f, 0.19f, 0.14f, 0.9f);
            _playLogButton = go.AddComponent<Button>();
            _playLogButton.targetGraphic = bg;

            var label = CreateText(go.transform, "Label", "기록", 22, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;

            _playLogButton.onClick.AddListener(TogglePlayLog);
        }

        /// <summary>윷 토큰(하트) 바로 왼쪽의 [+] — 놀이판 안에서도 토큰을 충전/구매할 수 있게.
        /// 실제 구매 로직(엽전/광고)은 YutScreen이 여는 YutTokenShopPopup이 담당하고, 여기선
        /// 탭 이벤트만 올려보낸다(YutMiniGame은 재화를 모른다).</summary>
        void EnsureTokenPlusButton()
        {
            if (_tokenPlusButton != null) return;

            var existing = transform.Find("TokenPlusButton")?.GetComponent<Button>();
            if (existing != null)
            {
                _tokenPlusButton = existing;
                WireButton(_tokenPlusButton, () => OnBuyTokensPressed?.Invoke());
                return;
            }

            var go = new GameObject("TokenPlusButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            SetAnchor((RectTransform)go.transform, TokenPlusLeft, 0.9f, TokenPlusRight, 0.97f, 0, 0, 0, 0);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.3f, 0.5f, 0.45f, 0.95f);
            _tokenPlusButton = go.AddComponent<Button>();
            _tokenPlusButton.targetGraphic = bg;

            var label = CreateText(go.transform, "Label", "+", 26, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;

            _tokenPlusButton.onClick.AddListener(() => OnBuyTokensPressed?.Invoke());
        }

        void EnsurePlayLogPanel()
        {
            if (_playLogPanel != null) return;

            var existing = transform.Find("PlayLogPanel") as RectTransform;
            if (existing != null)
            {
                _playLogPanel = existing.gameObject;
                _playLogViewport = existing.Find("Viewport") as RectTransform;
                _playLogContent = _playLogViewport != null ? _playLogViewport.Find("Content") as RectTransform : null;
                _playLogScroll = existing.GetComponent<ScrollRect>();
                if (_playLogScroll == null && _playLogViewport != null && _playLogContent != null)
                {
                    _playLogScroll = existing.gameObject.AddComponent<ScrollRect>();
                    _playLogScroll.viewport = _playLogViewport;
                    _playLogScroll.content = _playLogContent;
                    _playLogScroll.horizontal = false;
                    _playLogScroll.vertical = true;
                    _playLogScroll.movementType = ScrollRect.MovementType.Clamped;
                }
                var closeBtn = existing.Find("Close")?.GetComponent<Button>();
                WireButton(closeBtn, () => ShowPlayLog(false));
                _playLogPanel.SetActive(false);
                return;
            }

            var go = new GameObject("PlayLogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            SetAnchor(rt, 0.1f, 0.25f, 0.9f, 0.72f, 0, 0, 0, 0);
            go.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.06f, 0.97f);
            _playLogPanel = go;

            var title = CreateText(rt, "Title", "놀이기록", 30, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 0.88f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.raycastTarget = false;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            viewportGo.transform.SetParent(rt, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            SetAnchor(viewportRt, 0.04f, 0.16f, 0.96f, 0.86f, 0, 0, 0, 0);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportRt, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            _playLogViewport = viewportRt;
            _playLogContent = contentRt;
            _playLogScroll = go.AddComponent<ScrollRect>();
            _playLogScroll.viewport = _playLogViewport;
            _playLogScroll.content = _playLogContent;
            _playLogScroll.horizontal = false;
            _playLogScroll.vertical = true;
            _playLogScroll.movementType = ScrollRect.MovementType.Clamped;

            var closeBtnNew = CreateButton(rt, "Close", "닫기", () => ShowPlayLog(false));
            SetAnchor(closeBtnNew.GetComponent<RectTransform>(), 0.32f, 0.02f, 0.68f, 0.14f, 0, 0, 0, 0);

            go.SetActive(false);
        }

        void TogglePlayLog()
        {
            EnsurePlayLogPanel();
            ShowPlayLog(!_playLogPanel.activeSelf);
        }

        public void ShowPlayLog(bool on)
        {
            EnsurePlayLogPanel();
            _playLogPanel.SetActive(on);
            if (on) RebuildPlayLogView();
        }

        /// <summary>대화 말풍선과는 별개로 쌓이는 짧은 사건형 문장 — 놀이기록 팝업에서만 보인다.</summary>
        public void AddPlayLogEntry(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _playLogEntries.Add(text);
            if (_playLogEntries.Count > MaxPlayLogEntries) _playLogEntries.RemoveAt(0);
            if (_playLogPanel != null && _playLogPanel.activeSelf) RebuildPlayLogView();
        }

        void RebuildPlayLogView()
        {
            if (_playLogContent == null) return;

            for (int i = _playLogContent.childCount - 1; i >= 0; i--)
                Destroy(_playLogContent.GetChild(i).gameObject);

            const float lineH = 40f;
            _playLogContent.sizeDelta = new Vector2(0f, _playLogEntries.Count * lineH + 8f);

            for (int i = 0; i < _playLogEntries.Count; i++)
            {
                var t = CreateText(_playLogContent, $"Line{i}", $"· {_playLogEntries[i]}", 22, TextAnchor.MiddleLeft);
                t.rectTransform.anchorMin = new Vector2(0f, 1f);
                t.rectTransform.anchorMax = new Vector2(1f, 1f);
                t.rectTransform.pivot = new Vector2(0.5f, 1f);
                t.rectTransform.anchoredPosition = new Vector2(0f, -i * lineH);
                t.rectTransform.sizeDelta = new Vector2(0f, lineH);
                t.raycastTarget = false;
            }

            Canvas.ForceUpdateCanvases();
            if (_playLogScroll != null)
                _playLogScroll.verticalNormalizedPosition = 0f; // 최신 줄(맨 아래)이 보이게
        }

        /// <summary>매치 시작/재입장 때 이전 놀이기록이 안 남게 비운다.</summary>
        public void ClearPlayLog()
        {
            _playLogEntries.Clear();
            if (_playLogContent == null) return;
            for (int i = _playLogContent.childCount - 1; i >= 0; i--)
                Destroy(_playLogContent.GetChild(i).gameObject);
            _playLogContent.sizeDelta = Vector2.zero;
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

            _lastYokaiPieces = new List<YokaiPieceInfo>(pieces.Count);
            for (int i = 0; i < pieces.Count; i++)
                _lastYokaiPieces.Add(pieces[i]);

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
            var byNode = new Dictionary<int, List<RectTransform>>();
            foreach (var info in pieces)
            {
                if (!_yokaiPieces.TryGetValue(info.Id, out var piece) || piece == null)
                {
                    piece = BuildYokaiPieceVisual(info.Id, info.DisplayName, out var label);
                    _yokaiPieces[info.Id] = piece;
                    _yokaiPieceLabels[info.Id] = label;
                }

                if (info.NodeId < 0)
                {
                    waiting.Add(piece); // 남(South) 구역에 한꺼번에 나란히 배치
                }
                else
                {
                    if (!byNode.TryGetValue(info.NodeId, out var list))
                    {
                        list = new List<RectTransform>();
                        byNode[info.NodeId] = list;
                    }
                    list.Add(piece);
                }
            }
            // 같은 칸에 업힌 말이 여럿이면 겹쳐 보이지 않게 그 칸 안에서 가로로 나란히 배치.
            foreach (var kv in byNode)
                PlaceGroupOnNode(kv.Value, kv.Key);
            LayoutWaitingPieces(waiting);
        }

        void OnValidate()
        {
            // Play 중 Inspector에서 말 크기 필드를 바꾸면 바로 다시 배치한다(슬라이드 연출은 생략).
            if (!Application.isPlaying || _lastYokaiPieces == null || _lastYokaiPieces.Count == 0) return;
            _suppressPieceSlide = true;
            try { ShowYokaiPieces(_lastYokaiPieces); }
            finally { _suppressPieceSlide = false; }
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
                label = CreateText(go.transform, "Label", InitialOf(displayName), 30, TextAnchor.MiddleCenter);
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
            float sidePad = Mathf.Clamp01(waitingPieceSidePad);
            float vInset = Mathf.Clamp(waitingPieceVerticalInset, 0f, 0.49f);
            for (int i = 0; i < count; i++)
            {
                var piece = pieces[i];
                Vector3 fromPos = piece.position;
                piece.SetParent(south, false);
                SetPieceDragEnabled(piece, false); // 대기말은 드래그로 후보에 올리지 않는다
                float slotW = 1f / count;
                float pad = slotW * sidePad;
                piece.anchorMin = new Vector2(i * slotW + pad, vInset);
                piece.anchorMax = new Vector2((i + 1) * slotW - pad, 1f - vInset);
                piece.offsetMin = Vector2.zero;
                piece.offsetMax = Vector2.zero;
                SlideIn(piece, fromPos);
            }
        }

        /// <summary>한 칸에 말이 한 마리면 예전처럼 칸 가운데 크게, 업혀서 여럿이면 칸 폭에
        /// 욱여넣어 작아지는 대신 평소 크기를 유지한 채 가로로 겹치며 퍼진다 — 칸 밖으로
        /// 넘쳐도 된다(마스크가 없어 잘리지 않는다).</summary>
        void PlaceGroupOnNode(List<RectTransform> pieces, int nodeId)
        {
            nodeId = Mathf.Clamp(nodeId, 0, _pads.Length - 1);
            var padRt = _pads[nodeId].rectTransform;
            int count = pieces.Count;
            float fill = Mathf.Max(0.01f, boardPieceFill);
            var pieceSize = new Vector2(padRt.rect.width * fill, padRt.rect.height * fill);

            for (int i = 0; i < count; i++)
            {
                var piece = pieces[i];
                Vector3 fromPos = piece.position;
                piece.SetParent(padRt, false);
                SetPieceDragEnabled(piece, true);

                piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0.5f);
                piece.pivot = new Vector2(0.5f, 0.5f);
                piece.sizeDelta = pieceSize;

                if (count <= 1)
                {
                    piece.anchoredPosition = Vector2.zero;
                }
                else
                {
                    float spreadStep = pieceSize.x * stackedPieceSpread; // 서로 살짝 겹치도록 한 칸보다 좁게
                    float centerOffset = (i - (count - 1) / 2f) * spreadStep;
                    piece.anchoredPosition = new Vector2(centerOffset, 0f);
                }

                SlideIn(piece, fromPos);
            }
        }

        static void SetPieceDragEnabled(RectTransform piece, bool on)
        {
            if (piece == null) return;
            var drag = piece.GetComponent<YutPieceDragHandle>();
            if (drag != null) drag.enabled = on;
        }

        /// <summary>
        /// 말이 순간이동하지 않고 눈에 보이게 미끄러지도록 한다 — 이미 새 위치로 배치된 rt.position을
        /// 목표로 잡고 fromPos에서 슬라이드한다. 던질 때마다("모→이동→윷→이동→도→이동") 실제로
        /// 움직이는 게 보여야 보너스 턴이 이어지는 게 자연스럽게 읽힌다.
        /// 칸 단위 홉(PlayHopAlongPath) 직후 재배치할 때는 SetSuppressPieceSlide로 끈다.
        /// </summary>
        void SlideIn(RectTransform rt, Vector3 fromPos)
        {
            if (_suppressPieceSlide) return;
            Vector3 toPos = rt.position;
            if ((toPos - fromPos).sqrMagnitude < 1f) return; // 실질적으로 제자리면 생략
            rt.position = fromPos;
            StartCoroutine(SlideRoutine(rt, fromPos, toPos));
        }

        /// <summary>홉 연출 직후 ShowYokaiPieces가 다시 미끄러지지 않게 끈다.</summary>
        public void SetSuppressPieceSlide(bool on) => _suppressPieceSlide = on;

        // 웹(yokai-yut-garden) animateMove와 동일: 칸당 170ms ease-out + 완주 페이드 150ms.
        // (웹 step-land는 border만 살짝 바뀌는데, 우리 칸은 Image 채움이라 색을 건드리면 티가 너무 나서 생략)
        const float HopDuration = 0.17f;
        const float HopFinishFade = 0.15f;

        /// <summary>선택/이무기 이동 전 — hopNodes 칸을 순서대로 밟는다. finishing이면 마지막에 페이드아웃.</summary>
        public IEnumerator PlayHopAlongPath(IReadOnlyList<string> pieceIds, IReadOnlyList<int> hopNodes, bool finishing)
        {
            EnsureBoard();
            if (pieceIds == null || pieceIds.Count == 0) yield break;

            var pieces = new List<RectTransform>(pieceIds.Count);
            for (int i = 0; i < pieceIds.Count; i++)
            {
                if (_yokaiPieces.TryGetValue(pieceIds[i], out var rt) && rt != null)
                    pieces.Add(rt);
            }
            if (pieces.Count == 0) yield break;

            yield return HopPiecesAlongPath(pieces, hopNodes, finishing);
        }

        /// <summary>이무기 말 한 개용 홉. 대기에서 첫 입장 전이면 호출부가 참에 미리 띄워 둔다.</summary>
        public IEnumerator PlayOpponentHopAlongPath(IReadOnlyList<int> hopNodes)
        {
            EnsureBoard();
            if (_opponentPiece == null || !_opponentPiece.gameObject.activeInHierarchy) yield break;
            var pieces = new List<RectTransform>(1) { _opponentPiece };
            yield return HopPiecesAlongPath(pieces, hopNodes, finishing: false);
        }

        IEnumerator HopPiecesAlongPath(List<RectTransform> pieces, IReadOnlyList<int> hopNodes, bool finishing)
        {
            // 말은 평소엔 칸(Node) 자식이라, 월드 좌표로 옮겨도 형제 칸·사분면 Image 뒤에 깔린다.
            // 홉 연출 동안만 화면 루트로 올려 항상 위에 보이게 한다.
            RaisePiecesForHop(pieces);

            bool landedOnEast = false;
            if (hopNodes != null)
            {
                for (int n = 0; n < hopNodes.Count; n++)
                {
                    int nodeId = hopNodes[n];
                    Vector3 target;
                    // 날 경우: 참먹이에 멈추지 않고 동(東) 완주 자리로 직행.
                    if (finishing && nodeId == YutBoardLayout.Start)
                    {
                        target = GetFinishWorldPosition();
                        landedOnEast = true;
                    }
                    else
                    {
                        if (_pads == null || nodeId < 0 || nodeId >= _pads.Length || _pads[nodeId] == null)
                            continue;
                        target = _pads[nodeId].rectTransform.position;
                    }

                    yield return HopPiecesTo(pieces, target);
                }
            }

            if (!finishing) yield break;

            // 이미 참에 서서 바로 날아가는 경우 등 — 홉에 참이 없으면 동으로 한 번 더.
            if (!landedOnEast)
                yield return HopPiecesTo(pieces, GetFinishWorldPosition());

            var images = new List<Image>(pieces.Count);
            var labels = new List<Text>(pieces.Count);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                images.Add(pieces[i].GetComponent<Image>());
                labels.Add(pieces[i].GetComponentInChildren<Text>());
            }

            float fade = 0f;
            while (fade < HopFinishFade)
            {
                fade += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(fade / HopFinishFade);
                for (int i = 0; i < images.Count; i++)
                {
                    if (images[i] != null)
                    {
                        var c = images[i].color;
                        c.a = a;
                        images[i].color = c;
                    }
                    if (i < labels.Count && labels[i] != null)
                    {
                        var c = labels[i].color;
                        c.a = a;
                        labels[i].color = c;
                    }
                }
                yield return null;
            }
        }

        /// <summary>홉 중 말이 칸·사분면에 가려지지 않게 화면 루트 맨 앞으로 올린다.</summary>
        void RaisePiecesForHop(List<RectTransform> pieces)
        {
            if (pieces == null) return;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece == null) continue;
                piece.SetParent(transform, worldPositionStays: true);
                piece.SetAsLastSibling();
            }
        }

        IEnumerator HopPiecesTo(List<RectTransform> pieces, Vector3 target)
        {
            var from = new Vector3[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
                from[i] = pieces[i] != null ? pieces[i].position : target;

            float t = 0f;
            while (t < HopDuration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / HopDuration);
                float eased = 1f - (1f - u) * (1f - u);
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (pieces[i] == null) continue;
                    pieces[i].position = Vector3.Lerp(from[i], target, eased);
                }
                yield return null;
            }
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null) pieces[i].position = target;

            yield return new WaitForSecondsRealtime(0.035f);
        }

        Vector3 GetFinishWorldPosition()
        {
            var anchor = GetFinishCandidateAnchor();
            return anchor != null ? anchor.position : Vector3.zero;
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

        /// <summary>FlashCandidates 그룹 키 — 완주(날 경우) 후보는 참(0)이 아니라 동 구역으로 묶는다.</summary>
        const int FinishCandidateSlot = -1;

        public readonly struct YokaiMoveCandidate
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly int DestinationNode;
            public readonly bool UseShortcut;
            /// <summary>이번 이동으로 완주(참을 지나 날아감)하는지. true면 후보·반짝임을 동(東)에 띄운다.</summary>
            public readonly bool WillFinish;
            /// <summary>업힌 말 전원(대표 Id 포함). 한 마리면 길이 1. 후보 칸에 전원 초상을 그릴 때 쓴다.</summary>
            public readonly string[] StackMemberIds;
            /// <summary>StackMemberIds와 같은 길이의 표시 이름(이니셜 폴백용).</summary>
            public readonly string[] StackDisplayNames;

            public YokaiMoveCandidate(
                string id,
                string displayName,
                int destinationNode,
                bool useShortcut,
                string[] stackMemberIds = null,
                string[] stackDisplayNames = null,
                bool willFinish = false)
            {
                Id = id;
                DisplayName = displayName;
                DestinationNode = destinationNode;
                UseShortcut = useShortcut;
                WillFinish = willFinish;
                if (stackMemberIds != null && stackMemberIds.Length > 0)
                    StackMemberIds = stackMemberIds;
                else
                    StackMemberIds = new[] { id };

                if (stackDisplayNames != null && stackDisplayNames.Length == StackMemberIds.Length)
                    StackDisplayNames = stackDisplayNames;
                else
                {
                    StackDisplayNames = new string[StackMemberIds.Length];
                    for (int i = 0; i < StackMemberIds.Length; i++)
                        StackDisplayNames[i] = StackMemberIds[i] == id ? displayName : StackMemberIds[i];
                }
            }
        }

        // 한 칸에 후보가 여럿(대기 말 여럿이 같은 결과로 동시 입장 가능 등) 겹칠 때, 그 칸을
        // 중심으로 위/아래/왼쪽/오른쪽에 하나씩 붙여서 보여준다 — 팀이 최대 4마리라 방향 4개면 충분.
        static readonly Vector2[] CrossDirs = { new(0, 1), new(0, -1), new(-1, 0), new(1, 0) };

        /// <summary>던진 결과로 움직일 수 있는 말 후보를 보드 위에 직접 보여준다. 후보가 한 마리면
        /// 그 칸 위에 바로, 여럿이 같은 칸으로 겹치면 그 칸 둘레(상하좌우)에 하나씩 붙여서 —
        /// 다이얼로그 없이 보드만 보고 원하는 말을 탭해서 고르게 한다.
        /// 업힌 스택이면 대표 한 마리가 아니라 스택 전원 초상을 칸에 나란히 그린다.</summary>
        public void FlashCandidates(IReadOnlyList<YokaiMoveCandidate> candidates)
        {
            ClearCandidates();
            EnsureBoard();
            if (_pads == null || _pads.Length == 0 || candidates == null || candidates.Count == 0) return;

            var byNode = new Dictionary<int, List<YokaiMoveCandidate>>();
            foreach (var c in candidates)
            {
                // 날 경우(완주)는 참먹이가 아니라 동 구역을 도착지로 보여준다.
                int node = c.WillFinish
                    ? FinishCandidateSlot
                    : Mathf.Clamp(c.DestinationNode, 0, _pads.Length - 1);
                if (!byNode.TryGetValue(node, out var list))
                {
                    list = new List<YokaiMoveCandidate>();
                    byNode[node] = list;
                }
                list.Add(c);
            }

            foreach (var kv in byNode)
            {
                if (kv.Key == FinishCandidateSlot)
                {
                    if (kv.Value.Count == 1)
                        _candidateMarkers.Add(BuildCandidateOnEast(kv.Value[0]));
                    else
                        _candidateMarkers.AddRange(BuildCandidateCrossOnEast(kv.Value));
                    HighlightEastFinish();
                    continue;
                }

                if (kv.Value.Count == 1)
                    _candidateMarkers.Add(BuildCandidateOnNode(kv.Key, kv.Value[0]));
                else
                    _candidateMarkers.AddRange(BuildCandidateCross(kv.Key, kv.Value));
                HighlightPad(kv.Key);
            }
        }

        /// <summary>FlashCandidates로 띄운 후보 마커를 전부 지운다.</summary>
        public void ClearCandidates()
        {
            foreach (var go in _candidateMarkers)
                if (go != null) Destroy(go);
            _candidateMarkers.Clear();
            _candidateHits.Clear();
            ClearPadHighlights();
        }

        readonly Dictionary<int, Color> _highlightedPadOriginals = new();
        Image _eastFinishHighlight;
        Color _eastFinishHighlightOriginal;
        bool _eastFinishHighlightActive;
        bool _padHighlightRunning;

        void HighlightPad(int nodeId)
        {
            if (_pads == null || nodeId < 0 || nodeId >= _pads.Length || _pads[nodeId] == null) return;
            if (!_highlightedPadOriginals.ContainsKey(nodeId))
                _highlightedPadOriginals[nodeId] = _pads[nodeId].color;
            EnsurePadHighlightRoutine();
        }

        /// <summary>완주(날 경우) 도착지 — 동 구역 하단 절반(완주 말 자리)만 노란빛으로 펄스.</summary>
        void HighlightEastFinish()
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureFinishedPiecesRoot(east);
            var img = ((RectTransform)_finishedPiecesRoot).GetComponent<Image>();
            if (img == null) return;
            if (!_eastFinishHighlightActive)
            {
                _eastFinishHighlight = img;
                _eastFinishHighlightOriginal = img.color;
                _eastFinishHighlightActive = true;
            }
            EnsurePadHighlightRoutine();
        }

        void EnsurePadHighlightRoutine()
        {
            if (!_padHighlightRunning)
                StartCoroutine(PadHighlightRoutine());
        }

        void ClearPadHighlights()
        {
            foreach (var kv in _highlightedPadOriginals)
                if (_pads != null && kv.Key < _pads.Length && _pads[kv.Key] != null)
                    _pads[kv.Key].color = kv.Value;
            _highlightedPadOriginals.Clear();

            if (_eastFinishHighlightActive && _eastFinishHighlight != null)
                _eastFinishHighlight.color = _eastFinishHighlightOriginal;
            _eastFinishHighlightActive = false;
            _eastFinishHighlight = null;
        }

        /// <summary>갈 수 있는 칸(또는 완주 시 동 구역)을 노란빛으로 은은하게 펄스시킨다.</summary>
        IEnumerator PadHighlightRoutine()
        {
            _padHighlightRunning = true;
            var glow = new Color(1f, 0.92f, 0.35f, 1f);
            var eastGlow = new Color(1f, 0.92f, 0.35f, 0.55f);
            while (_highlightedPadOriginals.Count > 0 || _eastFinishHighlightActive)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
                foreach (var nodeId in _highlightedPadOriginals.Keys)
                {
                    if (_pads == null || nodeId >= _pads.Length || _pads[nodeId] == null) continue;
                    _pads[nodeId].color = Color.Lerp(_highlightedPadOriginals[nodeId], glow, pulse);
                }
                if (_eastFinishHighlightActive && _eastFinishHighlight != null)
                    _eastFinishHighlight.color = Color.Lerp(_eastFinishHighlightOriginal, eastGlow, pulse);
                yield return null;
            }
            _padHighlightRunning = false;
        }

        /// <summary>
        /// 말 아이콘을 드래그해서 놓았을 때(YutPieceDragHandle) 호출된다. 지금 보드 위에 떠 있는
        /// 후보 마커 중 이 말 것이면서 놓은 지점과 겹치는 게 있으면 그 후보를 고른 것으로 처리한다
        /// (탭했을 때와 동일하게 OnCandidateTapped를 쏨). 겹치는 후보가 없으면 조용히 무시.
        /// 업힌 스택이면 어느 멤버를 끌어도 그 스택 후보로 인정한다.
        /// </summary>
        public void ResolveDrop(string pieceId, Vector2 screenPos)
        {
            foreach (var hit in _candidateHits)
            {
                if (hit.rect == null) continue;
                if (!CandidateHitMatches(hit, pieceId)) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(hit.rect, screenPos, null))
                {
                    OnCandidateTapped?.Invoke(hit.pieceId, hit.useShortcut);
                    return;
                }
            }
        }

        static bool CandidateHitMatches((RectTransform rect, string pieceId, bool useShortcut, string[] stackMemberIds) hit, string pieceId)
        {
            if (hit.pieceId == pieceId) return true;
            if (hit.stackMemberIds == null) return false;
            for (int i = 0; i < hit.stackMemberIds.Length; i++)
                if (hit.stackMemberIds[i] == pieceId) return true;
            return false;
        }

        GameObject BuildCandidateOnNode(int nodeId, YokaiMoveCandidate candidate)
        {
            var go = new GameObject($"Candidate_{nodeId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_pads[nodeId].transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.05f);
            rt.anchorMax = new Vector2(0.95f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var hitImg = go.GetComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0.01f); // 투명에 가깝지만 Button 레이캐스트용
            var padSize = _pads[nodeId].rectTransform.rect.size;
            FillCandidateStackVisuals(rt, candidate, new Vector2(padSize.x * 0.9f, padSize.y * 0.9f));
            WireCandidateButton(go, hitImg, candidate.Id, candidate.UseShortcut, candidate.StackMemberIds);
            StartCoroutine(PulseScale(rt));
            return go;
        }

        /// <summary>완주(날 경우) 후보 — 참먹이 대신 동(東) 완주 자리 위에 띄운다.</summary>
        GameObject BuildCandidateOnEast(YokaiMoveCandidate candidate)
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureFinishedPiecesRoot(east);
            var go = new GameObject("Candidate_Finish", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            // FinishedPieces는 HorizontalLayoutGroup이라 후보 마커 부모로 쓰면 배치가 깨진다.
            go.transform.SetParent(east, false);
            var rt = go.GetComponent<RectTransform>();
            // 동 하단 절반(완주 말 자리)에만 후보를 올린다.
            rt.anchorMin = new Vector2(0.05f, 0f);
            rt.anchorMax = new Vector2(0.95f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var hitImg = go.GetComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0.01f);
            var area = rt.rect.size;
            if (area.x < 1f) area = new Vector2(72f, 40f);
            FillCandidateStackVisuals(rt, candidate, new Vector2(area.x * 0.9f, area.y * 0.9f));
            WireCandidateButton(go, hitImg, candidate.Id, candidate.UseShortcut, candidate.StackMemberIds);
            StartCoroutine(PulseScale(rt));
            return go;
        }

        List<GameObject> BuildCandidateCrossOnEast(List<YokaiMoveCandidate> group)
        {
            var result = new List<GameObject>();
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureFinishedPiecesRoot(east);
            var area = ((RectTransform)_finishedPiecesRoot).rect.size;
            if (area.x < 1f) area = new Vector2(64f, 40f);
            float step = Mathf.Max(area.x, 36f) * 0.7f;

            for (int i = 0; i < group.Count && i < CrossDirs.Length; i++)
            {
                var candidate = group[i];
                var dir = CrossDirs[i];
                var go = new GameObject($"Candidate_Finish_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(east, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.25f);
                rt.sizeDelta = area;
                rt.anchoredPosition = dir * step;
                var hitImg = go.GetComponent<Image>();
                hitImg.color = new Color(1f, 1f, 1f, 0.01f);
                FillCandidateStackVisuals(rt, candidate, area);
                WireCandidateButton(go, hitImg, candidate.Id, candidate.UseShortcut, candidate.StackMemberIds);
                StartCoroutine(PulseScale(rt));
                result.Add(go);
            }
            return result;
        }

        /// <summary>완주 홉 착지용 — 동 구역 하단(FinishedPieces) 월드 좌표.</summary>
        RectTransform GetFinishCandidateAnchor()
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureFinishedPiecesRoot(east);
            return (RectTransform)_finishedPiecesRoot;
        }

        /// <summary>경합 밭(같은 칸으로 갈 수 있는 후보 2마리 이상)을 홀드 없이 처음부터 사방에
        /// 펼쳐서 보여준다 — 각 아이콘은 탭도 되고(WireCandidateButton) 말을 드래그해서 놓아도
        /// 된다(ResolveDrop이 _candidateHits로 판정). 업힌 스택이면 그 방향에 전원 초상.</summary>
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
                var hitImg = go.GetComponent<Image>();
                hitImg.color = new Color(1f, 1f, 1f, 0.01f);
                FillCandidateStackVisuals(rt, candidate, pad.rect.size);
                WireCandidateButton(go, hitImg, candidate.Id, candidate.UseShortcut, candidate.StackMemberIds);
                StartCoroutine(PulseScale(rt));
                result.Add(go);
            }
            return result;
        }

        /// <summary>후보 마커 안에 스택 멤버 초상을 가로로 나란히 깐다(보드 말 PlaceGroupOnNode와 같은 간격 감각).</summary>
        void FillCandidateStackVisuals(RectTransform parent, YokaiMoveCandidate candidate, Vector2 areaSize)
        {
            var members = candidate.StackMemberIds;
            var names = candidate.StackDisplayNames;
            int count = members != null ? members.Length : 1;
            if (count <= 0) count = 1;

            float fill = Mathf.Max(0.01f, boardPieceFill);
            float areaW = areaSize.x > 1f ? areaSize.x : 64f;
            float areaH = areaSize.y > 1f ? areaSize.y : 64f;
            var pieceSize = new Vector2(areaW * fill, areaH * fill);

            for (int i = 0; i < count; i++)
            {
                string memberId = members != null && i < members.Length ? members[i] : candidate.Id;
                string name = names != null && i < names.Length ? names[i] : candidate.DisplayName;

                var child = new GameObject($"Stack_{memberId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(parent, false);
                var rt = child.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = pieceSize;

                if (count <= 1)
                {
                    rt.anchoredPosition = Vector2.zero;
                }
                else
                {
                    float spreadStep = pieceSize.x * stackedPieceSpread;
                    float centerOffset = (i - (count - 1) / 2f) * spreadStep;
                    rt.anchoredPosition = new Vector2(centerOffset, 0f);
                }

                var img = child.GetComponent<Image>();
                img.raycastTarget = false;
                ApplyCandidateMemberVisual(img, memberId, name);
            }
        }

        void ApplyCandidateMemberVisual(Image img, string pieceId, string displayName)
        {
            var sprite = PieceSpriteFor(pieceId);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = ColorForYokai(pieceId);
                var label = CreateText(img.transform, "Label", InitialOf(displayName), 28, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.raycastTarget = false;
            }
        }

        void WireCandidateButton(GameObject go, Image img, string tappedId, bool useShortcut, string[] stackMemberIds)
        {
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnCandidateTapped?.Invoke(tappedId, useShortcut));
            _candidateHits.Add((go.GetComponent<RectTransform>(), tappedId, useShortcut, stackMemberIds));
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

        static bool IsWaypoint(int nodeId) =>
            nodeId == YutBoardLayout.Start || nodeId == YutBoardLayout.Mo ||
            nodeId == YutBoardLayout.DwitMo || nodeId == YutBoardLayout.JjiMo ||
            nodeId == YutBoardLayout.Bang;

        /// <summary>Bake/Prefab에 이미 있는 자식이면 레이아웃(앵커·색)은 건드리지 않고 그대로 쓴다.
        /// 없을 때만 코드 기본값으로 새로 만든다 — Prefab이 레이아웃 최종 소스.</summary>
        RectTransform FindOrCreatePanel(Transform parent, string name, float xmin, float ymin, float xmax, float ymax,
            Color color, out bool created)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                created = false;
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchor(rt, xmin, ymin, xmax, ymax, 0, 0, 0, 0);
            rt.GetComponent<Image>().color = color;
            created = true;
            return rt;
        }

        void EnsureBoard()
        {
            if (_boardRoot != null && _pads != null) return;

            _boardRoot = FindOrCreatePanel(transform, "YutBoard", 0.1f, 0.2f, 0.9f, 0.65f,
                new Color(0.12f, 0.22f, 0.18f, 0.92f), out _);

            BindOrCreateHearts();
            EnsureTokenPlusButton();
            if (!TryBindPads())
                CreatePads();
            BindOrCreateOpponentPiece();

            EnsureQuadrants();
            EnsureOpponentMiniThrowPanel();
            EnsureRosterPanel();
            EnsurePlayLogButton();
            EnsurePlayLogPanel();
        }

        /// <summary>
        /// 화면 최하단(y 0~0.13) — 참가 요괴 전체를 초상화·이름·기력(♦)·친밀도(♡)·보드 위치와 함께
        /// 한 줄로 보여준다. ThrowSwipeZone(대략 0.08~0.30)과 겹칠 수 있어 입력은
        /// ThrowSwipeZone이 보드/로스터보다 위 형제로 올라가게 한다.
        /// </summary>
        void EnsureRosterPanel()
        {
            if (_rosterPanel != null) return;

            _rosterPanel = FindOrCreatePanel(transform, "RosterPanel", 0.02f, 0f, 0.98f, 0.13f,
                new Color(0.08f, 0.08f, 0.07f, 0.85f), out _);

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
                captionText = CreateText(_rosterPanel, "Caption", "", 26, TextAnchor.MiddleCenter);
                SetAnchor(captionText.rectTransform, 0f, 0f, 1f, 0.3f, 0, 0, 0, 0);
                captionText.color = new Color(0.85f, 0.8f, 0.7f, 0.85f);
                captionText.raycastTarget = false;
            }
            _rosterCaption = captionText;
        }

        /// <summary>참가 요괴 명단을 최신 상태로 다시 그린다. 인원 수가 바뀔 때만 칩을 새로 만들고,
        /// 그 외엔 이미 만든 칩의 초상·이름·스탯·상태 텍스트만 갱신한다.
        /// showExtraSlot이 true면 맨 끝에 빈 슬롯을 하나 더 붙인다 — 고라니 미소환이면
        /// "소환하기", 소환은 됐지만 아직 넋이라 말로 못 쓰면 "진화 필요"(탭하면 YutScreen이
        /// 진화 확인 다이얼로그를 띄운다). 키우는/쓸 수 있는 요괴 수만큼만 말을 쓸 수 있다는 걸
        /// 그 자리에서 바로 안내하기 위함.</summary>
        public void ShowRoster(IReadOnlyList<RosterEntry> entries, bool showExtraSlot, string extraSlotLabel,
            Action onExtraSlotTapped, Sprite extraSlotIcon = null)
        {
            EnsureBoard();
            if (_rosterRow == null || entries == null) return;

            int totalSlots = Mathf.Max(1, entries.Count + (showExtraSlot ? 1 : 0));

            if (_rosterChips.Count != entries.Count)
            {
                foreach (var chip in _rosterChips)
                    if (chip.Root != null) Destroy(chip.Root.gameObject);
                _rosterChips.Clear();

                for (int i = 0; i < entries.Count; i++)
                    _rosterChips.Add(BuildRosterChip(_rosterRow, i, totalSlots));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var chip = _rosterChips[i];
                RepositionRosterSlot(chip.Root, i, totalSlots);

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
                chip.Stats.text = $"♡{entry.Intimacy}"; // 기력은 윷판에서 안 보여줘도 된다는 요청으로 제외
                chip.Status.text = entry.StatusLabel;
            }

            _rosterCaption.text = "이동한 요괴마다 친밀도 +0.25";

            if (showExtraSlot)
            {
                if (_summonSlotChip == null) _summonSlotChip = BuildSummonSlotChip(_rosterRow);
                RepositionRosterSlot(_summonSlotChip.GetComponent<RectTransform>(), entries.Count, totalSlots);
                _summonSlotChip.SetActive(true);
                var labelText = _summonSlotChip.transform.Find("Label")?.GetComponent<Text>();
                if (labelText != null) labelText.text = extraSlotLabel ?? "";

                // 진화 필요(넋을 소환은 했지만 아직 말로 못 쓰는) 상태면 "+" 대신 넋 아이콘을 계속
                // 보여준다 — 이미 뭔가 소환돼 있는데 빈 슬롯처럼 "+"만 보이면 헷갈린다는 피드백.
                var plusText = _summonSlotChip.transform.Find("Plus")?.GetComponent<Text>();
                var iconImg = _summonSlotChip.transform.Find("Icon")?.GetComponent<Image>();
                bool showIcon = extraSlotIcon != null;
                if (plusText != null) plusText.gameObject.SetActive(!showIcon);
                if (iconImg != null)
                {
                    iconImg.gameObject.SetActive(showIcon);
                    if (showIcon)
                    {
                        iconImg.sprite = extraSlotIcon;
                        iconImg.preserveAspect = true;
                    }
                }

                var btn = _summonSlotChip.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                if (onExtraSlotTapped != null)
                    btn.onClick.AddListener(() => onExtraSlotTapped());
            }
            else if (_summonSlotChip != null)
            {
                _summonSlotChip.SetActive(false);
            }
        }

        /// <summary>키우는 중이 아니거나(소환 전) 아직 말로 못 쓰는(넋) 요괴 자리 — 라벨/탭 동작은
        /// YutScreen이 매번 넘겨준다(YutMiniGame은 소환·진화 로직을 모른다).</summary>
        GameObject BuildSummonSlotChip(RectTransform parent)
        {
            var go = new GameObject("SummonSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.3f, 0.3f, 0.28f, 0.4f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;

            var plus = CreateText(rt, "Plus", "+", 35, TextAnchor.MiddleCenter);
            plus.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            plus.rectTransform.anchorMax = new Vector2(1f, 1f);
            plus.rectTransform.offsetMin = plus.rectTransform.offsetMax = Vector2.zero;
            plus.color = new Color(0.85f, 0.8f, 0.7f, 0.9f);
            plus.raycastTarget = false;

            // 진화 필요 상태일 때 "+" 대신 켜지는 넋 아이콘 — ShowRoster가 필요할 때만 활성화한다.
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(rt, false);
            iconRt.anchorMin = new Vector2(0f, 0.42f);
            iconRt.anchorMax = new Vector2(1f, 1f);
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            iconGo.SetActive(false);

            var label = CreateText(rt, "Label", "", 22, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.color = new Color(0.85f, 0.8f, 0.7f, 0.9f);
            label.raycastTarget = false;

            return go;
        }

        /// <summary>소환 연출용 — 로스터 "소환하기" 슬롯의 현재 월드 위치(없으면 null).
        /// YutScreen이 암전 연출 중 그 자리로 넋 아이콘을 떨어뜨리는 데 쓴다.</summary>
        public Vector3? GetSummonSlotWorldPosition()
        {
            if (_summonSlotChip == null || !_summonSlotChip.activeSelf) return null;
            return _summonSlotChip.GetComponent<RectTransform>().position;
        }

        /// <summary>소환 연출용 — 넋(도깨비불) 아이콘.</summary>
        public Sprite GetNeokSprite() => CharacterSpawner.NeokFlameSprite();

        /// <summary>로스터 줄에서 index/count번째 칸 위치로 앵커한다 — 요괴 칩과 소환 슬롯 칩이
        /// 같은 규칙으로 나란히 놓이게 공용으로 쓴다.</summary>
        static void RepositionRosterSlot(RectTransform rt, int index, int count)
        {
            float slotW = 1f / count;
            const float pad = 0.03f;
            rt.anchorMin = new Vector2(index * slotW + pad, 0f);
            rt.anchorMax = new Vector2((index + 1) * slotW - pad, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        RosterChip BuildRosterChip(RectTransform parent, int index, int count)
        {
            var go = new GameObject($"Chip{index}", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            RepositionRosterSlot(rt, index, count);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            // 위에서부터 고정 픽셀로 쌓는다(초상 60px → 이름 22px → 스탯 20px → 상태 18px) —
            // Row 실제 높이와 상관없이 서로 겹치지 않게.
            const float portraitSize = 66f, nameH = 32f, statsH = 30f, statusH = 28f, gap = 3f;

            var portraitRt = (RectTransform)portraitGo.transform;
            portraitRt.SetParent(rt, false);
            portraitRt.anchorMin = portraitRt.anchorMax = new Vector2(0.5f, 1f);
            portraitRt.pivot = new Vector2(0.5f, 1f);
            portraitRt.sizeDelta = new Vector2(portraitSize, portraitSize);
            portraitRt.anchoredPosition = new Vector2(0f, 0f);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;

            float y = portraitSize + gap;
            var nameText = CreateText(rt, "Name", "", 28, TextAnchor.MiddleCenter);
            AnchorTopStrip(nameText.rectTransform, y, nameH);
            nameText.raycastTarget = false;
            y += nameH + gap;

            var statsText = CreateText(rt, "Stats", "", 25, TextAnchor.MiddleCenter);
            AnchorTopStrip(statsText.rectTransform, y, statsH);
            statsText.color = new Color(0.8f, 0.9f, 0.95f);
            statsText.raycastTarget = false;
            y += statsH + gap;

            var statusText = CreateText(rt, "Status", "", 24, TextAnchor.MiddleCenter);
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

        /// <summary>하트(윷 토큰) — Prefab에 있으면 그 자리를 유지하고, 없을 때만 코드 기본 좌표로 만든다.</summary>
        void BindOrCreateHearts()
        {
            if (_heartIcons != null) return;

            const int heartCount = 5 /* 구 KSpirits.Core.GameConstants.HeartMax, 새 설계에 맞게 나중에 재조정 */;
            _heartIcons = new Image[heartCount];
            bool foundAll = true;
            for (int i = 0; i < heartCount; i++)
            {
                var t = transform.Find($"Heart{i}");
                if (t == null) { foundAll = false; break; }
                _heartIcons[i] = t.GetComponent<Image>();
            }

            if (foundAll)
                return;

            _heartIcons = new Image[heartCount];
            const float iconW = 0.035f;
            const float gap = 0.008f;
            float totalW = heartCount * iconW + (heartCount - 1) * gap;
            float startX = HeartsRightEdge - totalW;
            for (int i = 0; i < heartCount; i++)
            {
                var heartGo = new GameObject($"Heart{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                heartGo.transform.SetParent(transform, false);
                _heartIcons[i] = heartGo.GetComponent<Image>();
                float x = startX + i * (iconW + gap);
                SetAnchor(_heartIcons[i].rectTransform, x, 0.9f, x + iconW, 0.97f, 0, 0, 0, 0);
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

            // Prefab에 구운 보드 칸 위치는 유지하고, 특수 칸 색만 매치 규칙에 맞게 다시 칠한다.
            for (int i = 0; i < _pads.Length; i++)
                RecolorPad(_pads[i], i);
            return true;
        }

        static void RecolorPad(Image pad, int nodeId)
        {
            if (pad == null) return;
            pad.color = YutBoardLayout.IsSpecialReward(nodeId)
                ? new Color(0.35f, 0.55f, 0.85f, 0.9f) // 특수 칸 — 엽전/공양물/보물상자/정화수(YutBoardLayout.SpecialSquareKind)
                : IsWaypoint(nodeId)
                    ? new Color(0.7f, 0.55f, 0.3f, 0.85f)
                    : new Color(0.35f, 0.32f, 0.28f, 0.9f);
        }

        /// <summary>매 윷판(매치)마다 새로 뽑히는 특수 칸 구성을 반영한다 — 색부터 다시 칠하고
        /// (EnsureBoard는 세션당 한 번만 돌아서 두 번째 매치부터는 갱신 안 됨), 칸마다 어떤
        /// 보상인지 보여줄 아이콘(엽전/그 칸에 배정된 공양물/보물상자)을 붙인다. 보물상자는
        /// 안이 뭔지 숨기고 상자 아이콘만 보여준다(호출부가 그렇게 넘겨준다).</summary>
        public void RefreshSpecialSquareVisuals(IReadOnlyDictionary<int, Sprite> iconsByNode)
        {
            EnsureBoard();
            if (_pads == null) return;
            for (int i = 0; i < _pads.Length; i++)
            {
                RecolorPad(_pads[i], i);
                Sprite icon = iconsByNode != null && iconsByNode.TryGetValue(i, out var s) ? s : null;
                SetSpecialSquareIcon(i, icon);
            }
        }

        void SetSpecialSquareIcon(int nodeId, Sprite icon)
        {
            if (_pads == null || nodeId < 0 || nodeId >= _pads.Length || _pads[nodeId] == null) return;
            var pad = _pads[nodeId];

            var iconT = pad.transform.Find("SpecialIcon");
            Image iconImg;
            if (iconT == null)
            {
                var go = new GameObject("SpecialIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(pad.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.12f, 0.12f);
                rt.anchorMax = new Vector2(0.88f, 0.88f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                iconImg = go.GetComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            else
            {
                iconImg = iconT.GetComponent<Image>();
            }

            iconImg.sprite = icon;
            iconImg.color = Color.white;
            iconImg.enabled = icon != null;
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
                RecolorPad(padImg, i);
                _pads[i] = padImg;
            }
        }

        void BindOrCreateOpponentPiece()
        {
            if (_pads == null || _pads.Length == 0) return;
            if (_opponentPiece != null) return;

            var opponentT = _pads[0].transform.Find("ImugiPiece");
            if (opponentT != null)
            {
                _opponentPiece = opponentT as RectTransform;
                return;
            }

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

        /// <summary>
        /// 대화가 전부 말풍선(피스 위)으로 옮겨가면서 예전 LogBar(대화 스크롤창)는 없앴다 — 이무기
        /// 던지기 결과만 보여주던 미니 윷가락은 이 작은 자리 하나로 옮겨서 그대로 유지한다.
        /// 던질 때만 켜지고 평소엔 꺼져 있다.
        /// </summary>
        void EnsureOpponentMiniThrowPanel()
        {
            if (_miniThrowContainer != null) return;

            var rt = FindOrCreatePanel(transform, "OpponentMiniThrow", 0.06f, 0.8f, 0.26f, 0.9f,
                new Color(0.08f, 0.1f, 0.16f, 0.92f), out bool created);
            _miniThrowContainer = rt.gameObject;

            if (!created)
            {
                _miniThrowSticks = new Image[4];
                for (int i = 0; i < 4; i++)
                    _miniThrowSticks[i] = rt.Find($"Stick{i}")?.GetComponent<Image>();
                return;
            }

            _miniThrowSticks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Stick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(rt, false);
                var srt = go.GetComponent<RectTransform>();
                float slotW = 1f / 4;
                srt.anchorMin = new Vector2(i * slotW + slotW * 0.12f, 0.1f);
                srt.anchorMax = new Vector2((i + 1) * slotW - slotW * 0.12f, 0.9f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                ApplyStickFace(img, front: true, isBaekdoStick: i == 0);
                _miniThrowSticks[i] = img;
            }
            _miniThrowContainer.SetActive(false);
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

        /// <summary>
        /// 두 대각선이 나누는 4개 삼각형 구역의 컨테이너를 만든다. 아직 내용은 비어 있고,
        /// 각 구역을 담당할 기능이 GetQuadrant()로 받아서 자기 UI를 채워 넣는 자리(베이스)다.
        /// </summary>
        void EnsureQuadrants()
        {
            if (_quadrants != null) return;

            _quadrants = new RectTransform[3];
            _quadrants[(int)YutBoardQuadrant.North] =
                FindOrCreateQuadrant("Quadrant_North", 0.42f, 0.49f, 0.63f, 0.575f);
            _quadrants[(int)YutBoardQuadrant.East] =
                FindOrCreateQuadrant("Quadrant_East", 0.67f, 0.32f, 0.87f, 0.53f);
            _quadrants[(int)YutBoardQuadrant.South] =
                FindOrCreateQuadrant("Quadrant_South", 0.3f, 0.22f, 0.7f, 0.33f);
        }

        RectTransform FindOrCreateQuadrant(string name, float xmin, float ymin, float xmax, float ymax)
        {
            // 투명 Image라도 raycastTarget=true면 보드 위 후보/말 클릭·드래그를 가로챈다.
            var rt = FindOrCreatePanel(transform, name, xmin, ymin, xmax, ymax, new Color(0, 0, 0, 0), out _);
            var img = rt.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            return rt;
        }

        /// <summary>다른 기능(대기말/특수능력/완주말+보물 등)이 자기 UI를 붙일 구역 컨테이너.</summary>
        public RectTransform GetQuadrant(YutBoardQuadrant quadrant)
        {
            EnsureBoard();
            return _quadrants[(int)quadrant];
        }

        /// <summary>동(東) 구역에 모은 아이템 한 칸 — 아이콘 + 개수.</summary>
        public struct CollectedItemView
        {
            public Sprite Icon;
            public int Count;
            public string Label;
            /// <summary>보통은 null(흰색 그대로). 보물상자 윷 토큰이 평소 상한을 넘긴 "보너스"일
            /// 때처럼 다른 색으로 눈에 띄게 표시하고 싶을 때만 채운다.</summary>
            public Color? Tint;
        }

        Transform _collectedItemsRoot;
        static Sprite _yeopjeonIcon;
        static Sprite _purifiedWaterIcon;

        /// <summary>엽전 아이콘. Resources/UI/Currency/Yeopjeon.</summary>
        public static Sprite YeopjeonIcon()
        {
            if (_yeopjeonIcon == null)
                _yeopjeonIcon = Resources.Load<Sprite>("UI/Currency/Yeopjeon");
            return _yeopjeonIcon;
        }

        /// <summary>정화수 아이콘. 카탈로그 OfferingData, 없으면 Resources 폴백.</summary>
        public static Sprite PurifiedWaterIcon()
        {
            if (_purifiedWaterIcon != null) return _purifiedWaterIcon;
            var offering = CharacterCatalog.FindOffering("purifiedwater");
            if (offering != null && offering.icon != null)
                _purifiedWaterIcon = offering.icon;
            if (_purifiedWaterIcon == null)
                _purifiedWaterIcon = Resources.Load<Sprite>("UI/Currency/PurifiedWater");
            return _purifiedWaterIcon;
        }

        /// <summary>동(東) 구역 — 이번 매치에서 특수 칸으로 모은 것들(공양물·정화수·엽전)을
        /// 아이콘+개수로 보여준다. YutScreen이 재화를 지급할 때마다 최신 목록을 넘겨준다.</summary>
        public void ShowCollectedItems(IReadOnlyList<CollectedItemView> items)
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureCollectedItemsRoot(east);

            for (int i = _collectedItemsRoot.childCount - 1; i >= 0; i--)
                Destroy(_collectedItemsRoot.GetChild(i).gameObject);

            if (items == null || items.Count == 0) return;

            for (int i = 0; i < items.Count; i++)
                CreateCollectedItemChip(_collectedItemsRoot, items[i]);
        }

        void EnsureCollectedItemsRoot(Transform east)
        {
            if (_collectedItemsRoot != null)
            {
                ApplyEastHalfAnchors((RectTransform)_collectedItemsRoot, upperHalf: true);
                return;
            }

            // 예전에 텍스트만 쓰던 Items 노드는 치운다
            var legacy = east.Find("Items");
            if (legacy != null)
                Destroy(legacy.gameObject);

            var existing = east.Find("ItemIcons");
            if (existing != null)
            {
                _collectedItemsRoot = existing;
                ApplyEastHalfAnchors((RectTransform)_collectedItemsRoot, upperHalf: true);
                return;
            }

            var go = new GameObject("ItemIcons", typeof(RectTransform));
            go.transform.SetParent(east, false);
            var rt = go.GetComponent<RectTransform>();
            // 동 구역 상단 절반 — 공양물·정화수·엽전. 하단 절반은 완주 말.
            ApplyEastHalfAnchors(rt, upperHalf: true);

            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(42f, 54f);
            grid.spacing = new Vector2(4f, 2f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(2, 2, 2, 2);
            _collectedItemsRoot = go.transform;
        }

        Transform _finishedPiecesRoot;
        /// <summary>동 구역 완주 초상 — 말풍선 앵커용 (pieceId → RectTransform).</summary>
        readonly Dictionary<string, RectTransform> _finishedPieceAnchors = new();

        void EnsureFinishedPiecesRoot(Transform east)
        {
            if (_finishedPiecesRoot != null)
            {
                ApplyEastHalfAnchors((RectTransform)_finishedPiecesRoot, upperHalf: false);
                EnsureFinishedHighlightImage((RectTransform)_finishedPiecesRoot);
                return;
            }

            var existing = east.Find("FinishedPieces");
            if (existing != null)
            {
                _finishedPiecesRoot = existing;
                ApplyEastHalfAnchors((RectTransform)_finishedPiecesRoot, upperHalf: false);
                EnsureFinishedHighlightImage((RectTransform)_finishedPiecesRoot);
                return;
            }

            var go = new GameObject("FinishedPieces", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(east, false);
            var rt = go.GetComponent<RectTransform>();
            ApplyEastHalfAnchors(rt, upperHalf: false);
            EnsureFinishedHighlightImage(rt);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(2, 2, 2, 2);
            _finishedPiecesRoot = go.transform;
        }

        /// <summary>동 구역을 상·하 절반으로 나눈다. 위=공양물, 아래=완주 말.</summary>
        static void ApplyEastHalfAnchors(RectTransform rt, bool upperHalf)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, upperHalf ? 0.5f : 0f);
            rt.anchorMax = new Vector2(1f, upperHalf ? 1f : 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void EnsureFinishedHighlightImage(RectTransform finishedRoot)
        {
            if (finishedRoot == null) return;
            var img = finishedRoot.GetComponent<Image>();
            if (img == null)
                img = finishedRoot.gameObject.AddComponent<Image>();
            // 평소엔 투명 — 완주 후보 하이라이트 때만 펄스.
            if (!_eastFinishHighlightActive)
            {
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = false;
            }
        }

        /// <summary>완주(골인)한 말들 — 참(시작점)에 그냥 멈춰 있는 것과 헷갈리지 않게, 보드에서
        /// 빠진 대신 동(東) 구역 하단에 작은 초상으로 한 줄 보여준다.</summary>
        public void ShowFinishedPieces(List<string> ids)
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureFinishedPiecesRoot(east);

            _finishedPieceAnchors.Clear();
            for (int i = _finishedPiecesRoot.childCount - 1; i >= 0; i--)
                Destroy(_finishedPiecesRoot.GetChild(i).gameObject);
            if (ids == null) return;

            for (int i = 0; i < ids.Count; i++)
            {
                string pieceId = ids[i];
                var go = new GameObject($"Finished_{pieceId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_finishedPiecesRoot, false);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(30f, 30f);
                var img = go.GetComponent<Image>();
                var sprite = PieceSpriteFor(pieceId);
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                }
                else
                {
                    img.color = ColorForYokai(pieceId);
                }
                img.raycastTarget = false;
                _finishedPieceAnchors[pieceId] = rt;
            }
        }

        void CreateCollectedItemChip(Transform parent, CollectedItemView item)
        {
            var go = new GameObject("Item", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.28f);
            iconRt.anchorMax = new Vector2(0.9f, 1f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var img = iconGo.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (item.Icon != null)
            {
                img.sprite = item.Icon;
                img.color = item.Tint ?? Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = item.Tint ?? new Color(0.85f, 0.75f, 0.45f, 0.85f);
            }

            var count = CreateText(go.transform, "Count", "x" + item.Count, 19, TextAnchor.MiddleCenter);
            var countRt = count.rectTransform;
            countRt.anchorMin = new Vector2(0f, 0f);
            countRt.anchorMax = new Vector2(1f, 0.3f);
            countRt.offsetMin = Vector2.zero;
            countRt.offsetMax = Vector2.zero;
            count.color = new Color(0.95f, 0.9f, 0.7f);
            count.raycastTarget = false;
        }

        /// <summary>
        /// 윷가락 4개를 던져서 흩뿌리는 연출. 결과(result)에 맞는 앞/뒤 패턴으로 착지한다 —
        /// 뒤집힌 가락 개수 = 0(모)/1(도·빽도)/2(개)/3(걸)/4(윷). 0번 가락은 빨간 점으로
        /// 표시된 "빽도 가락"이라, 1개만 뒤집혔을 때 그게 0번이면 빽도, 다른 가락이면 도로
        /// 갈린다(기획서 7-4 "빽도 가락만 엎어진 경우" 기준). 어느 가락이 뒤집힐지는 개/걸에서만
        /// 랜덤이고 개수는 항상 결과와 일치한다.
        /// power(0~1)는 슬라이드 던지기 속도 — 아치 높이·회전·착지 퍼짐만 키우고 줄인다.
        /// 시작=ThrowSwipeZone, 착지=YutBoard, 대기=North — Prefab/Scene 레이아웃을 따른다.
        /// </summary>
        public IEnumerator PlayThrowAnim(YutThrowResult result, float power = 1f)
        {
            EnsureBoard();
            EnsureThrowSwipeZone();
            ClearParkedSticks();
            EnsureStickSprites();

            var panel = (RectTransform)transform;
            Vector2 WorldToPanelLocal(Vector3 world) => panel.InverseTransformPoint(world);

            var throwRt = _throwZone != null ? (RectTransform)_throwZone.transform : null;
            var landZone = _boardRoot != null ? _boardRoot : panel;
            // 대기 윷(IdleStick)이 있는 Throw 존 위쪽 중앙에서 출발 — Scene 레이아웃 따름
            Vector2 originLocal = throwRt != null
                ? WorldToPanelLocal(ZoneNormToWorld(throwRt, new Vector2(0.5f, 0.72f)))
                : WorldToPanelLocal(ZoneNormToWorld(landZone, new Vector2(0.5f, 0.05f)));

            var frontStates = DetermineFrontStates(result);

            var sticks = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"YutStick{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(22f, 110f);
                rt.anchoredPosition = originLocal;
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
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
                    var markImg = markGo.GetComponent<Image>();
                    markImg.color = BaekdoMarkColor;
                    markImg.raycastTarget = false;
                }
                go.transform.SetAsLastSibling();
                sticks[i] = rt;
            }

            var routines = new Coroutine[4];
            for (int i = 0; i < 4; i++)
                routines[i] = StartCoroutine(ThrowOneStick(
                    sticks[i], originLocal, landZone, WorldToPanelLocal,
                    i * 0.05f, frontStates[i], isBaekdoStick: i == 0, power));
            for (int i = 0; i < 4; i++)
                yield return routines[i];

            yield return new WaitForSecondsRealtime(0.35f);

            // Prefab/Scene의 Quadrant_North 레이아웃을 따른다 — 고정 보드 좌표로 보내지 않음.
            var thrownZone = GetQuadrant(YutBoardQuadrant.North);
            yield return ParkSticksInto(sticks, thrownZone);
            _parkedSticks = sticks;
        }

        // North 구역 로컬(0~1) 기준 — 구역을 Scene에서 옮겨도 결과가 같이 따라간다.
        static readonly Vector2[] ParkSpotsInZone =
        {
            new(0.14f, 0.5f), new(0.38f, 0.5f), new(0.62f, 0.5f), new(0.86f, 0.5f),
        };
        static readonly Vector2 ParkedStickSize = new(10f, 42f);

        IEnumerator ParkSticksInto(RectTransform[] sticks, RectTransform zone)
        {
            if (zone == null) yield break;

            var starts = new Vector3[sticks.Length];
            var startRotations = new Quaternion[sticks.Length];
            var startSizes = new Vector2[sticks.Length];
            var targets = new Vector3[sticks.Length];
            for (int i = 0; i < sticks.Length; i++)
            {
                starts[i] = sticks[i].position;
                startRotations[i] = sticks[i].localRotation;
                startSizes[i] = sticks[i].sizeDelta;
                targets[i] = ZoneNormToWorld(zone, ParkSpotsInZone[i]);
            }

            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                for (int i = 0; i < sticks.Length; i++)
                {
                    sticks[i].position = Vector3.Lerp(starts[i], targets[i], u);
                    sticks[i].localRotation = Quaternion.Slerp(startRotations[i], Quaternion.identity, u);
                    sticks[i].sizeDelta = Vector2.Lerp(startSizes[i], ParkedStickSize, u);
                }
                yield return null;
            }

            for (int i = 0; i < sticks.Length; i++)
            {
                sticks[i].SetParent(zone, worldPositionStays: true);
                sticks[i].sizeDelta = ParkedStickSize;
                sticks[i].localRotation = Quaternion.identity;
                sticks[i].anchorMin = sticks[i].anchorMax = new Vector2(0.5f, 0.5f);
                sticks[i].pivot = new Vector2(0.5f, 0.5f);
                sticks[i].anchoredPosition = ZoneNormToLocal(zone, ParkSpotsInZone[i]);
            }
        }

        static Vector2 ZoneNormToLocal(RectTransform zone, Vector2 norm)
        {
            var r = zone.rect;
            return new Vector2((norm.x - 0.5f) * r.width, (norm.y - 0.5f) * r.height);
        }

        static Vector3 ZoneNormToWorld(RectTransform zone, Vector2 norm)
            => zone.TransformPoint(ZoneNormToLocal(zone, norm));

        void ClearParkedSticks()
        {
            if (_parkedSticks == null) return;
            foreach (var rt in _parkedSticks)
                if (rt != null) Destroy(rt.gameObject);
            _parkedSticks = null;
        }

        // 뒤집힌(등 보임) 가락 개수 = 0(모)/1(도·빽도)/2(개)/3(걸)/4(윷).
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

        IEnumerator ThrowOneStick(
            RectTransform rt,
            Vector2 startLocal,
            RectTransform landZone,
            System.Func<Vector3, Vector2> worldToPanelLocal,
            float delay,
            bool targetFront,
            bool isBaekdoStick,
            float power)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            // power(슬라이드 속도, 0~1)가 클수록 보드 안에서 더 멀리·높이·세게 — 확률과 무관, 연출 전용.
            // 착지 좌표는 YutBoard(landZone) 로컬 0~1 기준이라 Scene에서 보드를 옮겨도 따라간다.
            float spreadX = Mathf.Lerp(0.12f, 0.38f, power);
            float yMin = Mathf.Lerp(0.22f, 0.28f, power);
            float yMax = Mathf.Lerp(0.48f, 0.78f, power);
            var landNorm = new Vector2(
                Mathf.Clamp01(0.5f + UnityEngine.Random.Range(-spreadX, spreadX)),
                UnityEngine.Random.Range(yMin, yMax));
            Vector2 endLocal = landZone != null
                ? worldToPanelLocal(ZoneNormToWorld(landZone, landNorm))
                : startLocal + new Vector2(0f, 220f);

            float zoneH = landZone != null ? Mathf.Abs(landZone.rect.height) : 400f;
            float arcHeight = Mathf.Lerp(zoneH * 0.22f, zoneH * 0.55f, power)
                + UnityEngine.Random.Range(-15f, 15f);
            float spin = (Mathf.Lerp(480f, 1300f, power) + UnityEngine.Random.Range(-60f, 60f))
                * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            float duration = Mathf.Lerp(0.65f, 0.42f, power);

            Vector2 start = startLocal;
            Vector2 end = endLocal;
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
                text = CreateText(button.transform, "Label", label, 38, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
                text.raycastTarget = false;
            }
            text.text = label;
            text.font = ResolveFont();
            text.fontSize = UiFonts.Size(39);
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

            var text = CreateText(go.transform, "Label", label, 34, TextAnchor.MiddleCenter);
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
