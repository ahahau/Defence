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
        private static Type Resolve(string fullName)
        {
            var type = Type.GetType($"{fullName}, Assembly-CSharp");
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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04.Prefab/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null, "Node 프리팹을 찾지 못했습니다.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;

            var nodeType = Resolve("_01.Code.MapCreateSystem.DungeonNodeType");
            var dungeonNode = Resolve("_01.Code.MapCreateSystem.DungeonNode");
            var data = Activator.CreateInstance(
                dungeonNode,
                Enum.Parse(nodeType, nodeTypeName),
                position,
                4);

            var view = instance.GetComponent(Resolve("_01.Code.MapCreateSystem.Node"));
            var initialize = view.GetType().GetMethod("Initialize", new[] { dungeonNode, typeof(float) });
            Assert.That(initialize, Is.Not.Null, "Node.Initialize를 찾지 못했습니다.");
            initialize.Invoke(view, new[] { data, (object)1f });

            return instance;
        }

        /// <summary>FindPriorityTarget(from, out kind)을 부르고 (노드, 종류 이름)을 돌려준다.</summary>
        private static (Component target, string kind) FindPriorityTarget(Vector2 from)
        {
            var threat = Resolve("_01.Code.Manager.IntrusionThreat");
            var method = threat.GetMethod("FindPriorityTarget", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "IntrusionThreat.FindPriorityTarget을 찾지 못했습니다.");

            var args = new object[] { from, null };
            var target = (Component)method.Invoke(null, args);
            return (target, args[1]?.ToString());
        }

        [Test]
        public void 금고가_없으면_던전_핵심부를_노린다()
        {
            var entrance = CreateNode("Entrance", "Entrance", Vector2Int.zero);
            var other = CreateNode("Other", "Corridor", Vector2Int.left);
            try
            {
                var (target, kind) = FindPriorityTarget(other.transform.position);

                Assert.That(target, Is.Not.Null, "노릴 곳을 하나도 찾지 못했습니다.");
                Assert.That(target.gameObject, Is.SameAs(entrance));
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
            var fileType = Resolve("_01.Code.Persistence.RunSaveFile");
            var entryType = Resolve("_01.Code.Persistence.RunSaveEntry");

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
    }
}
