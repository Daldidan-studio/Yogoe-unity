using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yoegoe.Cooking;
using Yoegoe.Economy;

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
            session.TimeUp -= OnTimeUp;
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
        session.TimeUp += OnTimeUp;
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
            // 요리 중·연장 대기 중에 닫으면 만든 것까지 정산 (결과 팝업 없이)
            ConfirmPopup.Dismiss();
            session.RoundEnded -= OnRoundEnded;
            session.FinishNow();
            session.Changed -= RefreshView;
            session.RoundEnded -= OnRoundEnded;
            session.TimeUp -= OnTimeUp;
            session = null;
        }
    }

    bool EnsureShell()
    {
        BindMissingRefsFromHierarchy();
        if (root != null) return true;
        Debug.LogError(
            "[GongyangganScreen] Prefab 셸이 없습니다. Main 씬에 GongyangganScreen Prefab 인스턴스를 배치하세요.");
        return false;
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
            extendButton.onClick.AddListener(OnTimeUp);
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

    /// <summary>
    /// 시작 버튼. 재료가 20개 미만이면 "재료가 모자라는데도 요리할까요?" 확인 (19장).
    /// 보이는 판(미리보기)을 그대로 시작하고, 재료·부적은 이때 처음 차감된다.
    /// </summary>
    void OnStart()
    {
        if (session == null || session.Running || session.Finished) return;
        if (session.MaterialsOnBoard < CookingSession.MinMaterialsToCook)
        {
            if (statusText != null) statusText.text = "재료가 모자라 요리할 수 없어요";
            return;
        }
        if (session.IsShortBoard)
            ConfirmPopup.Show("재료가 모자라는데도 요리할까요?", BeginRound);
        else
            BeginRound();
    }

    void BeginRound()
    {
        if (session == null || session.Running || session.Finished) return;
        if (selectedCharm != CookingCharmType.None
            && (GameEconomy.Instance == null || !GameEconomy.Instance.TrySpendCharm(selectedCharm)))
        {
            // 부적이 그사이 없어졌으면 부적 없는 판으로 다시 깐다
            selectedCharm = CookingCharmType.None;
            RebuildSession();
            return;
        }
        if (!session.StartRound())
        {
            // 판에 깔린 재료가 인벤에 더는 없음 — 부적은 돌려주고 판을 다시 깐다
            if (selectedCharm != CookingCharmType.None && GameEconomy.Instance != null)
                GameEconomy.Instance.AddCharm(selectedCharm, 1);
            RebuildSession();
            return;
        }
        RefreshView();
    }

    /// <summary>선택한 부적으로 판(미리보기)을 새로 깐다. 재료는 차감하지 않는다.</summary>
    void RebuildSession()
    {
        if (session != null)
        {
            session.Changed -= RefreshView;
            session.RoundEnded -= OnRoundEnded;
            session.TimeUp -= OnTimeUp;
        }
        session = new CookingSession();
        session.Changed += RefreshView;
        session.RoundEnded += OnRoundEnded;
        session.TimeUp += OnTimeUp;
        session.Prepare(selectedCharm);
        RefreshView();
    }

    void SelectCharm(CookingCharmType charm)
    {
        if (session != null && session.Running) return;
        if (charm != CookingCharmType.None)
        {
            int held = GameEconomy.Instance != null ? GameEconomy.Instance.GetCharmCount(charm) : 0;
            if (held <= 0 && selectedCharm != charm) return;
        }
        selectedCharm = selectedCharm == charm ? CookingCharmType.None : charm;
        RebuildSession();
    }

    /// <summary>시간 종료 → "광고 보고 15초 더?" (19장, 무제한). 아니오·바깥 탭 = 그만하고 정산.
    /// 광고 SDK 미연동 — 예를 누르면 바로 연장(스텁).</summary>
    void OnTimeUp()
    {
        if (session == null || !session.AwaitingExtend) return;
        var s = session;
        ConfirmPopup.Show("시간이 다 됐어요!\n광고 보고 15초 더 할까요?",
            () => { if (session == s) s.ExtendByAd(); },
            () => { if (session == s) s.FinishAfterTimeUp(); },
            yes: "광고 보고 +15초", no: "그만하기");
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
            string charm = selectedCharm == CookingCharmType.None
                ? "부적 없음"
                : $"{CharmLabel(selectedCharm)} (보유 {(GameEconomy.Instance != null ? GameEconomy.Instance.GetCharmCount(selectedCharm) : 0)})";
            statusText.text = session.Running ? $"요리 중 · {charm}"
                : session.Finished ? $"끝 · {charm}"
                : $"준비 · 재료 {session.MaterialsOnBoard}개 · {charm}";
        }
        if (nagariButton != null)
            nagariButton.gameObject.SetActive(session.ShowNagari);
        if (extendButton != null)
            extendButton.gameObject.SetActive(session.AwaitingExtend);
        if (startButton != null)
            startButton.interactable = !session.Running && !session.Finished;

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
            int held = GameEconomy.Instance != null ? GameEconomy.Instance.GetCharmCount(t) : 0;
            bool sel = selectedCharm == t;
            bool canUse = held > 0;
            if (img != null)
            {
                if (sel) img.color = new Color(0.85f, 0.65f, 0.3f);
                else if (canUse) img.color = new Color(0.4f, 0.35f, 0.3f);
                else img.color = new Color(0.22f, 0.2f, 0.18f, 0.7f);
            }
            charmButtons[i].interactable = canUse || sel;
            var label = charmButtons[i].GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = $"{CharmLabel(t)}\n×{held}";
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
            // Bake 구조: Cell/Text(래퍼)/Text(실제 UI.Text) — 래퍼에는 Text가 없음
            var labelHost = cell.Find("Text");
            cellLabels[x, y] = labelHost != null
                ? labelHost.GetComponentInChildren<Text>(true)
                : null;
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
        if (titleText == null) titleText = FindUiText(rt, "Title/Text");
        if (timerText == null) timerText = FindUiText(rt, "Timer/Text");
        if (statusText == null) statusText = FindUiText(rt, "Status/Text");
        if (gridHost == null) gridHost = rt.Find("Grid");
        if (charmRail == null) charmRail = rt.Find("CharmRail");
        if (startButton == null) startButton = rt.Find("Start")?.GetComponent<Button>();
        if (nagariButton == null) nagariButton = rt.Find("Nagari")?.GetComponent<Button>();
        if (extendButton == null) extendButton = rt.Find("Extend")?.GetComponent<Button>();
        if (closeButton == null) closeButton = rt.Find("Close")?.GetComponent<Button>();
        if (resultPopup == null) resultPopup = rt.Find("ResultPopup")?.gameObject;
        if (resultPopup != null && resultBody == null)
            resultBody = FindUiText(resultPopup.transform, "Box/Body/Text");
    }

    static Text FindUiText(Transform root, string path)
    {
        var t = root.Find(path);
        if (t == null) return null;
        return t.GetComponent<Text>() ?? t.GetComponentInChildren<Text>(true);
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
