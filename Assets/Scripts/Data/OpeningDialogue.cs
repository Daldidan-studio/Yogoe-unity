using System;
using System.Collections.Generic;
using KSpirits.Core;
using UnityEngine;

namespace KSpirits.Data
{
    /// <summary>
    /// Resources/Dialogue/opening_tutorial.{locale}.json 로드.
    /// 대사 수정은 시트(OPENING 탭) → export_dialogue.py --character opening.
    /// </summary>
    public static class OpeningDialogue
    {
        const string ResourcePathPrefix = "Dialogue/opening_tutorial";

        static Dictionary<string, DialogueLine[]> _sections;
        static Dictionary<string, ChoiceOption[]> _choiceSets;
        static bool _loaded;
        static string _loadedLocale;

        public static IReadOnlyList<DialogueLine> Intro => Lines("opening_intro");
        public static IReadOnlyList<DialogueLine> Accept => Lines("opening_accept");
        public static IReadOnlyList<DialogueLine> Flee => Lines("opening_flee");
        public static IReadOnlyList<DialogueLine> Reveal => Lines("opening_reveal");

        public static IReadOnlyList<ChoiceOption> ChoiceOptions => Choices("opening_choice");

        public static void Invalidate()
        {
            _loaded = false;
            _loadedLocale = null;
            _sections = null;
            _choiceSets = null;
        }

        public static void EnsureLoaded()
        {
            var locale = GameLocale.Current;
            if (_loaded && _loadedLocale == locale) return;

            _sections = new Dictionary<string, DialogueLine[]>();
            _choiceSets = new Dictionary<string, ChoiceOption[]>();

            var asset = LoadLocaleAsset(locale);
            if (asset == null && locale != GameLocale.Fallback)
            {
                Debug.LogWarning($"[OpeningDialogue] Missing locale '{locale}', fallback to '{GameLocale.Fallback}'");
                asset = LoadLocaleAsset(GameLocale.Fallback);
                locale = GameLocale.Fallback;
            }

            if (asset == null)
            {
                Debug.LogError($"[OpeningDialogue] Missing Resources/{ResourcePathPrefix}.{GameLocale.Fallback}.json");
                _loaded = true;
                _loadedLocale = locale;
                return;
            }

            var file = JsonUtility.FromJson<DialogueFile>(asset.text);

            if (file.sections != null)
            {
                foreach (var s in file.sections)
                {
                    if (s == null || string.IsNullOrEmpty(s.id)) continue;
                    _sections[s.id] = s.lines ?? Array.Empty<DialogueLine>();
                }
            }

            if (file.choices != null)
            {
                foreach (var c in file.choices)
                {
                    if (c == null || string.IsNullOrEmpty(c.id)) continue;
                    _choiceSets[c.id] = c.options ?? Array.Empty<ChoiceOption>();
                }
            }

            _loaded = true;
            _loadedLocale = locale;
            Debug.Log($"[OpeningDialogue] Loaded v{file.version} ({locale}): {_sections.Count} sections, {_choiceSets.Count} choice sets");
        }

        static TextAsset LoadLocaleAsset(string locale)
        {
            return Resources.Load<TextAsset>($"{ResourcePathPrefix}.{locale}");
        }

        static DialogueLine[] Lines(string id)
        {
            EnsureLoaded();
            return _sections.TryGetValue(id, out var lines) ? lines : Array.Empty<DialogueLine>();
        }

        static ChoiceOption[] Choices(string id)
        {
            EnsureLoaded();
            return _choiceSets.TryGetValue(id, out var opts) ? opts : Array.Empty<ChoiceOption>();
        }
    }

    /// <summary>opening_tutorial JSON section/choice id.</summary>
    public static class OpeningDialogueSection
    {
        public const string Intro = "opening_intro";
        public const string Accept = "opening_accept";
        public const string Flee = "opening_flee";
        public const string Reveal = "opening_reveal";
        public const string Choice = "opening_choice";
    }
}
