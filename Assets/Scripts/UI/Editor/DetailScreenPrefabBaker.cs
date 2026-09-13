using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// 상세화면 셸을 Prefab으로 굽고 Main 씬에 배치한다.
    /// 메뉴: Yoegoe → Bake DetailScreen Prefab (Into Main Scene)
    /// </summary>
    public static class DetailScreenPrefabBaker
    {
        const string PrefabPath = "Assets/Prefabs/UI/DetailScreen.prefab";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string FontPath = "Assets/Fonts/DOSGothic.ttf";
        const string WaterIconPath = "Assets/Art/Offerings/Offering_PurifiedWater.png";

        [MenuItem("Yoegoe/Bake DetailScreen Prefab (Into Main Scene)")]
        public static void BakeFromMenu() => Bake();

        /// <summary>Unity -batchmode -executeMethod Yoegoe.UI.EditorTools.DetailScreenPrefabBaker.Bake</summary>
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var waterIcon = AssetDatabase.LoadAssetAtPath<Sprite>(WaterIconPath);

            var root = new GameObject("DetailScreen");
            var screen = root.AddComponent<DetailScreen>();
            screen.font = font;
            screen.purifiedWaterIcon = waterIcon;
            screen.EnsureBuiltForBake();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
            {
                Debug.LogError("[DetailScreenPrefabBaker] Prefab 저장 실패: " + PrefabPath);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            foreach (var old in Object.FindObjectsByType<DetailScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "DetailScreen";
            // Main 씬에서는 기본 비활성 — Prefab 에셋을 열어 레이아웃 편집
            instance.SetActive(false);

            var baked = instance.GetComponent<DetailScreen>();
            if (baked != null)
            {
                baked.font = font;
                baked.purifiedWaterIcon = waterIcon;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DetailScreenPrefabBaker] Prefab + Main 씬 배치 완료: " + PrefabPath);
        }
    }
}
