using Code.MapCreateSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 방 공사: 공사 중인 방은 막힌 길이고, 취소하면 그래프에서 흔적 없이 빠진다.
    ///
    /// 공사 중인 방이 길로 잡히면 파티가 짓고 있는 방을 지나가고, 취소한 방의 연결이 남으면
    /// 이웃 방의 연결 수가 줄어든 채로 굳어 그 자리에 다시 확장할 수 없게 된다.
    /// </summary>
    public class RoomConstructionTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                Object.DestroyImmediate(_host);
        }

        [Test]
        public void RemoveNode_FreesThePositionAndTheNeighboursPort()
        {
            var graph = new DungeonGraph();
            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            var room = graph.AddNode(DungeonNodeType.Corridor, Vector2Int.right);
            graph.Connect(entrance, room);
            var portsWhileConnected = entrance.FreePorts;

            Assert.That(graph.RemoveNode(room), Is.True);

            Assert.That(graph.IsOccupied(Vector2Int.right), Is.False);
            Assert.That(graph.GetNode(room.Id), Is.Null);
            Assert.That(graph.Nodes, Has.No.Member(room));
            Assert.That(entrance.ConnectedNodeIds, Has.No.Member(room.Id));
            Assert.That(entrance.FreePorts, Is.EqualTo(portsWhileConnected + 1));
        }

        [Test]
        public void RemoveNode_LetsTheSamePositionBeBuiltAgain()
        {
            var graph = new DungeonGraph();
            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            var room = graph.AddNode(DungeonNodeType.Corridor, Vector2Int.right);
            graph.Connect(entrance, room);
            graph.RemoveNode(room);

            var rebuilt = graph.AddNode(DungeonNodeType.Corridor, Vector2Int.right);

            Assert.That(rebuilt, Is.Not.Null);
            Assert.That(graph.Connect(entrance, rebuilt), Is.True);
        }

        [Test]
        public void Construction_BlocksPassageAndPlacement()
        {
            var node = CreateNode();

            node.BeginConstruction(4f, 10);

            Assert.That(node.IsUnderConstruction, Is.True);
            Assert.That(node.IsPassBlocked, Is.True);
            Assert.That(node.CanAcceptAdditionalUnit, Is.False);
            Assert.That(node.ConstructionRemaining, Is.EqualTo(4f));
            Assert.That(node.ConstructionPaidGold, Is.EqualTo(10));
        }

        [Test]
        public void Construction_WithZeroSecondsDoesNothing()
        {
            var node = CreateNode();

            node.BeginConstruction(0f, 10);

            Assert.That(node.IsUnderConstruction, Is.False);
            Assert.That(node.IsPassBlocked, Is.False);
        }

        // Node는 격자·전장 컴포넌트를 필수로 요구하고 표시용 글자도 프리팹에 연결돼 있어, 실제 프리팹을 쓴다.
        private Node CreateNode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Node>("Assets/GameModules/Prefabs/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null, "노드 프리팹을 찾지 못했습니다.");
            var node = Object.Instantiate(prefab);
            _host = node.gameObject;
            return node;
        }
    }
}
