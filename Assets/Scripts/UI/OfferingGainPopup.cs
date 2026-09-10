using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Yoegoe.UI
{
    /// <summary>
    /// 상세화면 공양 급여 시 초상 위에 큰 [+n] 연출 (기획 5-4).
    /// </summary>
    public static class OfferingGainPopup
    {
        const float Duration = 1.05f;
        const float RisePixels = 90f;

        public static void Play(Transform canvasRoot, RectTransform anchor, Font font, int staminaGain, float intimacyGain)
        {
            if (canvasRoot == null || anchor == null) return;
            if (staminaGain <= 0 && intimacyGain <= 0.0001f) return;

            var host = new GameObject("OfferingGainPopup");
            host.transform.SetParent(canvasRoot, false);
            var runner = host.AddComponent<Runner>();
            runner.Begin(anchor, font, staminaGain, intimacyGain);
        }

        class Runner : MonoBehaviour
        {
            public void Begin(RectTransform anchor, Font font, int staminaGain, float intimacyGain)
            {
                StartCoroutine(Run(anchor, font, staminaGain, intimacyGain));
            }

            IEnumerator Run(RectTransform anchor, Font font, int staminaGain, float intimacyGain)
            {
                var rt = gameObject.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(420f, 160f);

                // 초상 중앙 스크린 좌표 → 캔버스 로컬
                var canvas = GetComponentInParent<Canvas>();
                Camera uiCam = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    uiCam = canvas.worldCamera;

                Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, anchor.TransformPoint(anchor.rect.center));
                RectTransform parentRt = transform.parent as RectTransform;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, screen, uiCam, out local);
                rt.anchoredPosition = local;

                var layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 4;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childControlWidth = true;

                Text staminaText = null;
                Text intimacyText = null;

                if (staminaGain > 0)
                    staminaText = MakeLabel(transform, font, "+" + staminaGain, 72, new Color(0.25f, 0.85f, 0.4f, 1f));

                if (intimacyGain > 0.0001f)
                {
                    string s = intimacyGain >= 1f
                        ? "+" + intimacyGain.ToString("0")
                        : "+" + intimacyGain.ToString("0.##");
                    intimacyText = MakeLabel(transform, font, s, 40, new Color(0.95f, 0.4f, 0.5f, 1f));
                }

                Vector2 start = rt.anchoredPosition;
                float t = 0f;
                while (t < Duration)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / Duration);
                    float ease = 1f - (1f - u) * (1f - u);
                    rt.anchoredPosition = start + new Vector2(0f, RisePixels * ease);

                    float alpha = u < 0.15f ? u / 0.15f : (u > 0.55f ? 1f - (u - 0.55f) / 0.45f : 1f);
                    alpha = Mathf.Clamp01(alpha);
                    SetAlpha(staminaText, alpha);
                    SetAlpha(intimacyText, alpha);

                    float scale = Mathf.Lerp(0.75f, 1.12f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.2f)));
                    if (u > 0.35f) scale = Mathf.Lerp(1.12f, 1f, (u - 0.35f) / 0.65f);
                    rt.localScale = Vector3.one * scale;

                    yield return null;
                }

                Destroy(gameObject);
            }

            static Text MakeLabel(Transform parent, Font font, string text, int size, Color color)
            {
                var go = new GameObject("Label");
                go.transform.SetParent(parent, false);
                go.AddComponent<LayoutElement>().preferredHeight = size + 8;
                var t = go.AddComponent<Text>();
                t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = UiFonts.Size(size);
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = color;
                t.text = text;
                t.raycastTarget = false;
                // 가독성용 단순 그림자
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                outline.effectDistance = new Vector2(2f, -2f);
                return t;
            }

            static void SetAlpha(Text text, float a)
            {
                if (text == null) return;
                var c = text.color;
                c.a = a;
                text.color = c;
                var outline = text.GetComponent<Outline>();
                if (outline != null)
                {
                    var oc = outline.effectColor;
                    oc.a = 0.55f * a;
                    outline.effectColor = oc;
                }
            }
        }
    }
}
