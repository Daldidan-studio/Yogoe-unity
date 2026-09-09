using System;
using System.IO;
using UnityEngine;

namespace Yoegoe.Save
{
    /// <summary>
    /// 세이브 파일 읽기/쓰기만 담당. 오프라인 시뮬·씬 반영은 각각 다른 클래스.
    /// WebGL은 persistentDataPath 제약이 있어 PlayerPrefs(JSON 문자열)로 저장한다.
    /// </summary>
    public static class GameSaveService
    {
        public const string PrefsKey = "Yoegoe.GameSave.v1";
        public const string FileName = "games_save_v1.json";

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return PlayerPrefs.HasKey(PrefsKey);
#else
            return PlayerPrefs.HasKey(PrefsKey) || File.Exists(FilePath);
#endif
        }

        public static void Save(GameSaveData data)
        {
            if (data == null) return;
            data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(data, prettyPrint: true);

#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
#else
            try
            {
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameSaveService] 파일 저장 실패, PlayerPrefs로 폴백: " + e.Message);
            }
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
#endif
        }

        public static bool TryLoad(out GameSaveData data)
        {
            data = null;
            string json = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (PlayerPrefs.HasKey(PrefsKey))
                json = PlayerPrefs.GetString(PrefsKey);
#else
            if (File.Exists(FilePath))
            {
                try { json = File.ReadAllText(FilePath); }
                catch (Exception e)
                {
                    Debug.LogWarning("[GameSaveService] 파일 로드 실패: " + e.Message);
                }
            }
            if (string.IsNullOrEmpty(json) && PlayerPrefs.HasKey(PrefsKey))
                json = PlayerPrefs.GetString(PrefsKey);
#endif

            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                data = JsonUtility.FromJson<GameSaveData>(json);
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameSaveService] JSON 파싱 실패: " + e.Message);
                data = null;
                return false;
            }
        }

        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
#if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                // 과거에 잘못 저장된 파일명도 함께 제거
                string legacy = Path.Combine(Application.persistentDataPath, "games_save_v1.json");
                if (File.Exists(legacy)) File.Delete(legacy);
            }
            catch { /* ignore */ }
#endif
            Debug.Log("[GameSaveService] 세이브 삭제됨: " + PrefsKey);
        }
    }
}
