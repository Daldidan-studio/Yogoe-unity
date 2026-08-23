using KSpirits.Bootstrap;
using KSpirits.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KSpirits.UI.Editor
{
    public static class ScrollScreenUISetupEditor
    {
        const string BootScenePath = "Assets/Scenes/Boot.unity";
        const string PrefabPath = "Assets/Prefabs/UI/ScrollScreenUI.prefab";

        [MenuItem("KSpirits/Setup Boot Scene UI")]
        public static void SetupBootSceneUI()
        {
            if (!System.IO.File.Exists(BootScenePath))
            {
                EditorUtility.DisplayDialog("KSpirits", $"Boot 씬을 찾을 수 없습니다:\n{BootScenePath}", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            FindOrCreateCanvas();
            FindOrCreateEventSystem();

            var existing = Object.FindFirstObjectByType<ScrollScreenUI>();
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var uiGo = new GameObject("ScrollScreenUI", typeof(RectTransform), typeof(ScrollScreenUI));
            uiGo.transform.SetParent(Object.FindFirstObjectByType<Canvas>().transform, false);
            var ui = uiGo.GetComponent<ScrollScreenUI>();
            var uiRt = (RectTransform)uiGo.transform;
            uiRt.anchorMin = Vector2.zero;
            uiRt.anchorMax = Vector2.one;
            uiRt.offsetMin = Vector2.zero;
            uiRt.offsetMax = Vector2.zero;

            ScrollScreenUIBuilder.Build(ui);

            DialogueLayoutSetupEditor.EnsureOnScrollScreenUI(ui);

            var bootstrap = FindOrCreateGameBootstrap();
            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("_ui").objectReferenceValue = ui;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
            PrefabUtility.SaveAsPrefabAsset(uiGo, PrefabPath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = uiGo;

            GameViewMobileSetup.ApplyAfterSceneSetup();

            EditorUtility.DisplayDialog("KSpirits",
                "Boot 씬 UI 설정 완료!\n\n" +
                "• Hierarchy: GameCanvas → ScrollScreenUI\n" +
                "• Prefab: Assets/Prefabs/UI/ScrollScreenUI.prefab\n" +
                "• Game View: Mobile Portrait 9:16 (1080×1920)\n\n" +
                "배치는 Scene, 비율 확인은 Game 탭을 사용하세요.",
                "OK");
        }

        static Canvas FindOrCreateCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
                return canvas;

            var go = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        static void FindOrCreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            var inputModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
                es.AddComponent(inputModuleType);
            else
                es.AddComponent<StandaloneInputModule>();
        }

        static GameBootstrap FindOrCreateGameBootstrap()
        {
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap != null)
                return bootstrap;

            var go = new GameObject("GameBootstrap");
            return go.AddComponent<GameBootstrap>();
        }
    }
}
