using System;
using System.IO;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Persistence
{
    /// <summary>
    /// 대기 구간의 한 판을 조각 모음으로 저장하고 되돌린다.
    ///
    /// 시스템의 속을 여기서 알지 않는다. 명단(<see cref="SaveAgentRegistry"/>)에 적힌
    /// 것들에게 각자 자기 몫을 물어 열쇠와 함께 담고, 되돌릴 때도 열쇠로 나눠 준다.
    /// 시스템이 하나 늘어도 이 파일은 그대로다.
    /// </summary>
    public static class RunSaveSystem
    {
        private const string FileName = "defence-run-v2.json";
        private const string BackupFileName = "defence-run-v2.backup.json";

        /// <summary>
        /// 복원이 도중에 실패한 판인가.
        ///
        /// 저장 기능을 꺼 두어야 했던 이유가 정확히 이것이었다 — 일부만 되돌아온 판을
        /// 다음 자동 저장이 그대로 덮어써서, 되살릴 수 없는 세이브가 되었다.
        /// 한 번 이 깃발이 서면 그 판에서는 다시 저장하지 않는다.
        /// </summary>
        public static bool RestoreIncomplete { get; private set; }

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);

        public static bool HasSave => File.Exists(SavePath);

        public static bool SaveCurrentRun()
        {
            var day = DayManager.Current;
            var registry = SaveAgentRegistry.Current;
            if (registry == null || day == null || !day.IsStandby)
                return false;

            // 반쪽짜리 판을 덮어쓰지 않는다. 망가진 세이브보다 오래된 세이브가 낫다.
            if (RestoreIncomplete)
            {
                Debug.LogWarning("복원이 온전하지 않았던 판이라 저장을 건너뜁니다.");
                return false;
            }

            var file = new RunSaveFile
            {
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                completedDay = day.CurrentDay
            };

            foreach (var agent in registry.Agents)
            {
                try
                {
                    var json = agent.GetSaveData();
                    if (!string.IsNullOrWhiteSpace(json))
                        file.entries.Add(new RunSaveEntry { key = agent.SaveKey, json = json });
                }
                catch (Exception exception)
                {
                    // 한 조각을 못 담았는데 나머지만 저장하면, 다음에 불러온 판은
                    // 그 시스템만 초기 상태인 채로 조용히 굴러간다. 통째로 그만둔다.
                    Debug.LogError($"'{agent.SaveKey}' 저장 실패로 이번 저장을 취소합니다: {exception.Message}");
                    return false;
                }
            }

            try
            {
                WriteAtomically(JsonUtility.ToJson(file, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"게임 저장 실패: {exception.Message}");
                return false;
            }
        }

        public static bool TryRestoreCurrentRun()
        {
            var registry = SaveAgentRegistry.Current;
            if (registry == null || !TryLoad(out var file))
                return false;

            var restoredAny = false;

            foreach (var agent in registry.Agents)
            {
                var json = file.Find(agent.SaveKey);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                try
                {
                    agent.RestoreData(json);
                    restoredAny = true;
                }
                catch (Exception exception)
                {
                    // 실패가 이 열쇠 하나에 갇힌다. 나머지는 계속 되돌리되,
                    // 이 판은 더 이상 저장하지 않는다.
                    RestoreIncomplete = true;
                    Debug.LogError($"'{agent.SaveKey}' 복원 실패: {exception.Message}");
                }
            }

            if (!restoredAny)
                return false;

            Debug.Log(RestoreIncomplete
                ? $"{file.completedDay}일차 체크포인트를 일부만 불러왔습니다. 이 판은 저장되지 않습니다."
                : $"{file.completedDay}일차 체크포인트를 불러왔습니다. ({file.savedAtUtc})");

            return !RestoreIncomplete;
        }

        public static bool TryLoad(out RunSaveFile file)
        {
            file = null;
            if (!File.Exists(SavePath))
                return false;

            try
            {
                file = Parse(File.ReadAllText(SavePath));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"저장 파일을 읽지 못했습니다. 백업을 확인합니다: {exception.Message}");
                return TryLoadBackup(out file);
            }
        }

        public static void DeleteSave()
        {
            RestoreIncomplete = false;
            TryDelete(SavePath);
            TryDelete(BackupPath);
        }

        private static RunSaveFile Parse(string json)
        {
            var parsed = JsonUtility.FromJson<RunSaveFile>(json);
            if (parsed == null || parsed.version != RunSaveFile.CurrentVersion || parsed.entries == null || parsed.entries.Count == 0)
                throw new InvalidDataException("지원하지 않거나 비어 있는 저장 파일입니다.");

            return parsed;
        }

        private static bool TryLoadBackup(out RunSaveFile file)
        {
            file = null;
            if (!File.Exists(BackupPath))
                return false;

            try
            {
                file = Parse(File.ReadAllText(BackupPath));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"백업 저장 파일도 읽지 못했습니다: {exception.Message}");
                return false;
            }
        }

        private static void WriteAtomically(string json)
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            var temporaryPath = SavePath + ".tmp";
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, BackupPath, true);
                File.Delete(SavePath);
            }

            File.Move(temporaryPath, SavePath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"저장 파일 삭제 실패: {exception.Message}");
            }
        }
    }
}
