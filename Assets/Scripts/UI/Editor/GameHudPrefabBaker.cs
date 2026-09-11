using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// HUD 셸을 Prefab으로 굽고 Main 씬에 배치한다.
    /// 메뉴: Yoegoe → Bake GameHud Prefab (Into Main Scene)
    /// </summary>
    public static class GameHudPrefabBaker
    {
        const string PrefabPath = "Assets/Prefabs/UI/GameHud.prefab";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string FontPath = "Assets/Fonts/DOSGothic.ttf";
        const string WaterIconPath = "Assets/Art/Offerings/Offering_PurifiedWater.png";

        [MenuItem("Yoegoe/Bake GameHud Prefab (Into Main Scene)")]
        public static void BakeFromMenu()
        {
            Bake();
        }

        /// <summary>Unity -batchmode -executeMethod Yoegoe.UI.EditorTools.GameHudPrefabBaker.Bake</summary>
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var waterIcon = AssetDatabase.LoadAssetAtPath<Sprite>(WaterIconPath);

            var root = new GameObject("Hud");
            var hud = root.AddComponent<GameHud>();
            hud.font = font;
            hud.purifiedWaterIcon = waterIcon;
            hud.EnsureHudShell();

            // Prefab 저장 전에 런타임 리스너는 비우고(씬 로드 시 Start에서 다시 연결)
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
            {
                Debug.LogError("[GameHudPrefabBaker] Prefab 저장 실패: " + PrefabPath);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            // 기존 씬 HUD 제거
            foreach (var old in Object.FindObjectsByType<GameHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Hud";
            // 에디터에서 셸이 보이도록 활성 유지 (슬롯은 Play 때 채워짐)
            instance.SetActive(true);

            var baked = instance.GetComponent<GameHud>();
            if (baked != null)
            {
                baked.font = font;
                baked.purifiedWaterIcon = waterIcon;
                // detailScreen은 Main.CreateHud가 Play 때 연결
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GameHudPrefabBaker] Prefab + Main 씬 배치 완료: " + PrefabPath);
        }
    }
}
