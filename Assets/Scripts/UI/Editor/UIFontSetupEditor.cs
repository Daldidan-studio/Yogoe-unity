using UnityEditor;
using UnityEngine;

namespace KSpirits.UI.Editor
{
    public static class UIFontSetupEditor
    {
        const string SettingsPath = "Assets/Resources/Settings/UIFontSettings.asset";

        [MenuItem("KSpirits/Setup UI Font Settings")]
        public static void EnsureSettingsAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UIFontSettings>(SettingsPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorUtility.DisplayDialog("KSpirits",
                    "UIFontSettings 가 이미 있습니다.\n\n" +
                    "1. Assets/Fonts/ 에 .ttf 넣기\n" +
                    "2. UIFontSettings Inspector에서 역할별 연결\n" +
                    "   · Dialogue Font → 말풍선\n" +
                    "   · User Info Font → 유저 HUD\n" +
                    "   · Hud Numeric Font → 숫자",
                    "OK");
                return;
            }

            System.IO.Directory.CreateDirectory("Assets/Resources/Settings");
            var settings = ScriptableObject.CreateInstance<UIFontSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;
            EditorUtility.DisplayDialog("KSpirits",
                "UIFontSettings 생성 완료.\n\n" +
                "1. Assets/Fonts/ 폴더에 폰트 파일(.ttf) 넣기\n" +
                "2. UIFontSettings Inspector에서 역할별 연결\n" +
                "   · Dialogue / User Info / Hud Numeric\n" +
                "3. 개별 Text는 UIFontApplier 컴포넌트로 Role 지정 가능",
                "OK");
        }
    }
}
