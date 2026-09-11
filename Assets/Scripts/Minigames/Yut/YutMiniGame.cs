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
        /// <summary>윷 토큰(하트) 옆 [+] 버튼 — YutScreen이 YutTokenShopPopup을 연다.</summary>
        public event Action OnBuyTokensPressed;
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
        GameObject _miniThrowContainer;
        Image[] _miniThrowSticks;

        // 대화 말풍선(각 요괴 말 근처에 잠깐 떴다 사라짐)과 놀이기록(윷 토큰 옆 버튼으로 여는
        // 팝업, 짧은 사건형 문장만 쌓임)은 서로 분리된 별개의 두 체계다 — 말풍선 대사는
        // 놀이기록에 안 남는다.
        const float BubbleDuration = 2.2f;
        Button _playLogButton;
        GameObject _playLogPanel;
        RectTransform _playLogContent;
        RectTransform _playLogViewport;
        ScrollRect _playLogScroll;
        readonly List<string> _playLogEntries = new();
        const int MaxPlayLogEntries = 200;

        // 본게임 수련장 전용 — 보유 요괴 전체를 동시에 말로 표시(id → 말 오브젝트/이니셜 라벨).
        // 튜토리얼의 _piece/_opponentPiece(각본 대결용)와는 완전히 별개.
        readonly Dictionary<string, RectTransform> _yokaiPieces = new();
        readonly Dictionary<string, Text> _yokaiPieceLabels = new();

        RectTransform _rosterPanel;
        RectTransform _rosterRow;
        Text _rosterCaption;
        readonly List<RosterChip> _rosterChips = new();
        GameObject _summonSlotChip;

        RectTransform _turnTrackerPanel;
        Text _turnTrackerNumberText;
        Text _turnTrackerOrderText;

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

            // 보드(_boardRoot) 아래쪽 가장자리(y=0.2)에 바로 이어 붙여서 보드의 연장처럼 보이게 —
            // 폭도 보드와 동일(0.1~0.9), 배경색도 보드 배경색과 맞춘다. 화면 맨 아래(y<0.13)는
            // RosterPanel(요괴 명단) 자리로 비워둔다. 예전 Bake본은 이 자리/색이 어긋나 있을 수
            // 있어 found든 created든 항상 재적용한다(코드가 항상 최종 소스).
            var rt = FindOrCreatePanel(transform, "ThrowSwipeZone", 0.1f, 0.13f, 0.9f, 0.2f,
                new Color(0.12f, 0.22f, 0.18f, 0.92f), out bool created);
            _throwZone = rt.gameObject;

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
            ClearPlayLog();
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

        /// <summary>플레이어 쪽 요괴 하나(pieceId) 말 위에 잠깐 뜨는 대화 말풍선. 대기 말이면
        /// 남(南) 구역 자리 위에, 보드 위 말이면 그 칸 위에 뜬다 — 놀이기록엔 안 남는다.</summary>
        public void ShowPieceBubble(string pieceId, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_yokaiPieces.TryGetValue(pieceId, out var anchor) && anchor != null)
                ShowBubbleAbove(anchor, text);
        }

        /// <summary>이무기 말 위에 잠깐 뜨는 대화 말풍선.</summary>
        public void ShowOpponentBubble(string text)
        {
            if (!string.IsNullOrEmpty(text) && _opponentPiece != null && _opponentPiece.gameObject.activeInHierarchy)
                ShowBubbleAbove(_opponentPiece, text);
        }

        void ShowBubbleAbove(RectTransform anchor, string text)
        {
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
            rt.position = anchor.position + new Vector3(0f, anchor.rect.height * 0.6f + 12f, 0f);
            rt.SetAsLastSibling();
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.99f, 0.97f, 0.9f, 0.97f);
            bg.raycastTarget = false;

            var label = CreateText(rt, "Text", text, 20, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.color = new Color(0.15f, 0.12f, 0.08f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            StartCoroutine(DestroyAfter(go, BubbleDuration));
        }

        IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (go != null) Destroy(go);
        }

        void EnsurePlayLogButton()
        {
            if (_playLogButton != null) return;

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

            var closeBtn = CreateButton(rt, "Close", "닫기", () => ShowPlayLog(false));
            SetAnchor(closeBtn.GetComponent<RectTransform>(), 0.32f, 0.02f, 0.68f, 0.14f, 0, 0, 0, 0);

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

        /// <summary>한 칸에 말이 한 마리면 예전처럼 칸 가운데 크게, 업혀서 여럿이면 겹치지 않게
        /// 그 칸 폭을 나눠 가로로 나란히 붙여 배치한다.</summary>
        /// <summary>한 칸에 말이 한 마리면 예전처럼 칸 가운데 크게, 업혀서 여럿이면 칸 폭에
        /// 욱여넣어 작아지는 대신 평소 크기를 유지한 채 가로로 겹치며 퍼진다 — 칸 밖으로
        /// 넘쳐도 된다(마스크가 없어 잘리지 않는다).</summary>
        void PlaceGroupOnNode(List<RectTransform> pieces, int nodeId)
        {
            nodeId = Mathf.Clamp(nodeId, 0, _pads.Length - 1);
            var padRt = _pads[nodeId].rectTransform;
            int count = pieces.Count;
            var pieceSize = new Vector2(padRt.rect.width * 0.64f, padRt.rect.height * 0.64f);

            for (int i = 0; i < count; i++)
            {
                var piece = pieces[i];
                Vector3 fromPos = piece.position;
                piece.SetParent(padRt, false);

                piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0.5f);
                piece.pivot = new Vector2(0.5f, 0.5f);
                piece.sizeDelta = pieceSize;

                if (count <= 1)
                {
                    piece.anchoredPosition = Vector2.zero;
                }
                else
                {
                    float spreadStep = pieceSize.x * 0.62f; // 서로 살짝 겹치도록 한 칸보다 좁게
                    float centerOffset = (i - (count - 1) / 2f) * spreadStep;
                    piece.anchoredPosition = new Vector2(centerOffset, 0f);
                }

                SlideIn(piece, fromPos);
            }
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

            // 후보 아이콘뿐 아니라 갈 수 있는 칸 자체도 빛나게 강조한다.
            foreach (var nodeId in byNode.Keys)
                HighlightPad(nodeId);
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
        bool _padHighlightRunning;

        void HighlightPad(int nodeId)
        {
            if (_pads == null || nodeId < 0 || nodeId >= _pads.Length || _pads[nodeId] == null) return;
            if (!_highlightedPadOriginals.ContainsKey(nodeId))
                _highlightedPadOriginals[nodeId] = _pads[nodeId].color;
            if (!_padHighlightRunning)
                StartCoroutine(PadHighlightRoutine());
        }

        void ClearPadHighlights()
        {
            foreach (var kv in _highlightedPadOriginals)
                if (_pads != null && kv.Key < _pads.Length && _pads[kv.Key] != null)
                    _pads[kv.Key].color = kv.Value;
            _highlightedPadOriginals.Clear();
        }

        /// <summary>갈 수 있는 칸을 노란빛으로 은은하게 펄스시킨다 — 후보가 남아있는 동안만 돈다.</summary>
        IEnumerator PadHighlightRoutine()
        {
            _padHighlightRunning = true;
            var glow = new Color(1f, 0.92f, 0.35f, 1f);
            while (_highlightedPadOriginals.Count > 0)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
                foreach (var nodeId in _highlightedPadOriginals.Keys)
                {
                    if (_pads == null || nodeId >= _pads.Length || _pads[nodeId] == null) continue;
                    _pads[nodeId].color = Color.Lerp(_highlightedPadOriginals[nodeId], glow, pulse);
                }
                yield return null;
            }
            _padHighlightRunning = false;
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
                var label = CreateText(img.transform, "Label", InitialOf(candidate.DisplayName), 28, TextAnchor.MiddleCenter);
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

        /// <summary>Bake된 자식(씬에 이미 있는 UI)이 있으면 그대로 쓰고, 없으면 새로 만든다 —
        /// 이 프로젝트의 UI들은 이 두 갈래를 매번 손으로 복붙해오다 "새 비주얼은 생성 분기에만
        /// 추가하고 바인딩 분기는 빼먹는" 버그가 반복됐다. 앵커/색은 코드가 항상 최종 소스라는
        /// 기존 관례(EnsureThrowSwipeZone 등 참고)에 따라 found/created 상관없이 매번 재적용한다.
        /// 자식 UI 생성처럼 "새로 만들 때만" 필요한 작업은 호출자가 created로 분기한다.</summary>
        RectTransform FindOrCreatePanel(Transform parent, string name, float xmin, float ymin, float xmax, float ymax,
            Color color, out bool created)
        {
            var existing = parent.Find(name) as RectTransform;
            RectTransform rt;
            if (existing != null)
            {
                rt = existing;
                created = false;
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                rt = go.GetComponent<RectTransform>();
                created = true;
            }
            SetAnchor(rt, xmin, ymin, xmax, ymax, 0, 0, 0, 0);
            rt.GetComponent<Image>().color = color;
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
            BindOrCreatePieces();

            EnsureQuadrants();
            EnsureRulesOverlay();
            EnsureOpponentMiniThrowPanel();
            EnsureRosterPanel();
            EnsureTurnTracker();
            EnsurePlayLogButton();
            EnsurePlayLogPanel();
        }

        /// <summary>
        /// 月下修練(달빛 수련) 헤더 — LogBar 바로 위(y 0.68~0.75)에 내 차례 번호와, 이번 턴에
        /// 보너스(윷/모)로 이어 던진 결과 순서를 보여준다. 턴이 끝나면(이무기 턴으로 넘어가면)
        /// 다음 내 차례 시작할 때 YutMatch가 순서를 비우고 번호를 올린다.
        /// </summary>
        void EnsureTurnTracker()
        {
            if (_turnTrackerPanel != null) return;

            _turnTrackerPanel = FindOrCreatePanel(transform, "TurnTracker", 0.06f, 0.68f, 0.94f, 0.75f,
                new Color(0.1f, 0.09f, 0.06f, 0.9f), out _);

            var titleT = _turnTrackerPanel.Find("Title") as RectTransform;
            if (titleT == null)
            {
                var title = CreateText(_turnTrackerPanel, "Title", "月下修練 · 달빛 수련", 22, TextAnchor.MiddleLeft);
                title.rectTransform.anchorMin = new Vector2(0f, 0.55f);
                title.rectTransform.anchorMax = new Vector2(0.62f, 1f);
                title.rectTransform.offsetMin = new Vector2(14f, 0f);
                title.rectTransform.offsetMax = Vector2.zero;
                title.raycastTarget = false;
            }

            var turnT = _turnTrackerPanel.Find("TurnNumber") as RectTransform;
            _turnTrackerNumberText = turnT != null ? turnT.GetComponent<Text>() : null;
            if (_turnTrackerNumberText == null)
            {
                _turnTrackerNumberText = CreateText(_turnTrackerPanel, "TurnNumber", "", 21, TextAnchor.MiddleRight);
                _turnTrackerNumberText.rectTransform.anchorMin = new Vector2(0.62f, 0.55f);
                _turnTrackerNumberText.rectTransform.anchorMax = new Vector2(1f, 1f);
                _turnTrackerNumberText.rectTransform.offsetMin = Vector2.zero;
                _turnTrackerNumberText.rectTransform.offsetMax = new Vector2(-14f, 0f);
                _turnTrackerNumberText.color = new Color(1f, 0.9f, 0.7f);
                _turnTrackerNumberText.raycastTarget = false;
            }

            var orderT = _turnTrackerPanel.Find("Order") as RectTransform;
            _turnTrackerOrderText = orderT != null ? orderT.GetComponent<Text>() : null;
            if (_turnTrackerOrderText == null)
            {
                _turnTrackerOrderText = CreateText(_turnTrackerPanel, "Order", "", 20, TextAnchor.MiddleLeft);
                _turnTrackerOrderText.rectTransform.anchorMin = new Vector2(0f, 0f);
                _turnTrackerOrderText.rectTransform.anchorMax = new Vector2(1f, 0.55f);
                _turnTrackerOrderText.rectTransform.offsetMin = new Vector2(14f, 0f);
                _turnTrackerOrderText.rectTransform.offsetMax = new Vector2(-14f, 0f);
                _turnTrackerOrderText.color = new Color(0.85f, 0.9f, 0.95f);
                _turnTrackerOrderText.raycastTarget = false;
            }
        }

        /// <summary>내 차례 번호와 이번 턴에 나온 결과 순서를 갱신한다. results는 YutMatch가 들고
        /// 있는 리스트를 그대로 받아 인덱서로만 순회한다(새 struct에 LINQ 쓰면 IL2CPP WebGL에서
        /// "null function"이 나던 문제 때문에 — 여기선 enum이라 더 안전하지만 관례상 통일).</summary>
        public void ShowTurnTracker(int turnNumber, IReadOnlyList<YutThrowResult> results)
        {
            EnsureBoard();
            if (_turnTrackerNumberText != null)
                _turnTrackerNumberText.text = $"나의 {turnNumber}번째 차례";

            if (_turnTrackerOrderText != null)
            {
                if (results == null || results.Count == 0)
                {
                    _turnTrackerOrderText.text = "나온 순서 — · 윷·모는 한 번 더";
                }
                else
                {
                    var sb = new System.Text.StringBuilder("나온 순서 — ");
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (i > 0) sb.Append(" → ");
                        sb.Append(results[i].DisplayName());
                    }
                    sb.Append(" · 윷·모는 한 번 더");
                    _turnTrackerOrderText.text = sb.ToString();
                }
            }
        }

        /// <summary>
        /// 화면 최하단(y 0~0.13) — 참가 요괴 전체를 초상화·이름·기력(♦)·친밀도(♡)·보드 위치와 함께
        /// 한 줄로 보여준다. ThrowSwipeZone(0.13~0.2)과 겹치지 않게 그 아래 자리에 둔다.
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

        /// <summary>소환 연출용 — 고라니(넋) 아이콘. 아직 스폰 전이어도 CharacterData 기준으로 찾는다.
        /// 넋 단계는 혼 초상이 아니라 불꽃 에셋을 쓴다.</summary>
        public Sprite GetGoraniSprite() => GetNeokSprite() ?? PieceSpriteFor("Gorani");

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

        /// <summary>하트(윷 토큰) 자리는 "기록"·"+"(토큰 구매) 버튼과 한 줄에 나란히 있어야 해서,
        /// found든 created든 매번 좌표를 다시 맞춘다 — 하나만 고치고 다른 쪽을 빼먹으면 baked
        /// 씬에서만 옛 위치로 어긋나는 버그가 난다(다른 Ensure류와 같은 이유).</summary>
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

            if (!foundAll)
            {
                _heartIcons = new Image[heartCount];
                for (int i = 0; i < heartCount; i++)
                {
                    var heartGo = new GameObject($"Heart{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    heartGo.transform.SetParent(transform, false);
                    _heartIcons[i] = heartGo.GetComponent<Image>();
                }
            }

            const float iconW = 0.035f;
            const float gap = 0.008f;
            float totalW = heartCount * iconW + (heartCount - 1) * gap;
            float startX = HeartsRightEdge - totalW;
            for (int i = 0; i < heartCount; i++)
            {
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

            // 예전에 구운(Bake) 보드는 특수 칸이 생기기 전 색으로 저장돼 있을 수 있어 — 매번
            // CreatePads()와 같은 규칙으로 다시 칠해서(코드가 항상 최종 소스) 반영되게 한다.
            for (int i = 0; i < _pads.Length; i++)
                RecolorPad(_pads[i], i);
            return true;
        }

        static void RecolorPad(Image pad, int nodeId)
        {
            if (pad == null) return;
            pad.color = YutBoardLayout.IsSpecialReward(nodeId)
                ? new Color(0.35f, 0.55f, 0.85f, 0.9f) // 특수 칸 — 엽전/공양물/보물상자(YutBoardLayout.SpecialSquareKind)
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

        void EnsureRulesOverlay()
        {
            if (_rulesOverlay != null) return;

            var rt = FindOrCreatePanel(transform, "RulesOverlay", 0.14f, 0.32f, 0.86f, 0.64f,
                new Color(0.05f, 0.05f, 0.05f, 0.95f), out bool created);
            _rulesOverlay = rt.gameObject;

            if (!created)
            {
                var closeBtn = rt.Find("Close")?.GetComponent<Button>();
                WireButton(closeBtn, () => ShowRulesOverlay(false));
                return;
            }

            var rulesText = CreateText(_rulesOverlay.transform, "RulesText",
                "윷놀이 족보 (16분의)\n\n빽도 -1\n도 1\n개 2\n걸 3\n윷 4 (한 번 더)\n모 5 (한 번 더)",
                32, TextAnchor.MiddleCenter);
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

        RectTransform FindOrCreateQuadrant(string name, float xmin, float ymin, float xmax, float ymax) =>
            FindOrCreatePanel(transform, name, xmin, ymin, xmax, ymax, new Color(0, 0, 0, 0), out _);

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

        /// <summary>엽전 아이콘. Resources/UI/Currency/Yeopjeon.</summary>
        public static Sprite YeopjeonIcon()
        {
            if (_yeopjeonIcon == null)
                _yeopjeonIcon = Resources.Load<Sprite>("UI/Currency/Yeopjeon");
            return _yeopjeonIcon;
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
            if (_collectedItemsRoot != null) return;

            // 예전에 텍스트만 쓰던 Items 노드는 치운다
            var legacy = east.Find("Items");
            if (legacy != null)
                Destroy(legacy.gameObject);

            var existing = east.Find("ItemIcons");
            if (existing != null)
            {
                _collectedItemsRoot = existing;
                return;
            }

            var go = new GameObject("ItemIcons", typeof(RectTransform));
            go.transform.SetParent(east, false);
            var rt = go.GetComponent<RectTransform>();
            // 동 구역 아래쪽(0~0.32)은 완주한 말 자리로 남겨둔다.
            rt.anchorMin = new Vector2(0f, 0.32f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

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

        void EnsureFinishedPiecesRoot(Transform east)
        {
            if (_finishedPiecesRoot != null) return;

            var existing = east.Find("FinishedPieces");
            if (existing != null)
            {
                _finishedPiecesRoot = existing;
                return;
            }

            var go = new GameObject("FinishedPieces", typeof(RectTransform));
            go.transform.SetParent(east, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.3f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(2, 2, 2, 2);
            _finishedPiecesRoot = go.transform;
        }

        /// <summary>완주(골인)한 말들 — 참(시작점)에 그냥 멈춰 있는 것과 헷갈리지 않게, 보드에서
        /// 빠진 대신 동(東) 구역 하단에 작은 초상으로 한 줄 보여준다.</summary>
        public void ShowFinishedPieces(List<string> ids)
        {
            var east = GetQuadrant(YutBoardQuadrant.East);
            EnsureFinishedPiecesRoot(east);

            for (int i = _finishedPiecesRoot.childCount - 1; i >= 0; i--)
                Destroy(_finishedPiecesRoot.GetChild(i).gameObject);
            if (ids == null) return;

            for (int i = 0; i < ids.Count; i++)
            {
                var go = new GameObject("Finished", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_finishedPiecesRoot, false);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(30f, 30f);
                var img = go.GetComponent<Image>();
                var sprite = PieceSpriteFor(ids[i]);
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                }
                else
                {
                    img.color = ColorForYokai(ids[i]);
                }
                img.raycastTarget = false;
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
