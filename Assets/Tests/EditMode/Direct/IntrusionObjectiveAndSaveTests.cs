using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using _01.Code.Persistence;
using _01.Code.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Defence.EditMode.Tests
{
    public sealed class IntrusionObjectiveAndSaveTests
    {
        [Test]
        public void NoTreasury_FallsBackToEntranceCore()
        {
            var entranceObject = CreateNodeObject("Entrance");
            var otherObject = CreateNodeObject("Other");
            try
            {
                var entrance = entranceObject.GetComponent<Node>();
                entrance.Initialize(new DungeonNode(DungeonNodeType.Entrance, Vector2Int.zero, 4), 1f);
                var other = otherObject.GetComponent<Node>();
                other.Initialize(new DungeonNode(DungeonNodeType.Corridor, Vector2Int.left, 4), 1f);

                var target = IntrusionThreat.FindPriorityTarget(other.transform.position, out var kind);

                Assert.That(target, Is.SameAs(entrance));
                Assert.That(kind, Is.EqualTo(IntrusionThreat.ObjectiveKind.DungeonCore));
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
                Object.DestroyImmediate(entranceObject);
            }
        }

        [Test]
        public void TreasuryNode_TakesPriorityOverDungeonCore()
        {
            var entranceObject = CreateNodeObject("Entrance");
            var treasuryObject = CreateNodeObject("Treasury");
            try
            {
                var entrance = entranceObject.GetComponent<Node>();
                entrance.Initialize(new DungeonNode(DungeonNodeType.Entrance, Vector2Int.zero, 4), 1f);
                var treasury = treasuryObject.GetComponent<Node>();
                treasury.Initialize(new DungeonNode(DungeonNodeType.Treasury, Vector2Int.left, 4), 1f);

                var target = IntrusionThreat.FindPriorityTarget(Vector2.zero, out var kind);

                Assert.That(target, Is.SameAs(treasury));
                Assert.That(kind, Is.EqualTo(IntrusionThreat.ObjectiveKind.Treasury));
            }
            finally
            {
                Object.DestroyImmediate(treasuryObject);
                Object.DestroyImmediate(entranceObject);
            }
        }

        private static GameObject CreateNodeObject(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04.Prefab/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            return instance;
        }

        [Test]
        public void SaveData_JsonRoundTrip_PreservesVersionedCheckpoint()
        {
            var source = new RunSaveData
            {
                completedDay = 20,
                gold = 73,
                debt = 12,
                morale = 61
            };
            source.nodes.Add(new SavedNode
            {
                type = DungeonNodeType.Entrance,
                x = -2,
                y = 3,
                danger = 9
            });

            var restored = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(source));

            Assert.That(restored.version, Is.EqualTo(RunSaveData.CurrentVersion));
            Assert.That(restored.completedDay, Is.EqualTo(20));
            Assert.That(restored.nodes[0].type, Is.EqualTo(DungeonNodeType.Entrance));
            Assert.That(restored.nodes[0].danger, Is.EqualTo(9));
        }
    }
}
