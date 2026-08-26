using System;
using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;
using KSpirits.Systems;
using KSpirits.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.Tutorial
{
    /// <summary>
    /// 개발용 튜토리얼 스텝 점프 메뉴. CI 빌드가 Development Build 옵션 없이 도는 탓에
    /// UNITY_EDITOR/DEVELOPMENT_BUILD로는 걸러지지 않아, 지금은 모든 빌드에 항상 포함된다.
    /// 화면 구석 🐞 버튼을 누르면 스텝 목록이 펼쳐지고, 하나 고르면
    /// TutorialController.DebugJumpToStep으로 바로 진입한다. 맨 위 별도 버튼은
    /// 본게임 수련장(NurtureTrainingController)을 고라니왕 소환 상태로 바로 테스트한다.
    /// </summary>
    public class TutorialDebugMenu : MonoBehaviour
    {
        TutorialController _tutorial;
        NurtureTrainingController _nurtureTraining;
        ScrollScreenUI _ui;
        GameObject _panel;
        bool _open;

        public void Bind(TutorialController tutorial, ScrollScreenUI ui, NurtureTrainingController nurtureTraining = null)
        {
            _tutorial = tutorial;
            _nurtureTraining = nurtureTraining;
            _ui = ui;
            BuildToggle();
        }

        void BuildToggle()
        {
            var toggle = CreateButton(_ui.transform, "DebugToggle", "DEV", TogglePanel);
            toggle.transform.SetAsLastSibling();
            SetAnchor(toggle.GetComponent<RectTransform>(), 0.02f, 0.965f, 0.14f, 0.998f, 0, 0, 0, 0);

            var yutJump = CreateButton(_ui.transform, "GoraniYutJump", "고라니왕 수련장", JumpToGoraniTraining);
            yutJump.transform.SetAsLastSibling();
            SetAnchor(yutJump.GetComponent<RectTransform>(), 0.15f, 0.965f, 0.4f, 0.998f, 0, 0, 0, 0);
            var jumpLabel = yutJump.GetComponentInChildren<Text>();
            if (jumpLabel != null) jumpLabel.fontSize = 15;
        }

        /// <summary>
        /// 튜토리얼을 건너뛰고, 고라니왕이 이미 소환된 본게임 상태로 만든 뒤
        /// 곧바로 수련장 윷놀이 세션을 시작한다 (본게임 윷놀이 테스트용).
        /// </summary>
        void JumpToGoraniTraining()
        {
            if (_tutorial == null || _nurtureTraining == null) return;

            var entry = SummonCatalog.Pick(0); // 고라니왕 (첫 보장 소환)
            var state = _tutorial.State;
            state.FocusYokai = new YokaiInstance(entry.Id, entry.DisplayName)
            {
                Stage = YokaiStage.Manifest,
                Energy = GameConstants.EnergyMax,
                Intimacy = 50
            };
            state.TotalSummons = Math.Max(state.TotalSummons, 1);
            state.Wallet.Hearts = GameConstants.HeartMax;

            _tutorial.DebugJumpToStep(TutorialStepId.Done);
            _nurtureTraining.BeginSessionForDebug();

            if (_open) TogglePanel();
        }

        void TogglePanel()
        {
            _open = !_open;
            if (_open) BuildPanel();
            else if (_panel != null) Destroy(_panel);
        }

        void BuildPanel()
        {
            _panel = CreatePanel(_ui.transform, "DebugStepPanel", new Color(0, 0, 0, 0.88f));
            _panel.transform.SetAsLastSibling();
            SetAnchor(_panel.GetComponent<RectTransform>(), 0.02f, 0.5f, 0.55f, 0.96f, 0, 0, 0, 0);

            var steps = (TutorialStepId[])Enum.GetValues(typeof(TutorialStepId));
            const int cols = 2;
            int rows = Mathf.CeilToInt(steps.Length / (float)cols);
            float cellW = 1f / cols;
            float cellH = 1f / rows;

            for (int i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                int row = i / cols;
                int col = i % cols;
                var btn = CreateButton(_panel.transform, $"Step_{step}", $"{(int)step}. {step}", () =>
                {
                    _tutorial.DebugJumpToStep(step);
                    TogglePanel();
                });
                SetAnchor(btn.GetComponent<RectTransform>(),
                    col * cellW + 0.015f, 1f - (row + 1) * cellH + 0.01f,
                    (col + 1) * cellW - 0.015f, 1f - row * cellH - 0.01f, 0, 0, 0, 0);
                var label = btn.GetComponentInChildren<Text>();
                if (label != null) label.fontSize = 15;
            }
        }

        static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = UIFont.Default;
            text.fontSize = 21;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            Stretch((RectTransform)textGo.transform);
            return btn;
        }

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
