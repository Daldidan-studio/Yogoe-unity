using System.Collections;
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
    /// 출석보상 윷점 (기획 18장). Prefab: Assets/Prefabs/UI/AttendanceScreen.prefab
    /// 하루(새벽 4시 기준) 첫 접속 팝업 → 오늘 칸 탭 → 엽전 → 윷 3번 → 옥토끼 대화창(64괘).
    /// 팝업이 떠 있는 동안 ~ 옥토끼 대사가 끝날 때까지 요괴들은 말하지 않는다(SpeechGate).
    /// </summary>
    public class AttendanceScreen : MonoBehaviour
    {
        public static AttendanceScreen Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        public Font font;

        const int Days = 7;
        const int Throws = 3;

        [SerializeField] GameObject root;
        [SerializeField] GameObject boardPanel;
        [SerializeField] Image[] dayCircles = new Image[Days];
        [SerializeField] Text[] dayRewardTexts = new Text[Days];
        [SerializeField] Button[] dayButtons = new Button[Days];
        [SerializeField] Text guideText;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject throwPanel;
        [Tooltip("윷가락이 착지하는 영역 (윷놀이 YutBoard 역할)")]
        [SerializeField] RectTransform throwLandZone;
        [SerializeField] Text[] throwResultTexts = new Text[Throws];
        [SerializeField] GameObject dialogPanel;
        [SerializeField] Image dialogPortrait;
        [SerializeField] Text dialogNameText;
        [SerializeField] Text dialogBodyText;
        [SerializeField] Button dialogButton;

        static readonly Color DoneColor = new Color(0.45f, 0.4f, 0.35f, 1f);
        static readonly Color TodayColor = new Color(1f, 0.82f, 0.35f, 1f);
        static readonly Color FutureColor = new Color(0.85f, 0.8f, 0.7f, 1f);

        bool busy;
        readonly RectTransform[] sticks = new RectTransform[4];

        void Awake() => Instance = this;
        void OnEnable() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>오늘 아직 처리 안 했으면 연다 (Main: 로드 직후·앱 복귀 시). hudFont = 한글 폰트.</summary>
        public static void TryOpenIfDue(Font hudFont)
        {
            if (IsOpen || !Attendance.ShouldOpen(Attendance.TodayKey)) return;
            var screen = Resolve();
            if (screen == null) return;
            if (hudFont != null) screen.font = hudFont;
            screen.Open();
        }

        /// <summary>씬 인스턴스 → Resources 프리팹 → (없으면) 코드 셸 순으로 찾는다.</summary>
        public static AttendanceScreen Resolve()
        {
            if (Instance != null)
            {
                if (!Instance.gameObject.activeSelf) Instance.gameObject.SetActive(true);
                return Instance;
            }
            var found = Object.FindAnyObjectByType<AttendanceScreen>(FindObjectsInactive.Include);
            if (found == null)
            {
                var prefab = Resources.Load<GameObject>("UI/AttendanceScreen");
                if (prefab != null)
                    found = Object.Instantiate(prefab).GetComponent<AttendanceScreen>();
            }
            if (found == null)
            {
                Debug.LogWarning("[AttendanceScreen] Prefab이 없어 코드로 셸을 만듭니다. " +
                                 "Yoegoe → Bake AttendanceScreen Prefab 을 실행하세요.");
                found = new GameObject("AttendanceScreen").AddComponent<AttendanceScreen>();
            }
            if (!found.gameObject.activeSelf) found.gameObject.SetActive(true);
            Instance = found;
            return found;
        }

        public void Open()
        {
            if (!EnsureShell()) return;
            ApplyFont();
            IsOpen = true;
            busy = false;
            SpeechGate.Silence();
            root.SetActive(true);
            boardPanel.SetActive(true);
            throwPanel.SetActive(false);
            dialogPanel.SetActive(false);
            closeButton.gameObject.SetActive(true);
            for (int i = 0; i < Throws; i++) throwResultTexts[i].text = "";
            guideText.text = "오늘 칸을 눌러 엽전을 받으세요";
            RefreshDays(Attendance.NextDayIndex, claimed: false);
        }

        void RefreshDays(int todayIndex, bool claimed)
        {
            var rewards = AttendanceCatalog.Rewards;
            for (int i = 0; i < Days; i++)
            {
                bool exists = i < rewards.Length;
                dayCircles[i].gameObject.SetActive(exists);
                if (!exists) continue;
                dayRewardTexts[i].text = (i + 1) + "일차\n엽전 " + rewards[i];
                bool done = i < todayIndex || (claimed && i == todayIndex);
                bool today = i == todayIndex && !claimed;
                dayCircles[i].color = done ? DoneColor : today ? TodayColor : FutureColor;
                dayButtons[i].interactable = today;
            }
        }

        void OnDayTapped(int index)
        {
            int today = Attendance.TodayKey;
            if (busy || index != Attendance.NextDayIndex || !Attendance.ShouldOpen(today)) return;
            int amount = Attendance.Claim(today, AttendanceCatalog.Rewards);
            busy = true;
            closeButton.gameObject.SetActive(false);
            RefreshDays(index, claimed: true);
            guideText.text = "엽전 " + amount + " 받았어요!";
            GameSaveBridge.SaveFromWorld();
            StartCoroutine(ThrowRoutine());
        }

        /// <summary>받지 않고 닫기 — 그날은 다시 안 뜬다(칸은 그대로).</summary>
        void OnCloseTapped()
        {
            if (busy) return;
            Attendance.Dismiss(Attendance.TodayKey);
            GameSaveBridge.SaveFromWorld();
            Finish();
        }

        IEnumerator ThrowRoutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            throwPanel.SetActive(true);

            int gua = Attendance.RollGua(max => Random.Range(0, max), out int a, out int b, out int c);
            int[] results = { a, b, c };
            var panel = (RectTransform)throwPanel.transform;
            var land = throwLandZone != null ? throwLandZone : panel;
            for (int t = 0; t < Throws; t++)
            {
                ClearSticks();
                // 윷놀이와 같은 던지기 연출 (YutMiniGame.ThrowSticks) — 아래 가운데에서 던져 착지 영역에 떨어진다
                var origin = new Vector2(0f, -panel.rect.height * 0.45f);
                yield return YutMiniGame.ThrowSticks(this, panel, origin, land, ToThrowResult(results[t]), 0.75f, sticks);
                throwResultTexts[t].text = Attendance.ThrowName(results[t]);
                yield return new WaitForSecondsRealtime(0.6f);
            }

            yield return new WaitForSecondsRealtime(0.4f);
            ClearSticks();
            ShowFortune(gua, results);
        }

        /// <summary>윷점 0~3 → 윷놀이 결과 (윷점의 윷 = 등 4개, 모는 안 나옴).</summary>
        static YutThrowResult ToThrowResult(int v) => v switch
        {
            0 => YutThrowResult.Do,
            1 => YutThrowResult.Gae,
            2 => YutThrowResult.Geol,
            _ => YutThrowResult.Yut,
        };

        void ClearSticks()
        {
            for (int i = 0; i < sticks.Length; i++)
            {
                if (sticks[i] != null) Destroy(sticks[i].gameObject);
                sticks[i] = null;
            }
        }

        /// <summary>코드 셸(LegacyRuntime)로 만들어졌어도 한글 폰트로 덮는다.</summary>
        void ApplyFont()
        {
            if (font == null || root == null) return;
            foreach (var t in root.GetComponentsInChildren<Text>(true))
                t.font = font;
        }

        void ShowFortune(int gua, int[] results)
        {
            boardPanel.SetActive(false);
            throwPanel.SetActive(false);
            dialogPanel.SetActive(true);

            var okto = FindOkto();
            dialogPortrait.sprite = okto != null ? CharacterSpawner.FirstSprite(okto.Data) : null;
            dialogPortrait.enabled = dialogPortrait.sprite != null;
            dialogNameText.text = okto != null && okto.Data != null && !string.IsNullOrEmpty(okto.Data.displayName)
                ? okto.Data.displayName : "옥토끼";

            string guaName = Attendance.ThrowName(results[0]) + "·" + Attendance.ThrowName(results[1]) + "·"
                             + Attendance.ThrowName(results[2]);
            var f = AttendanceCatalog.Get(gua);
            string line = PickLine(f);
            string title = f != null && !string.IsNullOrEmpty(f.name) ? guaName + ", " + f.name + "이에요." : guaName + "이 나왔어요.";
            dialogBodyText.text = title + "\n" + line;
            busy = false;
        }

        static string PickLine(AttendanceCatalog.Fortune f)
        {
            if (f?.lines == null) return "오늘은 괘가 흐릿하네요. 그래도 좋은 하루 보내세요!";
            // 세 줄(일/사람/마음) 중 하나 — 빈 줄은 건너뛴다
            int start = Random.Range(0, 3);
            for (int i = 0; i < 3; i++)
            {
                int k = (start + i) % 3;
                if (k < f.lines.Length && !string.IsNullOrEmpty(f.lines[k])) return f.lines[k];
            }
            return "오늘은 괘가 흐릿하네요. 그래도 좋은 하루 보내세요!";
        }

        void OnDialogTapped()
        {
            if (busy) return;
            Finish();
        }

        void Finish()
        {
            ClearSticks();
            if (root != null) root.SetActive(false);
            IsOpen = false;
            busy = false;
            SpeechGate.Release();
        }

        static CharacterAgent FindOkto()
        {
            foreach (var a in CharacterAgent.All)
                if (a != null && a.Data != null && a.Data.id == CharacterId.Rabbit) return a;
            return null;
        }

        // ---------------- 셸 (Prefab / Bake) ----------------

        bool EnsureShell()
        {
            if (root == null)
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Build();
            }
            WireListeners();
            return root != null;
        }

        public void EnsureBuiltForBake()
        {
#if UNITY_EDITOR
            if (root == null)
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Build();
            }
            if (root != null) root.SetActive(false);
