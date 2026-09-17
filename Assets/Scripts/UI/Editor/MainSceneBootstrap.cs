using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// Main 씬에 부트스트랩(<see cref="Yoegoe.Main"/>)이 없으면 만든다.
    /// Bake가 OpenScene으로 씬을 다시 열 때 손수 만든 Main이 날아가는 문제를 막는다.
    /// </summary>
    public static class MainSceneBootstrap
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Yoegoe/Ensure Main Bootstrap (In Main Scene)")]
        public static void EnsureFromMenu()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            if (EnsureInOpenScene())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[MainSceneBootstrap] Main 부트스트랩을 씬에 추가하고 저장했습니다.");
            }
            else
                Debug.Log("[MainSceneBootstrap] Main 부트스트랩이 이미 있습니다.");
        }

        /// <summary>현재 열린 씬 기준. 추가했으면 true.</summary>
        public static bool EnsureInOpenScene()
        {
            if (Object.FindAnyObjectByType<Yoegoe.Main>(FindObjectsInactive.Include) != null)
                return false;

            var go = new GameObject("Main");
            go.AddComponent<Yoegoe.Main>();
            return true;
        }
    }
}
