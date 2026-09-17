using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// 공양간 셸 Prefab + Main 씬 배치.
    /// 메뉴: Yoegoe → Bake GongyangganScreen Prefab (Into Main Scene)
    /// </summary>
    public static class GongyangganScreenPrefabBaker
    {
        const string PrefabPath = "Assets/Prefabs/UI/GongyangganScreen.prefab";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string FontPath = "Assets/Fonts/DOSGothic.ttf";

        [MenuItem("Yoegoe/Bake GongyangganScreen Prefab (Into Main Scene)")]
        public static void BakeFromMenu() => Bake();

        /// <summary>Unity -batchmode -executeMethod Yoegoe.UI.EditorTools.GongyangganScreenPrefabBaker.Bake</summary>
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            var root = new GameObject("GongyangganScreen");
            var screen = root.AddComponent<GongyangganScreen>();
            screen.font = font;
            screen.EnsureBuiltForBake();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
            {
                Debug.LogError("[GongyangganScreenPrefabBaker] Prefab 저장 실패: " + PrefabPath);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Resources에도 동일 Prefab 복사 (런타임 Resolve 폴백용)
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");
            AssetDatabase.CopyAsset(PrefabPath, "Assets/Resources/UI/GongyangganScreen.prefab");

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            foreach (var old in Object.FindObjectsByType<GongyangganScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "GongyangganScreen";
            instance.SetActive(false);

            var baked = instance.GetComponent<GongyangganScreen>();
            if (baked != null) baked.font = font;

            EditorSceneManager.MarkSceneDirty(scene);
            MainSceneBootstrap.EnsureInOpenScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GongyangganScreenPrefabBaker] Prefab + Main 씬 배치 완료: " + PrefabPath);
        }
    }
}
