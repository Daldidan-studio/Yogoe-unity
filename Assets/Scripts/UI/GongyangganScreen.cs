using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yoegoe.Cooking;

namespace Yoegoe.UI
{
/// <summary>
/// 공양간(요리 미니게임). Prefab: Assets/Prefabs/UI/GongyangganScreen.prefab
/// </summary>
public class GongyangganScreen : MonoBehaviour
{
    public static GongyangganScreen Instance { get; private set; }

    public Font font;

    [SerializeField] GameObject root;
    [SerializeField] Text titleText;
    [SerializeField] Text timerText;
    [SerializeField] Text statusText;
    [SerializeField] Transform gridHost;
    [SerializeField] Transform charmRail;
    [SerializeField] Button startButton;
    [SerializeField] Button nagariButton;
    [SerializeField] Button extendButton;
    [SerializeField] Button closeButton;
    [SerializeField] GameObject resultPopup;
    [SerializeField] Text resultBody;

    readonly Image[,] cellImages = new Image[CookingSession.GridSize, CookingSession.GridSize];
    readonly Text[,] cellLabels = new Text[CookingSession.GridSize, CookingSession.GridSize];
    readonly Button[] charmButtons = new Button[5];
    CookingSession session;
    CookingCharmType selectedCharm = CookingCharmType.None;
    bool pointerDown;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        Instance = this;
    }

    /// <summary>비활성 Prefab 인스턴스도 찾아 켠 뒤 반환.</summary>
    public static GongyangganScreen Resolve()
    {
        if (Instance != null)
        {
            if (!Instance.gameObject.activeSelf)
                Instance.gameObject.SetActive(true);
            return Instance;
        }

        var found = Object.FindAnyObjectByType<GongyangganScreen>(FindObjectsInactive.Include);
        if (found != null)
        {
            if (!found.gameObject.activeSelf)
                found.gameObject.SetActive(true);
            Instance = found;
            return found;
        }

        var prefab = Resources.Load<GameObject>("UI/GongyangganScreen");
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab);
        go.name = "GongyangganScreen";
        var screen = go.GetComponent<GongyangganScreen>();
        if (screen != null) Instance = screen;
        return screen;
    }

    void Start()
    {
        if (!EnsureShell()) return;
        WireRuntimeListeners();
        if (root != null) root.SetActive(false);
        IsOpen = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (session != null)
        {
            session.Changed -= RefreshView;
            session.RoundEnded -= OnRoundEnded;
        }
    }

    void Update()
    {
        if (session != null && root != null && root.activeInHierarchy)
            session.Tick(Time.unscaledDeltaTime);
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        if (!EnsureShell()) return;
        WireRuntimeListeners();
        selectedCharm = CookingCharmType.None;
        session = new CookingSession();
        session.Changed += RefreshView;
        session.RoundEnded += OnRoundEnded;
        session.Prepare(CookingCharmType.None);
        if (resultPopup != null) resultPopup.SetActive(false);
        root.SetActive(true);
        IsOpen = true;
        RefreshView();
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
        IsOpen = false;
        if (session != null)
        {
            session.Changed -= RefreshView;
            session.RoundEnded -= OnRoundEnded;
            session = null;
        }
    }

    bool EnsureShell()
    {
        BindMissingRefsFromHierarchy();
        if (root != null) return true;

        // Prefab 참조가 깨진 인스턴스 복구 (한 번만 셸 재생성)
        Debug.LogWarning(
            "[GongyangganScreen] Prefab 셸 참조가 비어 있어 복구합니다. " +
            "가능하면 Yoegoe → Bake GongyangganScreen Prefab 을 다시 실행하세요.");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
        BindMissingRefsFromHierarchy();
        if (root != null) return true;

        Debug.LogError(
            "[GongyangganScreen] Prefab 셸이 없습니다. Yoegoe → Bake GongyangganScreen Prefab 을 실행하세요.");
        return false;
    }

    public void EnsureBuiltForBake()
    {
#if UNITY_EDITOR
        if (root == null)
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }
        WireRuntimeListeners();
        if (root != null) root.SetActive(true);
#else
        EnsureShell();
