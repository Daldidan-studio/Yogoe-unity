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

            var tutorial = gameObject.GetComponent<TutorialController>() ?? gameObject.AddComponent<TutorialController>();
            tutorial.Bind(state, _ui);
            tutorial.Begin();
        }
    }
}
