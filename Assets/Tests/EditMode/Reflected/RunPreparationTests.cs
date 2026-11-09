using System;
using System.Reflection;
using Code.Run;
using NUnit.Framework;

namespace Defence.EditMode.Tests
{
    /// <summary>목업 9안의 배치와 위기 전이 규칙. 전투 수치를 바꾸지 않고 외부 상태 변화에 반응하는지 검증한다.</summary>
    public class RunPreparationTests
    {
        private static RunEncounterSO Encounter => UnityEditor.AssetDatabase.LoadAssetAtPath<RunEncounterSO>("Assets/07.SO/Run/RunEncounter_Stage1.asset");

        /// <summary>상인 구매가 한 번만 반영되며 원정 보상은 마지막 전투의 배치에만 적용된다.</summary>
        [Test]
        public void DemoCampaign_MerchantAndRaidRewardCarryIntoDefense()
        {
            using IDisposable campaign = CreateDemoCampaign();
            RunDemoRoute route = (RunDemoRoute)campaign.GetType().GetProperty("Route", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(campaign);
            RunBattle first = Encounter.CreateBattle();
            foreach (RunCombatant actor in first.Combatants)
                if (actor.Side == RunSide.Intruder) first.DealDamage(null, actor, actor.Health);
            first.Tick(0.05f);
            DemoInvoke(campaign, "ResolveDefense", first);
            Assert.That(route.TryEnter(RunDemoRoute.Node.Merchant), Is.True);
            Assert.That(DemoInvoke(campaign, "ResolveBranch", 1), Is.True);
            Assert.That(DemoInvoke(campaign, "ResolveBranch", 1), Is.False);
            RunBattleSettings second = (RunBattleSettings)DemoInvoke(campaign, "DefenseSettings", 2);
            Assert.That(second.StartingManaCapacity, Is.EqualTo(3));
            Assert.That(route.TryEnter(RunDemoRoute.Node.SapperDefense), Is.True);
            Assert.That(route.TryResolve(RunDemoRoute.Result.Victory), Is.True);
            Assert.That(route.TryEnter(RunDemoRoute.Node.OutpostRaid), Is.True);
            Assert.That(route.TryResolve(RunDemoRoute.Result.Victory), Is.True);
            Assert.That(route.TryEnter(RunDemoRoute.Node.EliteDefense), Is.True);
            RunBattleSettings elite = (RunBattleSettings)DemoInvoke(campaign, "DefenseSettings", 3);
            Assert.That(elite.StartingManaCapacity, Is.EqualTo(4));
            Assert.That(float.IsPositiveInfinity(elite.TimeLimitSeconds), Is.True);
        }

        /// <summary>원정은 대장 처치 또는 실제 입구 도착으로 끝나고 호위 처치만으로는 끝나지 않는다.</summary>
        [TestCase(false, RunBattleOutcome.Victory)]
        [TestCase(true, RunBattleOutcome.Retreat)]
        public void DemoCampaign_RaidUsesCommanderAndPhysicalExit(bool retreat, RunBattleOutcome expected)
        {
            using IDisposable campaign = CreateDemoCampaign();
            RunBattle first = Encounter.CreateBattle();
            foreach (RunCombatant actor in first.Combatants)
                if (actor.Side == RunSide.Intruder) first.DealDamage(null, actor, actor.Health);
            first.Tick(0.05f);
            DemoInvoke(campaign, "ResolveDefense", first);
            RunBattle raid = (RunBattle)DemoInvoke(campaign, "CreateRaid", new[] { 0 }, true);
            Assert.That(raid.Rooms.Count, Is.EqualTo(6));
            Assert.That(raid.Links.Count, Is.EqualTo(6));
            raid.DealDamage(null, raid.Combatants[1], raid.Combatants[1].Health);
            raid.Tick(0.05f);
            Assert.That(raid.Outcome, Is.EqualTo(RunBattleOutcome.Ongoing));
            if (retreat)
            {
                raid.RequestRaidRetreat();
                Assert.That(raid.Combatants[0].HasLeft, Is.False);
                raid.Tick(10f);
                Assert.That(raid.Combatants[0].HasLeft, Is.True);
            }
            else
            {
                RunCombatant commander = raid.Combatants[3];
                raid.DealDamage(null, commander, commander.Health);
                raid.Tick(0.05f);
            }
            Assert.That(raid.Outcome, Is.EqualTo(expected));
        }

        /// <summary>저장에서 원정 완료를 복원하면 분기와 보상이 유지되고 원정을 반복할 수 없다.</summary>
        [Test]
        public void DemoCampaign_CheckpointRoundTripKeepsResolvedRaid()
        {
            Type checkpointType = typeof(RunDemoSave).GetNestedType("Checkpoint", BindingFlags.NonPublic);
            object checkpoint = Activator.CreateInstance(checkpointType, true);
            checkpointType.GetField("Version").SetValue(checkpoint, 1);
            checkpointType.GetField("Current").SetValue(checkpoint, RunDemoRoute.Node.OutpostRaid);
            checkpointType.GetField("Result").SetValue(checkpoint, RunDemoRoute.Result.Victory);
            checkpointType.GetField("RaidResult").SetValue(checkpoint, RunDemoRoute.Result.Victory);
            checkpointType.GetField("Branch").SetValue(checkpoint, (int)RunDemoRoute.Node.StoryEvent);
            checkpointType.GetField("TrapBonus").SetValue(checkpoint, 0);
            string json = UnityEngine.JsonUtility.ToJson(checkpoint);
            object restored = UnityEngine.JsonUtility.FromJson(json, checkpointType);
            RunDemoRoute route = (RunDemoRoute)checkpointType.GetMethod("GetRoute", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(restored, null);
            Assert.That(route.ChosenBranch, Is.EqualTo(RunDemoRoute.Node.StoryEvent));
            Assert.That(route.RaidResult, Is.EqualTo(RunDemoRoute.Result.Victory));
            Assert.That(route.TryEnter(RunDemoRoute.Node.OutpostRaid), Is.False);
            Assert.That(route.TryEnter(RunDemoRoute.Node.EliteDefense), Is.True);
        }

        private static IDisposable CreateDemoCampaign()
        {
            Type type = typeof(RunDemoRoute).Assembly.GetType("Code.Run.RunDemoCampaign", true);
            return (IDisposable)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { Encounter }, null);
        }

        private static object DemoInvoke(IDisposable campaign, string name, params object[] args)
            => campaign.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(campaign, args);

        /// <summary>분기는 한 곳만 방문하고, 원정의 모든 종료 결과는 마지막 방어로 이어진다.</summary>
        [TestCase(RunDemoRoute.Node.Merchant, RunDemoRoute.Result.Victory)]
        [TestCase(RunDemoRoute.Node.StoryEvent, RunDemoRoute.Result.Defeat)]
        [TestCase(RunDemoRoute.Node.StoryEvent, RunDemoRoute.Result.Retreat)]
        public void DemoRoute_VisitsOneBranchAndContinuesAfterRaid(RunDemoRoute.Node branch, RunDemoRoute.Result raidResult)
        {
            RunDemoRoute route = new();
            Assert.That(route.TryEnter(branch), Is.False);
            Assert.That(route.TryResolve(RunDemoRoute.Result.Victory), Is.True);
            Assert.That(route.TryEnter(RunDemoRoute.Node.SapperDefense), Is.False);
            Assert.That(route.TryEnter(branch), Is.True);
            Assert.That(route.TryResolve(RunDemoRoute.Result.Completed), Is.True);
            Assert.That(route.TryEnter(RunDemoRoute.Node.Merchant), Is.False);
            Assert.That(route.TryEnter(RunDemoRoute.Node.StoryEvent), Is.False);
            Assert.That(route.TryEnter(RunDemoRoute.Node.SapperDefense), Is.True);
            Assert.That(route.TryResolve(RunDemoRoute.Result.Victory), Is.True);
            Assert.That(route.TryEnter(RunDemoRoute.Node.OutpostRaid), Is.True);
            Assert.That(route.TryResolve(raidResult), Is.True);
            Assert.That(route.TryResolve(RunDemoRoute.Result.Victory), Is.False);
            Assert.That(route.TryEnter(RunDemoRoute.Node.EliteDefense), Is.True);
            Assert.That(route.ChosenBranch, Is.EqualTo(branch));
            Assert.That(route.RaidResult, Is.EqualTo(raidResult));
            Assert.That(route.TryResolve(RunDemoRoute.Result.Victory), Is.True);
            Assert.That(route.IsFinished, Is.True);
            Assert.That(route.TryEnter(RunDemoRoute.Node.EliteDefense), Is.False);
        }

        /// <summary>방어에서 심장을 잃으면 이후 칸으로 진행하지 못한다.</summary>
        [Test]
        public void DemoRoute_StopsAfterDefenseDefeat()
        {
            RunDemoRoute route = new();
            Assert.That(route.TryResolve(RunDemoRoute.Result.Retreat), Is.False);
            Assert.That(route.TryResolve(RunDemoRoute.Result.Defeat), Is.True);
            Assert.That(route.IsFinished, Is.True);
            Assert.That(route.TryEnter(RunDemoRoute.Node.Merchant), Is.False);
        }

        /// <summary>첫 판은 기존 구성의 자리와 마력 용량을 기본값으로 쓴다.</summary>
        [Test]
        public void Deployment_DefaultUsesEncounterAndStartsWithoutMana()
        {
            RunDeployment state = new(Encounter);
            Assert.That(state.MinionCount, Is.EqualTo(4));
            Assert.That(state.Capacity, Is.EqualTo(3));
            Assert.That(state.ManaInUse, Is.Zero);
            for (int i = 0; i < state.MinionCount; i++) Assert.That(state.RoomOf(i), Is.EqualTo(Encounter.Minions[i].Room));
        }

        /// <summary>다섯 방 모두에 여러 부하를 함께 놓을 수 있다.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Deployment_AllRoomsAllowStacking(int room)
        {
            RunDeployment state = new(Encounter);
            for (int i = 0; i < state.MinionCount; i++) Assert.That(state.Place(i, room), Is.True);
            for (int i = 0; i < state.MinionCount; i++) Assert.That(state.RoomOf(i), Is.EqualTo(room));
        }

        /// <summary>방 밖이나 없는 부하를 놓는 입력은 기존 배치를 망가뜨리지 않는다.</summary>
        [Test]
        public void Deployment_InvalidDropPreservesPlacement()
        {
            RunDeployment state = new(Encounter);
            int before = state.RoomOf(0);
            Assert.That(state.Place(0, -1), Is.False);
            Assert.That(state.Place(0, 5), Is.False);
            Assert.That(state.Place(4, 1), Is.False);
            Assert.That(state.RoomOf(0), Is.EqualTo(before));
        }

        /// <summary>방 한도와 전체 용량을 지키고 마력을 빼 다른 방에 넣을 수 있다.</summary>
        [Test]
        public void Deployment_ManaHonorsLimitsAndCanBeTransferred()
        {
            RunDeployment state = new(Encounter);
            Assert.That(state.SetMana(1, 3), Is.False);
            Assert.That(state.SetMana(3, 1), Is.False);
            Assert.That(state.SetMana(1, -1), Is.False);
            Assert.That(state.SetMana(1, 2), Is.True);
            Assert.That(state.SetMana(2, 1), Is.True);
            Assert.That(state.SetMana(0, 1), Is.False);
            Assert.That(state.SetMana(2, 0), Is.True);
            Assert.That(state.SetMana(0, 1), Is.True);
            Assert.That(state.ManaInUse, Is.EqualTo(3));
        }

        /// <summary>준비 중에는 제한 없이 기다려도 적 입장과 전투 시간이 진행하지 않는다.</summary>
        [Test]
        public void Flow_PreparationDoesNotAdvanceSimulation()
        {
            RunBattleFlow flow = new(Encounter, new RunDeploymentMemory());
            flow.Advance(999f);
            Assert.That(flow.Phase, Is.EqualTo(RunBattlePhase.Preparation));
            Assert.That(flow.Battle.Elapsed, Is.Zero);
            Assert.That(flow.Battle.Outcome, Is.EqualTo(RunBattleOutcome.Ongoing));
        }

        /// <summary>시작 순간 고른 자리와 마력이 새 전투에 반영되고 원본 구성은 그대로다.</summary>
        [Test]
        public void Flow_StartCopiesPlacementAndManaOnce()
        {
            RunBattleFlow flow = new(Encounter, new RunDeploymentMemory());
            int original = Encounter.Minions[0].Room;
            flow.Deployment.Place(0, 4);
            flow.Deployment.SetMana(1, 2);
            flow.Deployment.SetMana(0, 1);
            Assert.That(flow.StartBattle(), Is.True);
            Assert.That(flow.StartBattle(), Is.False);
            Assert.That(flow.Battle.Combatants[0].HomeRoom, Is.EqualTo(4));
            Assert.That(flow.Battle.Rooms[1].Mana, Is.EqualTo(2));
            Assert.That(flow.Battle.IsDoorLocked(0), Is.True);
            Assert.That(Encounter.Minions[0].Room, Is.EqualTo(original));
            flow.Advance(0.1f);
            Assert.That(flow.Battle.Elapsed, Is.GreaterThan(0f));
        }

        /// <summary>마지막 시작 배치를 복사해 기억하므로 진행 중 명령이 다음 기본값을 바꾸지 않는다.</summary>
        [Test]
        public void Memory_NextPreparationAndRestoreUsePreviousStartSnapshot()
        {
            RunDeploymentMemory memory = new();
            RunBattleFlow first = new(Encounter, memory);
            Assert.That(first.RestorePrevious(), Is.False);
            first.Deployment.Place(0, 2);
            first.Deployment.SetMana(2, 1);
            first.StartBattle();
            first.Deployment.Place(0, 3);
            RunBattleFlow next = new(Encounter, memory);
            Assert.That(next.Deployment.RoomOf(0), Is.EqualTo(2));
            Assert.That(next.Deployment.ManaAt(2), Is.EqualTo(1));
            next.Deployment.Place(0, 4);
            Assert.That(next.RestorePrevious(), Is.True);
            Assert.That(next.Deployment.RoomOf(0), Is.EqualTo(2));
        }

        /// <summary>전투가 끝난 뒤 시간 진행을 재개하지 않는다.</summary>
        [Test]
        public void Flow_OutcomeTransitionsToResult()
        {
            RunBattleFlow flow = new(Encounter, new RunDeploymentMemory());
            flow.StartBattle();
            flow.Advance(121f);
            Assert.That(flow.Phase, Is.EqualTo(RunBattlePhase.Result));
            float elapsed = flow.Battle.Elapsed;
            flow.Advance(1f);
            Assert.That(flow.Battle.Elapsed, Is.EqualTo(elapsed));
        }

        /// <summary>같은 예고는 반복하지 않고 같은 적의 다음 연번에서 다시 알린다.</summary>
        [Test]
        public void Crisis_NewSequencePausesAgainButSameWarningDoesNotRepeat()
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            RunCombatant caster = battle.Combatants[7];
            Set(caster, "HasEntered", true);
            Set(caster, "Position", battle.CenterOf(1));
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.Telegraph), Is.EqualTo(1));
            Assert.That(monitor.Events[0].Pauses, Is.True);
            monitor.Scan();
            Assert.That(monitor.Events, Is.Empty);
            battle.DealDamage(null, caster, 1);
            Set(caster, "SkillCooldownLeft", 0f);
            Set(caster, "AttackTimer", 0f);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.Telegraph), Is.EqualTo(1));
            Assert.That(monitor.Events[0].Sequence, Is.EqualTo(2));
        }

        /// <summary>수리한 방이 다시 고장 나면 다시 멈춘다.</summary>
        [Test]
        public void Crisis_BrokenRoomRearmsAfterRepair()
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            Set(battle.Rooms[1], "IsBroken", true);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.RoomBroken), Is.EqualTo(1));
            monitor.Scan();
            Assert.That(monitor.Events, Is.Empty);
            Set(battle.Rooms[1], "IsBroken", false);
            monitor.Scan();
            Set(battle.Rooms[1], "IsBroken", true);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.RoomBroken), Is.EqualTo(1));
        }

        /// <summary>도적이 금화를 훔치기 시작하는 전이만 한 번 감지한다.</summary>
        [Test]
        public void Crisis_ThiefStealingTransitionPauses()
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            RunCombatant thief = battle.Combatants[6];
            Set(thief, "HasEntered", true);
            Set(thief, "Position", battle.CenterOf(3));
            Set(thief, "Activity", RunActionKind.Steal);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.ThiefAtVault), Is.EqualTo(1));
            Assert.That(monitor.Events[0].Room, Is.EqualTo(3));
            monitor.Scan();
            Assert.That(monitor.Events, Is.Empty);
        }

        /// <summary>낮은 체력은 멈추지 않고 알리고, 회복한 뒤 다시 낮아지면 재알림한다.</summary>
        [Test]
        public void Notice_LowHealthRearmsAfterRecoveryAndDoesNotPause()
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            RunCombatant actor = battle.Combatants[0];
            Set(actor, "Health", (int)Math.Ceiling(actor.MaxHealth * 0.3f));
            monitor.Scan();
            Assert.That(monitor.Events, Is.Empty);
            Set(actor, "Health", 1);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.LowHealth), Is.EqualTo(1));
            Assert.That(monitor.Events[0].Pauses, Is.False);
            monitor.Scan();
            Assert.That(monitor.Events, Is.Empty);
            Set(actor, "Health", actor.MaxHealth);
            monitor.Scan();
            Set(actor, "Health", 1);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.LowHealth), Is.EqualTo(1));
        }

        /// <summary>심장을 치고 즉시 퇴장한 프레임도 심장 진입 알림을 놓치지 않는다.</summary>
        [Test]
        public void Notice_HeartEntryIncludesImmediateExit()
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            RunCombatant actor = battle.Combatants[4];
            Set(actor, "Position", battle.CenterOf(4));
            Set(actor, "HasLeft", true);
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.HeartEntered), Is.EqualTo(1));
            Assert.That(monitor.Events[0].Pauses, Is.False);
        }

        /// <summary>같은 순간 생긴 위기를 누락하지 않고 함께 전달한다.</summary>
        [Test]
        public void Crisis_SimultaneousRoomAndThiefEventsAreGrouped()
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            Set(battle.Rooms[0], "IsBroken", true);
            Set(battle.Rooms[1], "IsBroken", true);
            Set(battle.Combatants[6], "HasEntered", true);
            Set(battle.Combatants[6], "Activity", RunActionKind.Steal);
            monitor.Scan();
            Assert.That(monitor.Events.Count, Is.EqualTo(3));
            foreach (RunCrisis item in monitor.Events) Assert.That(item.Pauses, Is.True);
        }

        /// <summary>준비 때 만든 배열과 목록만으로 계속 감시한다.</summary>
        [Test]
        public void Crisis_ScanDoesNotAllocateAfterSetup()
        {
            RunCrisisMonitor monitor = new(Encounter.CreateBattle());
            for (int i = 0; i < 20; i++) monitor.Scan();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) monitor.Scan();
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        /// <summary>시전자 번호가 다르면 예고 연번이 같아도 두 위기를 모두 보인다.</summary>
        [Test]
        public void Crisis_DifferentCastersWithSameSequenceRemainDistinct()
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            foreach (int id in new[] { 7, 8 })
            {
                RunCombatant caster = battle.Combatants[id];
                Set(caster, "HasEntered", true);
                Set(caster, "Position", RunPosition.InRoom(1, 2.4f));
                Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            }
            monitor.Scan();
            Assert.That(Count(monitor, RunCrisisKind.Telegraph), Is.EqualTo(2));
        }

        /// <summary>목록의 위험 표시는 방·일자 경로·고정 대상의 차이를 지켜야 한다.</summary>
        [TestCase(4, true, false)]
        [TestCase(5, false, false)]
        [TestCase(7, true, true)]
        [TestCase(8, true, true)]
        public void Crisis_DangerMatchesTelegraphScope(int casterId, bool second, bool behind)
        {
            RunBattle battle = Encounter.CreateBattle();
            RunCrisisMonitor monitor = new(battle);
            RunCombatant caster = battle.Combatants[casterId];
            Set(caster, "HasEntered", true);
            Set(caster, "Position", RunPosition.InRoom(1, 1f));
            Set(battle.Combatants[0], "Position", RunPosition.InRoom(1, 1.5f));
            Set(battle.Combatants[1], "Position", RunPosition.InRoom(1, 2f));
            Set(battle.Combatants[2], "Position", battle.CenterOf(0));
            Set(battle.Combatants[3], "Position", RunPosition.InRoom(1, 0f));
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            RunCrisis crisis = new(RunCrisisKind.Telegraph, casterId, 1, caster.Telegraph.Sequence);
            Assert.That(monitor.IsEndangered(battle.Combatants[0], crisis), Is.True);
            Assert.That(monitor.IsEndangered(battle.Combatants[1], crisis), Is.EqualTo(second));
            Assert.That(monitor.IsEndangered(battle.Combatants[2], crisis), Is.False);
            Assert.That(monitor.IsEndangered(battle.Combatants[3], crisis), Is.EqualTo(behind));
        }

        private static int Count(RunCrisisMonitor monitor, RunCrisisKind kind)
        {
            int count = 0;
            foreach (RunCrisis item in monitor.Events) if (item.Kind == kind) count++;
            return count;
        }

        private static void Set(object target, string property, object value)
        {
            FieldInfo field = target.GetType().GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, property);
            field.SetValue(target, value);
        }
    }
}
