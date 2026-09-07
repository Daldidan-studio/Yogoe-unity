using UnityEngine;
using UnityEngine.UI;

namespace KSpirits.UI
{
    /// <summary>
    /// UI Text 공통 폰트 접근.
    /// </summary>
    public static class UIFont
    {
        const string SettingsResourcePath = "Settings/UIFontSettings";

        static UIFontSettings _settings;

        public static Font Default => Get(UIFontRole.Default);

        public static Font Get(UIFontRole role)
        {
            EnsureLoaded();
            if (_settings != null)
                return _settings.Get(role);

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static void Apply(Text text, UIFontRole role)
        {
            if (text == null) return;
            text.font = Get(role);
        }

        public static void Invalidate() => _settings = null;

        static void EnsureLoaded()
        {
            if (_settings != null) return;
            _settings = Resources.Load<UIFontSettings>(SettingsResourcePath);
        }
    }
}
