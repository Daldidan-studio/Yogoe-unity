using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// 윷 화면 셸(+보드)을 Prefab으로 굽고 Main 씬에 둔다.
    /// 메뉴: Yoegoe → Bake YutScreen Prefab (Into Main Scene)
    /// Prefab을 더블클릭하면 Play 없이 레이아웃을 볼 수 있다.
    /// </summary>
    public static class YutScreenPrefabBaker
    {
        const string PrefabPath = "Assets/Prefabs/UI/YutScreen.prefab";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string FontPath = "Assets/Fonts/DOSGothic.ttf";

        [MenuItem("Yoegoe/Bake YutScreen Prefab (Into Main Scene)")]
        public static void BakeFromMenu() => Bake();

        /// <summary>Unity -batchmode -executeMethod Yoegoe.UI.EditorTools.YutScreenPrefabBaker.Bake</summary>
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            var root = new GameObject("YutScreen");
            var screen = root.AddComponent<YutScreen>();
            screen.font = font;
            screen.EnsureBuiltForBake();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
            {
                Debug.LogError("[YutScreenPrefabBaker] Prefab 저장 실패: " + PrefabPath);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            foreach (var old in Object.FindObjectsByType<YutScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "YutScreen";
            // Main 씬에서는 기본 비활성 — Prefab 에셋을 열어 레이아웃 편집
            instance.SetActive(false);

            var baked = instance.GetComponent<YutScreen>();
            if (baked != null) baked.font = font;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[YutScreenPrefabBaker] Prefab 완료. 레이아웃은 Assets/Prefabs/UI/YutScreen.prefab 을 열어 보세요.");
        }
    }
}
