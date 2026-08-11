using System;
using System.IO;
using KSpirits.Model;
using UnityEngine;

namespace KSpirits.Systems
{
    /// <summary>
    /// 단일 슬롯(slot0) JSON 세이브.
    /// 경로: Application.persistentDataPath/save_slot0.json
    /// </summary>
    public static class SaveService
    {
        public const int SlotIndex = 0;
        const string FileName = "save_slot0.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists() => File.Exists(SavePath);

        public static bool TryLoad(out GameState state)
        {
            state = null;
            if (!Exists()) return false;

            try
            {
                var json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null)
                {
                    Debug.LogWarning($"[Save] 파싱 실패: {SavePath}");
                    return false;
                }

                MigrateIfNeeded(data);
                state = SaveMapper.ToState(data);
                Debug.Log($"[Save] 로드 OK step={state.TutorialStep} path={SavePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 로드 실패: {e.Message}");
                return false;
            }
        }

        public static void Save(GameState state)
        {
            if (state == null) return;

            try
            {
                var data = SaveMapper.FromState(state);
                var json = JsonUtility.ToJson(data, prettyPrint: true);
                var dir = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[Save] 저장 OK step={state.TutorialStep}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e.Message}");
            }
        }

        public static void Delete()
        {
            try
            {
                if (Exists()) File.Delete(SavePath);
                Debug.Log("[Save] 삭제 OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 삭제 실패: {e.Message}");
            }
        }

        static void MigrateIfNeeded(SaveData data)
        {
            if (data.version >= SaveData.CurrentVersion) return;

            // v1: 최초 스키마. 이후 버전에서 필드 보정.
            Debug.Log($"[Save] migrate {data.version} → {SaveData.CurrentVersion}");
            data.version = SaveData.CurrentVersion;
        }
    }
}
