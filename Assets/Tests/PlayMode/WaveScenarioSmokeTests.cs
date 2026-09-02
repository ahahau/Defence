using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Gameplay
{
    /// <summary>
    /// 실제 SampleScene의 매니저·이벤트 연결을 통과해 대표 웨이브가 시작되는지 검증한다.
    /// 프로덕션 어셈블리가 asmdef로 분리되어 있지 않아 경계 접근은 기존 EditMode 테스트처럼 리플렉션을 쓴다.
    /// </summary>
    public sealed class WaveScenarioSmokeTests
    {
        private const string GameplaySceneName = "SampleScene";
        private const string SafeSceneName = "Start";
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private Type _saveSystemType;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveSystemType = RequireType("_01.Code.Persistence.RunSaveSystem");
            SetSaveSuppressed(true);
            Time.timeScale = 1f;

            yield return LoadScene(GameplaySceneName);
            // DungeonGraphController.Start가 체크포인트 복원을 끝낸 뒤 임시 전장을 만든다.
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return LoadScene(SafeSceneName);
            SetSaveSuppressed(false);
        }

        [UnityTest]
        public IEnumerator Day2_TouristParty_StartsAsRegularWave()
        {
            yield return StartScenario(2);

            var day = Current("_01.Code.Manager.DayManager");
            var wave = Current("_01.Code.Manager.WaveManager");
            Assert.That(Read<int>(day, "CurrentDay"), Is.EqualTo(2));
            Assert.That(Read<bool>(wave, "IsWaveRunning"), Is.True);
            Assert.That(Read<int>(wave, "TotalEnemyCount"), Is.EqualTo(4));
            Assert.That(Read<bool>(wave, "IsBossWave"), Is.False);
            Assert.That(Read<int>(wave, "ActiveEnemyCount"), Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Day12_GreedKnight_StartsAsBossWave()
        {
            yield return StartScenario(12);

            var day = Current("_01.Code.Manager.DayManager");
            var wave = Current("_01.Code.Manager.WaveManager");
            Assert.That(Read<int>(day, "CurrentDay"), Is.EqualTo(12));
            Assert.That(Read<bool>(wave, "IsWaveRunning"), Is.True);
            Assert.That(Read<int>(wave, "TotalEnemyCount"), Is.EqualTo(8));
            Assert.That(Read<bool>(wave, "IsBossWave"), Is.True);
            Assert.That(Read<int>(wave, "ActiveEnemyCount"), Is.GreaterThan(0));
        }

        private static IEnumerator StartScenario(int targetDay)
        {
            var dungeon = Current("_01.Code.MapCreateSystem.DungeonGraphController");
            var day = Current("_01.Code.Manager.DayManager");
            var wave = Current("_01.Code.Manager.WaveManager");
            var portalData = FindLoadedPortalData();
            Assert.That(portalData, Is.Not.Null, "플레이테스트 포탈 데이터가 필요합니다.");

            var arenaMethod = dungeon.GetType().GetMethod("EditorPrepareWavePlaytestArena", InstanceFlags);
            Assert.That(arenaMethod, Is.Not.Null);
            var portalNode = arenaMethod.Invoke(dungeon, new object[] { portalData });
            Assert.That(portalNode, Is.Not.Null, "임시 포탈 전장을 만들지 못했습니다.");
            Assert.That(Read<bool>(wave, "HasPortal"), Is.True);

            Invoke(day, "RestoreCheckpoint", targetDay - 1, true);
            var blocked = Invoke(wave, "GetWaveStartBlockedReason", targetDay) as string;
            Assert.That(blocked, Is.Empty);

            Invoke(day, "StartWave");
            yield return null;
        }

        private static ScriptableObject FindLoadedPortalData()
        {
            foreach (var asset in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (asset == null || asset.GetType().FullName != "_01.Code.Buildings.BuildingDataSO")
                    continue;

                var displayName = asset.GetType().GetProperty("DisplayName", InstanceFlags)?.GetValue(asset) as string;
                if (displayName == "포탈" || asset.name == "PortalBuildingData")
                    return asset;
            }

            return null;
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, $"{sceneName} 씬을 불러오지 못했습니다.");
            while (!operation.isDone)
                yield return null;
        }

        private void SetSaveSuppressed(bool suppressed)
        {
            var property = _saveSystemType?.GetProperty("EditorSuppressWrites", StaticFlags);
            Assert.That(property, Is.Not.Null, "PlayMode 테스트의 실제 세이브 쓰기 차단 장치가 필요합니다.");
            property.SetValue(null, suppressed);
        }

        private static object Current(string typeName)
        {
            var type = RequireType(typeName);
            var current = type.GetProperty("Current", StaticFlags)?.GetValue(null);
            Assert.That(current, Is.Not.Null, typeName + ".Current를 찾지 못했습니다.");
            return current;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            Assert.That(target, Is.Not.Null);
            var method = target.GetType().GetMethod(methodName, InstanceFlags);
            Assert.That(method, Is.Not.Null, methodName + " 메서드를 찾지 못했습니다.");
            return method.Invoke(target, arguments);
        }

        private static T Read<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            var property = target.GetType().GetProperty(propertyName, InstanceFlags);
            Assert.That(property, Is.Not.Null, propertyName + " 프로퍼티를 찾지 못했습니다.");
            return (T)property.GetValue(target);
        }

        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName + " 타입을 찾지 못했습니다.");
            return type;
        }
    }
}
