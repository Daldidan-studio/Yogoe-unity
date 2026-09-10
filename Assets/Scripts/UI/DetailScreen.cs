using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.Characters;
using Yoegoe.Core;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.Save;

namespace Yoegoe.UI
{
    /// <summary>
    /// 캐릭터 상세화면. 좌 초상 / 우 이름·스탯·설명 / 하단 정화수·선호공양·인벤토리.
    /// 정화수·공양물은 드래그해서 본문(초상·정보) 위에 놓아 먹인다.
    /// </summary>
    public class DetailScreen : MonoBehaviour
    {
        public Font font;
        public OfferingData[] offerings;
        public Sprite purifiedWaterIcon;

        private CharacterAgent currentAgent;
        private GameObject root;
        private Canvas rootCanvas;
        private Image portraitImage;
        private RectTransform portraitRt;
        private RectTransform portraitDropRt;
        /// <summary>급여 드롭 판정용 — 초상만이 아니라 본문 전체(정보 패널 포함).</summary>
        private RectTransform feedDropRt;
        private Image portraitPanelHighlight;
        private Vector3 portraitBaseScale = Vector3.one;
        private Text nameValueText;
        private Text statusText;
        private Text intimacyLevelText;
        private Image intimacyFill;
        private RectTransform intimacyFillRt;
        private Text staminaValueText;
        private Image staminaFill;
        private RectTransform staminaFillRt;
        private Text descriptionText;
        private RectTransform preferredRow;
        private GameObject preferredHostGO;
        private GameObject inventoryButtonGO;
        private GameObject intimacyColGO;
        private GameObject inventoryPanel;
        private RectTransform inventoryRow;
        private Text feedHintText;
        private Text purifiedCountText;
        private CanvasGroup purifiedDragGroup;
        private readonly List<CountBadge> offeringCountBadges = new List<CountBadge>();
        private Sprite neokPlaceholderSprite;
        private GrowthStage lastPortraitStage = GrowthStage.Hon;
        private Coroutine portraitEvolveFx;
        private bool offeringDragActive;
        /// <summary>드래그 중 한 프레임이라도 드롭존 위였으면 터치 릴리즈 지터로 실패하지 않게.</summary>
        private bool feedDropHoverLatched;

        struct CountBadge
        {
            public Text Label;
            public OfferingData Offering;
            public bool PurifiedWater;
        }

        static readonly Color Bg = new Color(0.90f, 0.90f, 0.92f, 1f);
        static readonly Color Panel = new Color(1f, 1f, 1f, 1f);
        static readonly Color Border = new Color(0.15f, 0.15f, 0.18f, 1f);
        static readonly Color PortraitBg = new Color(0.78f, 0.88f, 0.95f, 1f);
        static readonly Color PortraitHighlight = new Color(0.55f, 0.85f, 0.55f, 1f);
        static readonly Color LabelDark = new Color(0.12f, 0.12f, 0.14f, 1f);
        static readonly Color AccentBlue = new Color(0.35f, 0.55f, 0.95f, 1f);
        static readonly Color IntimacyPink = new Color(0.92f, 0.35f, 0.45f, 1f);
        static readonly Color StaminaGreen = new Color(0.35f, 0.78f, 0.42f, 1f);
        static readonly Color BarTrack = new Color(0.85f, 0.85f, 0.87f, 1f);

        private void Start()
        {
            EnsureBuilt();
            root.SetActive(false);
        }

        private void Update()
        {
            if (currentAgent == null || root == null || !root.activeSelf) return;
            RefreshStats();
        }

        public void Open(CharacterAgent agent, string highlightOfferingId = null)
        {
            EnsureBuilt();
            if (portraitEvolveFx != null)
            {
                StopCoroutine(portraitEvolveFx);
                portraitEvolveFx = null;
            }

            currentAgent = agent;
            root.SetActive(true);
            if (inventoryPanel != null) inventoryPanel.SetActive(false);

            if (agent.Data != null)
                CharacterCatalog.ApplyTo(agent.Data);

            lastPortraitStage = agent.Stats.Stage;
            ApplyStageLayout(agent.Stats.Stage == GrowthStage.Neok);
            RefreshDescription();
            RefreshIdentity();
            ApplyPortraitImmediate(agent.Stats.Stage);
            RebuildPreferredRow();
            RebuildInventoryRow(highlightOfferingId);
            RefreshStats();
            RefreshItemCounts();

            if (!string.IsNullOrEmpty(highlightOfferingId) && inventoryPanel != null)
                inventoryPanel.SetActive(true);
        }

