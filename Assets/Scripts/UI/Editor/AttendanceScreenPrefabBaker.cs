using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// 출석 윷점 셸 Prefab + Main 씬 배치.
    /// 메뉴: Yoegoe → Bake AttendanceScreen Prefab (Into Main Scene)
    /// </summary>
    public static class AttendanceScreenPrefabBaker
    {
        const string PrefabPath = "Assets/Prefabs/UI/AttendanceScreen.prefab";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string FontPath = "Assets/Fonts/DOSGothic.ttf";

        [MenuItem("Yoegoe/Bake AttendanceScreen Prefab (Into Main Scene)")]
        public static void BakeFromMenu() => Bake();

        /// <summary>Unity -batchmode -executeMethod Yoegoe.UI.EditorTools.AttendanceScreenPrefabBaker.Bake</summary>
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            var root = new GameObject("AttendanceScreen");
            var screen = root.AddComponent<AttendanceScreen>();
            screen.font = font;
            screen.EnsureBuiltForBake();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
            {
                Debug.LogError("[AttendanceScreenPrefabBaker] Prefab 저장 실패: " + PrefabPath);
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 씬에만 배치한다 (Resources 복사본은 두지 않음 — 이중 Prefab이 서로 어긋나는 문제 방지)
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            foreach (var old in Object.FindObjectsByType<AttendanceScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "AttendanceScreen";
            var baked = instance.GetComponent<AttendanceScreen>();
            if (baked != null) baked.font = font;

            EditorSceneManager.MarkSceneDirty(scene);
            MainSceneBootstrap.EnsureInOpenScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AttendanceScreenPrefabBaker] Prefab + Main 씬 배치 완료: " + PrefabPath);
        }
    }
}
