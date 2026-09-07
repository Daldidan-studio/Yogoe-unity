using System;
using System.Collections.Generic;
using KSpirits.Core;
using UnityEngine;

namespace KSpirits.Data
{
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string text;
        public bool narration;
        public string layout;
        public string fx;

        public string Speaker => string.IsNullOrEmpty(speaker) ? null : speaker;
        public string Text => text;
        public bool IsNarration => narration;
        public string Fx => string.IsNullOrWhiteSpace(fx) ? null : fx.Trim();

        public bool TryGetLayout(out DialogueLayoutId layoutId)
        {
            layoutId = default;
            if (string.IsNullOrWhiteSpace(layout))
                return false;

            var key = layout.Trim();
            if (System.Enum.TryParse(key, true, out DialogueLayoutId parsed))
            {
                layoutId = parsed;
                return true;
            }

            switch (key.ToLowerInvariant())
            {
                case "bottom_wide":
                case "bottom":
                    layoutId = DialogueLayoutId.BottomWide;
                    return true;
                case "near_yokai":
                case "near_okto":
                    layoutId = DialogueLayoutId.NearYokai;
                    return true;
                case "above_mortar":
                    layoutId = DialogueLayoutId.AboveMortar;
                    return true;
                case "top_narration":
                case "narration":
                    layoutId = DialogueLayoutId.TopNarration;
                    return true;
                default:
                    return false;
            }
        }

        public DialogueLine() { }

        public DialogueLine(string text, string speaker = null, bool narration = false)
        {
            this.text = text;
            this.speaker = speaker ?? "";
            this.narration = narration;
        }