        /// <summary>
        /// 넋: 초상(넋)·기력·정화수만 활성. 친밀도·선호공양·인벤토리 비활성.
        /// </summary>
        void ApplyStageLayout(bool isNeok)
        {
            if (preferredHostGO != null) preferredHostGO.SetActive(!isNeok);
            if (inventoryButtonGO != null) inventoryButtonGO.SetActive(!isNeok);
            if (inventoryPanel != null && isNeok) inventoryPanel.SetActive(false);

            if (intimacyColGO != null)
            {
                intimacyColGO.SetActive(true);
                var cg = intimacyColGO.GetComponent<CanvasGroup>();
                if (cg == null) cg = intimacyColGO.AddComponent<CanvasGroup>();
                cg.alpha = isNeok ? 0.35f : 1f;
                cg.interactable = !isNeok;
                cg.blocksRaycasts = !isNeok;
            }

            if (purifiedDragGroup != null)
            {
                purifiedDragGroup.alpha = 1f;
                purifiedDragGroup.blocksRaycasts = true;
                purifiedDragGroup.interactable = true;
            }

            if (feedHintText != null)
            {
                feedHintText.text = DefaultFeedHint();
            }
        }

        void RefreshDescription()
        {
            if (descriptionText == null) return;
            string desc = "";
            if (currentAgent?.Data != null)
            {
                desc = currentAgent.Data.detailDescription;
                if (string.IsNullOrEmpty(desc)
                    && CharacterCatalog.TryGet(currentAgent.Data.id, out var entry)
                    && entry != null)
                    desc = entry.detailDescription;
            }
            if (string.IsNullOrEmpty(desc) && currentAgent != null
                && currentAgent.Stats.Stage == GrowthStage.Neok)
                desc = "소환된 넋. 정화수로 기력을 채워 혼으로 진화한다.";
            descriptionText.text = desc ?? "";
        }

        public void Close()
        {
            if (portraitEvolveFx != null)
            {
                StopCoroutine(portraitEvolveFx);
                portraitEvolveFx = null;
            }
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (root != null) root.SetActive(false);
            currentAgent = null;
        }

        private void EnsureBuilt()
        {
            if (root != null) return;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        // ---------------- refresh ----------------

        private void RefreshIdentity()
        {
            if (currentAgent == null || nameValueText == null) return;
            var data = currentAgent.Data;
            string name = data != null ? data.displayName : "?";
            if (string.IsNullOrEmpty(name) || name == "?")
            {
                if (data != null && CharacterCatalog.TryGet(data.id, out var entry) && entry != null)
                    name = entry.displayName;
            }
            bool isNeok = currentAgent.Stats.Stage == GrowthStage.Neok;
            nameValueText.text = name + ", " + (isNeok ? "넋" : "혼");
            if (statusText != null)
                statusText.text = isNeok ? "넋" : CharacterStatusPresentation.ForDetail(currentAgent.Stats.State);
        }

        private void RefreshStats()
        {
            if (currentAgent == null) return;
            var stats = currentAgent.Stats;
            bool isNeok = stats.Stage == GrowthStage.Neok;

            if (preferredHostGO != null && preferredHostGO.activeSelf == isNeok)
                ApplyStageLayout(isNeok);

            int stamina = Mathf.RoundToInt(stats.Stamina);
            if (staminaValueText != null)
                staminaValueText.text = stamina + "/100";
            SetBarFill(staminaFillRt, Mathf.Clamp01(stats.Stamina / 100f));
            if (staminaFill != null)
                staminaFill.color = CharacterStatusPresentation.ForStaminaBar(stats.State, Time.unscaledTime);

            if (isNeok)
            {
                if (intimacyLevelText != null) intimacyLevelText.text = "—";
                SetBarFill(intimacyFillRt, 0f);
            }
            else
            {
                int lv = Mathf.Clamp(Mathf.FloorToInt(stats.Intimacy / 20f), 0, 5);
                if (stats.Intimacy > 0f && lv == 0) lv = 1;
                if (intimacyLevelText != null)
                    intimacyLevelText.text = "Lv. " + lv;
                SetBarFill(intimacyFillRt, Mathf.Clamp01(stats.Intimacy / 100f));
            }

            RefreshIdentity();
            RefreshPortrait();
            RefreshItemCounts();
        }

        private void RefreshItemCounts()
        {
            if (purifiedCountText != null)
                purifiedCountText.text = "x" + GameEconomy.Instance.PurifiedWater;

            for (int i = 0; i < offeringCountBadges.Count; i++)
            {
                var b = offeringCountBadges[i];
                if (b.Label == null) continue;
                int n = b.PurifiedWater
                    ? GameEconomy.Instance.PurifiedWater
                    : GameEconomy.Instance.GetOfferingCount(b.Offering);
                b.Label.text = "x" + n;
            }
        }

        static void SetBarFill(RectTransform fillRt, float ratio)
        {
            if (fillRt == null) return;
            var parentRt = fillRt.parent as RectTransform;
            float parentW = parentRt != null ? parentRt.rect.width : 160f;
            if (parentW < 1f) parentW = 160f;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(parentW * ratio, 0f);
            fillRt.localScale = Vector3.one;
        }

        private void RefreshPortrait()
        {
            if (portraitImage == null || currentAgent == null) return;
            if (portraitEvolveFx != null) return;

            var stage = currentAgent.Stats.Stage;
            if (lastPortraitStage == GrowthStage.Neok && stage == GrowthStage.Hon)
            {
                portraitEvolveFx = StartCoroutine(PortraitEvolveFxRoutine());
                lastPortraitStage = stage;
                return;
            }

            lastPortraitStage = stage;
            ApplyPortraitImmediate(stage);
        }

        private void ApplyPortraitImmediate(GrowthStage stage)
        {
            if (portraitImage == null) return;
            if (portraitRt != null) portraitRt.localScale = portraitBaseScale;

            if (stage == GrowthStage.Neok)
            {
                portraitImage.sprite = GetNeokPlaceholderSprite();
                portraitImage.color = new Color(0.45f, 0.85f, 1f, 1f);
            }
            else
            {
                portraitImage.sprite = currentAgent != null ? FirstSprite(currentAgent.Data) : null;
                portraitImage.color = Color.white;
            }
            portraitImage.preserveAspect = true;
        }

        private IEnumerator PortraitEvolveFxRoutine()
        {
            Color neokColor = portraitImage.color;
            Vector3 startScale = portraitRt != null ? portraitRt.localScale : Vector3.one;
            const float fadeOut = 0.35f;
            float t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / fadeOut);
                float e = u * u;
                var c = neokColor;
                c.a = 1f - e;
                portraitImage.color = c;
                if (portraitRt != null)
                    portraitRt.localScale = Vector3.Lerp(startScale, startScale * 0.7f, e);
                yield return null;
            }

