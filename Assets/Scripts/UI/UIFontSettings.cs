using UnityEngine;

namespace KSpirits.UI
{
    /// <summary>
    /// 역할별 UI 폰트 설정.
    /// Assets/Resources/Settings/UIFontSettings.asset
    /// </summary>
    [CreateAssetMenu(fileName = "UIFontSettings", menuName = "KSpirits/UI Font Settings")]
    public class UIFontSettings : ScriptableObject
    {
        [Tooltip("역할별 미지정 시 사용")]
        public Font defaultFont;

        [Tooltip("말풍선 · 대사 (Dialogue/Speaker/Body)")]
        public Font dialogueFont;

        [Tooltip("유저 정보 · HUD 이름/스테이지 (Header/YokaiName 등)")]
        public Font userInfoFont;

        [Tooltip("엽전 · 수량 등 숫자")]
        public Font hudNumericFont;

        public Font Get(UIFontRole role)
        {
            var font = role switch
            {
                UIFontRole.Dialogue => dialogueFont,
                UIFontRole.UserInfo => userInfoFont,
                UIFontRole.HudNumeric => hudNumericFont,
                _ => defaultFont
            };

            if (font != null) return font;
            if (defaultFont != null) return defaultFont;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
