using KSpirits.Core;
using KSpirits.Data;
using KSpirits.Model;
using KSpirits.UI;
using UnityEngine;

namespace KSpirits.Systems
{
    /// <summary>소환 화면 ↔ 게임 상태 연결.</summary>
    public class SummonController : MonoBehaviour
    {
        GameState _state;
        ScrollScreenUI _ui;
        SummonScreenUI _summonScreen;

        public void Bind(GameState state, ScrollScreenUI ui)
        {
            _state = state;
            _ui = ui;
            _summonScreen = ui.EnsureSummonScreen();
            _summonScreen.OnSummonConfirmed -= HandleSummonConfirmed;
            _summonScreen.OnSummonConfirmed += HandleSummonConfirmed;
            _summonScreen.OnClosed -= HandleClosed;
            _summonScreen.OnClosed += HandleClosed;
        }

        public void Open()
        {
            if (_state == null || _ui == null) return;
            _ui.RefreshAll(_state);
            _summonScreen.Show(_state);
        }

        void HandleSummonConfirmed(YokaiInstance yokai, SummonEntry entry)
        {
            // 이전에 육성하던 요괴는 버리지 않고 보유 목록에 남겨둔 채, 새로 소환한 애로 포커스만 옮긴다.
            _state.OwnedYokai.Add(yokai);
            _state.FocusYokai = yokai;
            _state.ScrollMode = ScrollMode.Nurture;
            _state.TutorialStep = TutorialStepId.Done;
            SaveService.Save(_state);
            _ui.RefreshAll(_state);
            _ui.ShowStatus($"{entry.DisplayName} 육성을 시작합니다");
        }

        void HandleClosed()
        {
            _ui.ShowStatus("족자로 돌아왔습니다");
        }

        void OnDestroy()
        {
            if (_summonScreen == null) return;
            _summonScreen.OnSummonConfirmed -= HandleSummonConfirmed;
            _summonScreen.OnClosed -= HandleClosed;
        }
    }
}
