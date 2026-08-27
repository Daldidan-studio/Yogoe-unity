using System.Runtime.InteropServices;
using KSpirits.Model;
using KSpirits.Systems;
using KSpirits.Tutorial;
using KSpirits.UI;
using UnityEngine;

namespace KSpirits.Bootstrap
{
    /// <summary>
    /// Boot 씬에 배치. 씬의 ScrollScreenUI·EventSystem을 사용해 게임을 시작한다.
    /// UI 최초 생성: KSpirits → Setup Boot Scene UI
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] ScrollScreenUI _ui;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void YogoeHideLoadingOverlay();
#endif

        void Awake()
        {
            if (_ui == null)
                _ui = FindFirstObjectByType<ScrollScreenUI>();

            if (_ui == null)
            {
                Debug.LogError("[Bootstrap] ScrollScreenUI가 씬에 없습니다. KSpirits → Setup Boot Scene UI 를 실행하세요.");
                enabled = false;
                return;
            }

            _ui.EnsureWired();

            var state = SaveService.TryLoad(out var loaded) ? loaded : GameState.CreateNewGame();
            var saveHost = gameObject.GetComponent<SaveHost>() ?? gameObject.AddComponent<SaveHost>();
            saveHost.Bind(state);

            var opening = gameObject.GetComponent<OpeningController>() ?? gameObject.AddComponent<OpeningController>();
            opening.Bind(state, _ui);

            if (!state.OpeningSeen)
            {
                opening.Play(() =>
                {
                    SaveService.Save(state);
                    StartTutorialFlow(state, opening);
                });
            }
            else
            {
                StartTutorialFlow(state, opening);
            }
        }

        void StartTutorialFlow(GameState state, OpeningController opening)
        {
            var summon = gameObject.GetComponent<SummonController>() ?? gameObject.AddComponent<SummonController>();
            summon.Bind(state, _ui);

            var tutorial = gameObject.GetComponent<TutorialController>() ?? gameObject.AddComponent<TutorialController>();
            tutorial.Bind(state, _ui, summon);
            tutorial.Begin();

            var nurtureTraining = gameObject.GetComponent<NurtureTrainingController>() ?? gameObject.AddComponent<NurtureTrainingController>();
            nurtureTraining.Bind(state, _ui);

            var debugMenu = gameObject.GetComponent<TutorialDebugMenu>() ?? gameObject.AddComponent<TutorialDebugMenu>();
            debugMenu.Bind(tutorial, _ui, nurtureTraining, opening);
        }

        void Start()
        {
            // HTML 타이틀+progress는 Boot가 뜬 뒤에만 내린다 (Unity Splash/빈 화면 노출 방지)
#if UNITY_WEBGL && !UNITY_EDITOR
            YogoeHideLoadingOverlay();
#endif
        }
    }
}
