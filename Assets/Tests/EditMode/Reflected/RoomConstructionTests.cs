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

        /// <summary>방 크기 수치(사용자 결정): 소 ×0.5·3초·2명·70%, 중 기본값·3명·50%, 대 ×2·8초·4명·30%·등급 2.</summary>
        [Test]
        public void RoomSize_ScalesCostTimeCapacityAndRefund()
        {
            Assert.That(RoomSizeRules.BuildCost(8, RoomSize.Small), Is.EqualTo(4));
            Assert.That(RoomSizeRules.BuildCost(8, RoomSize.Medium), Is.EqualTo(8));
            Assert.That(RoomSizeRules.BuildCost(8, RoomSize.Large), Is.EqualTo(16));
            Assert.That(RoomSizeRules.BuildCost(1, RoomSize.Small), Is.EqualTo(1), "확장비는 최소 1G입니다.");

            Assert.That(RoomSizeRules.ConstructionSeconds(RoomSize.Small, 4f), Is.EqualTo(3f));
            Assert.That(RoomSizeRules.ConstructionSeconds(RoomSize.Medium, 4f), Is.EqualTo(4f), "중형은 씬의 기본 공사 시간을 씁니다.");
            Assert.That(RoomSizeRules.ConstructionSeconds(RoomSize.Large, 4f), Is.EqualTo(8f));

            Assert.That(RoomSizeRules.TeamCapacity(RoomSize.Small), Is.EqualTo(2));
            Assert.That(RoomSizeRules.TeamCapacity(RoomSize.Medium), Is.EqualTo(3));
            Assert.That(RoomSizeRules.TeamCapacity(RoomSize.Large), Is.EqualTo(4));

            Assert.That(RoomSizeRules.CancelRefundRate(RoomSize.Small), Is.EqualTo(0.7f));
            Assert.That(RoomSizeRules.CancelRefundRate(RoomSize.Large), Is.EqualTo(0.3f));

            Assert.That(RoomSizeRules.AllowsFacility(RoomSize.Small), Is.False);
            Assert.That(RoomSizeRules.AllowsFacility(RoomSize.Medium), Is.True);
            Assert.That(RoomSizeRules.MeetsGrade(RoomSize.Large, 1), Is.False);
            Assert.That(RoomSizeRules.MeetsGrade(RoomSize.Large, 2), Is.True);
            Assert.That(RoomSizeRules.CandidateLabel(RoomSize.Large, 16, 8f, 0), Does.Contain("등급 2 필요"));
            Assert.That(RoomSizeRules.CandidateLabel(RoomSize.Small, 4, 3f, 0), Is.EqualTo("소형\n4G · 3초"));
        }

        /// <summary>등급이 오를수록 대형 후보가 늘고 소형이 준다. 등급 0: 소 40% · 중 50% · 대 10%.</summary>
        [Test]
        public void RoomSize_RollFavorsLargeRoomsAtHigherGrades()
        {
            Assert.That(RoomSizeRules.Roll(0.39f, 0), Is.EqualTo(RoomSize.Small));
            Assert.That(RoomSizeRules.Roll(0.41f, 0), Is.EqualTo(RoomSize.Medium));
            Assert.That(RoomSizeRules.Roll(0.89f, 0), Is.EqualTo(RoomSize.Medium));
            Assert.That(RoomSizeRules.Roll(0.91f, 0), Is.EqualTo(RoomSize.Large));

            Assert.That(RoomSizeRules.Roll(0.75f, 6), Is.EqualTo(RoomSize.Large), "등급 6이면 대형이 40%입니다.");
            Assert.That(RoomSizeRules.Roll(0.21f, 10), Is.EqualTo(RoomSize.Medium), "소형은 20% 아래로 줄지 않습니다.");
        }

        /// <summary>크기는 방의 팀당 정원(유닛·모험가 모두)을 바꾸고, 소형 방에는 시설을 못 짓는다.</summary>
        [Test]
        public void RoomSize_SetsTheBattlefieldCapacityAndBlocksFacilitiesInSmallRooms()
        {
            var node = CreateNode();
            Assert.That(node.Size, Is.EqualTo(RoomSize.Medium), "처음 방과 옛 저장의 방은 중형입니다.");
            node.SetRoomSize(RoomSize.Medium);
            Assert.That(node.UnitCapacity, Is.EqualTo(3), "중형은 지금까지와 같은 팀당 3명입니다.");

            node.SetRoomSize(RoomSize.Large);
            Assert.That(node.UnitCapacity, Is.EqualTo(4));

            node.SetRoomSize(RoomSize.Small);
            Assert.That(node.UnitCapacity, Is.EqualTo(2));

            var store = AssetDatabase.LoadAssetAtPath<Code.Buildings.BuildingDataSO>("Assets/GameModules/Data/Buildings/StoreBuildingData.asset");
            Assert.That(store, Is.Not.Null, "상점 데이터를 찾지 못했습니다.");
            Assert.That(Code.Buildings.BuildingPlacement.ViolatesRoomSizeRule(node, store), Is.True, "소형 방에는 상점을 못 짓습니다.");

            node.SetRoomSize(RoomSize.Medium);
            Assert.That(Code.Buildings.BuildingPlacement.ViolatesRoomSizeRule(node, store), Is.False);
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
