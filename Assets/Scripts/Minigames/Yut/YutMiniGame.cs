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
        List<GameObject> _revealedContestedIcons;
        readonly List<(RectTransform rect, string pieceId, bool useShortcut)> _candidateHits = new();
        GameObject _logBar;
        Image _logBarLeftPortrait;
        Image _logBarRightPortrait;
        RectTransform _logContent;
        ScrollRect _logScroll;

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
            EnsureThrowSwipeZone();

            _leaveButton = transform.Find("Leave")?.GetComponent<Button>();
            if (_leaveButton == null)
            {
                _leaveButton = CreateButton(transform, "Leave", "나가기", null);
                SetAnchor(_leaveButton.GetComponent<RectTransform>(), 0.02f, 0.93f, 0.18f, 0.99f, 0, 0, 0, 0);
            }

            WireButton(_leaveButton, () => OnLeavePressed?.Invoke());
        }

        /// <summary>탭 버튼 대신 아래→위 슬라이드로 던지는 입력 영역. 손 모양 힌트가 살짝 위아래로
        /// 통통 튀어서 "여기서 위로 밀어라"를 안내한다.</summary>
        void EnsureThrowSwipeZone()
        {
            if (_throwZone != null) return;

            _throwZone = new GameObject("ThrowSwipeZone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _throwZone.transform.SetParent(transform, false);
            SetAnchor((RectTransform)_throwZone.transform, 0.2f, 0.02f, 0.8f, 0.18f, 0, 0, 0, 0);
            _throwZone.GetComponent<Image>().color = new Color(0.25f, 0.22f, 0.18f, 0.55f);

            var label = CreateText(_throwZone.transform, "Label", "↑ 위로 슬라이드해서 던지기", 24, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;

            _throwSwipe = _throwZone.AddComponent<YutThrowSwipeZone>();
            _throwSwipe.OnSwipeThrow += power => OnThrowPressed?.Invoke(power);

            StartCoroutine(BounceHint(label.rectTransform));
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
        /// 그 이름을 쓴다. 새 줄이 쌓이면 자동으로 맨 아래로 스크롤된다.
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

            var rowGo = new GameObject("Row", typeof(RectTransform));
            rowGo.transform.SetParent(_logContent, false);
            var rowLe = rowGo.AddComponent<LayoutElement>();
            int extraLines = Mathf.Max(0, Mathf.CeilToInt(text.Length / 16f) - 1);
            rowLe.preferredHeight = 34f + extraLines * 24f;
            rowLe.flexibleWidth = 1f;

            var bubbleGo = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bubbleGo.transform.SetParent(rowGo.transform, false);
            var bubbleRt = bubbleGo.GetComponent<RectTransform>();
            bubbleRt.anchorMin = new Vector2(leftSpeaking ? 0f : 0.26f, 0f);
            bubbleRt.anchorMax = new Vector2(leftSpeaking ? 0.74f : 1f, 1f);
            bubbleRt.offsetMin = Vector2.zero;
            bubbleRt.offsetMax = Vector2.zero;
            bubbleGo.GetComponent<Image>().color = leftSpeaking
                ? new Color(0.22f, 0.2f, 0.3f, 0.95f)
                : new Color(0.2f, 0.32f, 0.24f, 0.95f);

            var label = CreateText(bubbleGo.transform, "Text", text, 18,
                leftSpeaking ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(12f, 2f);
            labelRt.offsetMax = new Vector2(-12f, -2f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            // 레이아웃이 자동으로 갱신될 때까지 기다리지 않고 이 프레임에 바로 크기를 확정한다 —
            // 매치 시작 직후(첫 줄)처럼 캔버스가 막 켜진 시점엔 지연 레이아웃이 한 박자 늦을 수 있다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_logContent);
            StartCoroutine(ScrollLogToBottomNextFrame());
        }

        /// <summary>매치 시작/재입장 때 이전 대화가 안 남게 게임로그를 비운다.</summary>
        public void ClearLog()
        {
            if (_logContent == null) return;
            for (int i = _logContent.childCount - 1; i >= 0; i--)
                Destroy(_logContent.GetChild(i).gameObject);
        }

        IEnumerator ScrollLogToBottomNextFrame()
        {
            yield return null; // 레이아웃이 새 줄 크기만큼 다시 계산된 다음에 스크롤해야 정확하다
            if (_logScroll != null) _logScroll.verticalNormalizedPosition = 0f;
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
                label = CreateText(go.transform, "Label", InitialOf(displayName), 20, TextAnchor.MiddleCenter);
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
                    _candidateMarkers.Add(BuildContestedMarker(kv.Key, kv.Value));
            }
        }

        /// <summary>FlashCandidates로 띄운 후보 마커를 전부 지운다.</summary>
        public void ClearCandidates()
        {
            foreach (var go in _candidateMarkers)
                if (go != null) Destroy(go);
            _candidateMarkers.Clear();
            _candidateHits.Clear();
            _revealedContestedIcons = null;
        }

        /// <summary>YutContestedHold가 꾹 누른 지 HoldDelay가 지나면 호출 — 경합 밭 둘레에 후보를
        /// 펼친다(개별 탭으로도 바로 고를 수 있음, 끌기로 고르는 건 HighlightContested가 담당).</summary>
        public void RevealContested(int nodeId, List<YokaiMoveCandidate> group)
        {
            _revealedContestedIcons = BuildCandidateCross(nodeId, group);
            _candidateMarkers.AddRange(_revealedContestedIcons);
        }

        /// <summary>드래그 방향이 가리키는 후보를 강조(선택 중 표시). index<0이면 전부 원래 크기로.</summary>
        public void HighlightContested(int index)
        {
            if (_revealedContestedIcons == null) return;
            for (int i = 0; i < _revealedContestedIcons.Count; i++)
            {
                var rt = _revealedContestedIcons[i] != null ? _revealedContestedIcons[i].transform as RectTransform : null;
                if (rt != null) rt.localScale = Vector3.one * (i == index ? 1.35f : 1f);
            }
        }

        /// <summary>경합 밭에서 손을 뗀 지점이 유효한 후보를 가리키고 있었을 때 — 탭한 것과 동일하게 처리.</summary>
        public void CommitContested(YokaiMoveCandidate candidate) =>
            OnCandidateTapped?.Invoke(candidate.Id, candidate.UseShortcut);

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

        /// <summary>경합 밭(후보 2마리 이상) 마커 — 탭 버튼이 아니라 YutContestedHold가 꾹 누르기·
        /// 드래그·떼기를 전부 처리한다. 꾹 눌러 HoldDelay를 채워야 RevealContested로 후보가 펼쳐진다.</summary>
        GameObject BuildContestedMarker(int nodeId, List<YokaiMoveCandidate> group)
        {
            var go = new GameObject($"Contested_{nodeId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_pads[nodeId].transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.75f, 0.3f, 0.3f, 0.92f);

            var label = CreateText(go.transform, "Count", $"⚔{group.Count}", 20, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.raycastTarget = false;

            var hold = go.AddComponent<YutContestedHold>();
            hold.Owner = this;
            hold.NodeId = nodeId;
            hold.Candidates = group;

            StartCoroutine(PulseScale(rt));
            return go;
        }

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
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
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
                var label = CreateText(img.transform, "Label", InitialOf(candidate.DisplayName), 18, TextAnchor.MiddleCenter);
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
            EnsureLogBar();
        }

        /// <summary>
        /// 보드 위쪽 게임로그 — 위쪽은 이무기(왼쪽)·옥토끼(팀 대표, 오른쪽) 초상이 고정으로 있고,
        /// 그 아래는 말풍선이 카톡처럼 위로 쌓이는 채팅창(스크롤). 말한 쪽 초상이 살짝 커진다.
        /// </summary>
        void EnsureLogBar()
        {
            if (_logBar != null) return;

            _logBar = new GameObject("LogBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _logBar.transform.SetParent(transform, false);
            SetAnchor((RectTransform)_logBar.transform, 0.06f, 0.68f, 0.94f, 0.9f, 0, 0, 0, 0);
            _logBar.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.92f);

            _logBarLeftPortrait = BuildLogHeaderPortrait(_logBar.transform, "Imugi", "이무기", left: true);
            _logBarRightPortrait = BuildLogHeaderPortrait(_logBar.transform, "Rabbit", "옥토끼", left: false);

            var scrollGo = new GameObject("ScrollArea", typeof(RectTransform));
            scrollGo.transform.SetParent(_logBar.transform, false);
            SetAnchor((RectTransform)scrollGo.transform, 0.02f, 0.02f, 0.98f, 0.72f, 0, 0, 0, 0);
            _logScroll = scrollGo.AddComponent<ScrollRect>();
            _logScroll.horizontal = false;
            _logScroll.vertical = true;
            _logScroll.movementType = ScrollRect.MovementType.Clamped;
            _logScroll.scrollSensitivity = 12f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            Stretch((RectTransform)viewportGo.transform);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f); // Mask 작동에만 필요, 안 보임
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _logContent = contentGo.GetComponent<RectTransform>();
            _logContent.anchorMin = new Vector2(0f, 1f);
            _logContent.anchorMax = new Vector2(1f, 1f);
            _logContent.pivot = new Vector2(0.5f, 1f);
            _logContent.anchoredPosition = Vector2.zero;
            var vlayout = contentGo.AddComponent<VerticalLayoutGroup>();
            vlayout.childAlignment = TextAnchor.UpperCenter;
            vlayout.spacing = 5f;
            vlayout.padding = new RectOffset(2, 2, 2, 2);
            vlayout.childControlWidth = true;
            vlayout.childControlHeight = true;
            vlayout.childForceExpandWidth = true;
            vlayout.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _logScroll.viewport = (RectTransform)viewportGo.transform;
            _logScroll.content = _logContent;
        }

        Image BuildLogHeaderPortrait(Transform parent, string spriteId, string displayLabel, bool left)
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

            var nameText = CreateText(go.transform, "Name", displayLabel, 13, TextAnchor.MiddleCenter);
            nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 0.28f);
            nameText.rectTransform.offsetMin = Vector2.zero;
            nameText.rectTransform.offsetMax = Vector2.zero;
            nameText.raycastTarget = false;

            return img;
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
            _quadrants[(int)YutBoardQuadrant.North] =
                CreateQuadrantContainer("Quadrant_North", 0.42f, 0.49f, 0.63f, 0.575f);
            _quadrants[(int)YutBoardQuadrant.West] =
                CreateQuadrantContainer("Quadrant_West", 0.13f, 0.32f, 0.33f, 0.53f);
            _quadrants[(int)YutBoardQuadrant.East] =
                CreateQuadrantContainer("Quadrant_East", 0.67f, 0.32f, 0.87f, 0.53f);
            _quadrants[(int)YutBoardQuadrant.South] =
                CreateQuadrantContainer("Quadrant_South", 0.3f, 0.22f, 0.7f, 0.33f);
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
