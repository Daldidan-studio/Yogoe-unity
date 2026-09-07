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
        static bool _suppressNextAutoSave;

        /// <summary>
        /// 세이브 삭제 직후 씬을 리로드할 때(게임 초기화 등) 호출 — 리로드가 이 오브젝트를
        /// 파괴하면서 OnDestroy가 자동저장을 돌려, 방금 지운 파일을 그대로 되살리는 걸 막는다.
        /// </summary>
        public static void SuppressNextAutoSave() => _suppressNextAutoSave = true;

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

        void OnDestroy()
        {
            if (_suppressNextAutoSave)
            {
                _suppressNextAutoSave = false;
                return;
            }
            SaveNow();
        }
    }
}