#endif
    }

    void WireRuntimeListeners()
    {
        if (root == null) return;
        BindMissingRefsFromHierarchy();
        CacheGridCells();
        CacheCharmButtons();

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStart);
        }
        if (nagariButton != null)
        {
            nagariButton.onClick.RemoveAllListeners();
            nagariButton.onClick.AddListener(() => session?.CancelNagari());
        }
        if (extendButton != null)
        {
            extendButton.onClick.RemoveAllListeners();
            extendButton.onClick.AddListener(() => session?.ExtendByAd());
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        if (resultPopup != null)
        {
            var closeRes = resultPopup.transform.Find("Box/Close");
            if (closeRes != null)
            {
                var b = closeRes.GetComponent<Button>() ?? closeRes.gameObject.AddComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() =>
                {
                    resultPopup.SetActive(false);
                    Close();
                });
            }
        }
    }

    void OnStart()
    {
        if (session == null) return;
        // 시작 전 선택 부적으로 재딜
        if (!session.Running)
        {
            session.Changed -= RefreshView;
            session.RoundEnded -= OnRoundEnded;
            session = new CookingSession();
            session.Changed += RefreshView;
            session.RoundEnded += OnRoundEnded;
            session.Prepare(selectedCharm);
        }
        session.StartRound();
        RefreshView();
    }

    void SelectCharm(CookingCharmType charm)
    {
        if (session != null && session.Running) return;
        selectedCharm = selectedCharm == charm ? CookingCharmType.None : charm;
        if (session != null)
        {
            session.Changed -= RefreshView;
            session.RoundEnded -= OnRoundEnded;
        }
        session = new CookingSession();
        session.Changed += RefreshView;
        session.RoundEnded += OnRoundEnded;
        session.Prepare(selectedCharm);
        RefreshView();
    }

    void OnRoundEnded()
    {
        if (resultPopup == null || resultBody == null || session == null) return;
        var sb = new System.Text.StringBuilder();
        if (session.Results.Count == 0)
            sb.Append("이번 판 획득 없음");
        else
        {
            sb.AppendLine("획득:");
            foreach (var (recipe, count) in session.Results)
                sb.AppendLine($"· {recipe.DisplayName} x{count}");
        }
        resultBody.text = sb.ToString();
        resultPopup.SetActive(true);
        RefreshView();
    }

    void RefreshView()
    {
        if (session == null) return;
        if (timerText != null)
        {
            timerText.text = $"{session.TimeLeft:0.0}s";
            timerText.color = session.TimeLeft <= 5f
                ? new Color(0.9f, 0.25f, 0.2f)
                : Color.white;
        }
        if (statusText != null)
        {
            string charm = selectedCharm == CookingCharmType.None ? "부적 없음" : CharmLabel(selectedCharm);
            statusText.text = session.Running ? $"요리 중 · {charm}" : $"준비 · {charm}";
        }
        if (nagariButton != null)
            nagariButton.gameObject.SetActive(session.ShowNagari);
        if (extendButton != null)
            extendButton.gameObject.SetActive(session.AllowAdExtend && (session.Running || session.Finished));
        if (startButton != null)
            startButton.interactable = !session.Running;

        for (int y = 0; y < CookingSession.GridSize; y++)
        for (int x = 0; x < CookingSession.GridSize; x++)
        {
            var img = cellImages[x, y];
            var label = cellLabels[x, y];
            if (img == null) continue;
            bool onPath = false;
            if (session.Path != null)
            {
                for (int i = 0; i < session.Path.Count; i++)
                    if (session.Path[i].x == x && session.Path[i].y == y) { onPath = true; break; }
            }

            if (session.Locked[x, y] || !session.Grid[x, y].HasValue)
            {
                img.color = new Color(0.15f, 0.12f, 0.1f, 0.55f);
                if (label != null) label.text = "";
            }
            else
            {
                img.color = onPath
                    ? new Color(0.95f, 0.75f, 0.35f, 1f)
                    : new Color(0.35f, 0.28f, 0.22f, 1f);
                if (label != null)
                    label.text = CookingRecipeCatalog.DisplayName(session.Grid[x, y].Value);
            }
        }

        for (int i = 0; i < charmButtons.Length; i++)
        {
            if (charmButtons[i] == null) continue;
            var img = charmButtons[i].GetComponent<Image>();
            var t = CharmAt(i);
            bool sel = selectedCharm == t;
            if (img != null)
                img.color = sel ? new Color(0.85f, 0.65f, 0.3f) : new Color(0.4f, 0.35f, 0.3f);
        }
    }

    static CookingCharmType CharmAt(int i) => i switch
    {
        0 => CookingCharmType.PlusFive,
        1 => CookingCharmType.Diagonal,
        2 => CookingCharmType.Clairvoyance,
        3 => CookingCharmType.Recycle,
        4 => CookingCharmType.Double,
        _ => CookingCharmType.None
    };

    static string CharmLabel(CookingCharmType t) => t switch
    {
        CookingCharmType.PlusFive => "+5초",
        CookingCharmType.Diagonal => "대각선",
        CookingCharmType.Clairvoyance => "천리안",
        CookingCharmType.Recycle => "회수",
        CookingCharmType.Double => "몰빵",
        _ => ""
    };

    void CacheGridCells()
    {
        if (gridHost == null) return;
        for (int y = 0; y < CookingSession.GridSize; y++)
        for (int x = 0; x < CookingSession.GridSize; x++)
        {
            var cell = gridHost.Find($"Cell_{x}_{y}");
            if (cell == null) continue;
            cellImages[x, y] = cell.GetComponent<Image>();
            cellLabels[x, y] = cell.Find("Text")?.GetComponent<Text>();
            EnsureCellPointer(cell.gameObject, x, y);
        }
    }

    void CacheCharmButtons()
    {
        if (charmRail == null) return;
        for (int i = 0; i < charmButtons.Length; i++)
        {
            var t = charmRail.Find("Charm_" + i);
            if (t == null) continue;
            charmButtons[i] = t.GetComponent<Button>();
            int captured = i;
            if (charmButtons[i] != null)
            {
                charmButtons[i].onClick.RemoveAllListeners();
                charmButtons[i].onClick.AddListener(() => SelectCharm(CharmAt(captured)));
            }
        }
    }

    void EnsureCellPointer(GameObject cell, int x, int y)
    {
        var proxy = cell.GetComponent<GongyangganCellProxy>()
                    ?? cell.AddComponent<GongyangganCellProxy>();
        proxy.Bind(this, x, y);
    }

    public void OnCellDown(int x, int y)
    {
        pointerDown = true;
        session?.TryBeginPath(x, y);
    }

    public void OnCellEnter(int x, int y)
    {
        if (!pointerDown) return;
        session?.TryExtendPath(x, y);
    }

    public void OnCellUp()
    {
        if (!pointerDown) return;
        pointerDown = false;
        session?.EndPath();
    }

    void BindMissingRefsFromHierarchy()
    {
        if (root == null)
        {
            var canvas = transform.Find("Canvas_Gongyanggan");
            if (canvas != null) root = canvas.Find("Root")?.gameObject;
        }
        if (root == null)
        {
            // Prefab 인스턴스에서 Find 경로가 어긋날 때 대비
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t != null && t.name == "Root" && t.parent != null
                    && t.parent.name == "Canvas_Gongyanggan")
                {
                    root = t.gameObject;
                    break;
                }
            }
        }
        if (root == null) return;
        var rt = root.transform;
        if (titleText == null) titleText = rt.Find("Title/Text")?.GetComponent<Text>();
        if (timerText == null) timerText = rt.Find("Timer/Text")?.GetComponent<Text>();
        if (statusText == null) statusText = rt.Find("Status/Text")?.GetComponent<Text>();
        if (gridHost == null) gridHost = rt.Find("Grid");
        if (charmRail == null) charmRail = rt.Find("CharmRail");
        if (startButton == null) startButton = rt.Find("Start")?.GetComponent<Button>();
        if (nagariButton == null) nagariButton = rt.Find("Nagari")?.GetComponent<Button>();
        if (extendButton == null) extendButton = rt.Find("Extend")?.GetComponent<Button>();
        if (closeButton == null) closeButton = rt.Find("Close")?.GetComponent<Button>();
        if (resultPopup == null) resultPopup = rt.Find("ResultPopup")?.gameObject;
        if (resultPopup != null && resultBody == null)
            resultBody = resultPopup.transform.Find("Box/Body/Text")?.GetComponent<Text>();
    }

    void Build()
    {
        var canvasGO = new GameObject("Canvas_Gongyanggan", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
        var rootRt = Stretch(root, canvasGO.transform);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.1f, 0.08f, 0.96f);

        titleText = MakeLabel(rootRt, "Title", "공양간", 64, new Vector2(0.5f, 0.93f), new Vector2(600, 80));
        timerText = MakeLabel(rootRt, "Timer", "15.0s", 48, new Vector2(0.85f, 0.93f), new Vector2(200, 60));
        statusText = MakeLabel(rootRt, "Status", "준비", 32, new Vector2(0.5f, 0.87f), new Vector2(800, 50));

        var railGO = new GameObject("CharmRail", typeof(RectTransform));
        Place(railGO, rootRt, new Vector2(0.08f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(120, 520));
        charmRail = railGO.transform;
        string[] charmNames = { "+5초", "대각", "천리안", "회수", "몰빵" };
        for (int i = 0; i < 5; i++)
        {
            var c = new GameObject("Charm_" + i, typeof(RectTransform));
            Place(c, charmRail, new Vector2(0.5f, 1f - (i + 0.5f) / 5f), new Vector2(0.5f, 0.5f), new Vector2(100, 90));
            var img = c.AddComponent<Image>();
            img.color = new Color(0.4f, 0.35f, 0.3f);
            c.AddComponent<Button>();
            MakeLabel(c.transform, "Text", charmNames[i], 28, new Vector2(0.5f, 0.5f), new Vector2(100, 40));
        }

        var gridGO = new GameObject("Grid", typeof(RectTransform));
        Place(gridGO, rootRt, new Vector2(0.58f, 0.52f), new Vector2(0.5f, 0.5f), new Vector2(720, 720));
        gridHost = gridGO.transform;
        float cell = 130f;
        float gap = 8f;
        float origin = -2f * (cell + gap);
        for (int y = 0; y < CookingSession.GridSize; y++)
        for (int x = 0; x < CookingSession.GridSize; x++)
        {
            var cellGO = new GameObject($"Cell_{x}_{y}", typeof(RectTransform));
            var crt = cellGO.GetComponent<RectTransform>();
            crt.SetParent(gridHost, false);
            crt.sizeDelta = new Vector2(cell, cell);
            crt.anchoredPosition = new Vector2(origin + x * (cell + gap), -origin - y * (cell + gap));
            var img = cellGO.AddComponent<Image>();
            img.color = new Color(0.35f, 0.28f, 0.22f);
            MakeLabel(crt, "Text", "", 22, new Vector2(0.5f, 0.5f), new Vector2(120, 60));
        }

        startButton = MakeButton(rootRt, "Start", "불 지피기", new Vector2(0.5f, 0.14f), new Vector2(280, 80));
        nagariButton = MakeButton(rootRt, "Nagari", "나가리", new Vector2(0.22f, 0.14f), new Vector2(180, 70));
        extendButton = MakeButton(rootRt, "Extend", "광고 +15초", new Vector2(0.78f, 0.14f), new Vector2(220, 70));
        closeButton = MakeButton(rootRt, "Close", "닫기", new Vector2(0.08f, 0.93f), new Vector2(120, 60));

        resultPopup = new GameObject("ResultPopup", typeof(RectTransform));
        Stretch(resultPopup, rootRt);
        var dim = resultPopup.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.55f);
        var box = new GameObject("Box", typeof(RectTransform));
        Place(box, resultPopup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(700, 500));
        box.AddComponent<Image>().color = new Color(0.2f, 0.16f, 0.12f, 1f);
        MakeLabel(box.transform, "Body", "결과", 36, new Vector2(0.5f, 0.55f), new Vector2(620, 320));
        resultBody = box.transform.Find("Body/Text")?.GetComponent<Text>();
        MakeButton(box.transform, "Close", "확인", new Vector2(0.5f, 0.12f), new Vector2(200, 70));
        resultPopup.SetActive(false);
    }

    Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Place(go, parent, anchor, new Vector2(0.5f, 0.5f), size);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.55f, 0.4f, 0.25f);
        var btn = go.AddComponent<Button>();
        MakeLabel(go.transform, "Text", label, 32, new Vector2(0.5f, 0.5f), size);
        return btn;
    }

    Text MakeLabel(Transform parent, string name, string text, int size, Vector2 anchor, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Place(go, parent, anchor, new Vector2(0.5f, 0.5f), sizeDelta);
        var tgo = new GameObject("Text", typeof(RectTransform));
        Stretch(tgo, go.transform);
        var t = tgo.AddComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static RectTransform Stretch(GameObject go, Transform parent)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }
}

/// <summary>그리드 셀 드래그 입력.</summary>
public class GongyangganCellProxy : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
{
    GongyangganScreen screen;
    int x, y;

    public void Bind(GongyangganScreen s, int cx, int cy)
    {
        screen = s;
        x = cx;
        y = cy;
    }

    public void OnPointerDown(PointerEventData eventData) => screen?.OnCellDown(x, y);
    public void OnPointerEnter(PointerEventData eventData) => screen?.OnCellEnter(x, y);
    public void OnPointerUp(PointerEventData eventData) => screen?.OnCellUp();
}
}
