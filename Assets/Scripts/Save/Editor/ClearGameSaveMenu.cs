#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Yoegoe.Save;

namespace Yoegoe.EditorTools
{
    /// <summary>Edit → Clear All PlayerPrefs 로는 디스크 JSON이 안 지워질 수 있어 전용 메뉴를 둔다.</summary>
    public static class ClearGameSaveMenu
    {
        [MenuItem("Yoegoe/세이브 삭제 (향·진행 초기화)")]
        public static void ClearSave()
        {
            // Play 중이면 종료 시 다시 저장될 수 있음
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("세이브 삭제",
                    "Play를 먼저 끄고 다시 실행해 주세요.", "OK");
                return;
            }

            GameSaveService.DeleteSave();
            try
            {
                string dir = Application.persistentDataPath;
                foreach (var name in new[] { "game_save_v1.json", "games_save_v1.json" })
                {
                    string path = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
            }
            catch { /* ignore */ }

            int hyang = Data.StartingStateSettings.Get().startingHyang;
            Debug.Log("[Yoegoe] 세이브 삭제 완료. Play 시 시작 향=" + hyang);
            EditorUtility.DisplayDialog("세이브 삭제",
                "삭제했습니다. Play하면 향 " + hyang + "으로 시작합니다.", "OK");
        }
    }
}
#endif
