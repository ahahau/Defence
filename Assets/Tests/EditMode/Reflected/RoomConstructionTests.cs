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

            var store = AssetDatabase.LoadAssetAtPath<Code.Buildings.BuildingDataSO>("Assets/07.SO/Buildings/StoreBuildingData.asset");
            Assert.That(store, Is.Not.Null, "상점 데이터를 찾지 못했습니다.");
            Assert.That(Code.Buildings.BuildingPlacement.ViolatesRoomSizeRule(node, store), Is.True, "소형 방에는 상점을 못 짓습니다.");

            node.SetRoomSize(RoomSize.Medium);
            Assert.That(Code.Buildings.BuildingPlacement.ViolatesRoomSizeRule(node, store), Is.False);
        }

        /// <summary>
        /// 특수 효과 방(사용자 결정: 풍요·요새·함정 지대, 드물고 느리게). 15% 확률, 확장비 ×1.5, 공사 +2초,
        /// 취소 환불 -20%p. 효과 수치가 이 규칙에만 있어야 화면과 실제 값이 갈리지 않는다.
        /// </summary>
        [Test]
        public void RoomFeature_IsRareCostlyAndAppliesItsEffect()
        {
            Assert.That(RoomFeatureRules.Roll(0.15f, 0f), Is.EqualTo(RoomFeature.None));
            Assert.That(RoomFeatureRules.Roll(0.1f, 0.1f), Is.EqualTo(RoomFeature.Bounty));
            Assert.That(RoomFeatureRules.Roll(0.1f, 0.5f), Is.EqualTo(RoomFeature.Fortress));
            Assert.That(RoomFeatureRules.Roll(0.1f, 1f), Is.EqualTo(RoomFeature.TrapGround));

            Assert.That(RoomFeatureRules.BuildCost(8, RoomFeature.None), Is.EqualTo(8));
            Assert.That(RoomFeatureRules.BuildCost(8, RoomFeature.Bounty), Is.EqualTo(12));
            Assert.That(RoomFeatureRules.ConstructionSeconds(4f, RoomFeature.Fortress), Is.EqualTo(6f).Within(0.001f));
            Assert.That(RoomFeatureRules.CancelRefundRate(0.5f, RoomFeature.TrapGround), Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(RoomFeatureRules.CancelRefundRate(0.3f, RoomFeature.None), Is.EqualTo(0.3f).Within(0.001f));

            Assert.That(RoomFeatureRules.ScaleIncome(20, RoomFeature.Bounty), Is.EqualTo(25));
            Assert.That(RoomFeatureRules.ScaleIncome(20, RoomFeature.Fortress), Is.EqualTo(20), "다른 효과는 수입을 건드리지 않습니다.");
            Assert.That(RoomFeatureRules.ScaleUnitDamage(5, RoomFeature.Fortress), Is.EqualTo(4));
            Assert.That(RoomFeatureRules.ScaleUnitDamage(1, RoomFeature.Fortress), Is.EqualTo(1), "1 피해는 0이 되지 않습니다.");
            Assert.That(RoomFeatureRules.ScaleTrapDamage(4, RoomFeature.TrapGround), Is.EqualTo(6));
        }

        /// <summary>
        /// 열기 전에 연결 방향을 보여 준다. 예측이 실제 연결(부모 먼저, 위·오른쪽·아래·왼쪽 순, 양쪽 남은 연결 수)과
        /// 다르면 "보여 준 길"과 "생긴 길"이 갈린다.
        /// </summary>
        [Test]
        public void ConnectionPreview_MatchesTheConnectionRule()
        {
            var ports = new System.Collections.Generic.Dictionary<Vector2Int, int>
            {
                { Vector2Int.right, 2 },
                { Vector2Int.up, 1 },
                { Vector2Int.down, 0 },
            };

            var connections = RoomFeatureRules.PredictConnections(Vector2Int.right, ports, 4);
            Assert.That(connections, Is.EqualTo(new[] { Vector2Int.right, Vector2Int.up }), "남은 연결 수가 0인 방과는 이어지지 않습니다.");
            Assert.That(RoomFeatureRules.ConnectionLabel(connections), Is.EqualTo("연결 ↑→"));

            Assert.That(RoomFeatureRules.PredictConnections(Vector2Int.right, ports, 1), Is.EqualTo(new[] { Vector2Int.right }),
                "새 방의 연결 수가 차면 더 잇지 않습니다.");

            var entrance = new System.Collections.Generic.Dictionary<Vector2Int, int> { { new Vector2Int(1, -1), 4 } };
            Assert.That(RoomFeatureRules.ConnectionLabel(RoomFeatureRules.PredictConnections(new Vector2Int(1, -1), entrance, 4)),
                Is.EqualTo("연결 ↘"), "입구 옆 첫 후보는 입구와 대각선으로 이어집니다.");
        }

        /// <summary>특수 효과는 열린 방에 남고 정원 표시 앞에 이름이 붙는다.</summary>
        [Test]
        public void RoomFeature_StaysOnTheBuiltRoom()
        {
            var node = CreateNode();
            Assert.That(node.Feature, Is.EqualTo(RoomFeature.None), "처음 방과 옛 저장의 방은 효과가 없습니다.");
            node.SetRoomSize(RoomSize.Medium);
            node.SetRoomFeature(RoomFeature.Fortress);
            Assert.That(node.Feature, Is.EqualTo(RoomFeature.Fortress));

            // 테스트 어셈블리는 TextMeshPro를 참조하지 않아 글자는 리플렉션으로 읽는다.
            var field = typeof(Node).GetField("unitCapacityText",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Node.unitCapacityText 를 찾지 못했습니다.");
            var label = field.GetValue(node);
            Assert.That(label, Is.Not.Null, "노드 프리팹에 정원 글자가 연결돼 있어야 합니다.");
            var text = (string)label.GetType().GetProperty("text").GetValue(label);
            Assert.That(text, Does.StartWith("요새 "), "열린 방 정원 표시에 효과 이름이 붙어야 합니다.");
        }

        /// <summary>
        /// 입구와 특별 방 프리팹은 규칙을 지킨다. 특별 방은 어느 부모 쪽 문에도 맞도록 네 변 모두 문이 있고 크기마다 하나 이상 있다.
        /// 특별 방이 부모 쪽 문을 못 맞추면 그 후보는 생성한 방으로 돌아가지만, 크기 하나가 비면 그 크기는 특별 방이 안 나온다.
        /// </summary>
        [Test]
        public void RoomLayouts_EntranceAndSpecialRoomsAreValid()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomLayoutCatalog>("Assets/07.SO/Map/RoomLayoutCatalog.asset");
            Assert.That(catalog, Is.Not.Null, "방 목록이 없습니다. RoomLayoutBuilder.BuildAll()을 돌리세요.");
            Assert.That(catalog.Entrance, Is.Not.Null);
            Assert.That(catalog.Entrance.IsEntrance, Is.True);
            Assert.That(RoomLayoutRules.Validate(catalog.Entrance, out var entranceProblem), Is.True, entranceProblem);
            foreach (var side in new[] { RoomDoors.Up, RoomDoors.Down, RoomDoors.Left })
                Assert.That(catalog.Entrance.HasDoor(side), Is.True, $"입구는 {side} 문으로 첫 후보를 뻗습니다.");

            var ids = new System.Collections.Generic.HashSet<string> { catalog.Entrance.LayoutId };
            foreach (var layout in catalog.Rooms)
            {
                Assert.That(layout, Is.Not.Null);
                Assert.That(RoomLayoutRules.Validate(layout, out var problem), Is.True, problem);
                Assert.That(ids.Add(layout.LayoutId), Is.True, $"방 이름이 겹칩니다: {layout.LayoutId}");
            }

            foreach (RoomSize size in System.Enum.GetValues(typeof(RoomSize)))
            foreach (var side in RoomDoorRules.Sides)
            {
                var picked = catalog.Pick(size, side, 0.5f);
                Assert.That(picked, Is.Not.Null, $"{size} 특별 방이 없습니다.");
                Assert.That(picked.HasDoor(side), Is.True, $"{size} 특별 방에 {side} 문이 없습니다.");
            }
        }

        /// <summary>
        /// 후보마다 시드로 깎는 방은 크기·부모 쪽 문과 상관없이 늘 규칙에 맞고, 같은 시드면 같은 모양이며, 시드마다 모양이 다양하다.
        /// 규칙에 어긋난 방이 나오면 함정·유닛이 막힌 칸에 놓이거나 길이 끊기고, 같은 시드에 다른 모양이 나오면 불러오기마다 지도가 바뀐다.
        /// </summary>
        [Test]
        public void RoomShapeGenerator_MakesValidVariedRoomsForEveryDoor()
        {
            foreach (RoomSize size in System.Enum.GetValues(typeof(RoomSize)))
            {
                var distinct = new System.Collections.Generic.HashSet<string>();
                foreach (var side in RoomDoorRules.Sides)
                {
                    for (var seed = 0; seed < 30; seed++)
                    {
                        var doors = RoomShapeGenerator.RollDoors(side, seed * 7 + 1);
                        Assert.That(RoomDoorRules.Has(doors, side), Is.True, "부모 쪽 문은 반드시 있습니다.");
                        Assert.That(RoomDoorRules.Count(doors), Is.InRange(2, 4));

                        var shape = RoomShapeGenerator.Generate(size, doors, false, seed);
                        Assert.That(RoomLayoutRules.Validate(shape, out var problem), Is.True, problem);
                        Assert.That(RoomShapeGenerator.HasThinFeatures(shape), Is.False, $"{size} 시드 {seed}");
                        Assert.That(RoomLayoutRules.CountFloor(shape),
                            Is.LessThanOrEqualTo(shape.Columns * shape.Rows * RoomShapeGenerator.MaxFillRatio));

                        var again = RoomShapeGenerator.Generate(size, doors, false, seed);
                        Assert.That(again.CopyLines(), Is.EqualTo(shape.CopyLines()), "같은 시드는 같은 모양이어야 합니다.");
                        distinct.Add(string.Join("/", shape.CopyLines()));
                    }
                }

                Assert.That(distinct.Count, Is.GreaterThan(40), $"{size} 방 모양이 너무 적게 나옵니다.");
            }
        }

        /// <summary>
        /// 방은 서로 마주 보는 문으로만 이어진다. 입구의 위·아래 문은 ㄱ자 복도로 왼쪽 위·아래 방의 오른쪽 문에 닿고,
        /// 오른쪽 바깥문으로는 후보를 뻗지 않는다(침입자가 들어오는 곳).
        /// </summary>
        [Test]
        public void RoomDoors_MapEntranceDiagonalsAndOrthogonalNeighbours()
        {
            Assert.That(RoomDoorRules.CandidateOffset(true, RoomDoors.Up), Is.EqualTo(new Vector2Int(-1, 1)));
            Assert.That(RoomDoorRules.CandidateOffset(true, RoomDoors.Down), Is.EqualTo(new Vector2Int(-1, -1)));
            Assert.That(RoomDoorRules.CandidateOffset(true, RoomDoors.Left), Is.EqualTo(Vector2Int.left));
            Assert.That(RoomDoorRules.CandidateOffset(true, RoomDoors.Right), Is.EqualTo(Vector2Int.zero));
            Assert.That(RoomDoorRules.RequiredDoor(true, RoomDoors.Up), Is.EqualTo(RoomDoors.Right));
            Assert.That(RoomDoorRules.RequiredDoor(false, RoomDoors.Up), Is.EqualTo(RoomDoors.Down));

            Assert.That(RoomDoorRules.SideToward(Vector2Int.zero, true, new Vector2Int(-1, 1), false), Is.EqualTo(RoomDoors.Up));
            Assert.That(RoomDoorRules.SideToward(new Vector2Int(-1, -1), false, Vector2Int.zero, true), Is.EqualTo(RoomDoors.Right));
            Assert.That(RoomDoorRules.SideToward(new Vector2Int(-2, 0), false, new Vector2Int(-1, 1), false), Is.EqualTo(RoomDoors.None),
                "입구가 아닌 방끼리는 대각선으로 잇지 않습니다.");
            Assert.That(RoomDoorRules.SideToward(new Vector2Int(-2, 0), false, new Vector2Int(-2, 1), false), Is.EqualTo(RoomDoors.Up));
        }

        /// <summary>
        /// 복도는 두 방이 반씩 깐다. 한쪽 반의 끝 바로 다음 칸이 다른 쪽 반의 첫 칸이어야 한 복도로 이어진다.
        /// 이어지지 않은 문은 막히고, 입구 위 문은 ㄱ자로 꺾이며, 입구 바깥문은 바깥으로 열려 있다.
        /// </summary>
        [Test]
        public void RoomTilePlan_CorridorHalvesMeet()
        {
            var left = NewShape(RoomSize.Medium, RoomDoors.Right | RoomDoors.Up, Rows(6));
            var right = NewShape(RoomSize.Large, RoomDoors.Left | RoomDoors.Down, Rows(8));
            const int spacing = 24;
            var a = RoomTilePlan.Build(left, RoomDoors.Right, RoomDoors.None, spacing);
            var b = RoomTilePlan.Build(right, RoomDoors.Left, RoomDoors.None, spacing);

            Assert.That(a.Floor, Has.Member(new Vector2Int(11, -1)).And.Member(new Vector2Int(11, 0)));
            Assert.That(a.Floor, Has.No.Member(new Vector2Int(12, 0)), "복도 반쪽은 두 방 사이 가운데까지만 깝니다.");
            Assert.That(b.Floor, Has.Member(new Vector2Int(12 - spacing, -1)).And.Member(new Vector2Int(12 - spacing, 0)));
            Assert.That(a.Floor, Has.No.Member(new Vector2Int(-1, 6)), "이어지지 않은 문은 벽으로 막힙니다.");

            var entrance = new RoomShape("Test_Entrance", RoomSize.Medium, true,
                RoomDoors.Up | RoomDoors.Down | RoomDoors.Left | RoomDoors.Right, Rows(6));
            var turning = RoomTilePlan.Build(entrance, RoomDoors.Up, RoomDoorRules.EntranceTurningDoors, spacing);
            Assert.That(turning.Floor, Has.Member(new Vector2Int(0, spacing)).And.Member(new Vector2Int(-12, spacing - 1)),
                "입구 위 문 복도는 다음 줄까지 올라가 서쪽으로 꺾입니다.");
            Assert.That(turning.Floor, Has.Member(new Vector2Int(6, -1)).And.Member(new Vector2Int(8, 0)),
                "입구 바깥문은 바깥으로 열려 있습니다(침입자가 들어오는 곳).");
        }

        /// <summary>
        /// 이어진 옆 문 양쪽에 2×2 기둥이 서서 문틀로 보인다. 옆 벽은 난간 한 줄이라 문 그림이 없어
        /// 모험가가 벽 틈으로 빠져나가는 것처럼 보였다. 기둥은 벽 줄과 그 바깥에만 서고 문 틈·방 바닥은 막지 않는다.
        /// </summary>
        [Test]
        public void RoomTilePlan_SideDoorGetsPostsOnBothSides()
        {
            var shape = NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, Rows(6));
            var plan = RoomTilePlan.Build(shape, RoomDoors.Right, RoomDoors.None, 24);

            // 6열 방의 오른쪽 벽 줄은 타일 x = 6, 문 틈은 y -1·0. 기둥은 가로 2 × 세로 3(머리·몸통·받침).
            Assert.That(plan.Pillars, Has.Member(new Vector2Int(6, 1)).And.Member(new Vector2Int(7, 3)));
            Assert.That(plan.Pillars, Has.Member(new Vector2Int(6, -2)).And.Member(new Vector2Int(7, -4)));
            Assert.That(plan.Corridor, Has.Member(new Vector2Int(6, 0)).And.Member(new Vector2Int(8, -1)),
                "문 틈부터 바깥은 복도로 따로 든다(어둡게 칠함).");
            Assert.That(plan.Corridor, Has.No.Member(new Vector2Int(5, 0)), "방 바닥은 복도가 아닙니다.");
            Assert.That(plan.Pillars, Has.No.Member(new Vector2Int(6, 0)).And.No.Member(new Vector2Int(6, -1)), "문 틈은 열려 있습니다.");
            Assert.That(plan.Floor, Has.Member(new Vector2Int(6, 0)).And.Member(new Vector2Int(6, -1)));
            Assert.That(plan.Pillars, Has.No.Member(new Vector2Int(5, 1)), "방 바닥에는 서지 않습니다.");
            Assert.That(plan.Pillars, Has.No.Member(new Vector2Int(-7, 1)), "이어지지 않은 문에는 기둥이 없습니다.");
        }

        /// <summary>방 안에 갇힌 막힌 칸은 기둥이고, 가장자리와 이어진 막힌 칸(깎인 모서리)은 벽이다.</summary>
        [Test]
        public void RoomTilePlan_EnclosedBlockedCellsBecomePillars()
        {
            var shape = NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, PillarRows);
            var plan = RoomTilePlan.Build(shape, RoomDoors.None, RoomDoors.None, 24);
            // 칸 (2,3)의 타일은 x -2..-1, y 0..1. 칸이 윗면이고 아래 칸 윗줄(y -1)에 받침이 내려온다.
            Assert.That(plan.Pillars, Has.Member(new Vector2Int(-2, 1)).And.Member(new Vector2Int(-1, 0)));
            Assert.That(plan.Pillars, Has.Member(new Vector2Int(-2, -1)).And.Member(new Vector2Int(-1, -1)));
            Assert.That(plan.Floor, Has.No.Member(new Vector2Int(-2, -1)), "받침 자리는 바닥 대신 기둥으로 칠합니다.");
            Assert.That(plan.Pillars, Has.No.Member(new Vector2Int(-2, 2)), "위 칸은 가리지 않습니다.");
            Assert.That(plan.Floor, Has.Member(new Vector2Int(-2, 2)));
            Assert.That(plan.Pillars, Has.No.Member(new Vector2Int(-6, 4)), "깎인 모서리는 기둥이 아니라 벽입니다.");
            Assert.That(plan.Floor, Has.No.Member(new Vector2Int(-6, 4)));
        }

        /// <summary>
        /// 룰 타일이 예제 씬과 같은 3/4 시점 조각을 고른다: 바닥 위 벽돌 면 2줄 + 난간, 옆 세로 난간, 아래 가로 난간, 바깥 모서리,
        /// 방 안 기둥 머리·받침. 두 방을 한 타일맵에 칠하면 복도 위 난간이 이음매 없이 이어진다(끝 조각이 생기지 않는다).
        /// 예전에는 타일이 시트의 엉뚱한 칸(문 자리에 석상, 위쪽 벽에 울타리)을 가리켜 방이 이상하게 보였다.
        /// </summary>
        [Test]
        public void RuleTiles_PickThreeQuarterWallPieces()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
            Assert.That(tileSet, Is.Not.Null, "방 룰 타일 셋이 없습니다. RoomLayoutBuilder.BuildAll()을 돌리세요.");
            Assert.That(tileSet.IsComplete, Is.True);

            var gridHost = new GameObject("TestGrid", typeof(Grid));
            _layoutHosts.Add(gridHost);
            var tiles = new GameObject("Tiles", typeof(UnityEngine.Tilemaps.Tilemap)).GetComponent<UnityEngine.Tilemaps.Tilemap>();
            tiles.transform.SetParent(gridHost.transform, false);

            const int spacing = 24;
            var a = RoomTilePlan.Build(NewShape(RoomSize.Medium, RoomDoors.Right | RoomDoors.Up, Rows(6)), RoomDoors.Right,
                RoomDoors.None, spacing);
            var b = RoomTilePlan.Build(NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Down, PillarRows), RoomDoors.Left,
                RoomDoors.None, spacing);
            RoomTilePainter.Paint(null, tiles, tileSet, new[]
            {
                new RoomTilePainter.Room(a, Vector2Int.zero, Color.white),
                new RoomTilePainter.Room(b, new Vector2Int(spacing, 0), Color.white)
            });

            Assert.That(Cell(tiles, -1, 6), Is.EqualTo((10, 9)), "바닥 바로 위는 벽돌 면 아래 줄입니다.");
            Assert.That(Cell(tiles, -1, 7), Is.EqualTo((10, 8)), "그 위는 벽돌 면 윗줄입니다.");
            Assert.That(Cell(tiles, -1, 8), Is.EqualTo((10, 7)), "그 위는 난간입니다.");
            Assert.That(Cell(tiles, -7, 0), Is.EqualTo((3, 3)), "왼쪽 벽은 세로 난간입니다.");
            Assert.That(Cell(tiles, 0, -7), Is.EqualTo((10, 12)), "아래 벽은 가로 난간입니다.");
            Assert.That(Cell(tiles, -7, -7), Is.EqualTo((3, 5)), "왼쪽 아래 바깥 모서리.");
            Assert.That(Cell(tiles, 11, 3), Is.EqualTo((10, 7)), "복도 위 난간이 이음매에서 끊기면 안 됩니다.");
            Assert.That(Cell(tiles, 12, 3), Is.EqualTo((10, 7)));
            Assert.That(Cell(tiles, 11, -2), Is.EqualTo((10, 12)));
            // 칸 기둥은 칸(타일 2×2)이 윗면이고 아래로 받침 한 줄이다.
            // 윗면은 시트 그림의 검은 속을 돌 색으로 채운 그림이다(방 한가운데서 구멍처럼 보이지 않게).
            Assert.That(SpriteName(tiles, spacing - 2, 1), Is.EqualTo("PillarTopLeft"), "윗면 왼쪽 위.");
            Assert.That(SpriteName(tiles, spacing - 1, 1), Is.EqualTo("PillarTopRight"), "윗면 오른쪽 위.");
            Assert.That(SpriteName(tiles, spacing - 2, 0), Is.EqualTo("PillarTopLowerLeft"), "윗면 왼쪽 아래.");
            Assert.That(SpriteName(tiles, spacing - 1, 0), Is.EqualTo("PillarTopLowerRight"), "윗면 오른쪽 아래.");
            Assert.That(Cell(tiles, spacing - 2, -1), Is.EqualTo((7, 2)), "기둥 왼쪽 받침.");
            Assert.That(Cell(tiles, spacing - 1, -1), Is.EqualTo((9, 2)), "기둥 오른쪽 받침.");
            Assert.That(Cell(tiles, spacing - 2, 2).row, Is.InRange(17, 18), "위 칸은 바닥 그대로입니다.");
            Assert.That(Cell(tiles, 0, 0).row, Is.InRange(17, 18), "바닥은 바닥 무늬 칸입니다.");
        }

        /// <summary>기둥·빈 공간 칸에는 함정도 유닛도 놓을 수 없다. 칸 수는 방 모양을 따른다.</summary>
        [Test]
        public void TrapGrid_FollowsLayoutSizeAndRejectsBlockedCells()
        {
            var shape = NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, new[]
            {
                "......",
                ".#....",
                "......",
                "......",
                "......",
                "......"
            });

            var node = CreateNode();
            node.ApplyLayout(shape, 15f);
            var grid = node.TrapGrid;
            Assert.That(grid.Columns, Is.EqualTo(6));
            Assert.That(grid.CellSize, Is.EqualTo(RoomLayoutRules.CellWorldSize).Within(0.0001f));
            Assert.That(grid.IsBlockedCell(1, 4), Is.True);
            Assert.That(grid.IsCellFree(1, 4), Is.False);
            Assert.That(grid.CanPlace(0, 4, new Vector2Int(2, 1)), Is.False, "여러 칸 함정이 기둥을 덮으면 안 됩니다.");
            Assert.That(grid.IsCellFree(0, 4), Is.True);
            Assert.That(node.IsUnitCellAvailable(1, 4), Is.False);

            var wide = NewShape(RoomSize.Large, RoomDoors.Left | RoomDoors.Right, new[]
            {
                ".........", ".........", ".........", ".........", ".........", "........."
            });
            var wideNode = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Node>("Assets/03.Prefabs/Map/Node.prefab"));
            try
            {
                wideNode.ApplyLayout(wide, 15f);
                Assert.That(wideNode.TrapGrid.Columns, Is.EqualTo(9), "정사각이 아닌 방도 칸 수를 그대로 따릅니다.");
                Assert.That(wideNode.TrapGrid.Rows, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(wideNode.gameObject);
            }
        }

        /// <summary>
        /// 방 안 칸 길은 벽을 돌아가고 모서리를 가로지르지 않는다. 가로지르면 모험가가 기둥 모서리를 뚫고 지나가 보인다.
        /// 칸 하나를 더 막아 방이 끊기는지도 같은 판정으로 본다(칸막이 벽 배치).
        /// </summary>
        [Test]
        public void RoomCellPathfinder_GoesAroundWallsWithoutCuttingCorners()
        {
            var map = new ShapeMap(NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, new[]
            {
                ".....",
                "..#..",
                "..#.."
            }));
            var path = new System.Collections.Generic.List<Vector2Int>();
            Assert.That(RoomCellPathfinder.TryFindPath(map, new Vector2Int(0, 0), new Vector2Int(4, 0), path), Is.True);
            Assert.That(path, Has.Member(new Vector2Int(2, 2)), "벽 위 줄로 돌아가야 합니다.");
            for (var i = 1; i < path.Count; i++)
                Assert.That(RoomCellPathfinder.CanStep(map, path[i - 1], path[i]), Is.True, $"{path[i - 1]} → {path[i]}");

            Assert.That(RoomCellPathfinder.CanStep(map, new Vector2Int(1, 0), new Vector2Int(2, 1)), Is.False,
                "벽 모서리를 대각선으로 가로지르면 안 됩니다.");
            Assert.That(RoomCellPathfinder.StaysConnectedWithout(map, new Vector2Int(2, 2)), Is.False, "유일한 길을 막으면 끊깁니다.");
            Assert.That(RoomCellPathfinder.StaysConnectedWithout(map, new Vector2Int(0, 2)), Is.True);
        }

        /// <summary>
        /// 칸막이 벽은 문 앞과 방을 끊는 자리에 못 놓고, 놓으면 걸을 수 없는 칸이 되어 기둥처럼 그려진다.
        /// 방을 끊는 벽을 허용하면 유닛·함정 칸에 아무도 못 가거나 파티가 방에 갇힌다.
        /// </summary>
        [Test]
        public void Partition_KeepsTheRoomOpenAndBlocksWalking()
        {
            var data = AssetDatabase.LoadAssetAtPath<Code.Buildings.BuildingDataSO>("Assets/07.SO/Buildings/PartitionBuildingData.asset");
            Assert.That(data, Is.Not.Null, "칸막이 데이터를 찾지 못했습니다.");
            Assert.That(data.Prefab, Is.InstanceOf<Code.Buildings.Partition>());

            var shape = NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, new[]
            {
                "......", "......", "......", "......", "......", "......"
            });
            var node = CreateNode();
            node.ApplyLayout(shape, 15f);
            var grid = node.TrapGrid;

            Assert.That(grid.KeepsRoomOpen(0, 2), Is.False, "문 앞 칸은 막을 수 없습니다.");
            Assert.That(Code.Buildings.BuildingPlacement.InstallOnCell(node, 0, 2, data), Is.Null);

            for (var row = 0; row < 5; row++)
                Assert.That(Code.Buildings.BuildingPlacement.InstallOnCell(node, 4, row, data), Is.Not.Null, $"(4, {row})");

            Assert.That(grid.KeepsRoomOpen(4, 5), Is.False, "마지막 틈을 막으면 방이 두 동강 납니다.");
            Assert.That(Code.Buildings.BuildingPlacement.InstallOnCell(node, 4, 5, data), Is.Null);
            Assert.That(grid.IsWalkableCell(4, 1), Is.False);
            Assert.That(node.IsUnitCellAvailable(4, 1), Is.False);

            var path = new System.Collections.Generic.List<Vector2Int>();
            Assert.That(RoomCellPathfinder.TryFindPath(grid, new Vector2Int(5, 0), new Vector2Int(3, 0), path), Is.True);
            Assert.That(path, Has.Member(new Vector2Int(4, 5)), "벽 끝 틈으로 돌아가야 합니다.");

            var cells = new System.Collections.Generic.List<Vector2Int>();
            grid.CollectPartitionCells(cells);
            var plan = RoomTilePlan.Build(shape, RoomDoors.None, RoomDoors.None, 24, cells);
            // 칸 (4,1)의 타일은 x 2..3, y -4..-3.
            Assert.That(plan.Pillars, Has.Member(new Vector2Int(2, -4)));
            Assert.That(plan.Floor, Has.No.Member(new Vector2Int(2, -4)));

            Assert.That(grid.Remove(4, 1), Is.True);
            Assert.That(grid.IsWalkableCell(4, 1), Is.True, "철거한 칸은 다시 바닥입니다.");
        }

        /// <summary>중·대형 생성 방에는 방 안 칸막이 벽(가장자리와 떨어진 2칸 넘는 막힌 줄)이 가끔 생기고, 그래도 규칙에 맞는다.</summary>
        [Test]
        public void RoomShapeGenerator_SometimesCarvesInteriorWalls()
        {
            var withWalls = 0;
            for (var seed = 0; seed < 60; seed++)
            {
                var shape = RoomShapeGenerator.Generate(RoomSize.Large, RoomDoors.Left | RoomDoors.Right, false, 5000 + seed);
                Assert.That(RoomLayoutRules.Validate(shape, out var problem), Is.True, problem);
                if (HasInteriorRun(shape))
                    withWalls++;
            }

            Assert.That(withWalls, Is.GreaterThan(5));
        }

        /// <summary>
        /// 위쪽 벽이 계단처럼 올라가는 곳은 높은 쪽 옆 벽이 낮은 벽 난간 줄까지 내려온다.
        /// 난간 조각(속이 검다)이 놓여 방 위쪽에 검은 칸이 생겼었다.
        /// </summary>
        [Test]
        public void RuleTiles_StepCornerContinuesTheSideWall()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
            Assert.That(tileSet, Is.Not.Null);
            var gridHost = new GameObject("TestGrid", typeof(Grid));
            _layoutHosts.Add(gridHost);
            var tiles = new GameObject("Tiles", typeof(UnityEngine.Tilemaps.Tilemap)).GetComponent<UnityEngine.Tilemaps.Tilemap>();
            tiles.transform.SetParent(gridHost.transform, false);

            // 위 줄은 오른쪽 4칸만 바닥: 왼쪽 2칸 위가 낮은 벽, 칸 (1,3)이 계단 모서리. 계단 모서리 타일은 (-3, 4).
            var left = RoomTilePlan.Build(NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, new[]
            {
                "##....", "......", "......", "......"
            }), RoomDoors.None, RoomDoors.None, 24);
            RoomTilePainter.Paint(null, tiles, tileSet, new[] { new RoomTilePainter.Room(left, Vector2Int.zero, Color.white) });
            Assert.That(Cell(tiles, -3, 4), Is.EqualTo((3, 3)), "왼쪽 계단 모서리는 높은 쪽의 왼쪽 옆 벽입니다.");

            var right = RoomTilePlan.Build(NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, new[]
            {
                "....##", "......", "......", "......"
            }), RoomDoors.None, RoomDoors.None, 24);
            RoomTilePainter.Paint(null, tiles, tileSet, new[] { new RoomTilePainter.Room(right, Vector2Int.zero, Color.white) });
            Assert.That(Cell(tiles, 2, 4), Is.EqualTo((12, 9)), "오른쪽 계단 모서리는 높은 쪽의 오른쪽 옆 벽입니다.");
        }

        /// <summary>
        /// 방 안으로 튀어나온 벽 덩어리·홈의 끝처럼 위쪽 벽(아래 1~2줄이 바닥)인데 옆도 바닥인 칸은 가장자리 있는 벽돌 면이다.
        /// 예전에는 옆 벽 조각(속이 검다)이 먼저 골라져 방 안에 검은 세로 줄이 생겼다.
        /// </summary>
        [Test]
        public void RuleTiles_WallEndsInsideTheRoomShowAFaceNotASideRail()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
            Assert.That(tileSet, Is.Not.Null);
            var gridHost = new GameObject("TestGrid", typeof(Grid));
            _layoutHosts.Add(gridHost);
            var tiles = new GameObject("Tiles", typeof(UnityEngine.Tilemaps.Tilemap)).GetComponent<UnityEngine.Tilemaps.Tilemap>();
            tiles.transform.SetParent(gridHost.transform, false);

            // 오른쪽 벽에서 안으로 튀어나온 벽 덩어리와, 위쪽 벽이 파인 홈.
            var plan = RoomTilePlan.Build(NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Down, new[]
            {
                "..##..", "......", "....##", "....##", "......", "......"
            }), RoomDoors.None, RoomDoors.None, 24);
            RoomTilePainter.Paint(null, tiles, tileSet, new[] { new RoomTilePainter.Room(plan, Vector2Int.zero, Color.white) });

            var checkedTiles = 0;
            foreach (var position in tiles.cellBounds.allPositionsWithin)
            {
                var tile = new Vector2Int(position.x, position.y);
                if (plan.Floor.Contains(tile) || plan.Pillars.Contains(tile) || tiles.GetTile(position) == null)
                    continue;

                var floorBelow = plan.Floor.Contains(tile + Vector2Int.down) || plan.Floor.Contains(tile + new Vector2Int(0, -2));
                var floorBeside = plan.Floor.Contains(tile + Vector2Int.left) || plan.Floor.Contains(tile + Vector2Int.right);
                if (!floorBelow || !floorBeside)
                    continue;

                checkedTiles++;
                var cell = Cell(tiles, tile.x, tile.y);
                Assert.That(cell, Is.Not.EqualTo((3, 3)).And.Not.EqualTo((12, 9)),
                    $"({tile.x}, {tile.y})는 위쪽 벽 끝이라 옆 벽(검은 줄) 조각이면 안 됩니다.");
            }

            Assert.That(checkedTiles, Is.GreaterThan(0), "검사할 벽 끝이 있어야 합니다.");
        }

        /// <summary>
        /// 칸 기둥은 칸마다 윗면(타일 2×2)을 따로 그린다. 예전에는 위아래로 붙은 칸막이가 머리 한 줄짜리 한 덩어리로 이어져
        /// 아래 칸막이 윗면이 보이지 않았고, 머리를 칸 위로 올려 위 칸 바닥을 가렸다. 좌우로 붙은 칸은 윗면·받침이 이어진다.
        /// </summary>
        [Test]
        public void PillarBlocks_DrawEachCellsOwnTop()
        {
            // 칸 (2,2)·(2,3)이 위아래, (3,2)가 (2,2) 오른쪽. 칸 (2,2) 타일은 x -2..-1, y -2..-1, 칸 (3,2)는 x 0..1.
            var partitions = new[] { new Vector2Int(2, 2), new Vector2Int(2, 3), new Vector2Int(3, 2) };
            var plan = RoomTilePlan.Build(NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, Rows(6)), RoomDoors.None,
                RoomDoors.None, 24, partitions);
            var blocks = plan.PillarBlocks;

            Assert.That(blocks[new Vector2Int(-2, 1)], Is.EqualTo(RoomWallKind.PillarTopLeft), "위 칸막이 윗면.");
            Assert.That(blocks[new Vector2Int(-2, 0)], Is.EqualTo(RoomWallKind.PillarTopLowerLeft));
            Assert.That(blocks[new Vector2Int(-2, -1)], Is.EqualTo(RoomWallKind.PillarTopLeft),
                "아래 칸막이 윗면이 위 칸막이 받침 자리를 덮어 보입니다.");
            Assert.That(blocks[new Vector2Int(-2, -3)], Is.EqualTo(RoomWallKind.PillarBaseLeft), "받침은 맨 아래 칸막이 밑에만.");
            Assert.That(plan.Pillars, Has.No.Member(new Vector2Int(-2, 2)), "위 칸은 가리지 않습니다.");

            Assert.That(blocks[new Vector2Int(-1, -1)], Is.EqualTo(RoomWallKind.PillarTop), "좌우로 붙은 윗면은 이어집니다.");
            Assert.That(blocks[new Vector2Int(1, -1)], Is.EqualTo(RoomWallKind.PillarTopRight));
            Assert.That(blocks[new Vector2Int(-1, -3)], Is.EqualTo(RoomWallKind.PillarBase));
            Assert.That(blocks[new Vector2Int(1, -3)], Is.EqualTo(RoomWallKind.PillarBaseRight));
            Assert.That(blocks[new Vector2Int(-1, 1)], Is.EqualTo(RoomWallKind.PillarTopRight), "옆이 비면 윗면 끝입니다.");
        }

        /// <summary>
        /// 시트 18행 바닥(17행보다 한 톤 어두운 그림자 바닥)은 벽·기둥 바로 아래 줄에만 깐다.
        /// 예전에는 무작위 무늬에 섞여 방 한가운데에 구멍 같은 어두운 칸이 박혔다.
        /// </summary>
        [Test]
        public void RuleTiles_ShadowFloorOnlyRunsUnderWalls()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
            Assert.That(tileSet, Is.Not.Null);
            var gridHost = new GameObject("TestGrid", typeof(Grid));
            _layoutHosts.Add(gridHost);
            var tiles = new GameObject("Tiles", typeof(UnityEngine.Tilemaps.Tilemap)).GetComponent<UnityEngine.Tilemaps.Tilemap>();
            tiles.transform.SetParent(gridHost.transform, false);

            var plan = RoomTilePlan.Build(NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right, PillarRows), RoomDoors.None,
                RoomDoors.None, 24);
            RoomTilePainter.Paint(null, tiles, tileSet, new[] { new RoomTilePainter.Room(plan, Vector2Int.zero, Color.white) });

            int shaded = 0, open = 0;
            foreach (var tile in plan.Floor)
            {
                var underWall = !plan.Floor.Contains(tile + Vector2Int.up);
                var row = Cell(tiles, tile.x, tile.y).row;
                Assert.That(row, Is.EqualTo(underWall ? 18 : 17),
                    $"({tile.x}, {tile.y})는 {(underWall ? "벽 바로 아래라 그림자" : "트인 바닥이라 밝은")} 바닥이어야 합니다.");
                if (underWall) shaded++;
                else open++;
            }

            Assert.That(shaded, Is.GreaterThan(0));
            Assert.That(open, Is.GreaterThan(0));
        }

        /// <summary>
        /// 장식은 위쪽 벽 벽돌 면(벽 칸)과 비어 있는 바닥에만 놓이고, 같은 방이면 늘 같은 자리다.
        /// 바닥 칸 위에 벽 장식이 놓이면 바닥에 횃불이 서 있고, 중앙 시설·문 앞을 가리면 건물과 드나드는 길이 안 보인다.
        /// </summary>
        [Test]
        public void RoomTilePlan_DecorStaysOnWallsAndFreeFloor()
        {
            var shape = NewShape(RoomSize.Medium, RoomDoors.Left | RoomDoors.Right | RoomDoors.Up, new[]
            {
                "..............", "..............", "..............", "..............", "..............",
                "..............", "..............", "..............", "..............", ".............."
            });
            var plan = RoomTilePlan.Build(shape, RoomDoors.Up, RoomDoors.None, 48);
            var again = RoomTilePlan.Build(shape, RoomDoors.Up, RoomDoors.None, 48);
            Assert.That(plan.Decor.Count, Is.EqualTo(again.Decor.Count));
            for (var i = 0; i < plan.Decor.Count; i++)
                Assert.That(plan.Decor[i].Tile, Is.EqualTo(again.Decor[i].Tile), "같은 방은 같은 자리에 장식이 있어야 합니다.");

            Assert.That(plan.Decor.Exists(d => d.Kind == RoomDecorKind.Torch), Is.True, "긴 위쪽 벽에는 횃불이 있어야 합니다.");
            var slot = RoomLayoutRules.CentralSlotMin(shape.Columns, shape.Rows);
            var slotMin = RoomTilePlan.CellOriginTile(shape.Columns, shape.Rows, slot.x, slot.y);
            foreach (var decor in plan.Decor)
            {
                var size = RoomTilePainter.DecorSize(decor.Kind);
                for (var dy = 0; dy < size.y; dy++)
                for (var dx = 0; dx < size.x; dx++)
                {
                    var tile = decor.Tile + new Vector2Int(dx, dy);
                    if (decor.Kind == RoomDecorKind.Floor)
                    {
                        Assert.That(plan.Floor, Has.Member(tile));
                        var inSlot = tile.x >= slotMin.x && tile.x < slotMin.x + 4 && tile.y >= slotMin.y && tile.y < slotMin.y + 4;
                        Assert.That(inSlot, Is.False, $"바닥 소품 {tile}이 중앙 시설 자리에 있습니다.");
                    }
                    else
                    {
                        Assert.That(plan.Floor, Has.No.Member(tile), $"벽 장식 {decor.Kind} {tile}이 바닥 위에 있습니다.");
                        Assert.That(tile.y, Is.EqualTo(shape.Rows + dy).Or.EqualTo(shape.Rows + 1), "벽 장식은 위쪽 벽 벽돌 면에 있어야 합니다.");
                        // 위쪽 문 틈(타일 x -1·0)과 그 양옆 한 칸은 문 그림 자리다.
                        Assert.That(tile.x < -2 || tile.x > 1, Is.True, $"벽 장식 {tile}이 위쪽 문 자리와 겹칩니다.");
                    }
                }
            }
        }

        /// <summary>
        /// 던전 타일맵은 그림 한 장이 칸 하나(0.625u)에 꼭 맞아야 한다. 격자 칸 크기만 0.625로 두면 그림이 1u로 그려져 1.6배로 겹쳤다.
        /// </summary>
        [Test]
        public void DungeonTilemap_DrawsOneSpritePerCell()
        {
            var parent = new GameObject("Root");
            _layoutHosts.Add(parent);
            var map = DungeonTilemap.Create(parent.transform, null, null, null);
            var grid = map.GetComponent<Grid>();
            Assert.That(grid.cellSize.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(map.transform.lossyScale.x, Is.EqualTo(RoomLayoutRules.TileWorldSize).Within(0.0001f));
            Assert.That(map.Tiles.CellToWorld(new Vector3Int(1, 0, 0)).x - map.Tiles.CellToWorld(Vector3Int.zero).x,
                Is.EqualTo(RoomLayoutRules.TileWorldSize).Within(0.0001f));
        }

        private static bool HasInteriorRun(RoomShape shape)
        {
            for (var y = 1; y < shape.Rows - 1; y++)
            for (var x = 1; x < shape.Columns - 2; x++)
            {
                if (!shape.IsFloor(x, y) && !shape.IsFloor(x + 1, y))
                    return true;
            }

            for (var x = 1; x < shape.Columns - 1; x++)
            for (var y = 1; y < shape.Rows - 2; y++)
            {
                if (!shape.IsFloor(x, y) && !shape.IsFloor(x, y + 1))
                    return true;
            }

            return false;
        }

        private sealed class ShapeMap : IRoomCellMap
        {
            private readonly RoomShape _shape;
            public ShapeMap(RoomShape shape) => _shape = shape;
            public int Columns => _shape.Columns;
            public int Rows => _shape.Rows;
            public bool IsWalkableCell(int column, int row) => _shape.IsFloor(column, row);
        }

        private static readonly string[] PillarRows =
        {
            "#.....",
            "......",
            "..#...",
            "......",
            "......",
            "......"
        };

        private readonly System.Collections.Generic.List<GameObject> _layoutHosts = new();

        [TearDown]
        public void DestroyLayouts()
        {
            foreach (var host in _layoutHosts)
            {
                if (host != null)
                    Object.DestroyImmediate(host);
            }

            _layoutHosts.Clear();
        }

        private static RoomShape NewShape(RoomSize size, RoomDoors doors, string[] rows) =>
            new($"Test_{size}_{doors}", size, false, doors, rows);

        /// <summary>그 칸에 그려진 스프라이트의 시트 칸(열, 행 — 왼쪽 위 기준 16px).</summary>
        private static (int column, int row) Cell(UnityEngine.Tilemaps.Tilemap tiles, int x, int y)
        {
            var sprite = tiles.GetSprite(new Vector3Int(x, y, 0));
            Assert.That(sprite, Is.Not.Null, $"({x}, {y})에 타일이 없습니다.");
            return (Mathf.RoundToInt(sprite.rect.x / 16f),
                Mathf.RoundToInt((sprite.texture.height - sprite.rect.y - sprite.rect.height) / 16f));
        }

        private static string SpriteName(UnityEngine.Tilemaps.Tilemap tiles, int x, int y)
        {
            var sprite = tiles.GetSprite(new Vector3Int(x, y, 0));
            Assert.That(sprite, Is.Not.Null, $"({x}, {y})에 타일이 없습니다.");
            return sprite.name;
        }

        private static string[] Rows(int count)
        {
            var rows = new string[count];
            for (var i = 0; i < count; i++)
                rows[i] = new string('.', count);
            return rows;
        }

        // Node는 격자·전장 컴포넌트를 필수로 요구하고 표시용 글자도 프리팹에 연결돼 있어, 실제 프리팹을 쓴다.
        private Node CreateNode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Node>("Assets/03.Prefabs/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null, "노드 프리팹을 찾지 못했습니다.");
            var node = Object.Instantiate(prefab);
            _host = node.gameObject;
            return node;
        }
    }
}
