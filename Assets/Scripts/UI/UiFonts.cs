using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.UI
{
    /// <summary>
    /// UI 글자 크기. UiStyleSettings 토큰 + ArtScaleSettings.uiFontScale 배율.
    /// </summary>
    public static class UiFonts
    {
        public static UiStyleSettings Style => UiStyleSettings.Get();

        public static int Size(int baseSize)
        {
            float scale = ArtScaleSettings.GetOrDefault().uiFontScale;
            if (scale < 0.5f) scale = 0.5f;
            if (scale > 2.5f) scale = 2.5f;
            return Mathf.Max(1, Mathf.RoundToInt(baseSize * scale));
        }

        public static int Caption => Size(Style.fontSizes.caption);
        public static int Small => Size(Style.fontSizes.small);
        public static int Hint => Size(Style.fontSizes.hint);
        public static int Body => Size(Style.fontSizes.body);
        public static int Label => Size(Style.fontSizes.label);
        public static int Emphasis => Size(Style.fontSizes.emphasis);
        public static int Button => Size(Style.fontSizes.button);
        public static int Title => Size(Style.fontSizes.title);
        public static int HudMerit => Size(Style.fontSizes.hudMerit);
        public static int HudCurrency => Size(Style.fontSizes.hudCurrency);
        public static int HudSlotName => Size(Style.fontSizes.hudSlotName);
        public static int HudSummonName => Size(Style.fontSizes.hudSummonName);
    }
}
