using System.Collections.Generic;
using Code.Enemies;
using Code.Manager;
using Code.MapCreateSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 모험가 길 고르기의 소문(Docs/Design/AdventurerRouteRiskDesign.md).
    ///
    /// 살아 나간 자만 소문을 옮기고, 전멸하면 견문이 사라지고, 큰돈을 들고 나가면 소문이 지워진다.
    /// 이 셋이 어긋나면 "다 잡으면 소문이 안 난다"는 선택이 사라지거나, 보스날 결과가 다음 주 길을 바꾼다.
    /// </summary>
    public class RouteRiskTests
    {
        private readonly List<GameObject> _hosts = new();
        private readonly DungeonGraph _graph = new();
        private int _nextX;

        [TearDown]
        public void TearDown()
        {
            foreach (var host in _hosts)
            {
                if (host != null)
                    Object.DestroyImmediate(host);
            }

            _hosts.Clear();
        }

        /// <summary>소문 세 항목은 따로 쌓이고, 하루가 지나면 절반(내림)만 남는다.</summary>
        [Test]
        public void RoomRumor_AddsPerKindAndHalvesRoundingDown()
        {
            var rumor = RoomRumor.Of(RumorKind.Trap)
                .Add(RoomRumor.Of(RumorKind.Trap))
                .Add(RoomRumor.Of(RumorKind.Trap))
                .Add(RoomRumor.Of(RumorKind.Down))
                .Add(RoomRumor.Of(RumorKind.Retreat));

            Assert.That((rumor.Traps, rumor.Downs, rumor.Retreats), Is.EqualTo((3, 1, 1)));

            var halved = rumor.Halved();
            Assert.That((halved.Traps, halved.Downs, halved.Retreats), Is.EqualTo((1, 0, 0)), "1은 하루 뒤 사라집니다.");
            Assert.That(new RoomRumor(-2, 0, 0).IsEmpty, Is.True, "음수 소문은 없습니다.");
        }

        /// <summary>쓰러짐이 함정·후퇴보다 두 배 무겁고, 보이는 방어(유닛·함정)도 위험에 들어간다.</summary>
        [Test]
        public void RoomRisk_WeighsDownsDoubleAndCountsVisibleDefense()
        {
            Assert.That(RouteRiskRules.RumorScore(new RoomRumor(1, 1, 1)), Is.EqualTo(4f).Within(0.001f));
            Assert.That(RouteRiskRules.RoomRisk(default, 2, 2), Is.EqualTo(3f).Within(0.001f),
                "소문이 없어도 유닛 2명(2)·함정 2개(1)는 위험입니다.");
        }

        /// <summary>겁쟁이가 가장 크게 피하고, 엘리트·보스는 소문을 무시한다.</summary>
        [Test]
        public void Aversion_CowardAvoidsMostAndEliteIgnores()
        {
            var coward = RouteRiskRules.Aversion(AdventurerTrait.Coward, 1, false);
            var normal = RouteRiskRules.Aversion(AdventurerTrait.None, 1, false);
            var shopper = RouteRiskRules.Aversion(AdventurerTrait.Shopaholic, 1, false);

            Assert.That(coward, Is.GreaterThan(normal));
            Assert.That(normal, Is.GreaterThan(shopper));
            Assert.That(RouteRiskRules.Aversion(AdventurerTrait.Coward, PauseLockRules.EliteStrengthTier, false), Is.Zero);
            Assert.That(RouteRiskRules.Aversion(AdventurerTrait.Coward, 1, true), Is.Zero);

            // 위험한 길(위험 4)의 가중치는 겁쟁이 쪽이 더 작다. 보상이 있으면 끌린다.
            Assert.That(RouteRiskRules.Weight(4f, coward, false), Is.LessThan(RouteRiskRules.Weight(4f, normal, false)));
            Assert.That(RouteRiskRules.Weight(4f, normal, true), Is.GreaterThan(RouteRiskRules.Weight(4f, normal, false)));
            Assert.That(RouteRiskRules.Weight(10f, 0f, false), Is.EqualTo(1f).Within(0.001f), "회피가 0이면 위험을 보지 않습니다.");
        }

        /// <summary>방 상세 꼬리말은 가중치가 절반 아래로 떨어지는 기준과 같다.</summary>
        [Test]
        public void AvoidanceNote_MatchesWeightThresholds()
        {
            Assert.That(RouteRiskRules.AvoidanceNote(0f), Is.Empty);
            Assert.That(RouteRiskRules.AvoidanceNote(1f), Is.EqualTo("겁쟁이가 피합니다"));
            Assert.That(RouteRiskRules.AvoidanceNote(4f), Is.EqualTo("대부분 피합니다"));
        }

        /// <summary>살아 나가면 견문이 방 소문이 되고, 그 뒤 견문은 비어 두 번 더해지지 않는다.</summary>
        [Test]
        public void Ledger_EscapeReportsSightingsOnce()
        {
            var ledger = new AdventurerRumorLedger();
            var room = CreateNode();
            ledger.Note(1, room, RumorKind.Trap);
            ledger.Note(1, room, RumorKind.Down);

            Assert.That(room.Rumor.IsEmpty, Is.True, "나가기 전에는 소문이 아닙니다.");

            ledger.Report(1, false);
            Assert.That((room.Rumor.Traps, room.Rumor.Downs), Is.EqualTo((1, 1)));

            ledger.Report(1, false);
            Assert.That((room.Rumor.Traps, room.Rumor.Downs), Is.EqualTo((1, 1)), "같은 견문을 두 번 옮기지 않습니다.");
        }

        /// <summary>전멸한 파티의 견문은 사라지고, 보스와 함께 온 파티는 아무것도 남기지 않는다.</summary>
        [Test]
        public void Ledger_WipeAndBossPartyLeaveNoRumor()
        {
            var ledger = new AdventurerRumorLedger();
            var room = CreateNode();

            ledger.Note(1, room, RumorKind.Down);
            ledger.Discard(1);
            ledger.Report(1, false);
            Assert.That(room.Rumor.IsEmpty, Is.True, "다 잡으면 소문이 나지 않습니다.");

            ledger.Silence(2);
            ledger.Note(2, room, RumorKind.Trap);
            ledger.Report(2, false);
            Assert.That(room.Rumor.IsEmpty, Is.True, "보스 파티의 견문은 소문이 되지 않습니다.");
        }

        /// <summary>큰돈을 들고 나가면 그 파티 견문을 더하지 않고 던전 소문을 지운다. 새 영업은 소문을 절반으로.</summary>
        [Test]
        public void Ledger_BigRewardClearsAndNewDayHalves()
        {
            var ledger = new AdventurerRumorLedger();
            var room = CreateNode();
            room.RestoreRumor(new RoomRumor(4, 2, 0));

            ledger.BeginBusinessDay();
            Assert.That((room.Rumor.Traps, room.Rumor.Downs), Is.EqualTo((2, 1)), "하루가 지나 절반입니다.");

            ledger.Note(3, room, RumorKind.Trap);
            ledger.Report(3, true);
            Assert.That(room.Rumor.IsEmpty, Is.True, "\"돈 된다더라\"가 겁을 덮습니다.");
        }

        /// <summary>남은 길에 노리는 것이 있는지: 쇼핑광은 영업 중인 수익 시설, 보물 탐색은 금화가 든 금고.</summary>
        [Test]
        public void PathHasReward_FollowsTraitAndPurpose()
        {
            var empty = CreateNode();
            var path = new List<Node> { empty };

            Assert.That(RouteRiskRules.PathHasReward(path, AdventurerTrait.Shopaholic, AdventurerVisitPurpose.Shopping), Is.False);
            Assert.That(RouteRiskRules.PathHasReward(path, AdventurerTrait.None, AdventurerVisitPurpose.TreasureHunt), Is.False);
            Assert.That(RouteRiskRules.PathHasReward(null, AdventurerTrait.Shopaholic, AdventurerVisitPurpose.Shopping), Is.False);

            var shopRoom = CreateNode();
            var store = AssetDatabase.LoadAssetAtPath<Code.Buildings.BuildingDataSO>("Assets/07.SO/Buildings/StoreBuildingData.asset");
            Assert.That(store, Is.Not.Null, "상점 데이터를 찾지 못했습니다.");
            Assert.That(Code.Buildings.BuildingPlacement.InstallCentral(shopRoom, store), Is.Not.Null, "빈 중형 방에는 상점을 지을 수 있어야 합니다.");

            var shopPath = new List<Node> { empty, shopRoom };
            Assert.That(RouteRiskRules.PathHasReward(shopPath, AdventurerTrait.Shopaholic, AdventurerVisitPurpose.Shopping), Is.True);
            Assert.That(RouteRiskRules.PathHasReward(shopPath, AdventurerTrait.None, AdventurerVisitPurpose.Shopping), Is.False,
                "쇼핑광이 아니면 시설을 보상으로 보지 않습니다.");
        }

        // 노드는 격자·전장 컴포넌트를 필수로 요구해 실제 프리팹을 쓴다. 방 목록에 오르도록 Initialize를 부른다.
        private Node CreateNode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Node>("Assets/03.Prefabs/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null, "노드 프리팹을 찾지 못했습니다.");
            var node = Object.Instantiate(prefab);
            _hosts.Add(node.gameObject);
            node.Initialize(_graph.AddNode(DungeonNodeType.Corridor, new Vector2Int(_nextX++, 50)), 1f);
            node.SetRoomSize(RoomSize.Medium);
            return node;
        }
    }
}
