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
    /// <summary>참가 요괴 명단 칩·소환 슬롯. (YutMiniGame 분할 — 본체는 YutMiniGame.cs)</summary>
    public partial class YutMiniGame
    {
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
        /// "소환하기". 키우는 요괴 수만큼만 말을 쓸 수 있다는 걸 그 자리에서 바로 안내하기 위함.</summary>
        public void ShowRoster(IReadOnlyList<RosterEntry> entries, bool showExtraSlot, string extraSlotLabel,
            Action onExtraSlotTapped)
        {
            EnsureBoard();
            if (_rosterRow == null || entries == null) return;

            int totalSlots = Mathf.Max(1, entries.Count + (showExtraSlot ? 1 : 0));

            if (_rosterChips.Count != entries.Count)
            {
                DetachYokaiPiecesFromRoster();
                foreach (var chip in _rosterChips)
                    if (chip.Root != null) Destroy(chip.Root.gameObject);
                _rosterChips.Clear();

                for (int i = 0; i < entries.Count; i++)
                    _rosterChips.Add(BuildRosterChip(_rosterRow, entries[i].Id, i, totalSlots));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var chip = _rosterChips[i];
                // Id가 바뀌었을 수 있으니(인원 수는 같은데 구성만 바뀜) 칩을 갱신한다.
                if (chip.Id != entry.Id)
                    _rosterChips[i] = chip = new RosterChip(entry.Id, chip.Root, chip.Portrait, chip.Name, chip.Stats, chip.Status);
                RepositionRosterSlot(chip.Root, i, totalSlots);

                var sprite = PieceSpriteFor(entry.Id);
                if (sprite != null)
                {
                    chip.Portrait.sprite = sprite;
                    chip.Portrait.preserveAspect = true;
                }
                else
                {
                    chip.Portrait.sprite = null;
                }

                // 대기 중이면 말이 초상 자리를 차지하므로 초상은 끈다.
                // 보드/완주면 실루엣으로 켠다.
                if (entry.WaitingInSlot)
                {
                    chip.Portrait.enabled = false;
                    chip.Portrait.color = Color.white;
                }
                else
                {
                    chip.Portrait.enabled = true;
                    chip.Portrait.color = sprite != null
                        ? RosterSilhouetteColor
                        : new Color(RosterSilhouetteColor.r, RosterSilhouetteColor.g, RosterSilhouetteColor.b, 0.75f);
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

        /// <summary>아직 소환 전인 요괴 자리 — 라벨/탭 동작은
        /// YutScreen이 매번 넘겨준다(YutMiniGame은 소환 로직을 모른다).</summary>
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

            var label = CreateText(rt, "Label", "", 22, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.color = new Color(0.85f, 0.8f, 0.7f, 0.9f);
            label.raycastTarget = false;

            return go;
        }

        /// <summary>소환 연출용 — 로스터 "소환하기" 슬롯의 현재 월드 위치(없으면 null).
        /// YutScreen이 암전 연출 중 그 자리로 소환된 요괴 아이콘을 떨어뜨리는 데 쓴다.</summary>
        public Vector3? GetSummonSlotWorldPosition()
        {
            if (_summonSlotChip == null || !_summonSlotChip.activeSelf) return null;
            return _summonSlotChip.GetComponent<RectTransform>().position;
        }

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

        RosterChip BuildRosterChip(RectTransform parent, string id, int index, int count)
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

            return new RosterChip(id, rt, portrait, nameText, statsText, statusText);
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
    }
}
