using UnityEngine;
using UnityEngine.UI;

namespace Yoegoe.UI
{
    /// <summary>기물 위/버튼 위 "+1 급" 플로트 텍스트.</summary>
    public static class PropUpgradeFx
    {
        private static Font sharedFont;

        public static void SpawnWorld(Vector3 worldPos, string text, Font font = null)
        {
            // TextMesh + 동적 한글 폰트는 글리프/머티리얼이 깨져 네모·이상한 도형으로 보인다.
            // 월드 스페이스 캔버스 + UI.Text로 띄운다.
            var go = new GameObject("PropUpgradeFx_World");
            go.transform.position = worldPos + Vector3.up * 0.9f;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 600;
            go.AddComponent<CanvasScaler>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 48f);
            // 월드에서 읽기 좋은 크기 (대략 캐릭터 머리 위)
            rt.localScale = Vector3.one * 0.012f;

            var labelGO = new GameObject("Label");
            var labelRt = labelGO.AddComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGO.AddComponent<Text>();
            label.font = font != null ? font : BuiltinFont();
            label.fontSize = UiFonts.Size(36);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.95f, 0.55f, 1f);
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            // 카메라를 향해 빌보드
            go.AddComponent<FloatAndFadeWorld>().Init(1.2f, 1.1f, label);
        }

        public static void SpawnUi(RectTransform parent, string text, Font font)
        {
            if (parent == null) return;
            var go = new GameObject("PropUpgradeFx_UI");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 8);
            rt.sizeDelta = new Vector2(280, 40);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.fontSize = UiFonts.Size(22);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.92f, 0.5f, 1f);
            label.text = text;
            label.raycastTarget = false;
            go.AddComponent<FloatAndFadeUi>().Init(1.1f, 60f);
        }

        private static Font BuiltinFont()
        {
            if (sharedFont == null)
                sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return sharedFont;
        }

        private class FloatAndFadeWorld : MonoBehaviour
        {
            private float life;
            private float duration;
            private float rise;
            private Text label;
            private Color baseColor;
            private Camera cam;

            public void Init(float durationSec, float riseSpeed, Text textLabel)
            {
                duration = durationSec;
                life = durationSec;
                rise = riseSpeed;
                label = textLabel;
                if (label != null) baseColor = label.color;
                cam = Camera.main;
            }

            private void LateUpdate()
            {
                if (cam == null) cam = Camera.main;
                if (cam != null)
                {
                    // 화면 정면 유지 (월드 텍스트가 뒤집히지 않게)
                    transform.rotation = cam.transform.rotation;
                }

                life -= Time.unscaledDeltaTime;
                transform.position += Vector3.up * (rise * Time.unscaledDeltaTime);
                if (label != null)
                {
                    float a = Mathf.Clamp01(life / duration);
                    var c = baseColor;
                    c.a = a;
                    label.color = c;
                }
                if (life <= 0f) Destroy(gameObject);
            }
        }

        private class FloatAndFadeUi : MonoBehaviour
        {
            private float life;
            private float duration;
            private float rise;
            private Text label;
            private RectTransform rt;
            private Color baseColor;

            public void Init(float durationSec, float risePixelsPerSec)
            {
                duration = durationSec;
                life = durationSec;
                rise = risePixelsPerSec;
                label = GetComponent<Text>();
                rt = GetComponent<RectTransform>();
                if (label != null) baseColor = label.color;
            }

            private void Update()
            {
                life -= Time.unscaledDeltaTime;
                if (rt != null)
                    rt.anchoredPosition += new Vector2(0, rise * Time.unscaledDeltaTime);
                if (label != null)
                {
                    float a = Mathf.Clamp01(life / duration);
                    var c = baseColor;
                    c.a = a;
                    label.color = c;
                }
                if (life <= 0f) Destroy(gameObject);
            }
        }
    }
}
