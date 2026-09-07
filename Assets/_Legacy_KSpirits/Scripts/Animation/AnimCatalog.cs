using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSpirits.Animation
{
    [Serializable]
    public class AnimClipDef
    {
        public string id;
        public string type;      // shake | color_flash | color_pulse | color_to | overlay
        public string target;    // yokai | energy_bar | glitch

        public float duration = 0.3f;
        public float intensityX = 10f;
        public float intensityY = 8f;
        public int steps = 8;

        public int flashes = 6;
        public float interval = 0.12f;

        public float speed = 2f;
        public bool loop;
        public bool visible = true;

        public float charsPerSecond = 40f;
        public float punctuationHold = 0.06f;

        public string color;
        public string colorA;
        public string colorB;
    }

    [Serializable]
    public class AnimCatalogFile
    {
        public string version;
        public AnimClipDef[] clips;
    }

    public static class AnimCatalog
    {
        const string ResourcePath = "Animation/ui_anims";
        static Dictionary<string, AnimClipDef> _clips;
        static bool _loaded;

        public static AnimClipDef Get(string id)
        {
            EnsureLoaded();
            return _clips.TryGetValue(id, out var clip) ? clip : null;
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;

            _clips = new Dictionary<string, AnimClipDef>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[AnimCatalog] Missing Resources/{ResourcePath}.json");
                _loaded = true;
                return;
            }

            var file = JsonUtility.FromJson<AnimCatalogFile>(asset.text);
            if (file.clips != null)
            {
                foreach (var c in file.clips)
                {
                    if (c == null || string.IsNullOrEmpty(c.id)) continue;
                    _clips[c.id] = c;
                }
            }

            _loaded = true;
            Debug.Log($"[AnimCatalog] Loaded v{file.version}: {_clips.Count} clips");
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback;
        }
    }
}
