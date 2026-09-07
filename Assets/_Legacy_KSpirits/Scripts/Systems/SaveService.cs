using System;
using System.IO;
using KSpirits.Model;
using UnityEngine;

namespace KSpirits.Systems
{
    /// <summary>
    /// 단일 슬롯(slot0) JSON 세이브.
    /// 경로: Application.persistentDataPath/save_slot0.json (+ 백업 .bak, 쓰기 중 .tmp)
    /// 쓰기는 tmp에 먼저 쓰고 기존 파일을 .bak으로 민 다음 tmp를 정식 파일로 옮기는
    /// 방식이라, 쓰는 도중 강종돼도 최소한 이전 저장(.bak)은 항상 온전하게 남는다.
    /// 로드도 정식 파일이 깨졌으면 자동으로 .bak을 시도한다.
    /// </summary>
    public static class SaveService
    {
        public const int SlotIndex = 0;
        const string FileName = "save_slot0.json";
        const string BackupSuffix = ".bak";
        const string TempSuffix = ".tmp";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        public static string BackupPath => SavePath + BackupSuffix;
        static string TempPath => SavePath + TempSuffix;

        public static bool Exists() => File.Exists(SavePath);

        public static bool TryLoad(out GameState state)
        {
            if (TryLoadFrom(SavePath, out state)) return true;

            if (!File.Exists(BackupPath)) return false;

            Debug.LogWarning("[Save] 정식 세이브 로드 실패 — 백업(.bak)에서 복구를 시도합니다");
            if (!TryLoadFrom(BackupPath, out state)) return false;

            Debug.LogWarning("[Save] 백업에서 복구 성공");
            return true;
        }

        static bool TryLoadFrom(string path, out GameState state)
        {
            state = null;
            if (!File.Exists(path)) return false;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null)
                {
                    Debug.LogWarning($"[Save] 파싱 실패: {path}");
                    return false;
                }

                MigrateIfNeeded(data);
                state = SaveMapper.ToState(data);
                Debug.Log($"[Save] 로드 OK step={state.TutorialStep} path={path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 로드 실패({path}): {e.Message}");
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

                // tmp에 먼저 쓰고, 기존 정식 파일을 백업으로 밀어둔 뒤 tmp를 정식 파일로 교체.
                // 이 셋 중 어느 단계에서 죽어도 정식 파일이나 백업 중 하나는 항상 온전하다.
                File.WriteAllText(TempPath, json);
                if (File.Exists(SavePath))
                    File.Copy(SavePath, BackupPath, overwrite: true);
                File.Copy(TempPath, SavePath, overwrite: true);
                File.Delete(TempPath);

                SyncWebGLFileSystem();
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
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                if (File.Exists(TempPath)) File.Delete(TempPath);
                SyncWebGLFileSystem();
                Debug.Log("[Save] 삭제 OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 삭제 실패: {e.Message}");
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void YogoeSyncFilesystem();
#endif

        /// <summary>
        /// WebGL의 persistentDataPath는 IndexedDB 위에 얹힌 가상 파일시스템(IDBFS)이라,
        /// File.Write만으로는 브라우저의 실제 IndexedDB에 안 남고 페이지를 새로고침하면
        /// 사라질 수 있다 — 저장할 때마다 명시적으로 FS.syncfs()를 호출해 밀어준다.
        /// </summary>
        static void SyncWebGLFileSystem()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { YogoeSyncFilesystem(); }
            catch (Exception e) { Debug.LogWarning($"[Save] WebGL syncfs 실패: {e.Message}"); }
#endif
        }

        static void MigrateIfNeeded(SaveData data)
        {
            if (data.version >= SaveData.CurrentVersion) return;

            if (data.version < 2)
            {
                // v1: 요괴 1마리(focusYokai) + 카드 1장(oktoCard)만 있던 스키마.
                // → 그 한 마리를 ownedYokai 배열의 첫 항목으로 이관(카드도 그 개체에 합침).
                if ((data.ownedYokai == null || data.ownedYokai.Length == 0) && data.focusYokai != null)
                {
                    data.focusYokai.card = data.oktoCard ?? new CardSave();
                    data.ownedYokai = new[] { data.focusYokai };
                    data.focusIndex = 0;
                }
            }

            if (data.version < 3)
            {
                // v3: 오프닝 컷씬 추가. 이전 세이브는 이미 튜토리얼을 진행 중이었으므로
                // 오프닝을 새로 보여주지 않는다.
                data.openingSeen = true;
            }

            Debug.Log($"[Save] migrate {data.version} → {SaveData.CurrentVersion}");
            data.version = SaveData.CurrentVersion;
        }
    }
}
