using _01.Code.Buildings;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// 긴 성장 과정을 건너뛰고 특정 웨이브의 구성과 보스 플래그를 실제 전투로 확인한다.
    /// 임시 전장은 Play Mode 메모리에만 만들고 저장 쓰기는 차단한다.
    /// </summary>
    [InitializeOnLoad]
    public static class WavePlaytestMenu
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string PortalDataPath = "Assets/03.SO/Buildings/PortalBuildingData.asset";
        private const string PendingKey = "Defence.WavePlaytest.Pending";
        private const string TargetDayKey = "Defence.WavePlaytest.TargetDay";
        private const int ReadyTimeoutFrames = 300;

        private static int _readyAttempts;
        private static int _targetDay;
        private static bool _waveStarted;

        static WavePlaytestMenu()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;

            if (EditorApplication.isPlaying && SessionState.GetBool(PendingKey, false))
                BeginWaitingForRuntime();
        }

        [MenuItem("Tools/Defence/Playtest/2일차 - 관광객 파티", priority = 201)]
        private static void PlayDay2() => StartScenario(2);

        [MenuItem("Tools/Defence/Playtest/12일차 - 탐욕의 기사", priority = 202)]
        private static void PlayDay12() => StartScenario(12);

        [MenuItem("Tools/Defence/Playtest/현재 웨이브 결과 출력", priority = 220)]
        private static void PrintCurrentResult()
        {
            var wave = WaveManager.Current;
            if (!EditorApplication.isPlaying || wave == null)
            {
                Debug.LogWarning("[Wave Playtest] Play Mode에서 실행 중인 웨이브가 없습니다.");
                return;
            }

            PrintResult(wave, wave.IsWaveRunning ? "진행 중" : "종료");
        }

        private static void StartScenario(int day)
        {
            if (EditorApplication.isPlaying)
            {
                if (WaveManager.Current != null && WaveManager.Current.IsWaveRunning)
                {
                    Debug.LogWarning("[Wave Playtest] 진행 중인 웨이브가 있습니다. Play Mode를 종료한 뒤 다시 실행하세요.");
                    return;
                }

                SessionState.SetInt(TargetDayKey, day);
                SessionState.SetBool(PendingKey, true);
                BeginWaitingForRuntime();
                return;
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                if (activeScene.isDirty)
                {
                    Debug.LogError($"[Wave Playtest] 저장하지 않은 씬이 있어 {ScenePath}을(를) 열 수 없습니다. 먼저 저장하세요.");
                    return;
                }

                EditorSceneManager.OpenScene(ScenePath);
            }

            SessionState.SetInt(TargetDayKey, day);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                RunSaveSystem.EditorSuppressWrites = true;

            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
                BeginWaitingForRuntime();

            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.update -= PrepareWhenReady;
            EditorApplication.update -= MonitorWave;
            RunSaveSystem.EditorSuppressWrites = false;
            _waveStarted = false;
        }

        private static void BeginWaitingForRuntime()
        {
            RunSaveSystem.EditorSuppressWrites = true;
            _targetDay = SessionState.GetInt(TargetDayKey, 0);
            _readyAttempts = 0;
            _waveStarted = false;
            EditorApplication.update -= PrepareWhenReady;
            EditorApplication.update += PrepareWhenReady;
        }

        private static void PrepareWhenReady()
        {
            _readyAttempts++;
            if (_readyAttempts < 3)
                return;

            var day = DayManager.Current;
            var wave = WaveManager.Current;
            var dungeon = DungeonGraphController.Current;
            if (day == null || wave == null || dungeon == null)
            {
                if (_readyAttempts < ReadyTimeoutFrames)
                    return;

                FailPreparation("필수 런타임 매니저를 찾지 못했습니다.");
                return;
            }

            EditorApplication.update -= PrepareWhenReady;
            var portalData = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(PortalDataPath);
            var portalNode = dungeon.EditorPrepareWavePlaytestArena(portalData);
            if (portalNode == null || !wave.HasPortal)
            {
                FailPreparation("임시 포탈 전장을 만들지 못했습니다.");
                return;
            }

            day.RestoreCheckpoint(_targetDay - 1, true);
            var enemyCount = wave.GetPreviewEnemyCount(_targetDay);
            var expectedBoss = _targetDay == 12;
            var actualBoss = wave.IsBossDay(_targetDay);
            var blockedReason = wave.GetWaveStartBlockedReason(_targetDay);
            if (enemyCount <= 0 || expectedBoss != actualBoss || !string.IsNullOrEmpty(blockedReason))
            {
                FailPreparation(
                    $"설정 검증 실패: enemies={enemyCount}, boss={actualBoss}, blocked='{blockedReason}'");
                return;
            }

            Debug.Log(
                $"[Wave Playtest][PASS] {_targetDay}일차 준비 완료 | " +
                $"적 {enemyCount}명 | 보스 {actualBoss} | 실제 세이브 쓰기 차단");

            day.StartWave();
            SessionState.SetBool(PendingKey, false);
            EditorApplication.update -= MonitorWave;
            EditorApplication.update += MonitorWave;
        }

        private static void MonitorWave()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= MonitorWave;
                return;
            }

            var day = DayManager.Current;
            var wave = WaveManager.Current;
            if (day == null || wave == null)
                return;

            if (!_waveStarted && wave.IsWaveRunning)
            {
                _waveStarted = true;
                var dayMatches = day.CurrentDay == _targetDay;
                var bossMatches = wave.IsBossWave == (_targetDay == 12);
                Debug.Log(
                    $"[Wave Playtest][{(dayMatches && bossMatches ? "PASS" : "FAIL")}] " +
                    $"웨이브 시작 | day={day.CurrentDay} | enemies={wave.TotalEnemyCount} | boss={wave.IsBossWave}");
                return;
            }

            if (!_waveStarted || wave.IsWaveRunning)
                return;

            EditorApplication.update -= MonitorWave;
            PrintResult(wave, "종료");
        }

        private static void PrintResult(WaveManager wave, string state)
        {
            var cleared = wave.TotalEnemyCount > 0 && wave.KillCount >= wave.TotalEnemyCount;
            Debug.Log(
                $"[Wave Playtest][{(cleared ? "PASS" : "CHECK")}] {state} | " +
                $"처치 {wave.KillCount}/{wave.TotalEnemyCount} | " +
                $"가한 피해 {wave.WaveDamageDealt} | 받은 피해 {wave.WaveDamageTaken} | " +
                $"함정 피해 {wave.WaveTrapDamage} | 시설 수익 {wave.WaveFacilityGold}");
        }

        private static void FailPreparation(string reason)
        {
            EditorApplication.update -= PrepareWhenReady;
            SessionState.SetBool(PendingKey, false);
            Debug.LogError($"[Wave Playtest][FAIL] {reason}");
        }
    }
}
