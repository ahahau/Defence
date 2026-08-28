using System;
using System.IO;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Artifacts;
using _01.Code.Progression;
using _01.Code.UI;
using UnityEngine;

namespace _01.Code.Persistence
{
    /// <summary>대기 구간의 한 판을 버전이 붙은 JSON 체크포인트로 저장한다.</summary>
    public static class RunSaveSystem
    {
        private const string FileName = "defence-run-v1.json";
        private const string BackupFileName = "defence-run-v1.backup.json";

        /// <summary>
        /// 런 저장을 통째로 끈다. 되살릴 때는 이 값만 true로 돌리면 되고,
        /// 저장·복원 코드는 그대로 남아 있다.
        ///
        /// 지금 끈 이유: 복원이 일부 실패한 판을 다음 자동 저장이 그대로 덮어써서,
        /// 되살릴 수 없는 상태로 세이브가 망가지는 일이 실제로 있었다(금고와 보관 금화가 사라짐).
        /// 부분 실패를 감지해 저장을 막는 가드가 붙기 전까지는 켜 두지 않는 편이 안전하다.
        /// </summary>
        /// <remarks>const가 아니라 readonly인 이유는 const로 두면 컴파일러가 나머지를
        /// 도달 불가 코드로 접어 경고를 쏟기 때문이다. 저장 코드는 살아 있어야 한다.</remarks>
        public static readonly bool Enabled = false;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        public static bool HasSave => Enabled && File.Exists(SavePath);

        public static bool SaveCurrentRun()
        {
            if (!Enabled)
                return false;

            var graph = DungeonGraphController.Current;
            var day = DayManager.Current;
            if (graph == null || day == null || !day.IsStandby)
                return false;

            try
            {
                var data = graph.CaptureRunSave();
                data.completedDay = day.CurrentDay;
                data.savedAtUtc = DateTime.UtcNow.ToString("O");

                var cost = CostManager.Current;
                if (cost != null)
                {
                    data.gold = cost.CurrentGold;
                    data.debt = cost.CurrentDebt;
                    data.buildDiscountRate = cost.CurrentBuildDiscountRate;
                }

                var morale = MoralePolicyManager.Current;
                if (morale != null)
                {
                    data.morale = morale.CurrentMorale;
                    morale.CaptureSaveState(data.policies);
                }

                HiredUnitRoster.Current?.CaptureSaveState(data.roster);
                var artifactController = UnityEngine.Object.FindAnyObjectByType<ArtifactEffectController>();
                if (artifactController?.Inventory != null)
                    foreach (var artifact in artifactController.Inventory.ObtainedArtifacts)
                        if (artifact != null) data.artifacts.Add(artifact.name);
                var merchant = UnityEngine.Object.FindAnyObjectByType<MerchantPanelView>();
                if (merchant != null)
                    data.merchantPurchaseCount = merchant.PurchaseCount;
                VillageConquestSystem.Current?.CaptureSaveState(data.villageConquests);
                WriteAtomically(JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"게임 저장 실패: {exception.Message}");
                return false;
            }
        }

        public static bool TryRestoreCurrentRun(DungeonGraphController graph)
        {
            if (!Enabled)
                return false;

            if (graph == null || !TryLoad(out var data))
                return false;

            try
            {
                HiredUnitRoster.Current?.RestoreSaveState(data.roster);
                var merchant = UnityEngine.Object.FindAnyObjectByType<MerchantPanelView>();
                merchant?.RestoreCheckpoint(data.artifacts, data.merchantPurchaseCount, data.completedDay);
                VillageConquestSystem.Current?.RestoreSaveState(data.villageConquests);
                UnityEngine.Object.FindAnyObjectByType<ExpeditionMapPanelView>()?.SyncConquestFromSystem();
                if (!graph.RestoreRunSave(data))
                    return false;

                CostManager.Current?.RestoreCheckpoint(data.gold, data.debt, data.buildDiscountRate);
                MoralePolicyManager.Current?.RestoreCheckpoint(data.morale, data.completedDay, data.policies);
                DayManager.Current?.RestoreCheckpoint(data.completedDay, WaveManager.Current?.PortalNode != null);
                Debug.Log($"{data.completedDay}일차 체크포인트를 불러왔습니다. ({data.savedAtUtc})");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"게임 불러오기 실패. 새 게임으로 시작합니다: {exception.Message}");
                return false;
            }
        }

        public static bool TryLoad(out RunSaveData data)
        {
            data = null;
            if (!Enabled || !File.Exists(SavePath))
                return false;

            try
            {
                var json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<RunSaveData>(json);
                if (data == null || data.version != RunSaveData.CurrentVersion || data.nodes == null || data.nodes.Count == 0)
                    throw new InvalidDataException("지원하지 않거나 비어 있는 저장 파일입니다.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"저장 파일을 읽지 못했습니다. 백업을 확인합니다: {exception.Message}");
                return TryLoadBackup(out data);
            }
        }

        public static void DeleteSave()
        {
            TryDelete(SavePath);
            TryDelete(Path.Combine(Application.persistentDataPath, BackupFileName));
        }

        private static bool TryLoadBackup(out RunSaveData data)
        {
            data = null;
            var backupPath = Path.Combine(Application.persistentDataPath, BackupFileName);
            if (!File.Exists(backupPath))
                return false;

            try
            {
                data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(backupPath));
                return data != null && data.version == RunSaveData.CurrentVersion && data.nodes?.Count > 0;
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
            var backupPath = Path.Combine(Application.persistentDataPath, BackupFileName);
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, backupPath, true);
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
