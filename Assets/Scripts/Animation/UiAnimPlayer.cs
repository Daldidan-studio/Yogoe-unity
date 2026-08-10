using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.Animation
{
    /// <summary>
    /// JSON 클립 id로 UI 연출을 재생. 화면(ScrollScreenUI)은 타겟만 넘기고 연출 로직은 여기.
    /// </summary>
    public class UiAnimPlayer : MonoBehaviour
    {
        public RectTransform YokaiRoot;
        public Image YokaiImage;
        public Image EnergyFill;
        public GameObject GlitchOverlay;

        readonly Dictionary<string, Coroutine> _loops = new();

        public IEnumerator Play(string clipId)
        {
            var clip = AnimCatalog.Get(clipId);
            if (clip == null)
            {
                Debug.LogWarning($"[UiAnimPlayer] Unknown clip: {clipId}");
                yield break;
            }

            switch (clip.type)
            {
                case "shake":
                    yield return Shake(clip);
                    break;
                case "color_flash":
                    yield return ColorFlash(clip);
                    break;
                case "color_to":
                    yield return ColorTo(clip);
                    break;
                case "overlay":
                    SetOverlay(clip);
                    break;
                case "color_pulse":
                    // loop clips use SetLoop
                    SetLoop(clipId, true);
                    break;
                default:
                    Debug.LogWarning($"[UiAnimPlayer] Unknown type: {clip.type}");
                    break;
            }
        }

        public void PlayFireAndForget(string clipId)
        {
            StartCoroutine(Play(clipId));
        }

        public void SetLoop(string clipId, bool on)
        {
            if (_loops.TryGetValue(clipId, out var running) && running != null)
            {
                StopCoroutine(running);
                _loops.Remove(clipId);
            }

            if (!on)
            {
                if (clipId == "energy_warning_pulse" && EnergyFill != null)
                    EnergyFill.color = AnimCatalog.ParseColor("#4DCC8C", new Color(0.3f, 0.8f, 0.55f));
                return;
            }

            var clip = AnimCatalog.Get(clipId);
            if (clip == null || clip.type != "color_pulse") return;
            _loops[clipId] = StartCoroutine(ColorPulse(clip));
        }

        IEnumerator Shake(AnimClipDef clip)
        {
            if (YokaiRoot == null) yield break;
            var origin = YokaiRoot.anchoredPosition;
            var steps = Mathf.Max(1, clip.steps);
            var stepTime = clip.duration / steps;
            for (int i = 0; i < steps; i++)
            {
                YokaiRoot.anchoredPosition = origin + new Vector2(
                    Random.Range(-clip.intensityX, clip.intensityX),
                    Random.Range(-clip.intensityY, clip.intensityY));
                yield return new WaitForSeconds(stepTime);
            }
            YokaiRoot.anchoredPosition = origin;
        }

        IEnumerator ColorFlash(AnimClipDef clip)
        {
            if (YokaiImage == null) yield break;
            var a = AnimCatalog.ParseColor(clip.colorA, Color.white);
            var b = AnimCatalog.ParseColor(clip.colorB, new Color(0.55f, 0.85f, 1f));
            var original = YokaiImage.color;
            for (int i = 0; i < clip.flashes; i++)
            {
                YokaiImage.color = i % 2 == 0 ? a : b;
                yield return new WaitForSeconds(clip.interval);
            }
            YokaiImage.color = original;
        }

        IEnumerator ColorTo(AnimClipDef clip)
        {
            if (YokaiImage == null) yield break;
            var from = YokaiImage.color;
            var to = AnimCatalog.ParseColor(clip.color, from);
            float t = 0f;
            float dur = Mathf.Max(0.01f, clip.duration);
            while (t < dur)
            {
                t += Time.deltaTime;
                YokaiImage.color = Color.Lerp(from, to, t / dur);
                yield return null;
            }
            YokaiImage.color = to;
        }

        IEnumerator ColorPulse(AnimClipDef clip)
        {
            if (EnergyFill == null) yield break;
            var a = AnimCatalog.ParseColor(clip.colorA, new Color(0.3f, 0.8f, 0.55f));
            var b = AnimCatalog.ParseColor(clip.colorB, Color.yellow);
            while (true)
            {
                EnergyFill.color = Color.Lerp(a, b, Mathf.PingPong(Time.time * clip.speed, 1f));
                yield return null;
            }
        }

        void SetOverlay(AnimClipDef clip)
        {
            if (clip.target == "glitch" && GlitchOverlay != null)
                GlitchOverlay.SetActive(clip.visible);
        }
    }
}
