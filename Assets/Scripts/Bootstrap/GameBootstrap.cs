using KSpirits.Model;
using KSpirits.Tutorial;
using KSpirits.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KSpirits.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            var canvas = CreateCanvas();
            var state = GameState.CreateNewGame();
            var ui = ScrollScreenUI.Create(canvas.transform);
            var tutorial = gameObject.AddComponent<TutorialController>();
            tutorial.Bind(state, ui);
            tutorial.Begin();
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // Prefer Input System UI module when available
            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
                es.AddComponent(inputModuleType);
            else
                es.AddComponent<StandaloneInputModule>();
        }

        static Canvas CreateCanvas()
        {
            var go = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            DontDestroyOnLoad(go);
            return canvas;
        }
    }
}