#endif
        }

        bool wired;

        void WireListeners()
        {
            if (wired) return;
            wired = true;
            for (int i = 0; i < Days; i++)
            {
                int idx = i;
                if (dayButtons[i] != null) dayButtons[i].onClick.AddListener(() => OnDayTapped(idx));
            }
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseTapped);
            if (dialogButton != null) dialogButton.onClick.AddListener(OnDialogTapped);
        }

        void Build()
        {
            var canvasGO = new GameObject("Canvas_Attendance", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // HUD·다른 팝업 위 (콜드스타트 첫 순서)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform));
            var rootRt = Stretch(root, canvasGO.transform);
            root.AddComponent<Image>().color = new Color(0f, 0f, 0.05f, 0.6f);

            // ---- 윷판 (일자 7칸) ----
            boardPanel = new GameObject("Board", typeof(RectTransform));
            var board = Place(boardPanel, rootRt, new Vector2(0.5f, 0.62f), new Vector2(1000, 640));
            boardPanel.AddComponent<Image>().color = new Color(0.2f, 0.15f, 0.11f, 0.97f);
            MakeText(board, "Title", "출석 윷점", 56, new Vector2(0.5f, 0.88f), new Vector2(800, 80));
            guideText = MakeText(board, "Guide", "", 32, new Vector2(0.5f, 0.12f), new Vector2(900, 60));

            var line = new GameObject("Line", typeof(RectTransform));
            Place(line, board, new Vector2(0.5f, 0.52f), new Vector2(840, 8));
            line.AddComponent<Image>().color = new Color(0.55f, 0.45f, 0.35f, 1f);

            for (int i = 0; i < Days; i++)
            {
                var node = new GameObject("Day" + (i + 1), typeof(RectTransform));
                Place(node, board, new Vector2(0.08f + i * (0.84f / (Days - 1)), 0.52f), new Vector2(118, 118));
                var img = node.AddComponent<Image>();
                img.sprite = CircleSprite();
                img.color = FutureColor;
                dayCircles[i] = img;
                dayButtons[i] = node.AddComponent<Button>();
                dayButtons[i].targetGraphic = img;
                dayRewardTexts[i] = MakeText(node.transform, "Reward", "", 22, new Vector2(0.5f, 0.5f), new Vector2(118, 80));
                dayRewardTexts[i].color = new Color(0.2f, 0.14f, 0.1f, 1f);
            }

            var close = new GameObject("Close", typeof(RectTransform));
            Place(close, board, new Vector2(0.95f, 0.9f), new Vector2(80, 80));
            close.AddComponent<Image>().color = new Color(0.35f, 0.3f, 0.28f, 1f);
            closeButton = close.AddComponent<Button>();
            MakeText(close.transform, "X", "X", 40, new Vector2(0.5f, 0.5f), new Vector2(80, 80));

            // ---- 윷 던지기 ----
            throwPanel = new GameObject("Throw", typeof(RectTransform));
            var tp = Place(throwPanel, rootRt, new Vector2(0.5f, 0.4f), new Vector2(900, 900));
            var landGO = new GameObject("LandZone", typeof(RectTransform));
            throwLandZone = Place(landGO, tp, new Vector2(0.5f, 0.55f), new Vector2(620, 420));
            for (int t = 0; t < Throws; t++)
            {
                throwResultTexts[t] = MakeText(tp, "Result" + t, "", 52, new Vector2(0.3f + t * 0.2f, 0.93f), new Vector2(160, 70));
                throwResultTexts[t].color = new Color(1f, 0.85f, 0.4f, 1f);
            }

            // ---- 옥토끼 스토리 대화창 (하단 슬롯을 가림) ----
            dialogPanel = new GameObject("Dialog", typeof(RectTransform));
            var dp = dialogPanel.GetComponent<RectTransform>();
            dp.SetParent(rootRt, false);
            dp.anchorMin = new Vector2(0f, 0f);
            dp.anchorMax = new Vector2(1f, 0.3f);
            dp.offsetMin = dp.offsetMax = Vector2.zero;
            dialogPanel.AddComponent<Image>().color = new Color(0.98f, 0.95f, 0.88f, 0.98f);
            dialogButton = dialogPanel.AddComponent<Button>();

            var portrait = new GameObject("Portrait", typeof(RectTransform));
            Place(portrait, dp, new Vector2(0.14f, 0.55f), new Vector2(220, 220));
            dialogPortrait = portrait.AddComponent<Image>();
            dialogPortrait.preserveAspect = true;
            dialogNameText = MakeText(dp, "Name", "옥토끼", 36, new Vector2(0.14f, 0.12f), new Vector2(260, 60));
            dialogNameText.color = new Color(0.45f, 0.25f, 0.2f, 1f);
            dialogBodyText = MakeText(dp, "Body", "", 36, new Vector2(0.62f, 0.55f), new Vector2(720, 420));
            dialogBodyText.alignment = TextAnchor.MiddleLeft;
            dialogBodyText.color = new Color(0.18f, 0.14f, 0.12f, 1f);
            var hint = MakeText(dp, "Hint", "▼ 탭해서 닫기", 24, new Vector2(0.9f, 0.1f), new Vector2(240, 40));
            hint.color = new Color(0.5f, 0.45f, 0.4f, 1f);

            throwPanel.SetActive(false);
            dialogPanel.SetActive(false);
            root.SetActive(false);
        }

        static Sprite circleSprite;

        static Sprite CircleSprite() => circleSprite != null ? circleSprite
            : (circleSprite = ProceduralSprite.Build("AttendanceCircle", 64, 100f, p =>
            {
                float d = (p - new Vector2(32f, 32f)).magnitude;
                if (d > 30f) return Color.clear;
                return d > 26f ? new Color(0.35f, 0.25f, 0.18f, 1f) : Color.white;
            }));

        static RectTransform Stretch(GameObject go, Transform parent)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            return rt;
        }

        Text MakeText(Transform parent, string name, string msg, int size, Vector2 anchor, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place(go, parent, anchor, box);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = UiFonts.Size(size);
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = msg;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