        public static DialogueLine Say(string speaker, string text) => new(text, speaker);
        public static DialogueLine Narrate(string text) => new(text, "", true);
        public static DialogueLine Okto(string text) => new(text, "옥토끼");
        public static DialogueLine Imugi(string text) => new(text, "이무기");
    }

    [Serializable]
    public class ChoiceOption
    {
        public string id;
        public string label;

        public string Id => id;
        public string Label => label;

        public ChoiceOption() { }

        public ChoiceOption(string id, string label)
        {
            this.id = id;
            this.label = label;
        }
    }

    [Serializable]
    public class DialogueSection
    {
        public string id;
        public DialogueLine[] lines;
    }

    [Serializable]
    public class ChoiceSection
    {
        public string id;
        public ChoiceOption[] options;
    }

    [Serializable]
    public class DialogueFile
    {
        public string version;
        public string character;
        public string locale;
        public DialogueSection[] sections;
        public ChoiceSection[] choices;
    }

    /// <summary>
    /// Resources/Dialogue/okto_tutorial.{locale}.json 로드.
    /// 대사 수정은 시트 → npm run dialogue.
    /// </summary>
    public static class OktoDialogue
    {
        const string ResourcePathPrefix = "Dialogue/okto_tutorial";

        static Dictionary<string, DialogueLine[]> _sections;
        static Dictionary<string, ChoiceOption[]> _choices;
        static bool _loaded;
        static string _loadedLocale;

        public static IReadOnlyList<DialogueLine> AfterFirstOffering => Lines("after_first_offering");
        public static IReadOnlyList<DialogueLine> AfterApparitionEvolve => Lines("after_apparition_evolve");
        public static IReadOnlyList<DialogueLine> Petting => Lines("petting");
        public static IReadOnlyList<DialogueLine> TrainingIntro => Lines("training_intro");
        public static IReadOnlyList<DialogueLine> TrainingGaeFirst => Lines("training_gae_first");
        public static IReadOnlyList<DialogueLine> TrainingPassToImugi => Lines("training_pass_to_imugi");
        public static IReadOnlyList<DialogueLine> TrainingImugiThrow1 => Lines("training_imugi_throw1");
        public static IReadOnlyList<DialogueLine> TrainingCapture => Lines("training_capture");
        public static IReadOnlyList<DialogueLine> TrainingImugiBonus => Lines("training_imugi_bonus");
        public static IReadOnlyList<DialogueLine> TrainingMoResult => Lines("training_mo_result");
        public static IReadOnlyList<DialogueLine> TrainingShortcut => Lines("training_shortcut");
        public static IReadOnlyList<DialogueLine> TrainingCoinTile => Lines("training_coin_tile");
        public static IReadOnlyList<DialogueLine> TrainingHandoff => Lines("training_handoff");
        public static IReadOnlyList<DialogueLine> EnergyWarning => Lines("energy_warning");
        public static IReadOnlyList<DialogueLine> NeedMoreEnergy => Lines("need_more_energy");
        public static IReadOnlyList<DialogueLine> AfterManifestEvolve => Lines("after_manifest_evolve");
        public static IReadOnlyList<DialogueLine> MemoryMoon => Lines("memory_moon");
        public static IReadOnlyList<DialogueLine> MemoryEarth => Lines("memory_earth");
        public static IReadOnlyList<DialogueLine> MemoryShop => Lines("memory_shop");
        public static IReadOnlyList<DialogueLine> BeforeBlackeningChoices => Lines("before_blackening_choices");
        public static IReadOnlyList<DialogueLine> Blackening => Lines("blackening");
        public static IReadOnlyList<DialogueLine> BlackRabbitFlee => Lines("black_rabbit_flee");
        public static IReadOnlyList<DialogueLine> ImugiRestore => Lines("imugi_restore");
        public static IReadOnlyList<DialogueLine> WishPrompt => Lines("wish_prompt");
        public static IReadOnlyList<DialogueLine> Doppelganger => Lines("doppelganger");
        public static IReadOnlyList<DialogueLine> HiddenConfirm => Lines("hidden_confirm");
        public static IReadOnlyList<DialogueLine> HiddenEnding => Lines("hidden_ending");
        public static IReadOnlyList<DialogueLine> CardAndIncense => Lines("card_and_incense");

        public static IReadOnlyList<ChoiceOption> BlackeningChoices => Choices("blackening");
        public static IReadOnlyList<ChoiceOption> WishChoices => Choices("wish");
        public static IReadOnlyList<ChoiceOption> HiddenConfirmChoices => Choices("hidden_confirm");

        public static void Invalidate()
        {
            _loaded = false;
            _loadedLocale = null;
            _sections = null;
            _choices = null;
        }

        public static void EnsureLoaded()
        {
            var locale = GameLocale.Current;
            if (_loaded && _loadedLocale == locale) return;

            _sections = new Dictionary<string, DialogueLine[]>();
            _choices = new Dictionary<string, ChoiceOption[]>();

            var asset = LoadLocaleAsset(locale);
            if (asset == null && locale != GameLocale.Fallback)
            {
                Debug.LogWarning($"[OktoDialogue] Missing locale '{locale}', fallback to '{GameLocale.Fallback}'");
                asset = LoadLocaleAsset(GameLocale.Fallback);
                locale = GameLocale.Fallback;
            }

            if (asset == null)
            {
                Debug.LogError($"[OktoDialogue] Missing Resources/{ResourcePathPrefix}.{GameLocale.Fallback}.json");
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
                    _choices[c.id] = c.options ?? Array.Empty<ChoiceOption>();
                }
            }

            _loaded = true;
            _loadedLocale = locale;
            Debug.Log($"[OktoDialogue] Loaded v{file.version} ({locale}): {_sections.Count} sections, {_choices.Count} choice sets");
        }

        static TextAsset LoadLocaleAsset(string locale)
        {
            var primary = Resources.Load<TextAsset>($"{ResourcePathPrefix}.{locale}");
            if (primary != null) return primary;
            // 구버전 호환: Dialogue/okto_tutorial.json
            if (locale == GameLocale.Fallback)
                return Resources.Load<TextAsset>(ResourcePathPrefix);
            return null;
        }

        static DialogueLine[] Lines(string id)
        {
            EnsureLoaded();
            return _sections.TryGetValue(id, out var lines) ? lines : Array.Empty<DialogueLine>();
        }

        static ChoiceOption[] Choices(string id)
        {
            EnsureLoaded();
            return _choices.TryGetValue(id, out var opts) ? opts : Array.Empty<ChoiceOption>();
        }
    }

    /// <summary>okto_tutorial JSON section id.</summary>
    public static class OktoDialogueSection
    {
        public const string AfterFirstOffering = "after_first_offering";
        public const string AfterApparitionEvolve = "after_apparition_evolve";
        public const string Petting = "petting";
        public const string TrainingIntro = "training_intro";
        public const string TrainingGaeFirst = "training_gae_first";
        public const string TrainingPassToImugi = "training_pass_to_imugi";
        public const string TrainingImugiThrow1 = "training_imugi_throw1";
        public const string TrainingCapture = "training_capture";
        public const string TrainingImugiBonus = "training_imugi_bonus";
        public const string TrainingMoResult = "training_mo_result";
        public const string TrainingShortcut = "training_shortcut";
        public const string TrainingCoinTile = "training_coin_tile";
        public const string TrainingHandoff = "training_handoff";
        public const string EnergyWarning = "energy_warning";
        public const string NeedMoreEnergy = "need_more_energy";
        public const string AfterManifestEvolve = "after_manifest_evolve";
        public const string MemoryMoon = "memory_moon";
        public const string MemoryEarth = "memory_earth";
        public const string MemoryShop = "memory_shop";
        public const string BeforeBlackeningChoices = "before_blackening_choices";
        public const string Blackening = "blackening";
        public const string BlackRabbitFlee = "black_rabbit_flee";
        public const string ImugiRestore = "imugi_restore";
        public const string WishPrompt = "wish_prompt";
        public const string HiddenConfirm = "hidden_confirm";
        public const string HiddenEnding = "hidden_ending";
        public const string Doppelganger = "doppelganger";
        public const string CardAndIncense = "card_and_incense";
    }
}
