using System.Collections.Generic;
using System.Reflection;
using Code.Run;
using Code.Skills;
using NUnit.Framework;
using UnityEngine;

namespace Defence.EditMode.Tests
{
    /// <summary>
    /// 던전 FTL 실시간 전투 규칙(Docs/Design/DungeonFtlDesign.md 8장,
    /// DungeonFtlImplementationDesign.md "실시간 1단계 임시 수치"). 사용자가 정한 규칙이라, 바뀌면 문서와 이 테스트를 함께 고친다.
    /// </summary>
    public class RunBattleTests
    {
        private const int North = 0, DoorControl = North, TrapRoom = 1, Spring = 2, Vault = 3, Heart = 4;
        private const float Step = 0.05f;

        private static readonly RunRoomLayout[] Layout =
        {
            new(RunRoomKind.DoorControl, 0, 1), new(RunRoomKind.Trap, 1, 1), new(RunRoomKind.HealingSpring, 1, 0),
            new(RunRoomKind.Vault, 1, 2), new(RunRoomKind.Heart, 2, 1),
        };

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        // ── 배치 ──

        /// <summary>승인된 1층 십자 다섯 방이 같은 길이의 사방 복도로 이어지는지 지킨다.</summary>
        [Test]
        public void Layout_OneFloorHasFiveRoomsAndFourCorridors()
        {
            RunBattle battle = NewBattle();
            Assert.That(battle.FloorCount, Is.EqualTo(1));
            Assert.That(battle.Rooms.Count, Is.EqualTo(5));
            Assert.That(battle.Rooms[Heart].Row, Is.EqualTo(2));
            Assert.That(battle.Rooms[Heart].Column, Is.EqualTo(1));
            Assert.That(battle.Links.Count, Is.EqualTo(4));
            int vertical = 0;
            foreach (int arm in new[] { North, Spring, Vault, Heart })
                Assert.That(LinkBetween(battle, TrapRoom, arm), Is.GreaterThanOrEqualTo(0));
            foreach (RunLink link in battle.Links)
            {
                if (link.IsVertical) vertical++;
                Assert.That(link.Length, Is.EqualTo(3f));
            }
            Assert.That(vertical, Is.EqualTo(2));
        }

        /// <summary>평면도 부하는 역할과 무관하게 방 중앙을 지키고 그림 겹침만 따로 보정한다.</summary>
        [Test]
        public void Post_AllMinionsUseRoomCenter()
        {
            RunBattle battle = NewBattle();
            RunCombatant melee = battle.AddCombatant(Minion(), TrapRoom);
            RunCombatant ranged = battle.AddCombatant(Minion(ranged: true), TrapRoom);

            Assert.That(melee.Position.Offset, Is.EqualTo(battle.Rooms[TrapRoom].Center));
            Assert.That(ranged.Position.Offset, Is.EqualTo(battle.Rooms[TrapRoom].Center));
        }

        // ── 마력 ──

        /// <summary>마력은 용량(동시에 켤 수 있는 양)이고 방마다 한도가 있다.</summary>
        [Test]
        public void Mana_RejectsOverCapacityAndRoomLimit()
        {
            RunBattle battle = NewBattle();

            Assert.That(battle.SetMana(TrapRoom, 3), Is.False, "함정 방 한도는 2");
            Assert.That(battle.SetMana(TrapRoom, 2), Is.True);
            Assert.That(battle.SetMana(Spring, 1), Is.True);
            Assert.That(battle.SetMana(DoorControl, 1), Is.False, "시작 용량 3을 넘는다");
            Assert.That(battle.SetMana(Vault, 1), Is.False, "금고에는 마력을 넣지 않는다");
            Assert.That(battle.ManaInUse, Is.EqualTo(3));
        }

        /// <summary>고장 난 방은 심장에서 먼 쪽 방의 마력을 끊고, 넣어 둔 마력은 그대로 묶인다(사용자 답).</summary>
        [Test]
        public void Mana_BrokenRoomCutsRoomsFartherFromHeart()
        {
            RunBattle battle = BottleneckBattle();
            battle.SetMana(TrapRoom, 2);
            battle.SetMana(Spring, 1);
            battle.AddCombatant(Intruder(attack: 4, role: RunIntruderRole.Sapper), Spring);

            battle.Tick(Step);

            Assert.That(battle.Rooms[Spring].IsBroken, Is.True);
            Assert.That(battle.IsPowered(TrapRoom), Is.False);
            Assert.That(battle.Rooms[TrapRoom].Mana, Is.EqualTo(2), "끊겨도 마력은 그 방에 남는다");
        }

        // ── 방 시스템 ──

        /// <summary>함정 방은 적이 방 안에 있을 때 터지고, 4초 쉰 뒤 다시 터진다.</summary>
        [Test]
        public void Trap_FiresWithIntruderInsideThenWaitsCooldown()
        {
            RunBattle battle = NewBattle();
            battle.SetMana(TrapRoom, 2);
            battle.AddCombatant(Minion(attack: 0), TrapRoom);
            RunCombatant intruder = battle.AddCombatant(Intruder(health: 50, attack: 0), TrapRoom);

            battle.Tick(Step);
            Assert.That(intruder.Health, Is.EqualTo(46), "마력 2 × 2 피해");
            battle.Tick(3.9f);
            Assert.That(intruder.Health, Is.EqualTo(46), "쿨 4초 동안은 쉰다");
            battle.Tick(0.2f);
            Assert.That(intruder.Health, Is.EqualTo(42));
        }

        /// <summary>잘 맞는 부하가 있으면 방 효과가 +50%(올림)다.</summary>
        [Test]
        public void Trap_FavoredMinionBoostsDamage()
        {
            RunBattle battle = NewBattle();
            battle.SetMana(TrapRoom, 2);
            battle.AddCombatant(Minion(attack: 0, favored: RunRoomKind.Trap), TrapRoom);
            RunCombatant intruder = battle.AddCombatant(Intruder(health: 50, attack: 0), TrapRoom);

            battle.Tick(Step);

            Assert.That(intruder.Health, Is.EqualTo(44));
        }

        /// <summary>치유 샘은 안의 부하를 초마다 회복한다.</summary>
        [Test]
        public void Spring_HealsMinionsEverySecond()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            battle.SetMana(Spring, 1);
            RunCombatant minion = battle.AddCombatant(Minion(health: 10, attack: 0), Spring);
            battle.DealDamage(null, minion, 5);

            battle.Tick(1f);
            Assert.That(minion.Health, Is.EqualTo(6));
            battle.Tick(2f);
            Assert.That(minion.Health, Is.EqualTo(8));
        }

        // ── 싸움·이동 ──

        /// <summary>부하가 있는 방은 지나가지 못한다. 적은 부하에게 공격 거리까지 다가가 멈춰 싸운다.</summary>
        [Test]
        public void Intruder_StopsAtMeleeRangeOfMinion()
        {
            RunBattle battle = NewBattle();
            RunCombatant minion = battle.AddCombatant(Minion(health: 99, attack: 0, ranged: true), Spring);
            RunCombatant intruder = battle.AddCombatant(Intruder(health: 99, attack: 0), Spring);
            Set(intruder, "Position", RunPosition.InRoom(Spring, 0f));

            battle.Tick(6f);

            Assert.That(battle.Distance(minion.Position, intruder.Position), Is.EqualTo(battle.Settings.MeleeRange).Within(0.07f));
            Assert.That(intruder.Activity, Is.EqualTo(RunActionKind.Attack));
        }

        /// <summary>부하가 쓰러지면 적은 다시 걸어 다음 방으로 간다.</summary>
        [Test]
        public void Intruder_WalksOnAfterMinionFalls()
        {
            RunBattle battle = NewBattle();
            RunCombatant minion = battle.AddCombatant(Minion(health: 2, attack: 0), TrapRoom);
            RunCombatant intruder = battle.AddCombatant(Intruder(attack: 1, speed: 6), TrapRoom);

            battle.Tick(Step);
            Assert.That(minion.Health, Is.EqualTo(1), "붙자마자 첫 공격");
            battle.Tick(1f);
            Assert.That(minion.IsDown, Is.True, "공격 간격 6 ÷ 속도 6 = 1초");
            battle.Tick(3f);
            Assert.That(battle.RoomOf(intruder), Is.Not.EqualTo(TrapRoom));
        }

        /// <summary>방어(방패 세우기)는 다음 피해 한 번만 줄인다.</summary>
        [Test]
        public void Guard_ReducesOnlyTheNextHit()
        {
            RunBattle battle = NewBattle();
            RunCombatant minion = battle.AddCombatant(Minion(health: 20, attack: 0), Vault);
            RunCombatant intruder = battle.AddCombatant(Intruder(attack: 3), Vault);
            minion.AddGuard(2);

            battle.DealDamage(intruder, minion, 3);
            battle.DealDamage(intruder, minion, 3);

            Assert.That(minion.Health, Is.EqualTo(16));
        }

        /// <summary>문 제어실은 입구 쪽 문을 잠그고, 적은 문 앞에서 문을 부숴야 지나간다. 부서진 문은 이번 전투 동안 열린다.</summary>
        [Test]
        public void Door_LockedByDoorControlUntilBroken()
        {
            RunBattle battle = DoorOnlyBattle();
            battle.SetMana(0, 1);
            RunCombatant intruder = battle.AddCombatant(Intruder(attack: 5, speed: 6), 0);
            RunDoor door = battle.Doors[0];
            Assert.That(battle.IsDoorLocked(door.Index), Is.True);
            battle.Tick(3f);
            Assert.That(intruder.Activity, Is.EqualTo(RunActionKind.AttackDoor));
            Assert.That(intruder.Position.Link, Is.EqualTo(door.Index));
            battle.Tick(1.2f);
            Assert.That(door.IsBrokenOpen, Is.True);
            battle.Tick(1f);
            Assert.That(battle.IsDoorLocked(door.Index), Is.False);
            Assert.That(battle.Distance(intruder.Position, battle.CenterOf(0)), Is.GreaterThan(2f));
        }

        /// <summary>잠긴 문은 적만 막는다(사용자 답).</summary>
        [Test]
        public void Door_MinionsPassLockedDoor()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            battle.SetMana(DoorControl, 1);
            RunCombatant minion = battle.AddCombatant(Minion(), DoorControl);
            battle.OrderMove(minion, TrapRoom);

            battle.Tick(6f);

