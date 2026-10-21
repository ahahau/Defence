using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Gameplay
{
    public class EncounterVarietyTests
    {
        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>초기화 전에도 첫 멤버를 리더로 유지해야 한다.</summary>
        [Test]
        public void PartyCohesion_KeepsFirstLeaderBeforeInitialization()
        {
            var first = new GameObject("first").AddComponent<Code.Enemies.EnemyMover>();
            var second = new GameObject("second").AddComponent<Code.Enemies.EnemyMover>();
            try
            {
                typeof(Code.Enemies.EnemyMover).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(first, null);
                typeof(Code.Enemies.EnemyMover).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(second, null);
                int party = Code.Enemies.EnemyMover.CreatePartyOccupancyId();
                first.ConfigurePartyOccupancy(party);
                second.ConfigurePartyOccupancy(party);
                Assert.That(first.IsPartyLeader, Is.True);
                Assert.That(second.IsPartyLeader, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        /// <summary>새 스폰 없이 기존 동료가 사라진 리더를 승계해야 한다.</summary>
        [Test]
        public void PartyCohesion_ExistingMemberSucceedsDestroyedLeader()
        {
            var first = new GameObject("first").AddComponent<Code.Enemies.EnemyMover>();
            var second = new GameObject("second").AddComponent<Code.Enemies.EnemyMover>();
            try
            {
                typeof(Code.Enemies.EnemyMover).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(first, null);
                typeof(Code.Enemies.EnemyMover).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(second, null);
                int party = Code.Enemies.EnemyMover.CreatePartyOccupancyId();
                first.ConfigurePartyOccupancy(party);
                second.ConfigurePartyOccupancy(party);
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                Assert.That(second.IsPartyLeader, Is.True);
            }
            finally
            {
                if (first != null) UnityEngine.Object.DestroyImmediate(first.gameObject);
                UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        /// <summary>갈림길에서 개인 목적지가 달라도 리더가 고른 방만 따라가야 한다.</summary>
        [Test]
        public void PartyCohesion_FollowsLeaderInsteadOfPersonalGoal()
        {
            var root = new GameObject("party-test");
            try
            {
                var graph = new Code.MapCreateSystem.DungeonGraph();
                var a = MakePartyRoom(root, graph, Vector2Int.zero);
                var b = MakePartyRoom(root, graph, Vector2Int.right);
                var c = MakePartyRoom(root, graph, Vector2Int.up);
                graph.Connect(a.Data, b.Data);
                graph.Connect(a.Data, c.Data);
                var leader = MakePartyMover(root);
                var follower = MakePartyMover(root);
                int party = Code.Enemies.EnemyMover.CreatePartyOccupancyId();
                leader.ConfigurePartyOccupancy(party);
                follower.ConfigurePartyOccupancy(party);
                leader.Initialize(b);
                follower.Initialize(a);
                bool personalGoalCalled = false;
                follower.RouteGoalResolver = () => { personalGoalCalled = true; return c; };
                var choose = typeof(Code.Enemies.EnemyMover).GetMethod("SelectNextNode", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(choose, Is.Not.Null);
                Assert.That(choose.Invoke(follower, null), Is.SameAs(b));
                Assert.That(personalGoalCalled, Is.False);
                leader.Initialize(a);
                Assert.That(choose.Invoke(follower, null), Is.Null, "같은 방에서는 혼자 출발하지 않습니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>빠른 멤버도 느린 동료와 속도를 맞추고, 비활성 리더는 즉시 승계한다.</summary>
        [Test]
        public void PartyCohesion_MatchesSlowestMemberAndReplacesDisabledLeader()
        {
            var root = new GameObject("party-test");
            try
            {
                var leader = MakePartyMover(root);
                var follower = MakePartyMover(root);
                int party = Code.Enemies.EnemyMover.CreatePartyOccupancyId();
                leader.ConfigurePartyOccupancy(party);
                follower.ConfigurePartyOccupancy(party);
                leader.MobilitySpeedMultiplier = 2f;
                follower.FatigueSpeedMultiplier = 0.5f;
                var speed = typeof(Code.Enemies.EnemyMover).GetProperty("PartyWalkSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(speed, Is.Not.Null);
                Assert.That((float)speed.GetValue(leader), Is.EqualTo((float)speed.GetValue(follower)));
                Assert.That((float)speed.GetValue(leader), Is.EqualTo(2.5f).Within(0.001f));
                leader.enabled = false;
                Assert.That(follower.IsPartyLeader, Is.True);
                Assert.That((float)speed.GetValue(follower), Is.EqualTo(2.5f).Within(0.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>다른 목적지를 보더라도 정원 때문에 뒤처진 동료는 공유된 방 순서를 먼저 따른다.</summary>
        [Test]
        public void PartyCohesion_KeepsQueuedRouteWhileLeaderMovesAhead()
        {
            var root = new GameObject("party-test");
            try
            {
                var graph = new Code.MapCreateSystem.DungeonGraph();
                var a = MakePartyRoom(root, graph, Vector2Int.zero);
                var b = MakePartyRoom(root, graph, Vector2Int.right);
                var c = MakePartyRoom(root, graph, Vector2Int.up);
                graph.Connect(a.Data, b.Data);
                graph.Connect(a.Data, c.Data);
                var leader = MakePartyMover(root);
                var follower = MakePartyMover(root);
                int party = Code.Enemies.EnemyMover.CreatePartyOccupancyId();
                leader.ConfigurePartyOccupancy(party);
                follower.ConfigurePartyOccupancy(party);
                leader.Initialize(b);
                follower.Initialize(a);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var share = leader.GetType().GetMethod("SharePartyDestination", flags);
                var choose = follower.GetType().GetMethod("SelectNextNode", flags);
                Assert.That(share, Is.Not.Null);
                Assert.That(choose, Is.Not.Null);
                share.Invoke(leader, new object[] { b });
                leader.Initialize(c);
                Assert.That(choose.Invoke(follower, null), Is.SameAs(b), "새 리더 위치로 지름길을 타지 않고 앞서 공유한 방을 먼저 지납니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>리더가 먼저 탐색을 끝내도 동료가 동선을 받아 걷기 전에는 출발하지 않는다.</summary>
        [Test]
        public void PartyCohesion_WaitsUntilSharedExplorationIsConsumed()
        {
            var root = new GameObject("party-test");
            try
            {
                var graph = new Code.MapCreateSystem.DungeonGraph();
                var room = MakePartyRoom(root, graph, Vector2Int.zero);
                var leader = MakePartyMover(root);
                var follower = MakePartyMover(root);
                int party = Code.Enemies.EnemyMover.CreatePartyOccupancyId();
                leader.ConfigurePartyOccupancy(party);
                follower.ConfigurePartyOccupancy(party);
                leader.Initialize(room);
                follower.Initialize(room);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var publish = leader.GetType().GetMethod("PublishExploration", flags);
                var depart = leader.GetType().GetMethod("CanLeadParty", flags);
                var follow = follower.GetType().GetMethod("FollowSharedExploration", flags);
                Assert.That(publish, Is.Not.Null);
                Assert.That(depart, Is.Not.Null);
                Assert.That(follow, Is.Not.Null);
                publish.Invoke(leader, null);
                Assert.That(depart.Invoke(leader, new object[] { true }), Is.False);
                var routine = (System.Collections.IEnumerator)follow.Invoke(follower, new object[] { leader });
                Assert.That(routine.MoveNext(), Is.False, "빈 탐색 경로도 동료에게 전달됩니다.");
                Assert.That(depart.Invoke(leader, new object[] { true }), Is.True);
                var turning = follower.GetType().GetField("_isTurning", flags);
                Assert.That(turning, Is.Not.Null);
                turning.SetValue(follower, true);
                Assert.That(depart.Invoke(leader, new object[] { true }), Is.False, "걷는 동료를 기다립니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Code.Enemies.EnemyMover MakePartyMover(GameObject root)
        {
            var host = new GameObject("member");
            host.transform.SetParent(root.transform);
            var mover = host.AddComponent<Code.Enemies.EnemyMover>();
            var enable = mover.GetType().GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enable, Is.Not.Null);
            enable.Invoke(mover, null);
            return mover;
        }

        private static Code.MapCreateSystem.Node MakePartyRoom(GameObject root, Code.MapCreateSystem.DungeonGraph graph, Vector2Int position)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Code.MapCreateSystem.Node>("Assets/03.Prefabs/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null);
            var room = UnityEngine.Object.Instantiate(prefab, root.transform);
            room.Initialize(graph.AddNode(Code.MapCreateSystem.DungeonNodeType.Corridor, position), 1f);
            return room;
        }

        [Test]
        public void ThreatPreview_AuthoredTextOverridesRoleInference()
        {
            var profile = RequireType("Code.Manager.WaveThreatProfile");
            var build = profile.GetMethod("BuildForRoleCounts", StaticFlags);
            Assert.That(build, Is.Not.Null);

            var preview = build.Invoke(null, new object[]
            {
                "정찰병 돌격", "경계 명령으로 막으세요", 4, 0, 0, 0
            });

            Assert.That(ReadProperty<string>(preview, "Title"), Is.EqualTo("정찰병 돌격"));
            Assert.That(ReadProperty<string>(preview, "CounterHint"), Is.EqualTo("경계 명령으로 막으세요"));
        }

        [Test]
        public void ThreatPreview_InfersHealerAndRangedCounters()
        {
            var profile = RequireType("Code.Manager.WaveThreatProfile");
            var build = profile.GetMethod("BuildForRoleCounts", StaticFlags);

            var healer = build.Invoke(null, new object[] { null, null, 1, 0, 1, 1 });
            Assert.That(ReadProperty<string>(healer, "Title"), Does.Contain("치유사"));
            Assert.That(ReadProperty<string>(healer, "CounterHint"), Does.Contain("먼저"));

            var ranged = build.Invoke(null, new object[] { null, null, 0, 2, 0, 1 });
            Assert.That(ReadProperty<string>(ranged, "Title"), Does.Contain("원거리"));
            Assert.That(ReadProperty<string>(ranged, "CounterHint"), Does.Contain("경계"));
        }

        [TestCase("Guard", "Nearest", "Frontline")]
        [TestCase("Assault", "Nearest", "Backline")]
        [TestCase("Standby", "LowestHealth", "LowestHealth")]
        [TestCase("Rest", "Focused", "Focused")]
        public void UnitCommand_ChangesTargetPriority(
            string commandName,
            string requestedName,
            string expectedName)
        {
            var utility = RequireType("Code.Units.UnitCommandUtility");
            var commandType = RequireType("Code.Units.UnitCommand");
            var priorityType = RequireType("Code.BT.TargetPriority");
            var resolve = utility.GetMethod("ResolveTargetPriority", StaticFlags);
            Assert.That(resolve, Is.Not.Null);

            var result = resolve.Invoke(null, new[]
            {
                Enum.Parse(commandType, commandName),
                Enum.Parse(priorityType, requestedName)
            });

            Assert.That(result.ToString(), Is.EqualTo(expectedName));
        }

        [Test]
        public void PolicyCombatTradeoffs_HaveNeutralSafeDefaults()
        {
            var policyType = RequireType("Code.Manager.PolicyDataSO");
            var policy = ScriptableObject.CreateInstance(policyType);
            try
            {
                Assert.That(ReadProperty<float>(policy, "UnitDamageMultiplier"), Is.EqualTo(1f));
                Assert.That(ReadProperty<int>(policy, "UnitDefenseBonus"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(policy);
            }
        }

        [Test]
        public void BossReinforcement_ReservesConfiguredEnemiesButKeepsBossInOpeningGroup()
        {
            var bossEntryType = RequireType("Code.Manager.WaveConfigSO+BossEntry");
            var partyType = RequireType("Code.Manager.AdventurerPartySO");
            var enemyDataType = RequireType("Code.Enemies.EnemyDataSO");
            var entry = Activator.CreateInstance(bossEntryType);
            var party = ScriptableObject.CreateInstance(partyType);
            var enemy = ScriptableObject.CreateInstance(enemyDataType);

            try
            {
                bossEntryType.GetField("enableReinforcementPhase").SetValue(entry, true);
                bossEntryType.GetField("reinforcementCount").SetValue(entry, 5);
                partyType.GetProperty("Members").SetValue(party, Array.CreateInstance(enemyDataType, 1));
                ((Array)partyType.GetProperty("Members").GetValue(party)).SetValue(enemy, 0);
                bossEntryType.GetField("reinforcementParty").SetValue(entry, party);

                var reserve = bossEntryType.GetMethod("GetReservedReinforcementCount");
                Assert.That(reserve.Invoke(entry, new object[] { 4 }), Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(party);
            }
        }

        /// <summary>
        /// 파티 인원 변동(사용자 결정: 짠 파티에 인원 변동). 리더는 늘 오고, 밤에는 더 잘 빠지며,
        /// 등급·시설이 클수록 한 명이 더 붙는다. 같은 난수면 같은 결과다(판 시드로 불러와도 같다).
        /// </summary>
        [Test]
        public void PartyComposition_KeepsTheLeaderAndVariesTheRest()
        {
            Assert.That(Code.Manager.PartyCompositionRules.DropChance(false), Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(Code.Manager.PartyCompositionRules.DropChance(true), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(Code.Manager.PartyCompositionRules.ExtraChance(0, 0), Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(Code.Manager.PartyCompositionRules.ExtraChance(3, 2), Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(Code.Manager.PartyCompositionRules.ExtraChance(20, 20), Is.EqualTo(0.4f).Within(0.001f));

            var everyoneDrops = new System.Collections.Generic.List<string> { "leader", "a", "b" };
            Code.Manager.PartyCompositionRules.Vary(everyoneDrops, new System.Random(1), 1f, 0f);
            Assert.That(everyoneDrops, Is.EqualTo(new[] { "leader" }), "리더는 빠지지 않습니다.");

            var extra = new System.Collections.Generic.List<string> { "leader", "a" };
            Code.Manager.PartyCompositionRules.Vary(extra, new System.Random(1), 0f, 1f);
            Assert.That(extra, Is.EqualTo(new[] { "leader", "a", "a" }), "더 오는 한 명은 리더가 아닌 파티원입니다.");

            var first = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4 };
            var second = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4 };
            Code.Manager.PartyCompositionRules.Vary(first, new System.Random(42), 0.4f, 0.4f);
            Code.Manager.PartyCompositionRules.Vary(second, new System.Random(42), 0.4f, 0.4f);
            Assert.That(first, Is.EqualTo(second));
        }

        /// <summary>시설·전투·이동 대기는 시간 초과로 풀리지 않고, 정원 초과만 순차 통과한다.</summary>
        [TestCase(true, false, true, false, true)]
        [TestCase(false, true, true, false, true)]
        [TestCase(false, false, false, false, true)]
        [TestCase(false, false, false, true, false)]
        [TestCase(false, false, true, false, false)]
        public void PartyCohesion_WaitsForBusyOrMissingMembers(bool moving, bool busy, bool sameRoom, bool full, bool expected)
        {
            Assert.That(Code.Enemies.PartyCohesionRules.ShouldWaitForMember(moving, busy, sameRoom, full), Is.EqualTo(expected));
        }

        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", DungeonKeeper.Runtime");
            Assert.That(type, Is.Not.Null, fullName + " 타입을 찾지 못했습니다.");
            return type;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName + " 프로퍼티를 찾지 못했습니다.");
            return (T)property.GetValue(target);
        }

        /// <summary>
        /// 입장 박자(Docs/Design/AdventurerArrivalPacingDesign.md). 그룹 간격은 기본 간격 × 내보낸 인원 × 배율 × 밤 배율.
        /// 낮·밤 단계를 넣으며 그룹 간격이 기본 간격으로 바뀌어 파티가 그룹 크기만큼 빨리 들어온 회귀가 있었다.
        /// </summary>
        [Test]
        public void SpawnPacing_GapScalesWithGroupJitterAndNight()
        {
            Assert.That(Code.Manager.SpawnPacingRules.GapJitter(0d), Is.EqualTo(Code.Manager.SpawnPacingRules.MinGapJitter).Within(0.0001f));
            Assert.That(Code.Manager.SpawnPacingRules.GapJitter(0.5d), Is.EqualTo(1f).Within(0.0001f), "배율 평균이 1이라 하루 길이는 그대로입니다.");
            Assert.That(Code.Manager.SpawnPacingRules.GapJitter(0.9999d), Is.LessThanOrEqualTo(Code.Manager.SpawnPacingRules.MaxGapJitter));

            // 4인 파티, 기본 2.5초: 낮 10초, 밤 16초. 한 사람 몫(2.5초)으로 줄면 회귀다.
            Assert.That(Code.Manager.SpawnPacingRules.GapSeconds(2.5f * 4, 1f, false, 1.6f), Is.EqualTo(10f).Within(0.001f));
            Assert.That(Code.Manager.SpawnPacingRules.GapSeconds(2.5f * 4, 1f, true, 1.6f), Is.EqualTo(16f).Within(0.001f));
            Assert.That(Code.Manager.SpawnPacingRules.GapSeconds(10f, 0.5f, false, 1.6f), Is.EqualTo(5f).Within(0.001f));
        }

        /// <summary>밤 손님은 레벨 -1(최소 1), 보스는 밤에도 그대로.</summary>
        [Test]
        public void SpawnPacing_NightVisitorsAreOneLevelLower()
        {
            Assert.That(Code.Manager.SpawnPacingRules.VisitorLevel(4, false, false), Is.EqualTo(4));
            Assert.That(Code.Manager.SpawnPacingRules.VisitorLevel(4, true, false), Is.EqualTo(3));
            Assert.That(Code.Manager.SpawnPacingRules.VisitorLevel(1, true, false), Is.EqualTo(1), "레벨은 1 아래로 내려가지 않습니다.");
            Assert.That(Code.Manager.SpawnPacingRules.VisitorLevel(4, true, true), Is.EqualTo(4), "보스는 밤에도 약해지지 않습니다.");
        }
    }
}
