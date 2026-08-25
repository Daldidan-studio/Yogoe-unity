#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using KSpirits.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.Tutorial
{
    /// <summary>
    /// 개발용 튜토리얼 스텝 점프 메뉴. 에디터/개발 빌드에서만 존재한다.
    /// 화면 구석 🐞 버튼을 누르면 스텝 목록이 펼쳐지고, 하나 고르면
    /// TutorialController.DebugJumpToStep으로 바로 진입한다.
    /// </summary>
    public class TutorialDebugMenu : MonoBehaviour
    {
        TutorialController _tutorial;
        ScrollScreenUI _ui;
        GameObject _panel;
        bool _open;

        public void Bind(TutorialController tutorial, ScrollScreenUI ui)
        {
            _tutorial = tutorial;
            _ui = ui;
            BuildToggle();
        }

        void BuildToggle()
        {
            var toggle = CreateButton(_ui.transform, "DebugToggle", "🐞", TogglePanel);
            toggle.transform.SetAsLastSibling();
            SetAnchor(toggle.GetComponent<RectTransform>(), 0.02f, 0.965f, 0.14f, 0.998f, 0, 0, 0, 0);
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
                if (label != null) label.fontSize = 14;
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
            text.fontSize = 20;
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
#endif