            portraitImage.sprite = FirstSprite(currentAgent != null ? currentAgent.Data : null);
            portraitImage.preserveAspect = true;
            var honColor = Color.white;
            honColor.a = 0f;
            portraitImage.color = honColor;
            if (portraitRt != null) portraitRt.localScale = portraitBaseScale * 0.8f;

            const float fadeIn = 0.5f;
            t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeIn));
                honColor.a = u;
                portraitImage.color = honColor;
                if (portraitRt != null)
                    portraitRt.localScale = Vector3.Lerp(portraitBaseScale * 0.8f, portraitBaseScale, u);
                yield return null;
            }

            portraitImage.color = Color.white;
            if (portraitRt != null) portraitRt.localScale = portraitBaseScale;
            portraitEvolveFx = null;
            ApplyStageLayout(false);
            RefreshDescription();
            RebuildPreferredRow();
            RebuildInventoryRow();
            RefreshStats();
        }

        // ---------------- feed / drag ----------------

        public void NotifyOfferingDragBegan()
        {
            offeringDragActive = true;
            feedDropHoverLatched = false;
            if (feedHintText != null)
                feedHintText.text = "캐릭터 위에 놓아 공양하세요";
            SetPortraitDropHighlight(false);
        }

        public void NotifyOfferingDragMoved(Vector2 screenPos)
        {
            bool over = IsOverFeedTarget(screenPos);
            if (over) feedDropHoverLatched = true;
            SetPortraitDropHighlight(over || feedDropHoverLatched);
        }

        public void NotifyOfferingDragEnded(bool accepted)
        {
            offeringDragActive = false;
            feedDropHoverLatched = false;
            SetPortraitDropHighlight(false);
            if (feedHintText == null) return;
            if (accepted)
            {
                feedHintText.text = "";
                return;
            }
            // 실패 사유를 NotifyFeedBlocked 로 이미 넣었으면 유지
            if (!string.IsNullOrEmpty(feedHintText.text)
                && feedHintText.text != "캐릭터 위에 놓아 공양하세요")
                return;
            feedHintText.text = DefaultFeedHint();
        }

        public void NotifyFeedBlocked(string reason)
        {
            if (feedHintText != null && !string.IsNullOrEmpty(reason))
                feedHintText.text = reason;
        }

        string DefaultFeedHint()
        {
            bool isNeok = currentAgent != null && currentAgent.Stats.Stage == GrowthStage.Neok;
            return isNeok
                ? "정화수를 드래그해 넋에게 먹이세요"
                : "정화수·공양물을 드래그해 캐릭터에게 먹이세요";
        }

        public bool TryAcceptOfferingDrop(Vector2 screenPos, OfferingData offering, bool purifiedWater)
        {
            bool overNow = IsOverFeedTarget(screenPos);
            if (!overNow && !feedDropHoverLatched)
            {
                NotifyFeedBlocked("캐릭터(초상·정보) 위에 놓아 주세요");
                return false;
            }

            if (CeremonyGate.BlocksWorldInput)
            {
                NotifyFeedBlocked("연출이 끝난 뒤 다시 시도해 주세요");
                return false;
            }

            bool isNeok = currentAgent != null && currentAgent.Stats.Stage == GrowthStage.Neok;
            if (isNeok)
            {
                // 넋은 정화수만
                if (!(purifiedWater || (offering != null && IsPurified(offering))))
                {
                    NotifyFeedBlocked("넋은 정화수만 먹을 수 있어요");
                    return false;
                }
                return OnFeedPurifiedWater();
            }
            if (purifiedWater || (offering != null && IsPurified(offering)))
                return OnFeedPurifiedWater();
            if (offering == null) return false;
            return OnFeed(offering);
        }

        bool IsOverFeedTarget(Vector2 screenPos)
        {
            var target = feedDropRt != null ? feedDropRt : portraitDropRt;
            if (target == null) return false;
            Camera cam = null;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = rootCanvas.worldCamera;
            // 터치 릴리즈 오차 여유 (레퍼런스 해상도 기준 로컬 유닛)
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target, screenPos, cam, out var local))
                return false;
            var r = target.rect;
            const float pad = 28f;
            r.xMin -= pad;
            r.xMax += pad;
            r.yMin -= pad;
            r.yMax += pad;
            return r.Contains(local);
        }

        void SetPortraitDropHighlight(bool on)
        {
            if (portraitPanelHighlight == null) return;
            portraitPanelHighlight.color = on ? PortraitHighlight : PortraitBg;
        }

        private bool OnFeedPurifiedWater()
        {
            if (currentAgent == null) return false;

            if (GameEconomy.Instance == null || GameEconomy.Instance.PurifiedWater < 1)
            {
                NotifyFeedBlocked("정화수가 없어요");
                return false;
            }

            // 혼 기력 풀이면 소모만 되고 변화가 없어 "안 먹힌다"로 보임 → 낭비 방지
            if (currentAgent.Stats.Stage != GrowthStage.Neok
                && currentAgent.Stats.Stamina >= 100f - 0.001f)
            {
                NotifyFeedBlocked("기력이 가득 찼어요");
                return false;
            }

            if (!GameEconomy.Instance.TrySpendPurifiedWater(1))
            {
                NotifyFeedBlocked("정화수가 없어요");
                return false;
            }

            var pw = FindPurifiedWater();
            int gain = pw != null ? pw.staminaGain : 20;
            currentAgent.ReceiveOffering(gain, 0f, OfferingKind.PurifiedWater);
            PlayGainPopup(gain, 0f);
            RefreshStats();
            RefreshItemCounts();
            if (currentAgent.Stats.Stage == GrowthStage.Hon)
                GameSaveBridge.SaveFromWorld();
            return true;
        }

        private bool OnFeed(OfferingData offering)
        {
            if (currentAgent == null || offering == null) return false;

            if (IsPurified(offering))
                return OnFeedPurifiedWater();

            if (currentAgent.Stats.Stage == GrowthStage.Neok)
            {
                NotifyFeedBlocked("넋은 정화수만 먹을 수 있어요");
                return false;
            }

            bool preferred = IsPreferred(offering);
            bool hasRequest = currentAgent.Requests != null && currentAgent.Requests.HasOfferingRequest;
            bool staminaFull = currentAgent.Stats.Stamina >= 100f - 0.001f;

            // 기력 풀 + 요구 없음 + 비선호 → 체감상 "안 먹힘". 선호·요구 이행은 허용.
            if (staminaFull && !hasRequest && !preferred)
            {
                NotifyFeedBlocked("기력이 가득 찼어요");
                return false;
            }

            if (GameEconomy.Instance == null || !GameEconomy.Instance.TrySpendOffering(offering, 1))
            {
                NotifyFeedBlocked("공양물이 없어요");
                return false;
            }

            var kind = preferred ? OfferingKind.Preferred : OfferingKind.General;
            int staminaGain = offering.staminaGain > 0 ? offering.staminaGain : 20;
            float intimacyGain = preferred ? 0.25f : 0f;

            bool clearedOfferingRequest = false;
            if (currentAgent.Requests != null
                && currentAgent.Requests.TryHandleFeed(offering, false, preferred,
                    out int reqStamina, out float reqIntimacy, out _))
            {
                staminaGain = reqStamina;
                intimacyGain = reqIntimacy;
                if (intimacyGain > 0f) kind = OfferingKind.Preferred;
                clearedOfferingRequest = true;
            }

            currentAgent.ReceiveOffering(staminaGain, intimacyGain, kind);
            PlayGainPopup(staminaGain, intimacyGain);
            RefreshStats();
            RefreshItemCounts();
            if (clearedOfferingRequest)
                RebuildInventoryRow();
            if (currentAgent.Stats.Stage == GrowthStage.Hon)
                GameSaveBridge.SaveFromWorld();
            return true;
        }

        void PlayGainPopup(int staminaGain, float intimacyGain)
        {
            if (rootCanvas == null || portraitDropRt == null) return;
            OfferingGainPopup.Play(rootCanvas.transform, portraitDropRt, font, staminaGain, intimacyGain);
        }

        static bool IsPurified(OfferingData offering)
        {
            if (offering == null) return false;
            return offering.kind == OfferingKind.PurifiedWater
                || string.Equals(offering.offeringId, "purifiedwater", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPreferred(OfferingData offering)
        {
            if (offering == null || currentAgent?.Data == null) return false;
            if (CharacterCatalog.TryGet(currentAgent.Data.id, out var entry) && entry?.preferredOfferings != null)
            {
                foreach (var p in entry.preferredOfferings)
                {
                    if (p != null && string.Equals(p.id, offering.offeringId, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            var prefs = currentAgent.Data.preferredOfferings;
            if (prefs == null) return false;
            foreach (var p in prefs)
                if (p == offering) return true;
            return false;
        }

        private OfferingData FindPurifiedWater()
        {
            if (offerings == null) return null;
            foreach (var o in offerings)
            {
                if (o == null) continue;
                if (IsPurified(o)) return o;
            }
            return CharacterCatalog.FindOffering("purifiedwater");
        }

        private void ToggleInventory()
        {
            if (inventoryPanel == null) return;
            bool show = !inventoryPanel.activeSelf;
            inventoryPanel.SetActive(show);
            if (show) RebuildInventoryRow();
        }

        private void RebuildPreferredRow()
        {
            if (preferredRow == null) return;
            ClearChildren(preferredRow);
            PruneDeadCountBadges();

            if (currentAgent != null && currentAgent.Stats.Stage == GrowthStage.Neok)
                return;

            CharacterCatalog.PreferredOffering[] prefs = null;
            if (currentAgent?.Data != null
                && CharacterCatalog.TryGet(currentAgent.Data.id, out var entry)
                && entry != null)
                prefs = entry.preferredOfferings;

            if (prefs == null || prefs.Length == 0)
            {
                var soPrefs = currentAgent?.Data?.preferredOfferings;
                if (soPrefs != null)
                {
                    foreach (var o in soPrefs)
                    {
                        if (o == null) continue;
                        CreatePreferredChip(preferredRow, o.displayName, o.icon, o);
                    }
                }
                return;
            }

            foreach (var p in prefs)
            {
                if (p == null) continue;
                var resolved = CharacterCatalog.FindOffering(p.id);
                string label = !string.IsNullOrEmpty(p.name) ? p.name : p.id;
                CreatePreferredChip(preferredRow, label, resolved != null ? resolved.icon : null, resolved);
            }
        }

        private void RebuildInventoryRow(string highlightOfferingId = null)
        {
            if (inventoryRow == null) return;
            ClearChildren(inventoryRow);
            PruneDeadCountBadges();
            if (offerings == null) return;

            // 요구 공양을 앞으로(강조 슬롯)
            if (!string.IsNullOrEmpty(highlightOfferingId))
            {
                foreach (var offering in offerings)
                {
                    if (offering == null || IsPurified(offering)) continue;
                    if (!string.Equals(offering.offeringId, highlightOfferingId, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    CreatePreferredChip(inventoryRow, offering.displayName, offering.icon, offering, highlight: true);
                }
            }

            foreach (var offering in offerings)
            {
                if (offering == null) continue;
                if (IsPurified(offering)) continue;
                if (!string.IsNullOrEmpty(highlightOfferingId)
                    && string.Equals(offering.offeringId, highlightOfferingId, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                CreatePreferredChip(inventoryRow, offering.displayName, offering.icon, offering, highlight: false);
            }
        }

        void PruneDeadCountBadges()
        {
            offeringCountBadges.RemoveAll(b => b.Label == null);
        }

        private void CreatePreferredChip(Transform parent, string label, Sprite icon, OfferingData feedTarget, bool highlight = false)
        {
            var itemGO = new GameObject("Pref_" + (label ?? "?"));
            itemGO.transform.SetParent(parent, false);
            var le = itemGO.AddComponent<LayoutElement>();
            le.preferredWidth = 100;
            le.preferredHeight = 130;

            var v = itemGO.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.childControlHeight = true;
            v.childControlWidth = true;

            var circleGO = new GameObject("Circle");
            circleGO.transform.SetParent(itemGO.transform, false);
            var circleLe = circleGO.AddComponent<LayoutElement>();
            circleLe.preferredWidth = 72;
            circleLe.preferredHeight = 72;
            var circleImg = circleGO.AddComponent<Image>();
            circleImg.color = highlight
                ? new Color(1f, 0.92f, 0.45f, 1f)
                : new Color(0.95f, 0.95f, 0.97f, 1f);

            var iconGO = new GameObject("Icon");
            SetupRect(iconGO, circleGO.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.color = icon != null ? Color.white : new Color(0.7f, 0.7f, 0.75f, 1f);
            iconImg.raycastTarget = false;

            if (feedTarget != null)
            {
                var drag = circleGO.AddComponent<OfferingDragItem>();
                drag.Configure(this, feedTarget, IsPurified(feedTarget), icon != null ? icon : feedTarget.icon);
                AttachCountBadge(circleGO.transform, feedTarget, purified: false);
            }

            var tagGO = new GameObject("Tag");
            tagGO.transform.SetParent(itemGO.transform, false);
            var tagLe = tagGO.AddComponent<LayoutElement>();
            tagLe.preferredHeight = 28;
            var tagBg = tagGO.AddComponent<Image>();
            tagBg.color = AccentBlue;
            tagBg.raycastTarget = false;
            var tagText = CreateText(tagGO.transform, label ?? "", 15, TextAnchor.MiddleCenter);
            tagText.color = Color.white;
            SetupRect(tagText.gameObject, tagGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }

        Text AttachCountBadge(Transform iconParent, OfferingData offering, bool purified)
        {
            var badgeGO = new GameObject("CountBadge");
            SetupRect(badgeGO, iconParent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-4f, -4f), new Vector2(36f, 22f));
            var bg = badgeGO.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);
            bg.raycastTarget = false;
            int n = purified ? GameEconomy.Instance.PurifiedWater : GameEconomy.Instance.GetOfferingCount(offering);
            var text = CreateText(badgeGO.transform, "x" + n, 14, TextAnchor.MiddleCenter);
            text.color = Color.white;
            SetupRect(text.gameObject, badgeGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            offeringCountBadges.Add(new CountBadge
            {
                Label = text,
                Offering = offering,
                PurifiedWater = purified
            });
            return text;
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }

        private static Sprite FirstSprite(CharacterData data)
        {
            if (data == null) return null;
            if (data.walkDown != null) foreach (var s in data.walkDown) if (s != null) return s;
            if (data.walkLeft != null) foreach (var s in data.walkLeft) if (s != null) return s;
            if (data.walkRight != null) foreach (var s in data.walkRight) if (s != null) return s;
            if (data.walkUp != null) foreach (var s in data.walkUp) if (s != null) return s;
            return null;
        }

        private Sprite GetNeokPlaceholderSprite()
        {
            if (neokPlaceholderSprite != null) return neokPlaceholderSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r = size * 0.45f;
            float cx = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cx));
                float a = Mathf.Clamp01(1f - (d - r + 1.5f) / 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, true);
            neokPlaceholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return neokPlaceholderSprite;
        }

        // ---------------- build ----------------

        private void Build()
        {
            var canvasGO = new GameObject("Canvas_Detail");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            rootCanvas = canvas;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            var rootRt = SetupRect(root, canvasGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var bg = root.AddComponent<Image>();
            bg.color = Bg;

            // 닫기
            var closeGO = new GameObject("CloseButton");
            SetupRect(closeGO, rootRt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(24, -24), new Vector2(64, 64));
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = Border;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);
            CreateCloseBar(closeGO.transform, 45f);
            CreateCloseBar(closeGO.transform, -45f);

            // 본문: 하단 바 위 (급여 드롭 존)
            var body = new GameObject("Body");
            var bodyRt = SetupRect(body, rootRt, new Vector2(0f, 0.22f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            feedDropRt = bodyRt;
            // top padding for close
            var bodyPad = new GameObject("BodyInner");
            var bodyInner = SetupRect(bodyPad, bodyRt, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.92f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // 좌: 초상 (드롭 타겟)
            var portraitPanel = CreateBorderedPanel(bodyInner, "PortraitPanel", PortraitBg);
            portraitDropRt = portraitPanel.GetComponent<RectTransform>();
            SetupRect(portraitPanel, bodyInner, new Vector2(0f, 0f), new Vector2(0.42f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var portraitInner = FindInner(portraitPanel);
            portraitPanelHighlight = portraitInner.GetComponent<Image>();
            var portraitGO = new GameObject("Portrait");
            SetupRect(portraitGO, portraitInner, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            portraitImage = portraitGO.AddComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            portraitRt = portraitGO.GetComponent<RectTransform>();
            portraitBaseScale = portraitRt.localScale;

            // 우: 정보 스택
            var infoCol = new GameObject("InfoColumn");
            var infoRt = SetupRect(infoCol, bodyInner, new Vector2(0.45f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var infoLayout = infoCol.AddComponent<VerticalLayoutGroup>();
            infoLayout.spacing = 16;
            infoLayout.padding = new RectOffset(0, 0, 0, 0);
            infoLayout.childAlignment = TextAnchor.UpperCenter;
            infoLayout.childControlHeight = true;
            infoLayout.childControlWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.childForceExpandWidth = true;

            // 이름 패널
            var namePanel = CreateBorderedPanel(infoRt, "NamePanel", Panel);
            namePanel.AddComponent<LayoutElement>().preferredHeight = 120;
            var nameInner = FindInner(namePanel);
            var nameLabel = CreateText(nameInner, "이름", 22, TextAnchor.UpperLeft);
            nameLabel.color = LabelDark;
            nameLabel.fontStyle = FontStyle.Bold;
            SetupRect(nameLabel.gameObject, nameInner, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f),
                new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            nameValueText = CreateText(nameInner, "", 28, TextAnchor.MiddleLeft);
            nameValueText.color = LabelDark;
            SetupRect(nameValueText.gameObject, nameInner, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.58f),
                new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            statusText = CreateText(nameInner, "", 16, TextAnchor.LowerRight);
            statusText.color = new Color(0.45f, 0.45f, 0.5f);
            SetupRect(statusText.gameObject, nameInner, new Vector2(0.4f, 0.02f), new Vector2(0.95f, 0.28f),
                new Vector2(1f, 0f), Vector2.zero, Vector2.zero);

            // 스탯 패널 (친밀도 | 기력)
            var statsPanel = CreateBorderedPanel(infoRt, "StatsPanel", Panel);
            statsPanel.AddComponent<LayoutElement>().preferredHeight = 160;
            var statsInner = FindInner(statsPanel);

            var intCol = new GameObject("IntimacyCol");
            intimacyColGO = intCol;
            SetupRect(intCol, statsInner, new Vector2(0.03f, 0.08f), new Vector2(0.48f, 0.92f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildStatColumn(intCol.transform, "친밀도", out intimacyLevelText, out intimacyFill, out intimacyFillRt, IntimacyPink, true);

            var staCol = new GameObject("StaminaCol");
            SetupRect(staCol, statsInner, new Vector2(0.52f, 0.08f), new Vector2(0.97f, 0.92f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildStatColumn(staCol.transform, "기력", out staminaValueText, out staminaFill, out staminaFillRt, StaminaGreen, false);

            // 설명 패널
            var descPanel = CreateBorderedPanel(infoRt, "DescPanel", Panel);
            var descLe = descPanel.AddComponent<LayoutElement>();
            descLe.preferredHeight = 280;
            descLe.flexibleHeight = 1f;
            var descInner = FindInner(descPanel);
            var descLabel = CreateText(descInner, "캐릭터 설명", 22, TextAnchor.UpperLeft);
            descLabel.color = LabelDark;
            descLabel.fontStyle = FontStyle.Bold;
            SetupRect(descLabel.gameObject, descInner, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f),
                new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            var scrollGO = new GameObject("DescScroll");
            SetupRect(scrollGO, descInner, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.78f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            var vpRt = SetupRect(viewport, scrollGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(1, 1, 1, 0.01f);

            var content = new GameObject("Content");
            var contentRt = SetupRect(content, vpRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 200f));
            descriptionText = CreateText(contentRt, "", 20, TextAnchor.UpperLeft);
            descriptionText.color = LabelDark;
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            var descTextRt = SetupRect(descriptionText.gameObject, contentRt, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var fitter = descriptionText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            descriptionText.gameObject.AddComponent<LayoutElement>();

            scroll.viewport = vpRt;
            scroll.content = contentRt;

            // 하단 바
            var bottom = new GameObject("BottomBar");
            var bottomRt = SetupRect(bottom, rootRt, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.20f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bottomLayout = bottom.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.spacing = 16;
            bottomLayout.childAlignment = TextAnchor.MiddleCenter;
            bottomLayout.childForceExpandWidth = false;
            bottomLayout.childForceExpandHeight = true;
            bottomLayout.padding = new RectOffset(8, 8, 8, 8);

            CreatePurifiedWaterDragChip(bottomRt);

            preferredHostGO = new GameObject("PreferredHost");
            preferredRow = preferredHostGO.AddComponent<RectTransform>();
            preferredRow.SetParent(bottomRt, false);
            var prefHostLe = preferredHostGO.AddComponent<LayoutElement>();
            prefHostLe.flexibleWidth = 1f;
            prefHostLe.preferredHeight = 140;
            var prefLayout = preferredHostGO.AddComponent<HorizontalLayoutGroup>();
            prefLayout.spacing = 12;
            prefLayout.childAlignment = TextAnchor.MiddleCenter;
            prefLayout.childForceExpandWidth = false;
            prefLayout.childForceExpandHeight = false;

            inventoryButtonGO = CreateSideActionButton(bottomRt, "인벤토리", null, ToggleInventory);

            feedHintText = CreateText(rootRt, "정화수·공양물을 드래그해 캐릭터에게 먹이세요", 18, TextAnchor.MiddleCenter);
            feedHintText.color = new Color(0.35f, 0.35f, 0.4f);
            SetupRect(feedHintText.gameObject, rootRt, new Vector2(0.5f, 0.205f), new Vector2(0.5f, 0.205f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 28));

            // 인벤토리 오버레이 (하단 바 위)
            inventoryPanel = CreateBorderedPanel(rootRt, "InventoryPanel", Panel);
            SetupRect(inventoryPanel, rootRt, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.42f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            inventoryPanel.SetActive(false);
            var invInner = FindInner(inventoryPanel);
            var invTitle = CreateText(invInner, "인벤토리", 22, TextAnchor.UpperLeft);
            invTitle.color = LabelDark;
            invTitle.fontStyle = FontStyle.Bold;
            SetupRect(invTitle.gameObject, invInner, new Vector2(0.04f, 0.75f), new Vector2(0.5f, 0.95f),
                new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            var invRowGO = new GameObject("InventoryRow");
            inventoryRow = SetupRect(invRowGO, invInner, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.72f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var invLayout = invRowGO.AddComponent<HorizontalLayoutGroup>();
            invLayout.spacing = 10;
            invLayout.childAlignment = TextAnchor.MiddleLeft;
            invLayout.childForceExpandWidth = false;
        }

        private void BuildStatColumn(Transform parent, string title, out Text valueText, out Image fill,
            out RectTransform fillRt, Color fillColor, bool showHeart)
        {
            var titleT = CreateText(parent, title, 22, TextAnchor.UpperLeft);
            titleT.color = LabelDark;
            titleT.fontStyle = FontStyle.Bold;
            SetupRect(titleT.gameObject, parent, new Vector2(0f, 0.7f), new Vector2(0.55f, 1f),
                new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            valueText = CreateText(parent, "", 22, TextAnchor.UpperRight);
            valueText.color = LabelDark;
            SetupRect(valueText.gameObject, parent, new Vector2(0.4f, 0.7f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            if (showHeart)
            {
                var heart = CreateText(parent, "♥", 22, TextAnchor.MiddleLeft);
                heart.color = IntimacyPink;
                SetupRect(heart.gameObject, parent, new Vector2(0f, 0.15f), new Vector2(0.15f, 0.55f),
                    new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            }

            float barLeft = showHeart ? 0.18f : 0f;
            var barBgGO = new GameObject("BarBg");
            SetupRect(barBgGO, parent, new Vector2(barLeft, 0.22f), new Vector2(1f, 0.48f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var barBg = barBgGO.AddComponent<Image>();
            barBg.color = BarTrack;

            var fillGO = new GameObject("BarFill");
            fillRt = SetupRect(fillGO, barBgGO.transform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero);
            fill = fillGO.AddComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;
        }

        private void CreatePurifiedWaterDragChip(Transform parent)
        {
            var go = new GameObject("Action_정화수");
            go.transform.SetParent(parent, false);
            purifiedDragGroup = go.AddComponent<CanvasGroup>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            le.preferredHeight = 140;

            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.childControlHeight = true;
            v.childControlWidth = true;

            var iconBox = new GameObject("IconBox");
            iconBox.transform.SetParent(go.transform, false);
            iconBox.AddComponent<LayoutElement>().preferredHeight = 72;
            var iconBg = iconBox.AddComponent<Image>();
            iconBg.color = AccentBlue;

            Sprite icon = purifiedWaterIcon;
            var pw = FindPurifiedWater();
            if (icon == null && pw != null) icon = pw.icon;

            if (icon != null)
            {
                var iconGO = new GameObject("Icon");
                SetupRect(iconGO, iconBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
                var img = iconGO.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else
            {
                var placeholder = CreateText(iconBox.transform, "정", 28, TextAnchor.MiddleCenter);
                placeholder.color = Color.white;
                SetupRect(placeholder.gameObject, iconBox.transform, Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }

            var drag = iconBox.AddComponent<OfferingDragItem>();
            drag.Configure(this, pw, purified: true, icon);
            purifiedCountText = AttachCountBadge(iconBox.transform, pw, purified: true);

            var tag = new GameObject("Label");
            tag.transform.SetParent(go.transform, false);
            tag.AddComponent<LayoutElement>().preferredHeight = 28;
            var tagBg = tag.AddComponent<Image>();
            tagBg.color = AccentBlue;
            tagBg.raycastTarget = false;
            var t = CreateText(tag.transform, "정화수", 16, TextAnchor.MiddleCenter);
            t.color = Color.white;
            SetupRect(t.gameObject, tag.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }

        private GameObject CreateSideActionButton(Transform parent, string label, Sprite icon, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Action_" + label);
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            le.preferredHeight = 140;

            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.childControlHeight = true;
            v.childControlWidth = true;

            var iconBox = new GameObject("IconBox");
            iconBox.transform.SetParent(go.transform, false);
            iconBox.AddComponent<LayoutElement>().preferredHeight = 72;
            var iconBg = iconBox.AddComponent<Image>();
            iconBg.color = AccentBlue;
            var btn = iconBox.AddComponent<Button>();
            btn.targetGraphic = iconBg;
            btn.onClick.AddListener(onClick);

            if (icon != null)
            {
                var iconGO = new GameObject("Icon");
                SetupRect(iconGO, iconBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
                var img = iconGO.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else
            {
                var placeholder = CreateText(iconBox.transform, label.Length > 0 ? label.Substring(0, 1) : "?",
                    28, TextAnchor.MiddleCenter);
                placeholder.color = Color.white;
                SetupRect(placeholder.gameObject, iconBox.transform, Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                placeholder.raycastTarget = false;
            }

            var tag = new GameObject("Label");
            tag.transform.SetParent(go.transform, false);
            tag.AddComponent<LayoutElement>().preferredHeight = 28;
            var tagBg = tag.AddComponent<Image>();
            tagBg.color = AccentBlue;
            var t = CreateText(tag.transform, label, 16, TextAnchor.MiddleCenter);
            t.color = Color.white;
            SetupRect(t.gameObject, tag.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            return go;
        }

        private GameObject CreateBorderedPanel(Transform parent, string name, Color fill)
        {
            var outer = new GameObject(name);
            outer.transform.SetParent(parent, false);
            var outerImg = outer.AddComponent<Image>();
            outerImg.color = Border;

            var inner = new GameObject("Inner");
            SetupRect(inner, outer.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var innerRt = inner.GetComponent<RectTransform>();
            innerRt.offsetMin = new Vector2(3f, 3f);
            innerRt.offsetMax = new Vector2(-3f, -3f);
            var innerImg = inner.AddComponent<Image>();
            innerImg.color = fill;
            return outer;
        }

        static Transform FindInner(GameObject borderedPanel)
        {
            var t = borderedPanel.transform.Find("Inner");
            return t != null ? t : borderedPanel.transform;
        }

        private static void CreateCloseBar(Transform parent, float zAngle)
        {
            var barGO = new GameObject("XBar");
            var rt = SetupRect(barGO, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(32, 4));
            rt.localRotation = Quaternion.Euler(0f, 0f, zAngle);
            var img = barGO.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private Text CreateText(Transform parent, string initial, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = UiFonts.Size(fontSize);
            text.alignment = alignment;
            text.color = LabelDark;
            text.text = initial;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform SetupRect(GameObject go, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
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
