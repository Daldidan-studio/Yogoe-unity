using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Data;
using Yoegoe.Economy;

namespace Yoegoe.UI
{
    /// <summary>
    /// 빈 캐릭터 슬롯 탭 → 소환 확인 (기획 9-1: 3번째 = 고라니, 4번째 = 구미호).
    /// 잠긴 4번째 슬롯 탭 → '엽전 99개로 열 수 있다' 안내 + 열기.
    /// </summary>
    public class SummonPopup : MonoBehaviour
    {
        public static SummonPopup Instance { get; private set; }

        public Font font;
        public CharacterData goraniData;

        GameObject root;
        Text titleText;
        Text costText;
        Button summonButton;
        Image summonButtonImage;
        Text summonButtonLabel;
        static readonly Color SummonEnabled = new Color(0.3f, 0.5f, 0.55f, 1f);
        static readonly Color SummonDisabled = new Color(0.25f, 0.25f, 0.28f, 1f);

        enum Mode { Summon, Unlock }
        Mode mode;
        CharacterId target = CharacterId.Gorani;

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

        /// <summary>빈 슬롯 소환 확인. target: 3번째 슬롯 고라니, 4번째(연 뒤) 구미호.</summary>
        public void Open(CharacterId summonTarget = CharacterId.Gorani)
        {
            if (!CharacterSummon.HasOpenSlotFor(summonTarget) || CharacterSummon.IsPresent(summonTarget)) return;
            EnsureBuilt();
            mode = Mode.Summon;
            target = summonTarget;
            titleText.text = "향 " + CharacterSummon.HyangCost + "개를 피워 요괴를 부르시겠습니까?";
            if (summonButtonLabel != null) summonButtonLabel.text = "부르기";
            RefreshAffordState();
            root.SetActive(true);
        }

