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
            var go = new GameObject("PropUpgradeFx_World");
            go.transform.position = worldPos + Vector3.up * 0.9f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.09f;
            tm.fontSize = 48;
            tm.color = new Color(1f, 0.95f, 0.55f, 1f);
            var f = font != null ? font : BuiltinFont();
            if (f != null) tm.font = f;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 600;
            go.AddComponent<FloatAndFadeWorld>().Init(1.2f, 1.1f);
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
            label.fontSize = 22;
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
            private TextMesh tm;
            private Color baseColor;

            public void Init(float durationSec, float riseSpeed)
            {
                duration = durationSec;
                life = durationSec;
                rise = riseSpeed;
                tm = GetComponent<TextMesh>();
                if (tm != null) baseColor = tm.color;
            }

            private void Update()
            {
                life -= Time.unscaledDeltaTime;
                transform.position += Vector3.up * (rise * Time.unscaledDeltaTime);
                if (tm != null)
                {
                    float a = Mathf.Clamp01(life / duration);
                    var c = baseColor;
                    c.a = a;
                    tm.color = c;
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