            Assert.That(minion.HomeRoom, Is.EqualTo(TrapRoom));
            Assert.That(minion.HasMoveOrder, Is.False);
        }

        /// <summary>싸우는 중인 부하도 이동 명령을 받으면 바로 빠진다. 뒤에 남긴 적은 무시한다(사용자 답).</summary>
        [Test]
        public void Minion_LeavesFightImmediatelyOnMoveOrder()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            RunCombatant minion = battle.AddCombatant(Minion(), Vault);
            battle.AddCombatant(Intruder(health: 50, attack: 0), Vault);
            battle.Tick(Step);
            Assert.That(minion.Activity, Is.EqualTo(RunActionKind.Attack));

            battle.OrderMove(minion, DoorControl);
            battle.Tick(Step);

            Assert.That(minion.Activity, Is.EqualTo(RunActionKind.Move));
        }

        /// <summary>복도에서 마주쳐도 그 자리에서 싸움이 붙는다(사용자 답). 가는 길을 막은 적과는 싸운다.</summary>
        [Test]
        public void Minion_FightsIntruderBlockingItsWay()
        {
            RunBattle battle = NewBattle();
            RunCombatant minion = battle.AddCombatant(Minion(health: 99, attack: 0), DoorControl);
            RunCombatant enemy = battle.AddCombatant(Intruder(health: 99, attack: 0), DoorControl);
            int link = LinkBetween(battle, DoorControl, TrapRoom);
            Set(minion, "Position", RunPosition.OnLink(link, 0.1f));
            Set(enemy, "Position", RunPosition.OnLink(link, 0.5f));
            battle.OrderMove(minion, Heart);
            battle.Tick(2f);

            Assert.That(minion.Activity, Is.EqualTo(RunActionKind.Attack));
            Assert.That(minion.HasMoveOrder, Is.True, "막은 적을 쓰러뜨리면 다시 간다");
        }

        /// <summary>체력이 30% 아래로 떨어진 부하는 켜진 치유 샘으로 물러나고, 다 나으면 지키던 방으로 돌아간다(사용자 답).</summary>
        [Test]
        public void Retreat_LowHealthMinionHealsAtSpringThenReturns()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            battle.SetMana(Spring, 1);
            RunCombatant minion = battle.AddCombatant(Minion(health: 10), Vault);
            battle.DealDamage(null, minion, 8);

            battle.Tick(11f);
            Assert.That(minion.IsRetreating, Is.True);
            Assert.That(minion.HomeRoom, Is.EqualTo(Spring));

            battle.Tick(25f);
            Assert.That(minion.IsRetreating, Is.False);
            Assert.That(minion.HomeRoom, Is.EqualTo(Vault));
            Assert.That(minion.Health, Is.EqualTo(10));
        }

        /// <summary>샘이 꺼져 있으면 물러나지 않는다(사용자 답).</summary>
        [Test]
        public void Retreat_SkippedWhenSpringIsOff()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            RunCombatant minion = battle.AddCombatant(Minion(health: 10), Vault);
            battle.DealDamage(null, minion, 8);

            battle.Tick(Step);

            Assert.That(minion.IsRetreating, Is.False);
        }

        // ── 스킬 ──

        /// <summary>쿨 3은 공격 간격 3번마다 한 번이고(쓰고·평타·평타·다시), 전투 시작부터 준비돼 있다.</summary>
        [Test]
        public void Skill_CooldownIsTurnsTimesAttackInterval()
        {
            RunBattle battle = NewBattle();
            battle.AddCombatant(Minion(attack: 1, speed: 6, skill: DamageSkill(3, 5)), Vault);
            RunCombatant intruder = battle.AddCombatant(Intruder(health: 99, attack: 0), Vault);

            battle.Tick(Step);
            Assert.That(intruder.Health, Is.EqualTo(94), "스킬");
            battle.Tick(1.05f);
            Assert.That(intruder.Health, Is.EqualTo(93), "평타");
            battle.Tick(1f);
            Assert.That(intruder.Health, Is.EqualTo(92), "평타");
            battle.Tick(1f);
            Assert.That(intruder.Health, Is.EqualTo(87), "다시 스킬");
        }

        // ── 고장·수리 ──

        /// <summary>사거리 안의 부하는 공병의 일반 시설 공격을 막을 수 있다.</summary>
        [Test]
        public void Sapper_FightsMinionInRangeBeforeAttackingRoom()
        {
            RunBattle battle = NewBattle();
            battle.SetMana(TrapRoom, 1);
            RunCombatant minion = battle.AddCombatant(Minion(attack: 0), TrapRoom);
            RunCombatant sapper = battle.AddCombatant(Intruder(attack: 2, role: RunIntruderRole.Sapper), TrapRoom);
            Set(minion, "Position", sapper.Position);

            battle.Tick(Step);

            Assert.That(sapper.Activity, Is.EqualTo(RunActionKind.Attack));
            Assert.That(sapper.Target, Is.SameAs(minion));
            Assert.That(battle.Rooms[TrapRoom].Durability, Is.EqualTo(8f));
        }

        /// <summary>적이 없을 때 부하가 초당 수리량만큼 고치고, 가득 차야 다시 작동한다.</summary>
        [Test]
        public void Repair_RestoresBrokenRoomOnlyWhenFull()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            battle.SetMana(TrapRoom, 1);
            RunCombatant sapper = battle.AddCombatant(Intruder(health: 3, attack: 8, role: RunIntruderRole.Sapper), TrapRoom);
            battle.Tick(Step);
            Assert.That(battle.Rooms[TrapRoom].IsBroken, Is.True);
            RunCombatant minion = battle.AddCombatant(Minion(attack: 1, repair: 4f), TrapRoom);
            battle.DealDamage(minion, sapper, sapper.Health);

            battle.Tick(1f);
            Assert.That(battle.Rooms[TrapRoom].IsBroken, Is.True, "4만 고쳐서 아직 고장");
            battle.Tick(1.2f);
            Assert.That(battle.Rooms[TrapRoom].IsBroken, Is.False, "가득 차면 다시 작동");
        }

        // ── 금고·심장 ──

        /// <summary>금고로 가는 도적은 다른 적과 교전 중인 부하를 지나치지만 금고 수비와 도주 차단은 가능하다.</summary>
        [TestCase(TrapRoom, 0.2f, false, true)]
        [TestCase(TrapRoom, 0.6f, false, true)]
        [TestCase(TrapRoom, 1.0f, false, true)]
        [TestCase(Vault, 0.2f, false, false)]
        [TestCase(TrapRoom, 0.2f, true, false)]
        public void Thief_PassesEngagedDefenderOnlyOnWayToVault(int room, float gap, bool escaping, bool shouldPass)
        {
            RunBattle battle = NewBattle();
            RunCombatant defender = battle.AddCombatant(Minion(attack: 0), room);
            RunCombatant fighter = battle.AddCombatant(Intruder(attack: 0), room);
            RunCombatant thief = battle.AddCombatant(Intruder(attack: 0, goal: RunGoal.Vault, role: RunIntruderRole.Thief), room);
            RunPosition start = RunPosition.InRoom(room, 1.5f - gap);
            Set(defender, "Position", RunPosition.InRoom(room, 1.5f));
            Set(fighter, "Position", defender.Position);
            Set(thief, "Position", start);
            Set(thief, "IsEscaping", escaping);

            battle.Tick(Step);

            Assert.That(defender.Target, Is.SameAs(fighter));
            if (shouldPass)
            {
                Assert.That(thief.Activity, Is.EqualTo(RunActionKind.Move));
                Assert.That(battle.Distance(start, thief.Position), Is.GreaterThan(0f));
            }
            else
            {
                Assert.That(thief.Activity, Is.EqualTo(RunActionKind.Attack));
                Assert.That(thief.Target, Is.SameAs(defender));
            }
        }

        /// <summary>도적은 금고에서 초당 10을 훔치고, 45를 채우면 입구로 도주한다. 도주하면 금화를 잃는다.</summary>
        [Test]
        public void Thief_StealsUntilLimitThenEscapes()
        {
            RunBattle battle = NewBattle();
            RunCombatant thief = battle.AddCombatant(Intruder(goal: RunGoal.Vault, role: RunIntruderRole.Thief), Vault);

            battle.Tick(4.6f);
            Assert.That(thief.CarriedGold, Is.EqualTo(45));
            Assert.That(thief.IsEscaping, Is.True);

            battle.Tick(20f);

            Assert.That(thief.HasLeft, Is.True);
            Assert.That(battle.VaultGold, Is.EqualTo(55));
            Assert.That(battle.Outcome, Is.EqualTo(RunBattleOutcome.Victory));
        }

        /// <summary>굴에서 도주 중인 도적을 잡아도 금화를 되찾고 처치 보상을 얻는다.</summary>
        [Test]
        public void Kill_ReturnsStolenGoldAndAddsManaAndGold()
        {
            RunBattle battle = NewBattle();
            RunCombatant thief = battle.AddCombatant(Intruder(health: 1, goal: RunGoal.Vault, role: RunIntruderRole.Thief), Vault);
            battle.Tick(4.6f);
            Assert.That(thief.CarriedGold, Is.EqualTo(45));
            Assert.That(thief.IsEscaping, Is.True);
            Set(thief, "Position", RunPosition.AtEntrance(0.5f));

            battle.DealDamage(null, thief, 5);

            Assert.That(battle.VaultGold, Is.EqualTo(110));
            Assert.That(battle.ManaCapacity, Is.EqualTo(4));
        }

        /// <summary>심장에 닿은 적은 자기 공격력만큼 한 번 치고 퇴장한다.</summary>
        [Test]
        public void Heart_HitOnceThenLeave()
        {
            RunBattle battle = NewBattle();
            RunCombatant intruder = battle.AddCombatant(Intruder(attack: 4), Heart);

            battle.Tick(Step);

            Assert.That(battle.HeartHealth, Is.EqualTo(26));
            Assert.That(intruder.HasLeft, Is.True);
            Assert.That(battle.Outcome, Is.EqualTo(RunBattleOutcome.Victory));
        }

        /// <summary>심장 방에 부하가 있으면 먼저 싸운다.</summary>
        [Test]
        public void Heart_FightsMinionInHeartRoomFirst()
        {
            RunBattle battle = NewBattle();
            battle.AddCombatant(Minion(attack: 0), Heart);
            RunCombatant intruder = battle.AddCombatant(Intruder(attack: 4), Heart);

            battle.Tick(Step);

            Assert.That(battle.HeartHealth, Is.EqualTo(30));
            Assert.That(intruder.Activity, Is.EqualTo(RunActionKind.Attack));
        }

        /// <summary>패배는 심장 0일 때뿐이다(부하 전멸은 패배가 아니다).</summary>
        [Test]
        public void Outcome_DefeatOnlyWhenHeartReachesZero()
        {
            RunBattle battle = NewBattle();
            battle.AddCombatant(Intruder(attack: 30), Heart);

            battle.Tick(Step);

            Assert.That(battle.HeartHealth, Is.EqualTo(0));
            Assert.That(battle.Outcome, Is.EqualTo(RunBattleOutcome.Defeat));
        }

        /// <summary>제한 시간 120초를 버티면 남은 적은 물러나고 승리로 끝난다.</summary>
        [Test]
        public void Outcome_IntrudersRetreatAfterTimeLimit()
        {
            RunBattle battle = NewBattle();
            battle.AddCombatant(Minion(attack: 0), TrapRoom);
            RunCombatant intruder = battle.AddCombatant(Intruder(health: 99, attack: 0), TrapRoom);

            battle.Tick(119f);
            Assert.That(battle.Outcome, Is.EqualTo(RunBattleOutcome.Ongoing));
            battle.Tick(2f);

            Assert.That(intruder.HasLeft, Is.True);
            Assert.That(battle.Outcome, Is.EqualTo(RunBattleOutcome.Victory));
            Assert.That(battle.Elapsed, Is.EqualTo(120f).Within(0.06f));
        }

        /// <summary>적은 입구에서 1.5초 간격으로 한 명씩 들어온다.</summary>
        [Test]
        public void Entry_IntrudersEnterOneByOne()
        {
            RunBattle battle = NewBattle();
            RunCombatant first = battle.AddIntruderAtEntrance(Intruder());
            RunCombatant second = battle.AddIntruderAtEntrance(Intruder());

            battle.Tick(Step);
            Assert.That(first.HasEntered, Is.True);
            Assert.That(second.HasEntered, Is.False);
            battle.Tick(1.5f);
            Assert.That(second.HasEntered, Is.True);
        }

        /// <summary>일정 길이 복도에서는 여러 방어 벌점이 합쳐지므로 누적 비용보다 짧은 우회로를 고르는지 확인한다.</summary>
        [Test]
        public void Routing_CombinedBlockerPenaltiesChooseCheaperDetour()
        {
            RunBattle battle = RoutingBattle();
            battle.SetMana(0, 1);
            battle.AddCombatant(Minion(attack: 0), 1);
            battle.AddCombatant(Minion(attack: 0), 2);
            Assert.That(battle.RouteCost(battle.CenterOf(0), 3), Is.EqualTo(30f));
            RunNavigation navigation = new(battle);
            Assert.That(navigation.NextStep(battle.CenterOf(0), battle.CenterOf(3), true, out RunPosition start, out _), Is.True);
            Assert.That(start.Axis, Is.EqualTo(RunAxis.Vertical));
        }

        /// <summary>우회 쪽도 잠겨 더 비싸면 방어 중인 방을 뚫는 길을 택해야 한다.</summary>
        [Test]
        public void Routing_MoreExpensiveDetourChoosesBreakthrough()
        {
            RunBattle battle = RoutingBattle();
            battle.SetMana(0, 1);
            battle.SetMana(2, 1);
            battle.AddCombatant(Minion(attack: 0), 1);
            battle.AddCombatant(Minion(attack: 0), 4);
            battle.AddCombatant(Minion(attack: 0), 5);
            battle.AddCombatant(Minion(attack: 0), 6);
            Assert.That(battle.RouteCost(battle.CenterOf(0), 3), Is.EqualTo(42f));
            RunNavigation navigation = new(battle);
            navigation.NextStep(battle.CenterOf(0), battle.CenterOf(3), true, out RunPosition start, out _);
            Assert.That(start.Axis, Is.EqualTo(RunAxis.Horizontal));
        }

        /// <summary>잠긴 문도 방 두 칸 벌점을 더하므로 금고를 노리는 도적은 위쪽으로 우회한다.</summary>
        [Test]
        public void Routing_LockedDoorChoosesCheaperDetour()
        {
            RunBattle battle = RoutingBattle();
            battle.SetMana(0, 1);
            battle.SetMana(2, 1);
            Assert.That(battle.RouteCost(battle.CenterOf(0), 3), Is.EqualTo(30f));
            RunNavigation navigation = new(battle);
            navigation.NextStep(battle.CenterOf(0), battle.CenterOf(3), true, out RunPosition start, out _);
            Assert.That(start.Axis, Is.EqualTo(RunAxis.Vertical));
        }

        /// <summary>문 제어실 자체가 목표일 때 긴 우회로보다 문을 부수는 비용이 작으면 문을 친다.</summary>
        [Test]
        public void Routing_LockedDoorBreaksWhenDetourExceedsTwoRooms()
        {
            RunBattle battle = NewBattle();
            battle.SetMana(DoorControl, 1);
            RunCombatant intruder = battle.AddCombatant(Intruder(attack: 5, speed: 6), North);
            int door = LinkBetween(battle, North, TrapRoom);
            Assert.That(battle.RouteCost(intruder.Position, TrapRoom), Is.EqualTo(12f));
            battle.Tick(3f);
            Assert.That(intruder.Activity, Is.EqualTo(RunActionKind.AttackDoor));
            battle.Tick(1.2f);
            Assert.That(battle.Doors[door].IsBrokenOpen, Is.True);
        }

        /// <summary>공병은 가장 가까운 실제 켜진 방을 고르고 마력 배분 변경에 바로 대응한다.</summary>
        [Test]
        public void Sapper_TargetChangesWithNearestPoweredRoom()
        {
            RunBattle battle = NewBattle();
            RunCombatant sapper = battle.AddCombatant(Intruder(role: RunIntruderRole.Sapper), Spring);
            battle.SetMana(Spring, 1);
            battle.SetMana(TrapRoom, 1);
            battle.SetMana(DoorControl, 1);
            Assert.That(battle.GoalRoomOf(sapper), Is.EqualTo(Spring));
            battle.SetMana(Spring, 0);
            Assert.That(battle.GoalRoomOf(sapper), Is.EqualTo(TrapRoom));
            battle.SetMana(TrapRoom, 0);
            Assert.That(battle.GoalRoomOf(sapper), Is.EqualTo(DoorControl));
        }

        /// <summary>공병이 꺼진 방을 지나며 멈춰 부수던 기존 동작이 새 목표 규칙에 남지 않게 한다.</summary>
        [Test]
        public void Sapper_IgnoresUnpoweredSystemAndFallsBackToHeart()
        {
            RunBattle battle = NewBattle();
            RunCombatant sapper = battle.AddCombatant(Intruder(attack: 2, role: RunIntruderRole.Sapper), TrapRoom);
            battle.Tick(Step);
            Assert.That(battle.GoalRoomOf(sapper), Is.EqualTo(Heart));
            Assert.That(battle.Rooms[TrapRoom].Durability, Is.EqualTo(8f));
            Assert.That(sapper.Activity, Is.EqualTo(RunActionKind.Move));
        }

        /// <summary>세 상성의 모든 조합을 검사해 방향이 뒤집히거나 평타 보정이 빠지는 것을 막는다.</summary>
        [TestCase(RunAffinity.Physical, RunAffinity.Physical, 4)]
        [TestCase(RunAffinity.Physical, RunAffinity.Magic, 6)]
        [TestCase(RunAffinity.Physical, RunAffinity.Curse, 3)]
        [TestCase(RunAffinity.Magic, RunAffinity.Physical, 3)]
        [TestCase(RunAffinity.Magic, RunAffinity.Magic, 4)]
        [TestCase(RunAffinity.Magic, RunAffinity.Curse, 6)]
        [TestCase(RunAffinity.Curse, RunAffinity.Physical, 6)]
        [TestCase(RunAffinity.Curse, RunAffinity.Magic, 3)]
        [TestCase(RunAffinity.Curse, RunAffinity.Curse, 4)]
        public void Affinity_BasicDamageFollowsCycle(RunAffinity attack, RunAffinity body, int damage)
        {
            RunBattle battle = NewBattle();
            RunCombatant source = battle.AddCombatant(Minion(), TrapRoom, attack);
            RunCombatant target = battle.AddCombatant(Intruder(health: 50), TrapRoom, body);
            battle.DealDamage(source, target, 4);
            Assert.That(target.Health, Is.EqualTo(50 - damage));
        }

        /// <summary>사용자가 고른 올림을 적용하고 방어는 상성 보정 뒤 한 번만 소모한다.</summary>
        [Test]
        public void Affinity_RoundsUpBeforeConsumingGuard()
        {
            RunBattle battle = NewBattle();
            RunCombatant source = battle.AddCombatant(Minion(), Vault, RunAffinity.Physical);
            RunCombatant target = battle.AddCombatant(Intruder(health: 20), Vault, RunAffinity.Curse);
            target.AddGuard(1);
            battle.DealDamage(source, target, 3);
            battle.DealDamage(source, target, 3);
            Assert.That(target.Health, Is.EqualTo(15));
        }

        /// <summary>상성은 평타뿐 아니라 기존 피해 스킬에도 같은 경로로 들어가야 한다.</summary>
        [Test]
        public void Affinity_DamageSkillUsesCasterAffinity()
        {
            RunBattle battle = NewBattle();
            battle.AddCombatant(Minion(attack: 0, skill: DamageSkill(3, 4)), Vault, RunAffinity.Magic);
            RunCombatant target = battle.AddCombatant(Intruder(health: 50, attack: 0), Vault, RunAffinity.Curse);
            battle.Tick(Step);
            Assert.That(target.Health, Is.EqualTo(44));
        }

        /// <summary>종류마다 다른 함정 상성이 방 데이터에서 피해까지 전달돼야 한다.</summary>
        [TestCase(RunAffinity.Physical, RunAffinity.Magic, 6)]
        [TestCase(RunAffinity.Magic, RunAffinity.Physical, 3)]
        [TestCase(RunAffinity.Curse, RunAffinity.Physical, 6)]
        public void Affinity_TrapUsesRoomConfiguredAffinity(RunAffinity trap, RunAffinity body, int damage)
        {
            RunRoomLayout[] layout = (RunRoomLayout[])Layout.Clone();
            layout[TrapRoom] = new RunRoomLayout(RunRoomKind.Trap, 1, 1, trapAffinity: trap);
            RunBattle battle = new(new RunBattleSettings(), layout);
            battle.SetMana(TrapRoom, 2);
            RunCombatant target = battle.AddCombatant(Intruder(health: 50, attack: 0), TrapRoom, body);
            battle.Tick(Step);
            Assert.That(target.Health, Is.EqualTo(50 - damage));
        }

        /// <summary>파티별 상성을 덮어써도 공유 원본이나 같은 직업의 다른 인물이 바뀌면 안 된다.</summary>
        [Test]
        public void Affinity_PartyOverrideLeavesSharedDefinitionUnchanged()
        {
            RunBattle battle = NewBattle();
            RunCombatantSO definition = Intruder();
            RunCombatant overridden = battle.AddIntruderAtEntrance(definition, RunAffinity.Curse);
            RunCombatant original = battle.AddIntruderAtEntrance(definition);
            Assert.That(overridden.Affinity, Is.EqualTo(RunAffinity.Curse));
            Assert.That(original.Affinity, Is.EqualTo(RunAffinity.Physical));
            Assert.That(definition.Affinity, Is.EqualTo(RunAffinity.Physical));
        }

        /// <summary>근접 부하가 방 안의 먼 적에게 접근하고, 싸움이 끝나면 지키던 자리로 복귀해야 한다.</summary>
        [Test]
        public void Melee_ApproachesRoomEnemyThenReturnsToPost()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            RunCombatant minion = battle.AddCombatant(Minion(attack: 0), TrapRoom);
            RunCombatant enemy = battle.AddCombatant(Intruder(attack: 0), TrapRoom);
            Set(enemy.Definition, "MoveSpeed", 0f);
            Set(enemy, "Position", RunPosition.InRoom(TrapRoom, 2.9f));
            float initial = battle.Distance(minion.Position, enemy.Position);
            battle.Tick(1f);
            Assert.That(battle.Distance(minion.Position, enemy.Position), Is.LessThan(initial));
            Assert.That(minion.HomeRoom, Is.EqualTo(TrapRoom));
            battle.DealDamage(null, enemy, 99);

            battle.Tick(3f);

            Assert.That(battle.Distance(minion.Position, battle.PostPosition(minion.Definition, TrapRoom)), Is.LessThan(0.001f));
        }

        /// <summary>옆방에서 자신을 공격하는 적에게도 접근하고, 적이 쓰러지면 담당 방으로 돌아간다.</summary>
        [Test]
        public void Melee_ApproachesOutsideAttackerThenReturns()
        {
            RunBattle battle = NewBattle();
            KeepBattleGoing(battle);
            RunCombatant minion = battle.AddCombatant(Minion(health: 99, attack: 0), TrapRoom);
            RunCombatant enemy = battle.AddCombatant(Intruder(attack: 1, ranged: true), North);
            Set(enemy.Definition, "MoveSpeed", 0f);
            Set(enemy, "Position", RunPosition.OnLink(LinkBetween(battle, North, TrapRoom), 2.5f));
            float initial = battle.Distance(minion.Position, enemy.Position);
            battle.Tick(1f);
            Assert.That(minion.LastAttacker, Is.EqualTo(enemy));
            Assert.That(battle.Distance(minion.Position, enemy.Position), Is.LessThan(initial));
            battle.DealDamage(null, enemy, 99);

            battle.Tick(4f);

            Assert.That(minion.HomeRoom, Is.EqualTo(TrapRoom));
            Assert.That(battle.Distance(minion.Position, battle.PostPosition(minion.Definition, TrapRoom)), Is.LessThan(0.001f));
        }

        /// <summary>옆방이라는 이유만으로 다른 적을 쫓지 않고 원거리 부하는 자리를 지켜야 한다.</summary>
        [Test]
        public void Minion_DoesNotChaseUnrelatedOutsideEnemyOrWithRangedWeapon()
        {
            RunBattle battle = NewBattle();
            RunCombatant minion = battle.AddCombatant(Minion(attack: 0), TrapRoom);
            RunCombatant ranged = battle.AddCombatant(Minion(attack: 0, ranged: true), Spring);
            RunCombatant enemy = battle.AddCombatant(Intruder(attack: 0), North);
            Set(enemy.Definition, "MoveSpeed", 0f);
            RunPosition post = minion.Position;
            RunPosition rangedPost = ranged.Position;
            battle.Tick(1f);
            Assert.That(battle.Distance(minion.Position, post), Is.Zero);
            Assert.That(battle.Distance(ranged.Position, rangedPost), Is.Zero);
        }

        /// <summary>격자에는 우회 연결이 있어 방 하나가 고장 나도 모든 먼 방이 꺼지지는 않는다.</summary>
        [Test]
        public void Mana_AlternateGraphPathSurvivesBrokenRoom()
        {
            RunBattle battle = AlternatePowerBattle();
            battle.SetMana(TrapRoom, 2);
            battle.SetMana(Spring, 1);
            battle.AddCombatant(Intruder(attack: 4, role: RunIntruderRole.Sapper), Spring);
            battle.Tick(Step);
            Assert.That(battle.Rooms[Spring].IsBroken, Is.True);
            Assert.That(battle.IsPowered(TrapRoom), Is.True);
            Assert.That(battle.IsPowered(Spring), Is.False);
            Assert.That(battle.ManaInUse, Is.EqualTo(3));
        }

        /// <summary>같은 열 위아래도 사다리와 방 내부를 거친 거리로 계산해 공격·지원 범위가 늘어나지 않게 한다.</summary>
        [Test]
        public void Range_UsesRoomInteriorAndGraphLinksForAttackAndSupport()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = battle.AddCombatant(Minion(ranged: true), TrapRoom);
            RunCombatant ally = battle.AddCombatant(Minion(), Vault);
            RunCombatant enemy = battle.AddCombatant(Intruder(attack: 0), Vault);
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, RunAxis.Vertical, 0f));
            Set(ally, "Position", RunPosition.InRoom(Vault, 3f));
            Set(enemy, "Position", RunPosition.InRoom(Vault, 3f));
            Assert.That(battle.Distance(caster.Position, enemy.Position), Is.EqualTo(9f));
            Assert.That(battle.PickOpponents(caster, RunTargetRule.AllInRange, 99), Is.Empty);
            Assert.That(battle.AlliesNear(caster), Has.No.Member(ally));
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, 3f));
            Set(enemy, "Position", RunPosition.OnLink(LinkBetween(battle, TrapRoom, Vault), 2f));
            Assert.That(battle.Distance(caster.Position, enemy.Position), Is.EqualTo(2f));
            Assert.That(battle.PickOpponents(caster, RunTargetRule.AllInRange, 99), Does.Contain(enemy));
        }

        /// <summary>지원 거리는 방에서 복도로 이어져 방 가장자리의 아군도 거리 안이면 닿아야 한다.</summary>
        [Test]
        public void Support_ReachesAllyInCorridorWithinSupportRadius()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = battle.AddCombatant(Minion(), TrapRoom);
            RunCombatant ally = battle.AddCombatant(Minion(), Vault);
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, 3f));
            Set(ally, "Position", RunPosition.OnLink(LinkBetween(battle, TrapRoom, Vault), 1f));
            Assert.That(battle.Distance(caster.Position, ally.Position), Is.EqualTo(1f));
            Assert.That(battle.AlliesNear(caster), Does.Contain(ally));
        }

        /// <summary>여러 층 데이터를 담더라도 층 간 길을 만들거나 전투 중 부하를 다른 층에 보내면 안 된다.</summary>
        [Test]
        public void Floors_StoreMultipleFloorsWithoutInterFloorMovement()
        {
            RunRoomLayout[] layout = new RunRoomLayout[Layout.Length + 1];
            Layout.CopyTo(layout, 0);
            layout[Layout.Length] = new RunRoomLayout(RunRoomKind.Trap, 0, 0, floor: 1);
            RunBattle battle = new(new RunBattleSettings(), layout);
            RunCombatant minion = battle.AddCombatant(Minion(), North);
            Assert.That(battle.FloorCount, Is.EqualTo(2));
            Assert.That(battle.IsConnected(5), Is.False);
            Assert.That(battle.Distance(battle.CenterOf(North), battle.CenterOf(5)), Is.EqualTo(float.PositiveInfinity));
            Assert.That(battle.OrderMove(minion, 5), Is.False);
        }

        /// <summary>탐색과 대상 정렬이 전투가 흐를 때마다 임시 객체를 만들지 않는지 확인한다.</summary>
        [Test]
        public void Simulation_GraphAndTargetQueriesDoNotAllocateAfterSetup()
        {
            RunBattle battle = NewBattle();
            battle.SetMana(DoorControl, 1);
            RunCombatant caster = battle.AddCombatant(Minion(attack: 0, ranged: true), TrapRoom);
            battle.AddCombatant(Intruder(health: 99, attack: 0), TrapRoom);
            battle.AddCombatant(Intruder(health: 99, attack: 0), TrapRoom);
            battle.Tick(Step);
            battle.PickOpponents(caster, RunTargetRule.AllInRange, 99);
            battle.AlliesNear(caster);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
            {
                battle.Tick(Step);
                battle.RouteCost(caster.Position, Heart);
                battle.PickOpponents(caster, RunTargetRule.AllInRange, 99);
                battle.AlliesNear(caster);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        /// <summary>실제로 씬이 읽는 구성에도 격자·시작 배치·파티 상성이 빠짐없이 저장돼야 한다.</summary>
        [Test]
        public void Encounter_StageOneStoresGridPlacementsAndPartyAffinities()
        {
            RunEncounterSO encounter = UnityEditor.AssetDatabase.LoadAssetAtPath<RunEncounterSO>(
                "Assets/07.SO/Run/RunEncounter_Stage1.asset");
            Assert.That(encounter, Is.Not.Null);
            Assert.That(encounter.GridLayout.Length, Is.EqualTo(5));
            RunBattle battle = encounter.CreateBattle();
            Assert.That(battle.Rooms[Heart].Kind, Is.EqualTo(RunRoomKind.Heart));
            Assert.That(battle.FloorCount, Is.EqualTo(1));
            Assert.That(battle.ManaCapacity, Is.EqualTo(3));
            Assert.That(battle.Rooms[TrapRoom].MaxMana, Is.EqualTo(2));
            Assert.That(battle.Rooms[Spring].MaxMana, Is.EqualTo(1));
            Assert.That(battle.Rooms[DoorControl].MaxMana, Is.EqualTo(1));
            Assert.That(battle.Rooms[TrapRoom].TrapAffinity, Is.EqualTo(RunAffinity.Physical));
            Assert.That(battle.Combatants.Count, Is.EqualTo(9));
            foreach (RunCombatant combatant in battle.Combatants)
            {
                if (combatant.Side == RunSide.Minion)
                {
                    int expectedRoom = combatant.Definition.name == "RunMinion_OrcShaman" ? DoorControl
                        : combatant.Definition.name == "RunMinion_SkeletonShield" ? Vault : TrapRoom;
                    Assert.That(combatant.HomeRoom, Is.EqualTo(expectedRoom));
                    RunAffinity expected = combatant.Definition.name == "RunMinion_OrcShaman" ? RunAffinity.Curse
                        : combatant.Definition.name == "RunMinion_SkeletonSniper" ? RunAffinity.Magic : RunAffinity.Physical;
                    Assert.That(combatant.Affinity, Is.EqualTo(expected));
                }
                else
                {
                    RunAffinity expected = combatant.Definition.name == "RunIntruder_Thief" ? RunAffinity.Curse
                        : combatant.Definition.name is "RunIntruder_Archer" or "RunIntruder_ApprenticeMage"
                            ? RunAffinity.Magic : RunAffinity.Physical;
                    Assert.That(combatant.Affinity, Is.EqualTo(expected));
                }
            }
            int door = LinkBetween(battle, North, TrapRoom);
            battle.SetMana(DoorControl, 1);
            for (int i = 0; i < battle.Doors.Count; i++)
            {
                Assert.That(battle.IsDoorLocked(i), Is.EqualTo(i == door));
            }
        }

        /// <summary>지키던 부하가 쓰러지면 경로의 방어 벌점도 즉시 사라져야 한다.</summary>
        [Test]
        public void Routing_RemovesPenaltyWhenDefenderFallsOrReceivesMoveOrder()
        {
            RunBattle battle = RoutingBattle();
            RunCombatant defender = battle.AddCombatant(Minion(), 1);
            battle.AddCombatant(Minion(), 2);
            Assert.That(battle.RouteCost(battle.CenterOf(0), 3), Is.EqualTo(30f));
            battle.OrderMove(defender, 5);
            Assert.That(battle.RouteCost(battle.CenterOf(0), 3), Is.EqualTo(24f));
            RunCombatant replacement = battle.AddCombatant(Minion(), 1);
            Assert.That(battle.RouteCost(battle.CenterOf(0), 3), Is.EqualTo(30f));
            battle.DealDamage(null, replacement, 99);
            Assert.That(battle.RouteCost(battle.CenterOf(0), 3), Is.EqualTo(24f));
        }

        /// <summary>같은 층이어도 빈 칸으로 끊긴 방에는 이동 명령을 받지 않아야 한다.</summary>
        [Test]
        public void Movement_RejectsDisconnectedDestinationOnSameFloor()
        {
            RunBattle battle = new(new RunBattleSettings(), new RunRoomLayout[]
            {
                new(RunRoomKind.Vault, 0, 0), new(RunRoomKind.Heart, 0, 1), new(RunRoomKind.Trap, 3, 3),
            });
            RunCombatant minion = battle.AddCombatant(Minion(), 0);
            Assert.That(battle.OrderMove(minion, 2), Is.False);
            Assert.That(minion.HasMoveOrder, Is.False);
        }

        /// <summary>부하 한 명이 지키는 방의 붙잡기는 적 인원 수에 제한이 없어야 한다.</summary>
        [Test]
        public void Defense_HoldsEveryIntruderUntilDefenderFalls()
        {
            RunBattle battle = NewBattle();
            RunCombatant defender = battle.AddCombatant(Minion(health: 99, attack: 0, ranged: true), TrapRoom);
            RunCombatant[] enemies = new RunCombatant[3];
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i] = battle.AddCombatant(Intruder(health: 99, attack: 0), TrapRoom);
                Set(enemies[i], "Position", RunPosition.InRoom(TrapRoom, i * 0.4f));
            }
            battle.Tick(8f);
            foreach (RunCombatant enemy in enemies)
            {
                Assert.That(battle.RoomOf(enemy), Is.EqualTo(TrapRoom));
                Assert.That(enemy.Target, Is.EqualTo(defender));
                Assert.That(enemy.HasLeft, Is.False);
            }
        }

        // ── 만들기 ──

        /// <summary>폭약은 초읽기 뒤 고정한 방의 양쪽 전원과 시스템에만 피해를 줘야 한다.</summary>
        [Test]
        public void PowderBlast_CountsDownThenHitsBothSidesAndSystemInFixedRoom()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.PowderBlast, systemDamage: 8);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            RunCombatant ally = battle.AddCombatant(Intruder(health: 50, attack: 0), TrapRoom);
            Set(ally.Definition, "MoveSpeed", 0f);
            RunCombatant outside = AddStationaryMinion(battle, Spring);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            Assert.That(battle.Telegraphs, Has.Count.EqualTo(1));
            Assert.That(caster.Telegraph.Scope, Is.EqualTo(RunTelegraphScope.Room));
            battle.Tick(0.5f);
            Assert.That(caster.Telegraph.RemainingSeconds, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(50));
            Assert.That(battle.Rooms[TrapRoom].Durability, Is.EqualTo(8));

            battle.Tick(0.55f);

            Assert.That(target.Health, Is.EqualTo(46));
            Assert.That(caster.Health, Is.EqualTo(46));
            Assert.That(ally.Health, Is.EqualTo(46));
            Assert.That(outside.Health, Is.EqualTo(50));
            Assert.That(battle.Rooms[TrapRoom].Durability, Is.Zero, "시스템 최종 피해 8에 공병 2배를 다시 곱하지 않는다");
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Completed));
            Assert.That(battle.Telegraphs, Is.Empty);
        }

        /// <summary>폭약과 화염구는 시작한 방을 고정하므로 그 방을 빠져나가면 피해를 받지 않는다.</summary>
        [TestCase(RunTelegraphKind.PowderBlast)]
        [TestCase(RunTelegraphKind.Fireball)]
        public void RoomTelegraph_LeavingTargetRoomAvoidsDamage(RunTelegraphKind kind)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind, seconds: 4f);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            Set(target.Definition, "MoveSpeed", 1.2f);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            Assert.That(battle.OrderMove(target, Spring), Is.True);

            battle.Tick(4.1f);

            Assert.That(battle.RoomOf(target), Is.Not.EqualTo(TrapRoom));
            Assert.That(target.Health, Is.EqualTo(50));
            Assert.That(caster.Telegraph.TargetRoom, Is.EqualTo(TrapRoom));
        }

        /// <summary>화염구는 목표 방에 남은 부하만 마법 피해를 받고 같은 방의 적은 맞지 않는다.</summary>
        [Test]
        public void Fireball_HitsOnlyMinionsInTargetRoom()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Fireball);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            RunCombatant another = AddStationaryMinion(battle, TrapRoom);
            RunCombatant ally = battle.AddCombatant(Intruder(health: 50, attack: 0), TrapRoom);
            Set(ally.Definition, "MoveSpeed", 0f);
            RunCombatant outside = AddStationaryMinion(battle, Vault);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);

            battle.Tick(1.05f);

            Assert.That(target.Health, Is.EqualTo(47), "마법 → 물리는 약점 배율 0.75");
            Assert.That(another.Health, Is.EqualTo(47));
            Assert.That(caster.Health, Is.EqualTo(50));
            Assert.That(ally.Health, Is.EqualTo(50));
            Assert.That(outside.Health, Is.EqualTo(50));
        }

        /// <summary>보호로 체력이 깎이지 않은 공격은 폭파·화염구를 끊지 않고 실제 피해 한 점은 끊는다.</summary>
        [TestCase(RunTelegraphKind.PowderBlast)]
        [TestCase(RunTelegraphKind.Fireball)]
        public void Telegraph_OnlyActualHealthLossInterruptsBombAndFireball(RunTelegraphKind kind)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            caster.AddGuard(5);
            battle.DealDamage(target, caster, 5);
            Assert.That(caster.Telegraph.IsActive, Is.True);

            battle.DealDamage(target, caster, 1);

            Assert.That(caster.Telegraph.IsInterrupted, Is.True);
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Damage));
            Assert.That(battle.Telegraphs, Is.Empty);
            Assert.That(caster.SkillCooldownLeft, Is.GreaterThan(0f));
            battle.Tick(1.1f);
            Assert.That(target.Health, Is.EqualTo(50));
        }

        /// <summary>함정 피해도 같은 실제 피해 경로를 지나 예고를 끊는 대응이 되어야 한다.</summary>
        [TestCase(RunTelegraphKind.PowderBlast)]
        [TestCase(RunTelegraphKind.Fireball)]
        public void Trap_InterruptsBombAndFireballWarning(RunTelegraphKind kind)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            battle.SetMana(TrapRoom, 1);

            battle.Tick(Step);

            Assert.That(caster.Health, Is.EqualTo(48));
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Damage));
            Assert.That(target.Health, Is.EqualTo(50));
        }

        /// <summary>화염구는 처음 잡은 경로의 문을 잠그면 우회로가 있어도 즉시 취소돼야 한다.</summary>
        [Test]
        public void Fireball_LockingCapturedNorthCorridorCancelsImmediately()
        {
            CheckCapturedDoor(NewBattle());
            // 시연 십자는 우회로가 없지만, 경로 고정이라는 기존 검증 의도도 유지한다.
            RunBattle alternate = AlternatePowerBattle();
            Assert.That(LinkBetween(alternate, 0, 3), Is.GreaterThanOrEqualTo(0));
            Assert.That(LinkBetween(alternate, 3, 4), Is.GreaterThanOrEqualTo(0));
            Assert.That(LinkBetween(alternate, 4, 1), Is.GreaterThanOrEqualTo(0));
            CheckCapturedDoor(alternate);
        }

        private void CheckCapturedDoor(RunBattle battle)
        {
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Fireball, room: North);
            Set(caster, "Position", RunPosition.OnLink(LinkBetween(battle, North, TrapRoom), 1f));
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            RunAxis axis = battle.Links[LinkBetween(battle, North, TrapRoom)].Axis;
            Set(target, "Position", RunPosition.InRoom(TrapRoom, axis, 0f));
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            int captured = caster.Telegraph.PathCount;
            Assert.That(captured, Is.GreaterThan(0));
            battle.SetMana(DoorControl, 1);
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.LockedDoor));
            Assert.That(battle.Telegraphs, Is.Empty);
            Assert.That(caster.Telegraph.PathCount, Is.EqualTo(captured));
            battle.Tick(1.1f);
            Assert.That(target.Health, Is.EqualTo(50));
        }

        /// <summary>사격은 대상 하나에만 피해를 주며 실제 피해를 받아도 시전자가 살아 있으면 예고를 유지한다.</summary>
        [Test]
        public void AimedShot_HitsOnlyLockedTargetAndSurvivesNonlethalDamage()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.AimedShot);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            RunCombatant other = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            battle.DealDamage(target, caster, 1);
            Assert.That(caster.Telegraph.IsActive, Is.True);
            battle.Tick(0.5f);
            Assert.That(target.Health, Is.EqualTo(50));

            battle.Tick(0.55f);

            Assert.That(target.Health, Is.EqualTo(46));
            Assert.That(other.Health, Is.EqualTo(50));
        }

        /// <summary>대상을 다른 방으로 빼면 사거리 안에 남아 있어도 조준 사격을 피할 수 있어야 한다.</summary>
        [Test]
        public void AimedShot_MissesWhenTargetLeavesRoomEvenWithinRange()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.AimedShot);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            Set(target, "Position", RunPosition.OnLink(LinkBetween(battle, TrapRoom, Vault), 0f));
            Assert.That(battle.Distance(caster.Position, target.Position), Is.LessThanOrEqualTo(battle.RangeOf(caster)));

            battle.Tick(1.05f);

            Assert.That(target.Health, Is.EqualTo(50));
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.TargetEscaped));
        }

        /// <summary>같은 방 안에서도 공격 거리 밖으로 물러난 부하는 조준 사격을 피해야 한다.</summary>
        [Test]
        public void AimedShot_MissesWhenTargetMovesOutOfRangeInSameRoom()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.AimedShot);
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, 0f));
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            Set(target, "Position", RunPosition.InRoom(TrapRoom, 3f));

            battle.Tick(1.05f);

            Assert.That(target.Health, Is.EqualTo(50));
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.TargetEscaped));
        }

        /// <summary>돌진은 같은 가로줄 앞쪽만 휩쓸고 사다리 쪽 다른 줄이나 뒤쪽 인물은 치지 않는다.</summary>
        [Test]
        public void Charge_HitsForwardHorizontalLineOnceAndExcludesOtherRowsAndBehind()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Charge);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            Set(target, "Position", battle.CenterOf(TrapRoom));
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, 2f));
            RunCombatant far = AddStationaryMinion(battle, Spring);
            RunCombatant outside = AddStationaryMinion(battle, Heart);
            RunCombatant behind = AddStationaryMinion(battle, Vault);
            RunCombatant ally = battle.AddCombatant(Intruder(health: 50, attack: 0), North);
            Set(ally.Definition, "MoveSpeed", 0f);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            Assert.That(caster.Telegraph.Scope, Is.EqualTo(RunTelegraphScope.StraightLine));
            battle.DealDamage(target, caster, 1);
            Assert.That(caster.Telegraph.IsActive, Is.True);
            battle.Tick(0.5f);
            Assert.That(far.Health, Is.EqualTo(50));

            battle.Tick(0.55f);

            Assert.That(target.Health, Is.EqualTo(46), "중앙 지점이 두 경로 조각에 겹쳐도 한 번만 맞는다");
            Assert.That(far.Health, Is.EqualTo(46));
            Assert.That(outside.Health, Is.EqualTo(50));
            Assert.That(behind.Health, Is.EqualTo(50));
            Assert.That(ally.Health, Is.EqualTo(50));
            Assert.That(caster.Position.Room, Is.EqualTo(Spring));
            Assert.That(caster.Position.Offset, Is.Zero.Within(0.001f));
        }

        /// <summary>가로 경로의 문을 예고 도중 잠그면 돌진은 문 앞에서 멈추고 뒤쪽 부하는 맞지 않는다.</summary>
        [Test]
        public void Charge_StopsAtDoorLockedAfterWarningStarts()
        {
            RunBattle battle = new(new RunBattleSettings(), new RunRoomLayout[]
            {
                new(RunRoomKind.DoorControl, 0, 0), new(RunRoomKind.Trap, 0, 1), new(RunRoomKind.Heart, 0, 2),
            });
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Charge, room: 0);
            Set(caster, "Position", RunPosition.InRoom(0, 3f));
            RunCombatant nearby = AddStationaryMinion(battle, 0);
            Set(nearby, "Position", RunPosition.OnLink(0, 0.2f));
            RunCombatant target = AddStationaryMinion(battle, 1);
            Set(target, "Position", RunPosition.OnLink(0, 2f));
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            battle.SetMana(0, 1);
            Assert.That(caster.Telegraph.IsActive, Is.True);

            battle.Tick(1.05f);

            Assert.That(nearby.Health, Is.EqualTo(46));
            Assert.That(target.Health, Is.EqualTo(50));
            Assert.That(caster.Position.Link, Is.EqualTo(0));
            Assert.That(caster.Position.Offset, Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(battle.Doors[0].Durability, Is.EqualTo(10));
        }

        /// <summary>모든 예고는 시전자가 쓰러지면 즉시 목록에서 사라지고 나중에 피해를 주지 않아야 한다.</summary>
        [TestCase(RunTelegraphKind.PowderBlast)]
        [TestCase(RunTelegraphKind.Fireball)]
        [TestCase(RunTelegraphKind.Charge)]
        [TestCase(RunTelegraphKind.AimedShot)]
        public void Telegraph_CasterDeathRemovesWarningWithoutPayload(RunTelegraphKind kind)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);

            battle.DealDamage(null, caster, 99);

            Assert.That(battle.Telegraphs, Is.Empty);
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.CasterLeft));
            battle.Tick(2f);
            Assert.That(target.Health, Is.EqualTo(50));
        }

        /// <summary>큰 기술도 상성을 쓰되 화염구만 시전자 몸 상성과 관계없이 마법이어야 한다.</summary>
        [TestCase(RunTelegraphKind.PowderBlast, 6)]
        [TestCase(RunTelegraphKind.Fireball, 3)]
        [TestCase(RunTelegraphKind.Charge, 6)]
        [TestCase(RunTelegraphKind.AimedShot, 6)]
        public void Telegraph_UsesCasterAffinityExceptMagicFireball(RunTelegraphKind kind, int expectedDamage)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind, affinity: RunAffinity.Curse);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);

            battle.Tick(1.05f);

            Assert.That(target.Health, Is.EqualTo(50 - expectedDamage));
            Assert.That(caster.Affinity, Is.EqualTo(RunAffinity.Curse));
        }

        /// <summary>예고 중 이동을 멈추되 공격 간격이 된 평타는 계속한다는 사용자 선택을 지킨다.</summary>
        [Test]
        public void Telegraph_StopsMovementButAllowsBasicAttacksDuringCountdown()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.AimedShot, seconds: 4f);
            Set(caster.Definition, "Attack", 1);
            Set(caster.Definition, "Speed", 6);
            Set(caster.Definition, "MoveSpeed", 1.2f);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            RunPosition origin = caster.Position;
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);

            battle.Tick(1.1f);

            Assert.That(caster.Telegraph.IsActive, Is.True);
            Assert.That(battle.Distance(caster.Position, origin), Is.Zero);
            Assert.That(target.Health, Is.EqualTo(49));
            Assert.That(caster.Telegraph.Sequence, Is.EqualTo(1), "평타로 예고를 중복해서 만들지 않는다");
        }

        /// <summary>화면용 목록과 상태를 재사용하고 쿨 뒤에는 연번으로 새 예고를 구분할 수 있어야 한다.</summary>
        [Test]
        public void Telegraph_ReusesReadonlyViewAndStateAcrossCooldownCycles()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.AimedShot, seconds: 0.1f);
            Set(caster.Definition, "Speed", 6);
            Set(caster.Definition.Skill, "CooldownTurns", 1);
            AddStationaryMinion(battle, TrapRoom);
            IReadOnlyList<RunTelegraph> view = battle.Telegraphs;
            RunTelegraph state = caster.Telegraph;
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            Assert.That(((IList<RunTelegraph>)view).IsReadOnly, Is.True);

            battle.Tick(1.05f);

            Assert.That(caster.Telegraph, Is.SameAs(state));
            Assert.That(battle.Telegraphs, Is.SameAs(view));
            Assert.That(state.Sequence, Is.EqualTo(2));
            Assert.That(state.IsActive, Is.True);
        }

        /// <summary>유효 대상이 없을 때 기술을 허공에 쓰고 쿨을 잃지 않게 한다.</summary>
        [TestCase(RunTelegraphKind.Fireball)]
        [TestCase(RunTelegraphKind.Charge)]
        [TestCase(RunTelegraphKind.AimedShot)]
        public void Telegraph_DoesNotStartWithoutValidTarget(RunTelegraphKind kind)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.False);
            Assert.That(caster.SkillCooldownLeft, Is.Zero);
            Assert.That(battle.Telegraphs, Is.Empty);
        }

        /// <summary>정상 시스템만 있는 빈 방에서도 공병은 폭약을 놓을 수 있지만 시스템도 없는 방에서는 쓰지 않는다.</summary>
        [Test]
        public void PowderBlast_StartsForIntactSystemWithoutMinionButNotTunnel()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.PowderBlast);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            RunCombatant outside = AddTelegrapher(battle, RunTelegraphKind.PowderBlast, room: North);
            Set(outside, "Position", RunPosition.AtEntrance(0.5f));
            Assert.That(battle.TryBeginTelegraph(outside, outside.Telegraph.Effect), Is.False);
        }

        /// <summary>
        /// 공병은 방에 들어오자마자 끝에서 폭약을 놓지 않고, 그 방 부하를 무시한 채 가운데까지 걸어가 예고를 시작한다.
        /// 이웃 방 부하가 예고 안에 닿을 시간을 주기 위한 규칙이다(2026-11-09 사용자 결정).
        /// </summary>
        [Test]
        public void PowderBlast_SapperWalksPastDefenderToRoomCenterBeforeWarning()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.PowderBlast);
            Set(caster.Definition, "MoveSpeed", 1f);
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, RunAxis.Vertical, 0f));
            RunCombatant defender = AddStationaryMinion(battle, TrapRoom);

            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.False, "방 끝에서는 시작하지 않는다");

            battle.Tick(1f);

            Assert.That(caster.Telegraph.IsActive, Is.False);
            Assert.That(caster.Activity, Is.EqualTo(RunActionKind.Move), "사거리 안 부하와 싸우지 않고 걷는다");

            battle.Tick(0.6f);

            Assert.That(caster.Telegraph.IsActive, Is.True);
            Assert.That(caster.Telegraph.TargetRoom, Is.EqualTo(TrapRoom));
            Assert.That(battle.Distance(caster.Position, battle.CenterOf(TrapRoom)), Is.LessThan(0.01f));
            Assert.That(defender.Health, Is.EqualTo(50));
        }

        /// <summary>
        /// 폭파를 예고 중인 공병이 있는 방으로 보낸 부하는 길을 막은 적을 지나쳐 공병부터 쳐서 예고를 끊는다.
        /// 위기 때 이웃 방 부하를 보내 폭파를 막는 대응이 성립하게 하는 규칙이다(2026-11-09 사용자 결정).
        /// </summary>
        [Test]
        public void PowderBlast_OrderedMinionPassesBlockerAndInterruptsCaster()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.PowderBlast, room: North, seconds: 4f);
            RunCombatantSO blockerData = Intruder(health: 99, attack: 0);
            Set(blockerData, "MoveSpeed", 0f);
            RunCombatant blocker = battle.AddCombatant(blockerData, TrapRoom);
            Set(blocker, "Position", RunPosition.OnLink(LinkBetween(battle, North, TrapRoom), 1.5f));
            // 가까운 적을 고르는 피해 스킬도 시전자를 먼저 맞혀야 한다.
            RunCombatantSO sentData = Minion(health: 50, attack: 2, ranged: true, skill: DamageSkill(1, 3));
            Set(sentData, "MoveSpeed", 2f);
            RunCombatant sent = battle.AddCombatant(sentData, TrapRoom);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);

            Assert.That(battle.OrderMove(sent, North), Is.True);
            for (int i = 0; i < 80 && caster.Telegraph.IsActive; i++) battle.Tick(0.05f);

            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Damage));
            Assert.That(caster.Health, Is.LessThan(50));
            Assert.That(blocker.Health, Is.EqualTo(99), "길을 막은 적은 지나친다");
        }

        /// <summary>
        /// 시전자에게 가려고 적을 지나친 부하는 끊은 뒤 새 이동 명령을 받으면 그 적을 다시 지나쳐 돌아온다.
        /// 적 사이에 끼인 부하를 플레이어가 불러들여 살릴 수 있게 하는 규칙이다(2026-11-09 사용자 결정).
        /// </summary>
        [Test]
        public void PowderBlast_MinionThatSlippedPastReturnsPastBlockerAfterInterrupt()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.PowderBlast, room: North, seconds: 4f);
            RunCombatantSO blockerData = Intruder(health: 99, attack: 0);
            Set(blockerData, "MoveSpeed", 0f);
            RunCombatant blocker = battle.AddCombatant(blockerData, TrapRoom);
            Set(blocker, "Position", RunPosition.OnLink(LinkBetween(battle, North, TrapRoom), 1.5f));
            RunCombatantSO sentData = Minion(health: 50, attack: 2, ranged: true);
            Set(sentData, "MoveSpeed", 2f);
            RunCombatant sent = battle.AddCombatant(sentData, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            battle.OrderMove(sent, North);
            for (int i = 0; i < 80 && caster.Telegraph.IsActive; i++) battle.Tick(0.05f);
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Damage));
            Assert.That(sent.HasSlippedPast, Is.True);

            Assert.That(battle.OrderMove(sent, TrapRoom), Is.True);
            Assert.That(sent.IsSlippingBack, Is.True);
            for (int i = 0; i < 100 && sent.HasMoveOrder; i++) battle.Tick(0.05f);

            Assert.That(sent.HasMoveOrder, Is.False, "막은 적에게 붙잡히지 않고 도착한다");
            Assert.That(battle.RoomOf(sent), Is.EqualTo(TrapRoom));
            Assert.That(blocker.Health, Is.EqualTo(99));
            Assert.That(sent.HasSlippedPast, Is.False, "돌아오면 지나치기가 끝난다");
        }

        /// <summary>제한 시간으로 퇴장한 시전자의 예고가 전투 뒤 화면에 남지 않게 한다.</summary>
        [Test]
        public void Telegraph_BattleEndClearsActiveWarnings()
        {
            RunBattleSettings settings = new();
            Set(settings, "TimeLimitSeconds", 0.5f);
            RunBattle battle = new(settings, Layout);
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.AimedShot, seconds: 4f);
            AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);

            battle.Tick(0.6f);

            Assert.That(battle.Telegraphs, Is.Empty);
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.BattleEnded));
        }

        /// <summary>폭파 시스템 피해는 별도 최종값이므로 작은 값에서도 공병의 2배가 중복되지 않아야 한다.</summary>
        [Test]
        public void PowderBlast_SystemDamageIsFinalValueWithoutSapperMultiplier()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.PowderBlast, systemDamage: 4);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            battle.Tick(1.05f);
            Assert.That(battle.Rooms[TrapRoom].Durability, Is.EqualTo(4f));
        }

        /// <summary>폭파로 다른 예고가 취소돼도 활성 목록 순회가 깨지거나 취소된 화염구가 발동하지 않게 한다.</summary>
        [Test]
        public void PowderBlast_InterruptingAnotherWarningDoesNotInvalidateCountdownLoop()
        {
            RunBattle battle = NewBattle();
            RunCombatant bomb = AddTelegrapher(battle, RunTelegraphKind.PowderBlast);
            RunCombatant fire = AddTelegrapher(battle, RunTelegraphKind.Fireball);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(bomb, bomb.Telegraph.Effect);
            battle.TryBeginTelegraph(fire, fire.Telegraph.Effect);

            battle.Tick(1.05f);

            Assert.That(target.Health, Is.EqualTo(46));
            Assert.That(fire.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Damage));
            Assert.That(bomb.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Completed));
            Assert.That(battle.Telegraphs, Is.Empty);
        }

        /// <summary>돌진 예고를 본 부하가 사다리로 다른 줄에 빠지면 고정된 가로 경로의 피해를 피해야 한다.</summary>
        [Test]
        public void Charge_MovingToOtherRowAfterWarningAvoidsDamage()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Charge);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            Set(target, "Position", battle.CenterOf(Heart));
            battle.Tick(1.05f);
            Assert.That(target.Health, Is.EqualTo(50));
            Assert.That(caster.Position.Room, Is.EqualTo(Spring));
        }

        /// <summary>조준 대상이 먼저 쓰러져도 다른 부하로 자동 조준을 바꿔 피해를 주지 않아야 한다.</summary>
        [Test]
        public void AimedShot_DoesNotRetargetAfterLockedTargetFalls()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.AimedShot);
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            RunCombatant other = AddStationaryMinion(battle, TrapRoom);
            battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            battle.DealDamage(null, target, 99);
            battle.Tick(1.05f);
            Assert.That(other.Health, Is.EqualTo(50));
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.TargetEscaped));
        }

        /// <summary>네 기술 모두 예고 시작·초읽기·발동에 준비된 상태와 경로 버퍼를 재사용해야 한다.</summary>
        [TestCase(RunTelegraphKind.PowderBlast)]
        [TestCase(RunTelegraphKind.Fireball)]
        [TestCase(RunTelegraphKind.Charge)]
        [TestCase(RunTelegraphKind.AimedShot)]
        public void Telegraph_StartCountdownAndPayloadDoNotAllocate(RunTelegraphKind kind)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind);
            AddStationaryMinion(battle, TrapRoom);
            battle.Distance(caster.Position, battle.CenterOf(North));
            long before = System.GC.GetAllocatedBytesForCurrentThread();

            bool started = battle.TryBeginTelegraph(caster, caster.Telegraph.Effect);
            battle.Tick(1.05f);

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(started, Is.True);
            Assert.That(allocated, Is.Zero);
            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Completed));
        }

        /// <summary>실제 적 데이터에 네 기술과 승인한 수치가 들어가고 부하의 즉시 사격이 유지되는지 확인한다.</summary>
        [Test]
        public void Encounter_StoresApprovedTelegraphsAndMagePartyWithoutReplacingMinionSkill()
        {
            string root = "Assets/07.SO/Run/";
            string[] actors = { "RunIntruder_Sapper", "RunIntruder_ApprenticeMage", "RunIntruder_Sword", "RunIntruder_Archer" };
            float[] seconds = { 4f, 3.5f, 2.5f, 4f };
            int[] damage = { 8, 6, 5, 7 };
            int[] cooldown = { 6, 5, 4, 4 };
            for (int i = 0; i < actors.Length; i++)
            {
                RunCombatantSO actor = UnityEditor.AssetDatabase.LoadAssetAtPath<RunCombatantSO>(root + "Combatants/" + actors[i] + ".asset");
                Assert.That(actor.Skill, Is.Not.Null);
                Assert.That(actor.Skill.CooldownTurns, Is.EqualTo(cooldown[i]));
                Assert.That(actor.Skill.TurnEffects, Has.Length.EqualTo(1));
                RunTelegraphSkillEffectSO effect = actor.Skill.TurnEffects[0] as RunTelegraphSkillEffectSO;
                Assert.That(effect, Is.Not.Null);
                Assert.That(effect.Kind, Is.EqualTo((RunTelegraphKind)i));
                Assert.That(effect.WarningSeconds, Is.EqualTo(seconds[i]));
                Assert.That(effect.Damage, Is.EqualTo(damage[i]));
                Assert.That(effect.SystemDamage, Is.EqualTo(i == 0 ? 8 : 0));
            }
            RunEncounterSO encounter = UnityEditor.AssetDatabase.LoadAssetAtPath<RunEncounterSO>(root + "RunEncounter_Stage1.asset");
            Assert.That(encounter.Party, Has.Length.EqualTo(5));
            Assert.That(encounter.Party[4].Combatant.name, Is.EqualTo("RunIntruder_ApprenticeMage"));
            Assert.That(encounter.Party[4].OverrideAffinity, Is.True);
            Assert.That(encounter.Party[4].Affinity, Is.EqualTo(RunAffinity.Magic));
            Assert.That(encounter.Party[4].Combatant.Affinity, Is.EqualTo(RunAffinity.Magic));
            RunCombatantSO sniper = UnityEditor.AssetDatabase.LoadAssetAtPath<RunCombatantSO>(root + "Combatants/RunMinion_SkeletonSniper.asset");
            Assert.That(sniper.Skill.TurnEffects[0], Is.TypeOf<RunDamageSkillEffectSO>());
        }

        /// <summary>복도를 걷는 부하도 공격 거리 안이면 돌진·사격 대상으로 잡을 수 있어야 한다.</summary>
        [TestCase(RunTelegraphKind.Charge)]
        [TestCase(RunTelegraphKind.AimedShot)]
        public void Telegraph_CanAcquireMinionWalkingInHorizontalCorridor(RunTelegraphKind kind)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, kind);
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, 3f));
            RunCombatant target = AddStationaryMinion(battle, North);
            int link = LinkBetween(battle, TrapRoom, Vault);
            Set(target, "Position", RunPosition.OnLink(link, 0.5f));
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            battle.Tick(1.05f);
            Assert.That(target.Health, Is.EqualTo(46));
        }

        /// <summary>굴은 별도 방을 만들지 않고 북 방 가장자리까지 실제로 걷는다.</summary>
        [Test]
        public void Entry_TunnelWalksIntoNorthRoomWithoutAddingRoom()
        {
            RunBattle battle = NewBattle();
            RunCombatant actor = battle.AddIntruderAtEntrance(Intruder(health: 50, attack: 0));
            Assert.That(actor.Position.IsAtEntrance, Is.True);
            Assert.That(battle.RoomOf(actor), Is.EqualTo(-1));
            battle.Tick(0.25f);
            Assert.That(actor.Position.IsAtEntrance, Is.True);
            Assert.That(actor.Position.Offset, Is.InRange(0.29f, 0.31f));
            battle.Tick(0.65f);
            Assert.That(battle.RoomOf(actor), Is.EqualTo(North));
            Assert.That(actor.Position.Axis, Is.EqualTo(RunAxis.Vertical));
            Assert.That(battle.Rooms.Count, Is.EqualTo(5));
        }

        /// <summary>북 방의 방어 벌점은 굴에서 진입할 때도 적용한다.</summary>
        [Test]
        public void Entry_TunnelIncludesDistanceAndNorthDefensePenalty()
        {
            RunBattle battle = NewBattle();
            RunPosition outside = battle.EntrancePosition;
            Assert.That(battle.Distance(outside, RunPosition.InRoom(North, 1.5f)), Is.EqualTo(2.5f));
            Assert.That(battle.RouteCost(outside, North), Is.EqualTo(2.5f));
            battle.AddCombatant(Minion(), North);
            Assert.That(battle.RouteCost(outside, North), Is.EqualTo(8.5f));
        }

        /// <summary>도적은 북 방에 닿은 순간이 아니라 굴 바깥 끝에 닿아야 퇴장한다.</summary>
        [Test]
        public void Thief_LeavesAtOuterTunnelEnd()
        {
            RunBattle battle = NewBattle();
            RunCombatant actor = battle.AddCombatant(Intruder(attack: 0, goal: RunGoal.Vault), North);
            Set(actor, "IsEscaping", true);
            Set(actor, "Position", RunPosition.AtEntrance(0.9f));
            battle.Tick(0.25f);
            Assert.That(actor.IsOnField, Is.True);
            Assert.That(actor.Position.Offset, Is.InRange(0.59f, 0.61f));
            battle.Tick(0.6f);
            Assert.That(actor.HasLeft, Is.True);
        }

        /// <summary>남북 복도도 동서와 같은 사거리·피해 규칙으로 싸운다.</summary>
        [Test]
        public void Combat_NorthSouthCorridorAllowsMelee()
        {
            RunBattle battle = NewBattle();
            RunCombatant ally = AddStationaryMinion(battle, TrapRoom);
            RunCombatant foe = battle.AddCombatant(Intruder(attack: 2), North);
            int link = LinkBetween(battle, North, TrapRoom);
            Set(ally, "Position", RunPosition.OnLink(link, 1.5f));
            Set(foe, "Position", RunPosition.OnLink(link, 1f));
            battle.Tick(Step);
            Assert.That(ally.Health, Is.EqualTo(48));
            Assert.That(foe.Target, Is.SameAs(ally));
        }

        /// <summary>굴도 사거리와 교전 경로에 포함된다.</summary>
        [Test]
        public void Combat_EntranceTunnelAllowsMelee()
        {
            RunBattle battle = NewBattle();
            RunCombatant ally = AddStationaryMinion(battle, North);
            RunCombatant foe = battle.AddCombatant(Intruder(attack: 2), North);
            Set(ally, "Position", RunPosition.InRoom(North, RunAxis.Vertical, 0f));
            Set(foe, "Position", RunPosition.AtEntrance(0.6f));
            battle.Tick(Step);
            Assert.That(ally.Health, Is.EqualTo(48));
            Assert.That(battle.RoomOf(foe), Is.EqualTo(-1));
        }

        /// <summary>세로 돌진은 남북 줄에서만 한 번씩 맞히고 다른 팔과 뒤쪽은 제외한다.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Charge_VerticalLineHitsForwardOnly(bool south)
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Charge);
            Set(caster, "Position", RunPosition.InRoom(TrapRoom, RunAxis.Vertical, south ? 1f : 2f));
            RunCombatant near = AddStationaryMinion(battle, TrapRoom);
            RunCombatant far = AddStationaryMinion(battle, south ? Heart : North);
            RunCombatant behind = AddStationaryMinion(battle, south ? North : Heart);
            RunCombatant otherColumn = AddStationaryMinion(battle, Vault);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            battle.Tick(1.05f);
            Assert.That(near.Health, Is.EqualTo(46));
            Assert.That(far.Health, Is.EqualTo(46));
            Assert.That(behind.Health, Is.EqualTo(50));
            Assert.That(otherColumn.Health, Is.EqualTo(50));
            Assert.That(caster.Position.Room, Is.EqualTo(south ? Heart : North));
            Assert.That(caster.Position.Axis, Is.EqualTo(RunAxis.Vertical));
        }

        /// <summary>북 복도의 문은 세로 돌진도 문 앞에서 멈춘다.</summary>
        [Test]
        public void Charge_NorthDoorStopsVerticalLine()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Charge, North);
            Set(caster, "Position", RunPosition.InRoom(North, RunAxis.Vertical, 3f));
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            Set(target, "Position", RunPosition.InRoom(TrapRoom, RunAxis.Vertical, 0f));
            Set(battle.Settings, "RangedRange", 5f);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);
            battle.SetMana(North, 1);
            battle.Tick(1.05f);
            Assert.That(caster.Position.Link, Is.EqualTo(LinkBetween(battle, North, TrapRoom)));
            Assert.That(caster.Position.Offset, Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(50));
        }

        /// <summary>
        /// 돌진은 예고를 시작할 때 잠겨 있던 문을 기억한다. 발동 전에 문 제어실이 꺼지거나 부서져도 그 문 앞에서 멈춘다.
        /// 플레이어가 예고 화면만 보고 안전을 판단할 수 있게 하는 규칙이다(2026-11-09 사용자 결정).
        /// </summary>
        [Test]
        public void Charge_DoorLockedAtWarningStartStillStopsAfterUnlock()
        {
            RunBattle battle = NewBattle();
            RunCombatant caster = AddTelegrapher(battle, RunTelegraphKind.Charge, North);
            Set(caster, "Position", RunPosition.InRoom(North, RunAxis.Vertical, 3f));
            RunCombatant target = AddStationaryMinion(battle, TrapRoom);
            Set(target, "Position", RunPosition.InRoom(TrapRoom, RunAxis.Vertical, 0f));
            Set(battle.Settings, "RangedRange", 5f);
            battle.SetMana(North, 1);
            int link = LinkBetween(battle, North, TrapRoom);
            Assert.That(battle.IsDoorLocked(link), Is.True);
            Assert.That(battle.TryBeginTelegraph(caster, caster.Telegraph.Effect), Is.True);

            battle.SetMana(North, 0);
            Assert.That(battle.IsDoorLocked(link), Is.False);
            battle.Tick(1.05f);

            Assert.That(caster.Telegraph.EndReason, Is.EqualTo(RunTelegraphEndReason.Completed));
            Assert.That(caster.Position.Link, Is.EqualTo(link));
            Assert.That(caster.Position.Offset, Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(50));
        }

        private RunCombatant AddTelegrapher(RunBattle battle, RunTelegraphKind kind, int room = TrapRoom,
            float seconds = 1f, int systemDamage = 0, RunAffinity affinity = RunAffinity.Physical)
        {
            RunTelegraphSkillEffectSO effect = Make<RunTelegraphSkillEffectSO>();
            Set(effect, "Kind", kind);
            Set(effect, "WarningSeconds", seconds);
            Set(effect, "Damage", 4);
            Set(effect, "SystemDamage", systemDamage);
            SkillDataSO skill = Make<SkillDataSO>();
            Set(skill, "CooldownTurns", 6);
            Set(skill, "TurnEffects", new RunTurnSkillEffectSO[] { effect });
            RunCombatantSO data = Intruder(health: 50, attack: 0, ranged: true,
                role: kind == RunTelegraphKind.PowderBlast ? RunIntruderRole.Sapper : RunIntruderRole.None);
            Set(data, "Skill", skill);
            Set(data, "MoveSpeed", 0f);
            RunCombatant caster = battle.AddCombatant(data, room, affinity);
            if (kind == RunTelegraphKind.Charge) Set(caster, "Position", RunPosition.InRoom(room, 2f));
            return caster;
        }

        private RunCombatant AddStationaryMinion(RunBattle battle, int room)
        {
            RunCombatantSO data = Minion(health: 50, attack: 0);
            Set(data, "MoveSpeed", 0f);
            return battle.AddCombatant(data, room);
        }

        private static int LinkBetween(RunBattle battle, int a, int b)
        {
            foreach (RunLink link in battle.Links)
            {
                if ((link.From == a && link.To == b) || (link.From == b && link.To == a)) return link.Index;
            }
            Assert.Fail($"방 {a}와 {b} 사이 연결이 없습니다.");
            return -1;
        }

        private static RunBattle NewBattle() => new(new RunBattleSettings(), Layout);

        private static RunBattle BottleneckBattle() => new(new RunBattleSettings(), new RunRoomLayout[]
        {
            new(RunRoomKind.Vault, 0, 0), new(RunRoomKind.Trap, 0, 1),
            new(RunRoomKind.HealingSpring, 0, 2), new(RunRoomKind.Heart, 1, 2),
        });

        private static RunBattle DoorOnlyBattle() => new(new RunBattleSettings(), new RunRoomLayout[]
        {
            new(RunRoomKind.DoorControl, 0, 0), new(RunRoomKind.Trap, 1, 0),
            new(RunRoomKind.Vault, 1, 1), new(RunRoomKind.Heart, 1, 2),
        });

        private static RunBattle RoutingBattle() => new(new RunBattleSettings(), new RunRoomLayout[]
        {
            new(RunRoomKind.DoorControl, 0, 0), new(RunRoomKind.Trap, 0, 1),
            new(RunRoomKind.DoorControl, 0, 2), new(RunRoomKind.Trap, 0, 3),
            new(RunRoomKind.Vault, 1, 0), new(RunRoomKind.Vault, 1, 1),
            new(RunRoomKind.Vault, 1, 2), new(RunRoomKind.Heart, 1, 3),
        });

        private static RunBattle AlternatePowerBattle() => new(new RunBattleSettings(), new RunRoomLayout[]
        {
            new(RunRoomKind.DoorControl, 0, 0), new(RunRoomKind.Trap, 0, 1), new(RunRoomKind.HealingSpring, 0, 2),
            new(RunRoomKind.Vault, 1, 0), new(RunRoomKind.Vault, 1, 1), new(RunRoomKind.Heart, 1, 2),
        });

        /// <summary>침입자가 모두 빠지면 전투가 끝나므로, 심장에서 서로 때리지 않는 한 쌍을 세워 둔다.</summary>
        private void KeepBattleGoing(RunBattle battle)
        {
            battle.AddCombatant(Minion(attack: 0), Heart);
            battle.AddCombatant(Intruder(health: 99, attack: 0), Heart);
        }

        private RunCombatantSO Minion(int health = 10, int attack = 1, int speed = 1, bool ranged = false,
            float repair = 0f, RunRoomKind? favored = null, SkillDataSO skill = null)
        {
            RunCombatantSO data = Make<RunCombatantSO>();
            Set(data, "Side", RunSide.Minion);
            FillStats(data, health, attack, speed, ranged);
            Set(data, "RepairPerSecond", repair);
            Set(data, "HasFavoredRoom", favored.HasValue);
            if (favored.HasValue) Set(data, "FavoredRoom", favored.Value);
            Set(data, "Skill", skill);
            return data;
        }

        private RunCombatantSO Intruder(int health = 10, int attack = 1, int speed = 1, bool ranged = false,
            RunGoal goal = RunGoal.Heart, RunIntruderRole role = RunIntruderRole.None)
        {
            RunCombatantSO data = Make<RunCombatantSO>();
            Set(data, "Side", RunSide.Intruder);
            FillStats(data, health, attack, speed, ranged);
            Set(data, "Goal", goal);
            Set(data, "IntruderRole", role);
            return data;
        }

        private SkillDataSO DamageSkill(int cooldownTurns, int amount)
        {
            RunDamageSkillEffectSO effect = Make<RunDamageSkillEffectSO>();
            Set(effect, "Amount", amount);
            SkillDataSO skill = Make<SkillDataSO>();
            Set(skill, "CooldownTurns", cooldownTurns);
            Set(skill, "TurnEffects", new RunTurnSkillEffectSO[] { effect });
            return skill;
        }

        private static void FillStats(RunCombatantSO data, int health, int attack, int speed, bool ranged)
        {
            Set(data, "MaxHealth", health);
            Set(data, "Attack", attack);
            Set(data, "Speed", speed);
            Set(data, "IsRanged", ranged);
        }

        private T Make<T>() where T : ScriptableObject
        {
            T created = ScriptableObject.CreateInstance<T>();
            _created.Add(created);
            return created;
        }

        private static void Set(object target, string property, object value)
        {
            FieldInfo field = target.GetType().GetField($"<{property}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{property} 를 찾지 못했습니다.");
            field.SetValue(target, value);
        }
    }
}