        /// <summary>잠긴 4번째 슬롯: 엽전 99개로 열 수 있다는 안내 + 열기.</summary>
        public void OpenUnlock()
        {
            if (CharacterSummon.LockedSlotUnlocked) return;
            EnsureBuilt();
            mode = Mode.Unlock;
            titleText.text = "잠긴 자리입니다.\n엽전 " + CharacterSummon.LockedSlotYeopjeonCost + "개로 열 수 있어요.";
            if (summonButtonLabel != null) summonButtonLabel.text = "열기";
            RefreshAffordState();
            root.SetActive(true);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        void RefreshAffordState()
        {
            bool can;
            if (mode == Mode.Unlock)
            {
                int have = GameEconomy.Instance.Yeopjeon;
                can = have >= CharacterSummon.LockedSlotYeopjeonCost;
                costText.text = can
                    ? "엽전 " + CharacterSummon.LockedSlotYeopjeonCost + "개 소모 (보유 " + have + ")"
                    : "엽전이 부족합니다 (필요 " + CharacterSummon.LockedSlotYeopjeonCost + ", 보유 " + have + ")";
            }
            else
            {
                can = CharacterSummon.CanSummon(target);
                costText.text = can
                    ? "향 " + CharacterSummon.HyangCost + "개 소모 (보유 " + GameEconomy.Instance.Hyang + ")"
                    : "향이 부족합니다 (필요 " + CharacterSummon.HyangCost + ", 보유 " + GameEconomy.Instance.Hyang + ")";
            }
            if (summonButtonImage != null)
                summonButtonImage.color = can ? SummonEnabled : SummonDisabled;
            if (summonButtonLabel != null)
                summonButtonLabel.color = can ? Color.white : new Color(0.7f, 0.7f, 0.72f, 1f);
            // 부족해도 눌러서 상점으로 갈 수 있게 interactable 유지
            if (summonButton != null) summonButton.interactable = true;
        }

        void OnSummonClicked()
        {
            if (mode == Mode.Unlock)
            {
                if (CharacterSummon.TryUnlockLockedSlot())
                {
                    Close();
                    Yoegoe.Save.GameSaveBridge.SaveFromWorld();
                }
                else
                    RefreshAffordState(); // 엽전 부족 — 안내 유지
                return;
            }

            if (CharacterSummon.IsPresent(target))
            {
                Close();
                return;
            }

            if (!CharacterSummon.CanSummon(target))
            {
                Close();
                if (ShopScreen.Instance != null) ShopScreen.Instance.Open();
                return;
            }

            Close();
            // 절차는 CharacterSummon.TrySummon 하나 — 여기선 지금 보이는 화면에 맞는 연출만 고른다.
            // 윷 화면이 열려 있으면 맵 연출(월드)은 가려지므로 윷 화면 연출로.
            if (YutScreen.Instance != null && YutScreen.Instance.IsOpen && YutScreen.Instance.PlaySummon(target))
                return;
            if (SummonCeremony.Instance != null && SummonCeremony.Instance.TryPlay(target))
                return;

            // 연출 호스트 없으면 즉시 소환 폴백
            if (CharacterSummon.TrySummon(target, font, target == CharacterId.Gorani ? goraniData : null) == null)
                Debug.LogWarning("[SummonPopup] 소환 실패");
        }

        void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Build()
        {
            var canvasGO = new GameObject("Canvas_Summon");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // YutScreen(820)의 로스터 "소환하기" 슬롯에서도 열리므로 그보다 위에 있어야 한다 —
            // 예전엔 810이라 윷놀이 화면 뒤에 가려져서 눌러도 안 보였다.
            canvas.sortingOrder = 830;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Panel");
            var rootRt = SetupRect(root, canvasGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimBtn = root.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.onClick.AddListener(Close);

            var box = new GameObject("Box");
            SetupRect(box, rootRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(640, 380));
            var boxBg = box.AddComponent<Image>();
            boxBg.color = new Color(0.14f, 0.1f, 0.08f, 0.98f);
            var boxBlock = box.AddComponent<Button>();
            boxBlock.targetGraphic = boxBg;
            boxBlock.transition = Selectable.Transition.None;

            var titleGO = new GameObject("Title");
            SetupRect(titleGO, box.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -50), new Vector2(580, 100));
            titleText = titleGO.AddComponent<Text>();
            ApplyFont(titleText);
            titleText.fontSize = UiFonts.Size(32);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.95f, 0.85f);
            titleText.raycastTarget = false;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;

            var costGO = new GameObject("Cost");
            SetupRect(costGO, box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 10), new Vector2(560, 70));
            costText = costGO.AddComponent<Text>();
            ApplyFont(costText);
            costText.fontSize = UiFonts.Size(28);
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.85f, 0.45f);
            costText.raycastTarget = false;
            costText.horizontalOverflow = HorizontalWrapMode.Wrap;
            costText.verticalOverflow = VerticalWrapMode.Overflow;

            summonButton = CreateActionButton(box.transform, "부르기", new Vector2(-130, -120), SummonEnabled,
                OnSummonClicked, out summonButtonImage, out summonButtonLabel);
            CreateActionButton(box.transform, "닫기", new Vector2(130, -120), new Color(0.4f, 0.3f, 0.28f, 1f),
                Close, out _, out _);
        }

        void ApplyFont(Text text)
        {
            if (text == null) return;
            if (font != null) text.font = font;
        }

        Button CreateActionButton(Transform parent, string label, Vector2 pos, Color color,
            UnityEngine.Events.UnityAction onClick, out Image img, out Text labelText)
        {
            var go = new GameObject("Btn_" + label);
            SetupRect(go, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(200, 70));
            img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Label");
            SetupRect(textGO, go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            labelText = textGO.AddComponent<Text>();
            ApplyFont(labelText);
            labelText.fontSize = UiFonts.Size(32);
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;
            return btn;
        }

        static RectTransform SetupRect(GameObject go, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return rt;
        }
    }
}
