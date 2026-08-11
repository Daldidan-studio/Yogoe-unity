using KSpirits.Model;
using UnityEngine;

namespace KSpirits.Systems
{
    /// <summary>
    /// 앱 일시정지/종료 시 자동 저장. Bootstrap이 Bind한다.
    /// </summary>
    public class SaveHost : MonoBehaviour
    {
        GameState _state;

        public void Bind(GameState state) => _state = state;

        public void SaveNow()
        {
            if (_state != null) SaveService.Save(_state);
        }

        void OnApplicationPause(bool pause)
        {
            if (pause) SaveNow();
        }

        void OnApplicationQuit() => SaveNow();

        void OnDestroy() => SaveNow();
    }
}