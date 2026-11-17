using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Intrusion
{
    /// <summary>
    /// 침입자가 무엇을 노리는지, 그리고 저장 파일이 왕복해도 값이 남는지.
    ///
    /// 이 둘은 원래 asmdef 밖에 있어 게임 타입을 직접 참조했고, 그래서 컴파일은 되는데
    /// 테스트 러너가 발견하지 못했다 — 있는 줄 알았지만 한 번도 돌지 않은 커버리지였다.
    /// 다른 테스트들과 같은 리플렉션 방식으로 옮겨 실제로 돌게 한다.
    /// </summary>
    public class IntrusionAndSaveTests
    {
        /// <summary>일차 뒤에 등급이 복원되므로 마지막 조각까지 끝나기 전에 미리보기를 알리면 이전 등급이 화면에 남는다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void RunRestore_PublishesPreviewOnlyAfterSuccessfulCompletion(bool failLastSection)
        {
            GameObject host = new GameObject("RestorePreviewTest");
            Code.Manager.DayManager previousDay = Code.Manager.DayManager.Current;
            bool previousIncomplete = Code.Persistence.RunSaveSystem.RestoreIncomplete;
            try
            {
                Code.Manager.DayManager day = host.AddComponent<Code.Manager.DayManager>();
                SetStaticProperty(typeof(Code.Manager.DayManager), "Current", day);
                SetStaticProperty(typeof(Code.Persistence.RunSaveSystem), "RestoreIncomplete", false);
                Code.Persistence.SaveAgentRegistry registry = host.AddComponent<Code.Persistence.SaveAgentRegistry>();
                int previewCount = 0;
                int lastSectionValue = 0;
                day.DayPreviewChanged += nextDay =>
                {
                    previewCount++;
                    Assert.That(nextDay, Is.EqualTo(3));
                    Assert.That(lastSectionValue, Is.EqualTo(60), "마지막 저장 섹션이 복원되기 전에 알리면 안 됩니다.");
                };
                Code.Persistence.RunSaveFile file = ConfigureRestore(registry,
                    new RestoreProbe("day", () => day.RestoreCheckpoint(2)),
                    new RestoreProbe("last", () =>
                    {
                        if (failLastSection)
                            throw new InvalidOperationException("expected restore failure");
                        lastSectionValue = 60;
                    }));
                if (failLastSection)
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "'last' 복원 실패: expected restore failure");

                bool restored = (bool)CallStatic(typeof(Code.Persistence.RunSaveSystem), "RestoreLoadedRun", registry, file);

                Assert.That(restored, Is.EqualTo(!failLastSection));
                Assert.That(previewCount, Is.EqualTo(failLastSection ? 0 : 1));
                Assert.That(Code.Persistence.RunSaveSystem.RestoreIncomplete, Is.EqualTo(failLastSection));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                SetStaticProperty(typeof(Code.Manager.DayManager), "Current", previousDay);
                SetStaticProperty(typeof(Code.Persistence.RunSaveSystem), "RestoreIncomplete", previousIncomplete);
            }
        }

        /// <summary>복원 뒤 준비 카드가 첫날·입구 로딩 상태에 머물러 영업 버튼이 잠겼던 실제 표시 경로를 지킨다.</summary>
        [Test]
        public void RunRestore_RefreshesWaveHudAndUnsubscribesWhenDisabled()
        {
            GameObject host = new GameObject("RestoredWaveHudTest");
            Code.Manager.DayManager previousDay = Code.Manager.DayManager.Current;
            bool previousIncomplete = Code.Persistence.RunSaveSystem.RestoreIncomplete;
            Code.UI.WaveView view = null;
            try
            {
                Code.Manager.DayManager day = host.AddComponent<Code.Manager.DayManager>();
                Code.Manager.WaveManager wave = host.AddComponent<Code.Manager.WaveManager>();
                Code.Manager.DungeonGradeManager grade = host.AddComponent<Code.Manager.DungeonGradeManager>();
                Code.Persistence.SaveAgentRegistry registry = host.AddComponent<Code.Persistence.SaveAgentRegistry>();
                view = host.AddComponent<Code.UI.WaveView>();
                SetStaticProperty(typeof(Code.Manager.DayManager), "Current", day);
                SetStaticProperty(typeof(Code.Persistence.RunSaveSystem), "RestoreIncomplete", false);
                SetInstanceField(wave, "waveConfig", AssetDatabase.LoadAssetAtPath<Code.Manager.WaveConfigSO>("Assets/07.SO/WaveConfig.asset"));
                SetInstanceField(wave, "_dungeonGrade", grade);
                SetInstanceField(view, "_dungeonGrade", grade);
                SetInstanceField(view, "dayManager", day);
                SetInstanceField(view, "waveManager", wave);
                InvokeInstance(view, "OnEnable");
                InvokeInstance(view, "Start");
                Assert.That(view.Model.PreparationTitle, Is.EqualTo("DAY 1 · 영업 준비"));
                Assert.That(view.Model.StartEnabled, Is.False);

                Code.Persistence.RunSaveFile file = ConfigureRestore(registry,
                    new RestoreProbe("day", () =>
                    {
                        GameObject entrance = CreateNode("RestoredEntrance", "Entrance", new Vector2Int(2000, 0));
                        entrance.transform.SetParent(host.transform);
                        GameObject interior = CreateNode("RestoredRoom", "Corridor", new Vector2Int(2001, 0));
                        interior.transform.SetParent(host.transform);
                        day.RestoreCheckpoint(2);
                    }),
                    new RestoreProbe("grade", () => grade.RestoreTotalKills(60)));
                int modelChanges = 0;
                view.ModelChanged += _ => modelChanges++;

                Assert.That((bool)CallStatic(typeof(Code.Persistence.RunSaveSystem), "RestoreLoadedRun", registry, file), Is.True);

                Assert.That(wave.CanStartWave(3), Is.True, "복원한 입구·방·웨이브 데이터가 시작 조건을 충족해야 합니다.");
                Assert.That(view.Model.PreparationTitle, Is.EqualTo("DAY 3 · 영업 준비"));
                Assert.That(view.Model.StartLabel, Is.EqualTo("영업 시작"));
                Assert.That(view.Model.StartEnabled, Is.True);
                Assert.That(view.Model.PreparationBody, Does.Contain($"던전 등급 {grade.Grade}"));
                Assert.That(modelChanges, Is.EqualTo(1), "복원 완료 후 한 번만 화면에 알려야 합니다.");
                InvokeInstance(view, "OnDisable");
                day.ShowNextWaveDay(0f);
                Assert.That(modelChanges, Is.EqualTo(1), "꺼진 화면의 이벤트 구독은 해제돼야 합니다.");
            }
            finally
            {
                if (view != null)
                    InvokeInstance(view, "OnDisable");
                UnityEngine.Object.DestroyImmediate(host);
                SetStaticProperty(typeof(Code.Manager.DayManager), "Current", previousDay);
                SetStaticProperty(typeof(Code.Persistence.RunSaveSystem), "RestoreIncomplete", previousIncomplete);
            }
        }

        private sealed class RestoreProbe : Code.Persistence.ISaveable
        {
            private readonly Action _restore;
            public string SaveKey { get; }
            public RestoreProbe(string key, Action restore)
            {
                SaveKey = key;
                _restore = restore;
            }
            public string GetSaveData() => "{}";
            public void RestoreData(string savedData) => _restore();
        }

        private static Code.Persistence.RunSaveFile ConfigureRestore(Code.Persistence.SaveAgentRegistry registry, params RestoreProbe[] probes)
        {
            FieldInfo field = typeof(Code.Persistence.SaveAgentRegistry).GetField("_agents", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "SaveAgentRegistry._agents를 찾지 못했습니다.");
            System.Collections.Generic.List<Code.Persistence.ISaveable> agents = (System.Collections.Generic.List<Code.Persistence.ISaveable>)field.GetValue(registry);
            Code.Persistence.RunSaveFile file = new Code.Persistence.RunSaveFile { completedDay = 2 };
            foreach (RestoreProbe probe in probes)
            {
                agents.Add(probe);
                file.entries.Add(new Code.Persistence.RunSaveEntry { key = probe.SaveKey, json = "{}" });
            }
            return file;
        }

        private static void SetStaticProperty(Type type, string name, object value)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{type.Name}.{name}을 찾지 못했습니다.");
            property.SetValue(null, value);
        }

        private static void SetInstanceField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name}을 찾지 못했습니다.");
            field.SetValue(target, value);
        }

        private static void InvokeInstance(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{name}을 찾지 못했습니다.");
            method.Invoke(target, null);
        }

        private static Type Resolve(string fullName)
        {
            var type = Type.GetType($"{fullName}, DungeonKeeper.Runtime");
            Assert.That(type, Is.Not.Null, $"{fullName} 타입을 찾지 못했습니다.");
            return type;
        }

        private static object CallStatic(Type type, string method, params object[] args)
        {
            var m = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"{type.Name}.{method} 정적 메서드를 찾지 못했습니다.");
            return m.Invoke(null, args);
        }

        /// <summary>노드 프리팹을 하나 세우고 지정한 종류로 연다.</summary>
        private static GameObject CreateNode(string name, string nodeTypeName, Vector2Int position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03.Prefabs/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null, "Node 프리팹을 찾지 못했습니다.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;

            var nodeType = Resolve("Code.MapCreateSystem.DungeonNodeType");
            var dungeonNode = Resolve("Code.MapCreateSystem.DungeonNode");
            var data = Activator.CreateInstance(
                dungeonNode,
                Enum.Parse(nodeType, nodeTypeName),
                position,
                4);

            var view = instance.GetComponent(Resolve("Code.MapCreateSystem.Node"));
            var initialize = view.GetType().GetMethod("Initialize", new[] { dungeonNode, typeof(float) });
            Assert.That(initialize, Is.Not.Null, "Node.Initialize를 찾지 못했습니다.");
            initialize.Invoke(view, new[] { data, (object)1f });

            return instance;
        }

        /// <summary>FindPriorityTarget(from, out kind)을 부르고 (노드, 종류 이름)을 돌려준다.</summary>
        private static (Component target, string kind) FindPriorityTarget(Vector2 from)
        {
            var threat = Resolve("Code.Manager.IntrusionThreat");
            var method = threat.GetMethod("FindPriorityTarget", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "IntrusionThreat.FindPriorityTarget을 찾지 못했습니다.");

            var args = new object[] { from, null };
            var target = (Component)method.Invoke(null, args);
            return (target, args[1]?.ToString());
        }

        [Test]
        public void 금고가_없으면_가장_안쪽_방을_노린다()
        {
            var entrance = CreateNode("Entrance", "Entrance", Vector2Int.zero);
            var other = CreateNode("Other", "Corridor", Vector2Int.left);
            try
            {
                var (target, kind) = FindPriorityTarget(other.transform.position);

                Assert.That(target, Is.Not.Null, "노릴 곳을 하나도 찾지 못했습니다.");
                Assert.That(target.gameObject, Is.SameAs(other));
                Assert.That(kind, Is.EqualTo("DungeonCore"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(entrance);
            }
        }

        [Test]
        public void 금고형_방이_있으면_핵심부보다_먼저_노린다()
        {
            var entrance = CreateNode("Entrance", "Entrance", Vector2Int.zero);
            var treasury = CreateNode("Treasury", "Treasury", Vector2Int.left);
            try
            {
                var (target, kind) = FindPriorityTarget(Vector2.zero);

                Assert.That(target, Is.Not.Null, "노릴 곳을 하나도 찾지 못했습니다.");
                Assert.That(target.gameObject, Is.SameAs(treasury), "금고형 방을 두고 핵심부로 갔습니다.");
                Assert.That(kind, Is.EqualTo("Treasury"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(treasury);
                UnityEngine.Object.DestroyImmediate(entrance);
            }
        }

        [Test]
        public void 저장_파일은_조각을_열쇠로_찾아_되돌린다()
        {
            var fileType = Resolve("Code.Persistence.RunSaveFile");
            var entryType = Resolve("Code.Persistence.RunSaveEntry");

            var file = Activator.CreateInstance(fileType);
            fileType.GetField("completedDay").SetValue(file, 20);
            fileType.GetField("savedAtUtc").SetValue(file, "2026-08-31T00:00:00.0000000Z");

            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("key").SetValue(entry, "cost.state");
            entryType.GetField("json").SetValue(entry, "{\"gold\":73,\"debt\":12}");

            var entries = fileType.GetField("entries").GetValue(file);
            entries.GetType().GetMethod("Add").Invoke(entries, new[] { entry });

            var json = JsonUtility.ToJson(file);
            var restored = JsonUtility.FromJson(json, fileType);

            var version = (int)fileType.GetField("version").GetValue(restored);
            var currentVersion = (int)fileType.GetField("CurrentVersion").GetValue(null);
            Assert.That(version, Is.EqualTo(currentVersion), "판올림된 파일을 그대로 읽었습니다.");
            Assert.That(fileType.GetField("completedDay").GetValue(restored), Is.EqualTo(20));

            var found = fileType.GetMethod("Find").Invoke(restored, new object[] { "cost.state" });
            Assert.That(found, Is.EqualTo("{\"gold\":73,\"debt\":12}"), "열쇠로 조각을 되찾지 못했습니다.");

            var missing = fileType.GetMethod("Find").Invoke(restored, new object[] { "없는열쇠" });
            Assert.That(missing, Is.Empty, "없는 열쇠에 빈 문자열이 아닌 것이 돌아왔습니다.");
        }

        /// <summary>
        /// 판 시드(사용자 결정: 판 시드 + 시스템별 난수). 같은 시드·같은 자리면 같은 값이라 불러와도 방 후보·사건
        /// 순서·파티가 다시 뽑히지 않는다. 옛 저장(시드 0)은 새로 뽑는다.
        /// </summary>
        [Test]
        public void RunSeed_GivesTheSameRollsForTheSameRun()
        {
            var previous = Code.Manager.RunRandom.Seed;
            try
            {
                Code.Manager.RunRandom.Restore(12345);
                var a = Code.Manager.RunRandom.Value(Code.Manager.RunRandomStream.RoomSize, -2, 1);
                var waveFirst = Code.Manager.RunRandom.Create(Code.Manager.RunRandomStream.Wave, 9).Next();

                Code.Manager.RunRandom.BeginNewRun(999);
                Assert.That(Code.Manager.RunRandom.Value(Code.Manager.RunRandomStream.RoomSize, -2, 1), Is.Not.EqualTo(a),
                    "다른 판이면 다른 값이 나와야 합니다.");

                Code.Manager.RunRandom.Restore(12345);
                Assert.That(Code.Manager.RunRandom.Value(Code.Manager.RunRandomStream.RoomSize, -2, 1), Is.EqualTo(a));
                Assert.That(Code.Manager.RunRandom.Create(Code.Manager.RunRandomStream.Wave, 9).Next(), Is.EqualTo(waveFirst));
                Assert.That(Code.Manager.RunRandom.Value(Code.Manager.RunRandomStream.RoomSize, -2, 2), Is.Not.EqualTo(a),
                    "자리가 다르면 다른 값입니다.");
                Assert.That(a, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));

                Code.Manager.RunRandom.Restore(0);
                Assert.That(Code.Manager.RunRandom.Seed, Is.Not.Zero, "옛 저장은 새 시드를 받습니다.");
            }
            finally
            {
                Code.Manager.RunRandom.Restore(previous);
            }
        }
    }
}
