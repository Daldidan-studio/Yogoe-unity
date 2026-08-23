using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KSpirits.UI.Editor
{
    public static class GameViewMobileSetup
    {
        const string SizeName = "Mobile Portrait 9:16";
        const int Width = 1080;
        const int Height = 1920;

        [MenuItem("KSpirits/Game View/Mobile Portrait (9:16)")]
        public static void SetMobilePortraitGameView()
        {
            if (!TrySetGameViewSize(Width, Height, SizeName))
            {
                EditorUtility.DisplayDialog("KSpirits",
                    "Game View 크기 변경에 실패했습니다.\n\n" +
                    "Game 탭 → 상단 해상도 드롭다운 → + → Aspect Ratio → 9:16 Portrait 를 수동으로 추가해 주세요.",
                    "OK");
                return;
            }

            FocusGameView();
            Debug.Log("[KSpirits] Game View → Mobile Portrait 1080×1920 (9:16)");
        }

        internal static void ApplyAfterSceneSetup()
        {
            TrySetGameViewSize(Width, Height, SizeName);
            FocusGameView();
        }

        static void FocusGameView()
        {
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            EditorWindow.GetWindow(gameViewType, false, "Game", true);
        }

        static bool TrySetGameViewSize(int width, int height, string label)
        {
            try
            {
                var sizesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instanceProp = singleType.GetProperty("instance");
                var sizesInstance = instanceProp?.GetValue(null);
                if (sizesInstance == null) return false;

                var currentGroup = sizesType.GetMethod("GetCurrentGroup")?.Invoke(sizesInstance, null);
                if (currentGroup == null) return false;

                var groupType = currentGroup.GetType();
                var index = FindOrAddSize(groupType, currentGroup, width, height, label);
                if (index < 0) return false;

                var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                var gameView = EditorWindow.GetWindow(gameViewType, false, "Game", false);
                var selectedSizeIndexProp = gameViewType.GetProperty("selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                selectedSizeIndexProp?.SetValue(gameView, index);
                gameView.Repaint();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[KSpirits] GameView setup: {e.Message}");
                return false;
            }
        }

        static int FindOrAddSize(Type groupType, object group, int width, int height, string label)
        {
            var getBuiltinCount = groupType.GetMethod("GetBuiltinCount");
            var getCustomCount = groupType.GetMethod("GetCustomCount");
            var getGameViewSize = groupType.GetMethod("GetGameViewSize");
            var addCustomSize = groupType.GetMethod("AddCustomSize");

            var gameViewSizeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
            var sizeTypeEnum = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
            var fixedResolution = Enum.Parse(sizeTypeEnum, "FixedResolution");

            int builtin = (int)getBuiltinCount.Invoke(group, null);
            int custom = (int)getCustomCount.Invoke(group, null);

            for (int i = 0; i < builtin + custom; i++)
            {
                var size = getGameViewSize.Invoke(group, new object[] { i });
                if (size == null) continue;
                var w = (int)gameViewSizeType.GetProperty("width").GetValue(size);
                var h = (int)gameViewSizeType.GetProperty("height").GetValue(size);
                if (w == width && h == height) return i;
            }

            var ctor = gameViewSizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            if (ctor == null) return -1;
            var newSize = ctor.Invoke(new[] { fixedResolution, width, height, label });
            addCustomSize.Invoke(group, new[] { newSize });
            return builtin + custom;
        }
    }
}
