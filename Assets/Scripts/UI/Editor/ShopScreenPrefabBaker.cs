using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// 상점 셸을 Prefab으로 굽고 Main 씬에 배치한다.
    /// 메뉴: Yoegoe → Bake ShopScreen Prefab (Into Main Scene)
    /// </summary>
    public static class ShopScreenPrefabBaker
    {
        const string PrefabPath = "Assets/Prefabs/UI/ShopScreen.prefab";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string FontPath = "Assets/Fonts/DOSGothic.ttf";
        const string BgPath = "Assets/Resources/UI/ShopInterior.png";
        const string ImugiPath = "Assets/Resources/UI/ImugiPortrait.png";

        [MenuItem("Yoegoe/Bake ShopScreen Prefab (Into Main Scene)")]
        public static void BakeFromMenu() => Bake();

        /// <summary>Unity -batchmode -executeMethod Yoegoe.UI.EditorTools.ShopScreenPrefabBaker.Bake</summary>
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
            var imugi = AssetDatabase.LoadAssetAtPath<Sprite>(ImugiPath);

            var root = new GameObject("ShopScreen");
            var screen = root.AddComponent<ShopScreen>();
            screen.font = font;
            screen.shopBackground = bg;
            screen.imugiSprite = imugi;
            screen.EnsureBuiltForBake();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
            {
                Debug.LogError("[ShopScreenPrefabBaker] Prefab 저장 실패: " + PrefabPath);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            foreach (var old in Object.FindObjectsByType<ShopScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "ShopScreen";
            instance.SetActive(false);

            var baked = instance.GetComponent<ShopScreen>();
            if (baked != null)
            {
                baked.font = font;
                baked.shopBackground = bg;
                baked.imugiSprite = imugi;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ShopScreenPrefabBaker] Prefab + Main 씬 배치 완료: " + PrefabPath);
        }
    }
}
