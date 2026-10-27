using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    public class SettlementRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<UnityEngine.Object> created = new();
        private object previousRoster;
        private object previousCost;
        private object previousSettlement;
        private float previousTimeScale = 1f;

        private static Type Resolve(string name) => Type.GetType(name + ", DungeonKeeper.Runtime", true);
        private static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private Component Component(string typeName, string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            created.Add(go);
            if (typeName == "Code.MapCreateSystem.Node")
                go.AddComponent<BoxCollider2D>();
            var component = go.AddComponent(Resolve(typeName));
            Assert.That(component, Is.Not.Null);
            return component;
        }

        private ScriptableObject UnitData(string name, int cost)
        {
            var data = ScriptableObject.CreateInstance(Resolve("Code.Units.UnitDataSO"));
            created.Add(data);
            Set(data, "<Name>k__BackingField", name);
            Set(data, "<Cost>k__BackingField", cost);
            return data;
        }

        private Component Unit(ScriptableObject data, float fatigue)
        {
            var unit = Component("Code.Units.Unit", data.name);
            Set(unit, "<Data>k__BackingField", data);
            Set(unit, "fatigue", fatigue);
            return unit;
        }

        private static object Event(string name, params object[] args) =>
            Activator.CreateInstance(Resolve("Code.Events." + name), args);

        /// <summary>정산 보고서의 피로 줄을 한 줄로 모은다. 화면이 아니라 보고서가 무엇을 담았는지를 본다.</summary>
        private static string FatigueSummary(object manager)
        {
            var report = Call(manager, "BuildReport");
            var lines = (IEnumerable)report.GetType().GetProperty("Fatigue").GetValue(report);
            var text = new StringBuilder();
            foreach (var line in lines)
            {
                var type = line.GetType();
                text.Append(type.GetProperty("Label").GetValue(line));
                text.Append(' ');
                text.Append(type.GetProperty("Fatigue").GetValue(line));
                text.Append("  ");
            }

            return text.ToString();
        }

        [SetUp]
        public void SetUp()
        {
            previousRoster = Resolve("Code.Manager.HiredUnitRoster").GetProperty("Current").GetValue(null);
            previousCost = Resolve("Code.Manager.CostManager").GetProperty("Current").GetValue(null);
            previousSettlement = Resolve("Code.Manager.ManagementSettlementManager").GetProperty("Current").GetValue(null);
            previousTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
                UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();
            Resolve("Code.Manager.HiredUnitRoster").GetProperty("Current").SetValue(null, previousRoster);
            Resolve("Code.Manager.CostManager").GetProperty("Current").SetValue(null, previousCost);
            Resolve("Code.Manager.ManagementSettlementManager").GetProperty("Current").SetValue(null, previousSettlement);

            // 배속 테스트는 전역 시간을 건드린다. 되돌리지 않으면 뒤따르는 테스트가 멈춘 채로 돈다.
            Time.timeScale = previousTimeScale;
        }

        private Code.Manager.GameScope CostScope(int gold, int debt = 0, int warnings = 0)
        {
            Code.Manager.GameScope scope = GradeScope((Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Cost grade"));
            scope.Cost.RestoreCheckpoint(gold, debt, 0f, "default", warnings, 1);
            return scope;
        }

        private Code.Manager.ManagementSettlementManager ScopedSettlement(Code.Manager.GameScope scope)
        {
            Code.Manager.ManagementSettlementManager manager = (Code.Manager.ManagementSettlementManager)Component("Code.Manager.ManagementSettlementManager", "Scoped cost settlement");
            Set(manager, "scope", scope);
            Call(manager, "OnEnable");
            return manager;
        }

        /// <summary>잔액이 넉넉한 전역 매니저가 있어도 전달받은 지갑이 비었으면 고용 요청을 보내면 안 된다.</summary>
        [Test]
        public void CostScope_HireChecksAssignedWalletAndRebindsOnEnable()
        {
            object previousDay = typeof(Code.Manager.DayManager).GetProperty("Current").GetValue(null);
            Code.Manager.DayManager day = (Code.Manager.DayManager)Component("Code.Manager.DayManager", "Recruitment day");
            Set(day, "currentDay", 7);
            typeof(Code.Manager.DayManager).GetProperty("Current").SetValue(null, day);
            Code.Manager.GameScope poor = CostScope(0);
            Code.Manager.GameScope rich = CostScope(100);
            typeof(Code.Manager.CostManager).GetProperty("Current").SetValue(null, rich.Cost);
            Code.Manager.HiredUnitRoster roster = ScopedRoster(poor);
            Code.Units.UnitDataSO data = (Code.Units.UnitDataSO)UnitData("Candidate", 10);
            ((Dictionary<Code.Units.UnitDataSO, int>)typeof(Code.Manager.HiredUnitRoster).GetField("_ownedUnits", PrivateInstance).GetValue(roster)).Add(data, 1);
            Code.Core.GameEventChannelSO channel = (Code.Core.GameEventChannelSO)typeof(Code.Manager.HiredUnitRoster).GetField("costEventChannel", PrivateInstance).GetValue(roster);
            int requests = 0;
            System.Action<Code.Events.RosterHireRequestedEvent> listener = _ => requests++;
            channel.AddListener(listener);
            try
            {
                Assert.That(roster.TryRequestHire(data), Is.False);
                Assert.That(requests, Is.Zero);
                Call(roster, "OnDisable");
                Set(roster, "scope", rich);
                Call(roster, "OnEnable");
                typeof(Code.Manager.CostManager).GetProperty("Current").SetValue(null, poor.Cost);
                Assert.That(roster.TryRequestHire(data), Is.True);
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(poor.Cost.CurrentGold, Is.Zero);
                Assert.That(rich.Cost.CurrentGold, Is.EqualTo(100), "이 테스트의 채널은 요청만 받으며 결제 매니저는 연결하지 않았다.");
            }
            finally
            {
                channel.RemoveListener(listener);
                Call(roster, "OnDisable");
                typeof(Code.Manager.DayManager).GetProperty("Current").SetValue(null, previousDay);
            }
        }

        /// <summary>다른 판의 이연 상태 때문에 같은 수입이 정산에서 두 번 들어오거나 누락되면 안 된다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void CostScope_IncomeDeferralAndNetUseAssignedWallet(bool deferred)
        {
            Code.Manager.GameScope own = CostScope(100);
            Code.Manager.GameScope other = CostScope(1000);
            Set(own.Cost, "_isSettlementDeferred", deferred);
            Set(other.Cost, "_isSettlementDeferred", !deferred);
            typeof(Code.Manager.CostManager).GetProperty("Current").SetValue(null, other.Cost);
            Code.Manager.ManagementSettlementManager manager = ScopedSettlement(own);
            try
            {
                if (!deferred) own.Cost.AddGold(20);
                Call(manager, "HandleGoldEarned", new Code.Events.GoldEarnedEvent(20));
                Call(manager, "ApplyNetToGold");
                Assert.That(own.Cost.CurrentGold, Is.EqualTo(120));
                Assert.That(other.Cost.CurrentGold, Is.EqualTo(1000));
                Assert.That(typeof(Code.Manager.ManagementSettlementManager).GetField("_lastSettlementNet", PrivateInstance).GetValue(manager), Is.EqualTo(deferred ? 20 : 0));
                Code.Manager.SettlementReport report = (Code.Manager.SettlementReport)Call(manager, "BuildReport");
                Assert.That(report.TotalIncome, Is.EqualTo(20));
                Call(manager, "ApplyNetToGold");
                Assert.That(own.Cost.CurrentGold, Is.EqualTo(120), "이미 반영한 장부를 다시 적용해도 금화가 늘지 않는다.");
            }
            finally { Call(manager, "OnDisable"); }
        }

        /// <summary>청산과 상환 안내가 전역 지갑 대신 같은 조립 루트의 빚을 사용해야 한다.</summary>
        [Test]
        public void CostScope_WeeklyPaymentAndDebtNoteUseAssignedWallet()
        {
            object previousDay = typeof(Code.Manager.DayManager).GetProperty("Current").GetValue(null);
            Code.Manager.DayManager day = (Code.Manager.DayManager)Component("Code.Manager.DayManager", "Settlement day");
            Set(day, "currentDay", 7);
            typeof(Code.Manager.DayManager).GetProperty("Current").SetValue(null, day);
            Code.Manager.GameScope own = CostScope(200, 100);
            Code.Manager.GameScope other = CostScope(0, 9000, 4);
            typeof(Code.Manager.CostManager).GetProperty("Current").SetValue(null, other.Cost);
            Code.Manager.ManagementSettlementManager manager = ScopedSettlement(own);
            try
            {
                Set(manager, "currentDay", 7);
                int due = own.Cost.WeeklyDue;
                Call(manager, "SettleWeekIfDue");
                Assert.That(own.Cost.CurrentGold, Is.EqualTo(200 - due));
                Assert.That(own.Cost.CurrentDebt, Is.LessThan(100));
                Assert.That(other.Cost.CurrentGold, Is.Zero);
                Assert.That(other.Cost.CurrentDebt, Is.EqualTo(9000));
                List<Code.Manager.SettlementNote> notes = new();
                Call(manager, "AppendDebtNote", notes);
                Assert.That(notes, Has.Some.Matches<Code.Manager.SettlementNote>(note => note.Text.Contains("청산했습니다")));
                Assert.That(typeof(Code.Manager.ManagementSettlementManager).GetField("_weeklyBankrupt", PrivateInstance).GetValue(manager), Is.False);
            }
            finally
            {
                Call(manager, "OnDisable");
                typeof(Code.Manager.DayManager).GetProperty("Current").SetValue(null, previousDay);
            }
        }

        /// <summary>한도 긴급 상환과 신용 경고 문구가 별도 판의 상태와 섞이지 않아야 한다.</summary>
        [Test]
        public void CostScope_CreditResolutionAndNotesUseAssignedWallet()
        {
            Code.Manager.GameScope own = CostScope(20, 120);
            Set(own.Cost, "weeklyLoanCreditLimit", 100);
            Code.Manager.GameScope other = CostScope(999, 999, 4);
            typeof(Code.Manager.CostManager).GetProperty("Current").SetValue(null, other.Cost);
            Code.Manager.ManagementSettlementManager manager = ScopedSettlement(own);
            try
            {
                Call(manager, "ResolveCreditLimit");
                Assert.That(own.Cost.CurrentGold, Is.Zero);
                Assert.That(own.Cost.CurrentDebt, Is.EqualTo(100));
                Assert.That(own.Cost.CreditWarnings, Is.EqualTo(1));
                Assert.That(other.Cost.CurrentGold, Is.EqualTo(999));
                Assert.That(other.Cost.CurrentDebt, Is.EqualTo(999));
                List<Code.Manager.SettlementNote> notes = new();
                Call(manager, "AppendCreditNote", notes);
                Assert.That(notes, Has.Some.Matches<Code.Manager.SettlementNote>(note => note.Text.Contains("신용 경고 1/5")));
                Assert.That(notes, Has.None.Matches<Code.Manager.SettlementNote>(note => note.Text.Contains("신용 경고 4/5")));
                Assert.That(typeof(Code.Manager.ManagementSettlementManager).GetField("_lastEmergencyRepaid", PrivateInstance).GetValue(manager), Is.EqualTo(20));
            }
            finally { Call(manager, "OnDisable"); }
        }

        /// <summary>재활성화로 조립 루트가 바뀌면 이후 정산은 새 지갑에만 반영해야 한다.</summary>
        [Test]
        public void CostScope_SettlementRebindsOnEnable()
        {
            Code.Manager.GameScope first = CostScope(100);
            Code.Manager.GameScope second = CostScope(1000);
            Code.Manager.ManagementSettlementManager manager = ScopedSettlement(first);
            try
            {
                Set(manager, "settlementIncome", 5);
                Call(manager, "ApplyNetToGold");
                Call(manager, "OnDisable");
                Set(manager, "scope", second);
                Call(manager, "OnEnable");
                Set(manager, "settlementIncome", 2);
                Call(manager, "ApplyNetToGold");
                Assert.That(first.Cost.CurrentGold, Is.EqualTo(105));
                Assert.That(second.Cost.CurrentGold, Is.EqualTo(1002));
            }
            finally { Call(manager, "OnDisable"); }
        }


        private Code.Manager.UnitRevivalSystem ScopedRevival(int kills)
        {
            Code.Manager.DungeonGradeManager grade = (Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Revival grade");
            grade.RestoreTotalKills(kills);
            Code.Manager.UnitRevivalSystem system = (Code.Manager.UnitRevivalSystem)Component("Code.Manager.UnitRevivalSystem", "Scoped revival");
            Set(system, "scope", GradeScope(grade));
            Call(system, "Awake");
            return system;
        }

        /// <summary>별도 판의 초기화·마력 소모·시작 보정이 기존 판의 마력과 상태 알림을 건드리면 안 된다.</summary>
        [Test]
        public void RevivalScope_ManaAndStartBonusesRemainIndependent()
        {
            Code.Manager.UnitRevivalSystem first = ScopedRevival(0);
            Code.Manager.UnitRevivalSystem second = ScopedRevival(20);
            Assert.That(first.CurrentRevivalMana, Is.EqualTo(30));
            Assert.That(second.CurrentRevivalMana, Is.EqualTo(36));
            int firstChanges = 0;
            int secondChanges = 0;
            first.StateChanged += () => firstChanges++;
            second.StateChanged += () => secondChanges++;
            first.AddMaxManaBonus(10);
            Assert.That(first.MaxRevivalMana, Is.EqualTo(40));
            Assert.That(first.CurrentRevivalMana, Is.EqualTo(40));
            Assert.That(second.MaxRevivalMana, Is.EqualTo(36));
            Assert.That(secondChanges, Is.Zero);
            Set(first, "_currentRevivalMana", 0f);
            Call(first, "RegenerateRevivalMana", 2f);
            Assert.That(first.CurrentRevivalMana, Is.EqualTo(2));
            Assert.That(second.CurrentRevivalMana, Is.EqualTo(36));
            second.AddMaxManaBonus(10);
            Assert.That(second.MaxRevivalMana, Is.EqualTo(46));
            Assert.That(first.MaxRevivalMana, Is.EqualTo(40));
            Assert.That(first.CurrentRevivalMana, Is.EqualTo(2));
            Assert.That(firstChanges, Is.EqualTo(2));
            Assert.That(secondChanges, Is.EqualTo(1));
        }

        /// <summary>마력이 모자란 FIFO 대기와 회복은 해당 인스턴스에만 적용하고 부활 뒤 피로·부상은 남긴다.</summary>
        [Test]
        public void RevivalScope_QueuesRemainIndependentAndPreserveFifo()
        {
            Code.Manager.UnitRevivalSystem first = ScopedRevival(0);
            Code.Manager.UnitRevivalSystem second = ScopedRevival(0);
            Code.Units.Unit head = (Code.Units.Unit)Unit(UnitData("Head", 0), 70f);
            Code.Units.Unit tail = (Code.Units.Unit)Unit(UnitData("Tail", 0), 60f);
            Code.Units.Unit other = (Code.Units.Unit)Unit(UnitData("Other", 0), 50f);
            foreach (Code.Units.Unit unit in new[] { head, tail, other })
            {
                Set(unit, "<IsIncapacitated>k__BackingField", true);
                Set(unit, "injury", Code.Units.InjurySeverity.Severe);
            }
            Dictionary<Code.Units.Unit, float> pending = (Dictionary<Code.Units.Unit, float>)typeof(Code.Manager.UnitRevivalSystem).GetField("_pending", PrivateInstance).GetValue(first);
            List<Code.Units.Unit> order = (List<Code.Units.Unit>)typeof(Code.Manager.UnitRevivalSystem).GetField("_revivalOrder", PrivateInstance).GetValue(first);
            pending.Add(head, 5f);
            pending.Add(tail, 0f);
            order.AddRange(new[] { head, tail });
            ((Dictionary<Code.Units.Unit, float>)typeof(Code.Manager.UnitRevivalSystem).GetField("_pending", PrivateInstance).GetValue(second)).Add(other, 10f);
            ((List<Code.Units.Unit>)typeof(Code.Manager.UnitRevivalSystem).GetField("_revivalOrder", PrivateInstance).GetValue(second)).Add(other);
            Set(first, "_currentRevivalMana", 20f);
            Call(first, "TickPending", 0f);
            Assert.That(tail.IsIncapacitated, Is.True, "먼저 준비됐어도 앞사람을 건너뛰지 않는다.");
            int cost = first.GetRevivalCost(head);
            Assert.That(cost, Is.EqualTo(15));
            Call(first, "TickPending", 5f);
            Assert.That(head.IsIncapacitated, Is.False);
            Assert.That(head.Fatigue, Is.EqualTo(70f));
            Assert.That(typeof(Code.Units.Unit).GetField("injury", PrivateInstance).GetValue(head), Is.EqualTo(Code.Units.InjurySeverity.Severe));
            Assert.That(tail.IsIncapacitated, Is.True);
            Assert.That(first.PendingUnits, Is.EqualTo(new[] { tail }));
            Assert.That(first.CurrentRevivalMana, Is.EqualTo(20 - cost));
            Assert.That(second.GetRemainingSeconds(other), Is.EqualTo(10f));
            Assert.That(second.CurrentRevivalMana, Is.EqualTo(30));
            Assert.That(second.PendingUnits, Is.EqualTo(new[] { other }));
            Call(first, "RegenerateRevivalMana", 10f);
            Call(first, "TickPending", 0f);
            Assert.That(tail.IsIncapacitated, Is.False);
            Assert.That(first.PendingCount, Is.Zero);
            Assert.That(first.CurrentRevivalMana, Is.Zero);
            Assert.That(other.IsIncapacitated, Is.True);
            Assert.That(second.PendingCount, Is.EqualTo(1));
            Assert.That(second.CurrentRevivalMana, Is.EqualTo(30));
        }

        /// <summary>주입 경로 이전이 끝난 부활 매니저에 전역 접근자가 다시 생기는 것을 막는다.</summary>
        [Test]
        public void RevivalScope_HasNoStaticCurrent()
        {
            Assert.That(typeof(Code.Manager.UnitRevivalSystem).GetProperty("Current", BindingFlags.Public | BindingFlags.Static), Is.Null);
        }


        private Code.Manager.GameScope GradeScope(Code.Manager.DungeonGradeManager grade)
        {
            Code.Manager.GameScope scope = (Code.Manager.GameScope)Component("Code.Manager.GameScope", "Grade scope");
            Set(scope, "dungeonGrade", grade);
            Code.Manager.CostManager cost = (Code.Manager.CostManager)Component("Code.Manager.CostManager", "Scope cost");
            Set(scope, "cost", cost);
            Set(scope, "runSummary", Component("Code.Progression.RunSummarySystem", "Scope summary"));
            Set(cost, "scope", scope);
            Code.Manager.MoralePolicyManager morale = (Code.Manager.MoralePolicyManager)Component("Code.Manager.MoralePolicyManager", "Neutral morale");
            morale.RestoreCheckpoint(100, 0, null);
            Set(scope, "moralePolicy", morale);
            return scope;
        }

        private Code.Manager.HiredUnitRoster ScopedRoster(Code.Manager.GameScope scope)
        {
            Code.Manager.HiredUnitRoster roster = (Code.Manager.HiredUnitRoster)Component("Code.Manager.HiredUnitRoster", "Scoped roster");
            Code.Core.GameEventChannelSO channel = ScriptableObject.CreateInstance<Code.Core.GameEventChannelSO>();
            created.Add(channel);
            Set(roster, "scope", scope);
            Set(roster, "costEventChannel", channel);
            Set(roster, "nodeEventChannel", channel);
            Set(roster, "dayEventChannel", channel);
            Set(roster, "unitCatalog", Array.Empty<Code.Units.UnitDataSO>());
            Call(roster, "OnEnable");
            return roster;
        }

        /// <summary>다른 판의 민심이 지원자와 유지비에 섞이거나 재활성화 뒤 옛 참조가 남는 것을 막는다.</summary>
        [Test]
        public void MoraleScope_RecruitmentAndUpkeepUseAssignedManager()
        {
            Code.Manager.GameScope lowScope = GradeScope((Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Grade low"));
            Code.Manager.GameScope highScope = GradeScope((Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Grade high"));
            lowScope.MoralePolicy.RestoreCheckpoint(0, 0, null);
            Code.Manager.HiredUnitRoster low = ScopedRoster(lowScope);
            Code.Manager.HiredUnitRoster high = ScopedRoster(highScope);
            Code.Manager.ManagementSettlementManager settlement = (Code.Manager.ManagementSettlementManager)Component("Code.Manager.ManagementSettlementManager", "Morale settlement");
            Set(settlement, "scope", lowScope);
            Code.Units.UnitDataSO data = (Code.Units.UnitDataSO)UnitData("Wages", 50);
            try
            {
                Call(settlement, "OnEnable");
                int weekly = Code.Manager.UnitMarketRules.WeeklyCandidateCount(2, 0);
                Assert.That(Call(low, "WeeklyIncomingCount"), Is.EqualTo(lowScope.MoralePolicy.AdjustRecruitCount(weekly)));
                Assert.That(Call(high, "WeeklyIncomingCount"), Is.EqualTo(weekly));
                Assert.That(settlement.EstimateUnitUpkeep(data), Is.EqualTo(15));
                Assert.That((string)Call(settlement, "BuildUpkeepLabel"), Does.Contain("+50%"));
                Set(low, "scope", highScope);
                Call(low, "OnDisable");
                Call(low, "OnEnable");
                Assert.That(Call(low, "WeeklyIncomingCount"), Is.EqualTo(weekly));
                Call(settlement, "OnDisable");
                Set(settlement, "scope", highScope);
                Call(settlement, "OnEnable");
                Assert.That(settlement.EstimateUnitUpkeep(data), Is.EqualTo(8));
                Assert.That((string)Call(settlement, "BuildUpkeepLabel"), Does.Contain("-20%"));
                lowScope.MoralePolicy.RestoreCheckpoint(50, 0, null);
                Assert.That(settlement.EstimateUnitUpkeep(data), Is.EqualTo(8));
                ((IDictionary)typeof(Code.Manager.HiredUnitRoster).GetField("_deployedUnits", PrivateInstance).GetValue(low)).Add(data, 2);
                ((IDictionary)typeof(Code.Manager.HiredUnitRoster).GetField("_ownedUnits", PrivateInstance).GetValue(low)).Add(data, 2);
                typeof(Code.Manager.HiredUnitRoster).GetProperty("Current").SetValue(null, low);
                Assert.That(Call(settlement, "CalculateDailyUpkeep"), Is.EqualTo(16));
            }
            finally
            {
                Call(low, "OnDisable");
                Call(high, "OnDisable");
                Call(settlement, "OnDisable");
            }
        }

        /// <summary>복원 전후 생성과 참조 교체 때 정책 보정이 누락되거나 중첩되는 것을 막는다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void MoraleScope_UnitBindingAppliesRestoredPolicyAndDetachesPreviousManager(bool restoreBeforeBinding)
        {
            Code.Manager.MoralePolicyManager first = (Code.Manager.MoralePolicyManager)Component("Code.Manager.MoralePolicyManager", "Policy first");
            Code.Manager.MoralePolicyManager second = (Code.Manager.MoralePolicyManager)Component("Code.Manager.MoralePolicyManager", "Policy second");
            Code.Manager.PolicyDataSO policy = ScriptableObject.CreateInstance<Code.Manager.PolicyDataSO>();
            created.Add(policy);
            policy.name = "Combat policy";
            Set(policy, "unitDamageMultiplier", 2f);
            Set(policy, "unitDefenseBonus", 7);
            Set(first, "availablePolicies", new[] { policy });
            Code.Persistence.SavedPolicyState saved = new();
            saved.active.Add(new Code.Persistence.SavedActivePolicy { assetKey = policy.name, remainingDays = 3 });
            Code.Units.Unit unit = (Code.Units.Unit)Component("Code.Units.Unit", "Policy unit");
            Code.Combat.Combatant combat = unit.gameObject.AddComponent<Code.Combat.Combatant>();
            GameLib.Entity.Stats.StatModule stats = unit.gameObject.AddComponent<GameLib.Entity.Stats.StatModule>();
            Dictionary<int, GameLib.Entity.Stats.StatSO> values = new();
            foreach (int index in new[] { GameLib.Entity.Stats.StatIndex.AttackDamage, GameLib.Entity.Stats.StatIndex.Defense })
            {
                GameLib.Entity.Stats.StatSO stat = ScriptableObject.CreateInstance<GameLib.Entity.Stats.StatSO>();
                created.Add(stat);
                stat.BaseValue = index == GameLib.Entity.Stats.StatIndex.AttackDamage ? 100f : 0f;
                values.Add(index, stat);
            }
            Set(stats, "_statByIndex", values);
            Set(unit, "combatant", combat);
            unit.Initialize((Code.Units.UnitDataSO)UnitData("Policy data", 10));
            Set(unit, "fatigue", 50f);
            Call(unit, "ApplyConditionModifiers");
            object artifactKey = new();
            combat.SetAttackModifier(artifactKey, 10f, 1.2f);
            combat.SetDefenseAndEvasionBonus(artifactKey, 3f, 0f);
            int baseDamage = combat.AttackDamage;
            int baseDefense = combat.Defense;
            if (restoreBeforeBinding) first.RestoreCheckpoint(60, 3, saved);
            unit.BindPolicies(first);
            if (!restoreBeforeBinding) first.RestoreCheckpoint(60, 3, saved);
            Assert.That(combat.AttackDamage, Is.EqualTo(baseDamage * 2));
            Assert.That(combat.Defense, Is.EqualTo(baseDefense + 7));
            unit.BindPolicies(first);
            Assert.That(combat.AttackDamage, Is.EqualTo(baseDamage * 2));
            Delegate listeners = (Delegate)typeof(Code.Manager.MoralePolicyManager).GetField("CombatModifiersChanged", PrivateInstance).GetValue(first);
            Assert.That(listeners.GetInvocationList(), Has.Length.EqualTo(1));
            unit.BindPolicies(second);
            Assert.That(combat.AttackDamage, Is.EqualTo(baseDamage));
            Assert.That(combat.Defense, Is.EqualTo(baseDefense));
            Assert.That(typeof(Code.Manager.MoralePolicyManager).GetField("CombatModifiersChanged", PrivateInstance).GetValue(first), Is.Null);
            first.RestoreCheckpoint(100, 4, saved);
            Assert.That(combat.AttackDamage, Is.EqualTo(baseDamage));
            unit.BindPolicies(null);
            Assert.That(typeof(Code.Manager.MoralePolicyManager).GetField("CombatModifiersChanged", PrivateInstance).GetValue(second), Is.Null);
            Assert.That(combat.Defense, Is.EqualTo(baseDefense));
        }

        /// <summary>정적 접근자가 되살아나 참조 주입 누락을 숨기는 것을 막는다.</summary>
        [Test]
        public void MoraleScope_HasNoStaticCurrent()
        {
            Assert.That(typeof(Code.Manager.MoralePolicyManager).GetProperty("Current", BindingFlags.Static | BindingFlags.Public), Is.Null);
        }


        [Test]
        public void GradeScope_RostersUseTheirOwnGradeAndRebindOnEnable()
        {
            Code.Manager.HiredUnitRoster first = null;
            Code.Manager.HiredUnitRoster second = null;
            try
            {
                Code.Manager.DungeonGradeManager firstGrade = (Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "First grade");
                Code.Manager.DungeonGradeManager secondGrade = (Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Second grade");
                secondGrade.RestoreTotalKills(40);
                Code.Manager.GameScope firstScope = GradeScope(firstGrade);
                Code.Manager.GameScope secondScope = GradeScope(secondGrade);
                first = ScopedRoster(firstScope);
                second = ScopedRoster(secondScope);
                int firstCount = (int)Call(first, "WeeklyIncomingCount");
                Assert.That(firstCount, Is.EqualTo(Code.Manager.UnitMarketRules.WeeklyCandidateCount(2, firstGrade.Grade)));
                Assert.That(Call(second, "WeeklyIncomingCount"), Is.EqualTo(Code.Manager.UnitMarketRules.WeeklyCandidateCount(2, secondGrade.Grade)));
                Assert.That(first.GetType().GetField("_dungeonGrade", PrivateInstance).GetValue(first), Is.SameAs(firstGrade));
                secondGrade.RestoreTotalKills(1000);
                Assert.That(Call(first, "WeeklyIncomingCount"), Is.EqualTo(firstCount), "다른 판의 등급 변경이 후보 수에 섞이지 않는다.");
                Call(first, "OnDisable");
                Set(first, "scope", secondScope);
                Call(first, "OnEnable");
                Assert.That(Call(first, "WeeklyIncomingCount"), Is.EqualTo(6), "재활성화하면 새 조립 루트의 등급을 얻는다.");
            }
            finally
            {
                if (first != null) Call(first, "OnDisable");
                if (second != null) Call(second, "OnDisable");
            }
        }

        [Test]
        public void GradeScope_SettlementReportsOnlyTheAssignedGrade()
        {
            object previousWave = Resolve("Code.Manager.WaveManager").GetProperty("Current").GetValue(null);
            Code.Manager.ManagementSettlementManager manager = (Code.Manager.ManagementSettlementManager)Component("Code.Manager.ManagementSettlementManager", "Scoped settlement");
            try
            {
                Code.Manager.DungeonGradeManager firstGrade = (Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Report grade");
                Code.Manager.DungeonGradeManager secondGrade = (Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Other grade");
                firstGrade.RestoreTotalKills(40);
                secondGrade.RestoreTotalKills(900);
                Set(manager, "scope", GradeScope(firstGrade));
                Code.Manager.WaveManager wave = (Code.Manager.WaveManager)Component("Code.Manager.WaveManager", "Report wave");
                object stats = wave.GetType().GetField("_stats", PrivateInstance).GetValue(wave);
                stats.GetType().GetMethod("Reset").Invoke(stats, new object[] { 3 });
                Resolve("Code.Manager.WaveManager").GetProperty("Current").SetValue(null, wave);
                Call(manager, "OnEnable");
                Code.Manager.SettlementReport report = (Code.Manager.SettlementReport)Call(manager, "BuildReport");
                Assert.That(report.Notes, Has.Some.Matches<Code.Manager.SettlementNote>(note => note.Text.Contains("누적 처치 40")));
                secondGrade.RecordKill();
                report = (Code.Manager.SettlementReport)Call(manager, "BuildReport");
                Assert.That(report.Notes, Has.None.Matches<Code.Manager.SettlementNote>(note => note.Text.Contains("901")));
                Call(manager, "OnDisable");
                Set(manager, "scope", GradeScope(secondGrade));
                Call(manager, "OnEnable");
                report = (Code.Manager.SettlementReport)Call(manager, "BuildReport");
                Assert.That(report.Notes, Has.Some.Matches<Code.Manager.SettlementNote>(note => note.Text.Contains("누적 처치 901")));
            }
            finally
            {
                Call(manager, "OnDisable");
                Resolve("Code.Manager.WaveManager").GetProperty("Current").SetValue(null, previousWave);
            }
        }

        [Test]
        public void GradeScope_HasNoStaticCurrent()
        {
            Assert.That(typeof(Code.Manager.DungeonGradeManager).GetProperty("Current", BindingFlags.Public | BindingFlags.Static), Is.Null);
        }

        [TestCase(false, 100, 25)]
        [TestCase(true, 100, 25)]
        [TestCase(false, 0, 25)]
        [TestCase(true, 0, 25)]
        [TestCase(false, 100, 0)]
        [TestCase(true, 100, 0)]
        public void ReopenCost_RecordsPaidExpenseOnceWithoutChargingSettlement(bool deferred, int gold, int fee)
        {
            Code.Manager.CostManager cost = (Code.Manager.CostManager)Component("Code.Manager.CostManager", "Reopen cost");
            Set(cost, "initialGold", gold);
            Call(cost, "Awake");
            Set(cost, "_isSettlementDeferred", deferred);
            Code.Core.GameEventChannelSO channel = ScriptableObject.CreateInstance<Code.Core.GameEventChannelSO>();
            created.Add(channel);
            Code.Manager.ManagementSettlementManager manager = (Code.Manager.ManagementSettlementManager)Component(
                "Code.Manager.ManagementSettlementManager", "Reopen ledger");
            Set(manager, "costEventChannel", channel);
            Code.Manager.GameScope reopenScope = GradeScope((Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Reopen grade"));
            Set(reopenScope, "cost", cost);
            Set(manager, "scope", reopenScope);
            Code.Manager.FacilityClosureSystem closure = (Code.Manager.FacilityClosureSystem)Component(
                "Code.Manager.FacilityClosureSystem", "Reopen system");
            Set(closure, "costEventChannel", channel);
            Code.Buildings.BuildingDataSO data = ScriptableObject.CreateInstance<Code.Buildings.BuildingDataSO>();
            created.Add(data);
            Set(data, "<DwellSeconds>k__BackingField", 3f);
            Code.Buildings.Building building = (Code.Buildings.Building)Component("Code.Buildings.Building", "Reopen facility");
            building.Initialize(data);
            Set(building, "reopenCost", fee);
            building.Close();

            Call(manager, "OnEnable");
            Call(manager, "OnDisable");
            Call(manager, "OnEnable");
            try
            {
                bool expected = gold >= fee;
                Assert.That(closure.TryBeginReopen(building, out _), Is.EqualTo(expected));
                Assert.That(closure.TryBeginReopen(building, out _), Is.False, "예약 중이거나 금화가 부족하면 다시 결제하지 않는다.");
                int paid = expected ? fee : 0;
                Assert.That(cost.CurrentGold, Is.EqualTo(gold - paid));
                Assert.That(cost.CurrentDebt, Is.Zero);
                if (expected)
                {
                    Assert.That(building.TryCompleteReopen(int.MaxValue), Is.True);
                    channel.RaiseEvent(new Code.Events.FacilityReopenedEvent(building));
                }

                Call(manager, "ApplyNetToGold");
                Code.Manager.SettlementReport report = (Code.Manager.SettlementReport)Call(manager, "BuildReport");
                Assert.That(report.TotalExpense, Is.EqualTo(paid), "이미 결제한 재개장비를 전체 지출에 한 번 기록한다.");
                Assert.That(report.Net, Is.Zero, "대기·영업 중 모두 이미 결제해 순정산에는 다시 넣지 않는다.");
                Assert.That(cost.CurrentGold, Is.EqualTo(gold - paid), "정산으로 두 번 차감하지 않는다.");
                Assert.That(report.Expense.Count, Is.EqualTo(paid > 0 ? 1 : 0));
                if (paid > 0)
                {
                    Assert.That(report.Expense[0].Label, Is.EqualTo("시설 재개장비"));
                    Assert.That(report.Expense[0].Amount, Is.EqualTo(paid));
                }
            }
            finally
            {
                Call(manager, "OnDisable");
            }

            int before = (int)manager.GetType().GetField("totalExpense", PrivateInstance).GetValue(manager);
            channel.RaiseEvent(new Code.Events.FacilityReopenScheduledEvent(building, 7, 1));
            Assert.That(manager.GetType().GetField("totalExpense", PrivateInstance).GetValue(manager), Is.EqualTo(before),
                "비활성화 뒤에는 이벤트를 받지 않는다.");
        }

        [Test]
        public void Upkeep_AfterRosterRestore_ChargesDeployedUnitsButNotReserveOrApplicants()
        {
            var roster = Component("Code.Manager.HiredUnitRoster", "Roster");
            var manager = Component("Code.Manager.ManagementSettlementManager", "Settlement");
            var unit = UnitData("Restored unit", 14);
            Resolve("Code.Manager.HiredUnitRoster").GetProperty("Current").SetValue(null, roster);
            // Equivalent to the restored roster: one reserve, two deployed, five unhired candidates.
            ((IList)roster.GetType().GetField("_availableUnits", PrivateInstance).GetValue(roster)).Add(unit);
            ((IDictionary)roster.GetType().GetField("_deployedUnits", PrivateInstance).GetValue(roster)).Add(unit, 2);
            ((IDictionary)roster.GetType().GetField("_ownedUnits", PrivateInstance).GetValue(roster)).Add(unit, 5);
            // 대기 명단의 부하는 유지비가 없다(기획서). 배치한 둘만 낸다.
            Assert.That(Call(manager, "CalculateDailyUpkeep"), Is.EqualTo(6),
                "Restoring a run must not make the wages of deployed units disappear, and reserve units are free.");
        }

        [Test]
        public void Fatigue_RestoredPlacementWithoutDeployEvent_IsReported()
        {
            var manager = Component("Code.Manager.ManagementSettlementManager", "Settlement");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03.Prefabs/Map/Node.prefab");
            var go = UnityEngine.Object.Instantiate(prefab);
            created.Add(go);
            var node = go.GetComponent(Resolve("Code.MapCreateSystem.Node"));
            var modelType = Resolve("Code.MapCreateSystem.DungeonNode");
            var model = Activator.CreateInstance(modelType,
                Enum.Parse(Resolve("Code.MapCreateSystem.DungeonNodeType"), "Corridor"),
                new Vector2Int(900, 900), 4);
            node.GetType().GetMethod("Initialize", new[] { modelType, typeof(float) })
                .Invoke(node, new[] { model, (object)1f });
            var data = UnitData("Restored slime", 14);
            var unit = Unit(data, 73);
            // RestoreUnits registers the placement directly and deliberately does not hire/deploy again.
            var assigned = node.GetType().GetMethod("TryAssignUnitToCell")
                .Invoke(node, new object[] { data, unit, 0, 0 });
            Assert.That(assigned, Is.EqualTo(true));
            Call(manager, "ApplyBattleFatigue");
            Assert.That(FatigueSummary(manager), Does.Contain("73"));
        }

        [Test]
        public void Fatigue_SameRoom_ReportsEveryUnitIncludingSameType()
        {
            var manager = Component("Code.Manager.ManagementSettlementManager", "Settlement");
            var node = Component("Code.MapCreateSystem.Node", "Room");
            var data = UnitData("Slime", 14);
            var tired = Unit(data, 80);
            var fresh = Unit(data, 20);
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, tired));
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, fresh));
            Call(manager, "ApplyBattleFatigue");
            var report = FatigueSummary(manager);
            Assert.That(report, Does.Contain("80"));
            Assert.That(report, Does.Contain("20"));
        }

        [Test]
        public void Fatigue_ReturningOneUnit_DoesNotRemoveItsRoommate()
        {
            var manager = Component("Code.Manager.ManagementSettlementManager", "Settlement");
            var node = Component("Code.MapCreateSystem.Node", "Room");
            var data = UnitData("Slime", 14);
            var remaining = Unit(data, 80);
            var returning = Unit(data, 20);
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, remaining));
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, returning));
            Call(manager, "HandleUnitReturned", Event("UnitReturnedFromNodeEvent", node, data, returning));
            Call(manager, "ApplyBattleFatigue");
            var report = FatigueSummary(manager);
            Assert.That(report, Does.Contain("80"));
            Assert.That(report, Does.Not.Contain("20"));
        }

        [Test]
        public void FailedConstruction_RefundsChargedGoldAndRestoresConsumedDiscount()
        {
            var channel = ScriptableObject.CreateInstance(Resolve("Code.Core.GameEventChannelSO"));
            created.Add(channel);
            var cost = Component("Code.Manager.CostManager", "Construction cost");
            var settlement = Component("Code.Manager.ManagementSettlementManager", "Construction ledger");
            var panel = Component("Code.UI.NodePanelView", "Construction panel");
            var node = Component("Code.MapCreateSystem.Node", "Construction room");
            var buildingData = ScriptableObject.CreateInstance(Resolve("Code.Buildings.BuildingDataSO"));
            created.Add(buildingData);

            Set(cost, "costEventChannel", channel);
            Set(settlement, "costEventChannel", channel);
            Code.Manager.GameScope constructionScope = GradeScope((Code.Manager.DungeonGradeManager)Component("Code.Manager.DungeonGradeManager", "Construction grade"));
            Set(constructionScope, "cost", cost);
            Set(settlement, "scope", constructionScope);
            Set(panel, "costEventChannel", channel);
            cost.gameObject.SetActive(true);
            settlement.gameObject.SetActive(true);
            // EditMode는 비활성 객체의 런타임 생명주기를 자동 실행하지 않는다.
            Call(cost, "Awake");
            Call(cost, "OnEnable");
            Call(settlement, "OnEnable");

            var pendingType = panel.GetType().GetNestedType("PendingBuildingInstall", BindingFlags.NonPublic);
            var central = pendingType.GetMethod("Central", BindingFlags.Public | BindingFlags.Static);
            Set(panel, "_pendingBuilding", central.Invoke(null, new object[] { node, buildingData }));

            channel.GetType().GetMethod("RaiseEvent").Invoke(channel,
                new[] { Event("ConstructionDiscountGrantedEvent", 0.5f) });
            channel.GetType().GetMethod("RaiseEvent").Invoke(channel,
                new[] { Event("BuildCostRequestedEvent", node, 40) });

            Assert.That(cost.GetType().GetProperty("CurrentGold").GetValue(cost), Is.EqualTo(80));
            Call(panel, "HandleBuildCostPaid", Event("BuildCostPaidEvent", node, 20, 80, 0.5f));

            Assert.That(cost.GetType().GetProperty("CurrentGold").GetValue(cost), Is.EqualTo(100));
            Assert.That(cost.GetType().GetProperty("CurrentBuildDiscountRate").GetValue(cost), Is.EqualTo(0.5f));
            Assert.That(settlement.GetType().GetField("totalExpense", PrivateInstance).GetValue(settlement), Is.EqualTo(20));
            Assert.That(settlement.GetType().GetField("totalIncome", PrivateInstance).GetValue(settlement), Is.EqualTo(20));
        }

        // ── 되살리기 ────────────────────────────────────────────────

        [Test]
        public void Revival_TakesGoldFirstAndBorrowsTheRest()
        {
            var cost = Component("Code.Manager.CostManager", "Revival cost");
            Set(cost, "initialGold", 30);
            Set(cost, "weeklyDebtInterest", 0.1f);
            Call(cost, "Awake");

            var borrowed = cost.GetType().GetMethod("ChargeOrBorrow").Invoke(cost, new object[] { 50 });

            Assert.That(borrowed, Is.EqualTo(20), "모자란 만큼만 빚이 됩니다.");
            Assert.That(cost.GetType().GetProperty("CurrentGold").GetValue(cost), Is.Zero, "가진 금화를 먼저 씁니다.");
            Assert.That(cost.GetType().GetProperty("CurrentDebt").GetValue(cost), Is.EqualTo(20));
        }

        [Test]
        public void LoanOptions_WithoutConfiguredProducts_ShowTheActiveConditions()
        {
            var cost = Component("Code.Manager.CostManager", "Loan desk");
            Set(cost, "weeklyLoanCreditLimit", 4200);
            Call(cost, "Awake");

            var options = (IEnumerable)cost.GetType().GetMethod("GetLoanProductOptions").Invoke(cost, null);
            var count = 0;
            foreach (var option in options)
            {
                count++;
                Assert.That(option.GetType().GetProperty("CreditLimit").GetValue(option), Is.EqualTo(4200));
            }

            // 목록이 비면 대출 조건이 없는 것처럼 보인다. 적용 중인 조건은 항상 한 줄 남아야 한다.
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void LoanContract_DefaultProductStartsWithOneWeekRemaining()
        {
            var cost = Component("Code.Manager.CostManager", "Loan contract");
            Call(cost, "Awake");

            // 기존 씬 데이터에 기간 필드가 없던 저장도 새 계약 한 주로 시작해야 한다.
            Assert.That(cost.GetType().GetProperty("ContractWeeksRemaining").GetValue(cost), Is.EqualTo(1));
        }

        [Test]
        public void LoanProductChange_OutsideSettlementDay_IsClosed()
        {
            var dayManager = Resolve("Code.Manager.DayManager").GetProperty("Current");
            var previousDay = dayManager.GetValue(null);
            dayManager.SetValue(null, null);
            try
            {
                var cost = Component("Code.Manager.CostManager", "Loan desk");
                Call(cost, "Awake");

                // 청산일이 아니면 상품 변경이 거절된다. 화면은 이 값으로 버튼을 잠근다.
                Assert.That(cost.GetType().GetProperty("CanChangeLoanProductToday").GetValue(cost), Is.False);
            }
            finally
            {
                dayManager.SetValue(null, previousDay);
            }
        }

        [Test]
        public void Revival_IsNeverRefusedForLackOfGold()
        {
            var cost = Component("Code.Manager.CostManager", "Revival cost");
            Set(cost, "initialGold", 0);
            Call(cost, "Awake");

            // 돈이 없을 때가 되살릴 이유가 가장 큰 때다. 여기서 막으면 밀린 판이 돌아오지 못한다.
            var borrowed = cost.GetType().GetMethod("ChargeOrBorrow").Invoke(cost, new object[] { 40 });

            Assert.That(borrowed, Is.EqualTo(40));
            Assert.That(cost.GetType().GetProperty("CurrentDebt").GetValue(cost), Is.EqualTo(40));
        }

        [Test]
        public void Revival_CostsMoreForAUnitYouRaised()
        {
            var system = Component("Code.Manager.UnitRevivalSystem", "Revival system");
            Set(system, "baseRevivalCost", 15);
            Set(system, "costPerLevel", 8);

            var getCost = system.GetType().GetMethod("GetRevivalCost");
            var data = UnitData("veteran", 40);
            var unit = Unit(data, 0f);

            // 레벨 컴포넌트가 없으면 1레벨로 본다. 기본값에 정가 10G당 1(40G → +4)만 붙는다.
            Assert.That(getCost.Invoke(system, new object[] { unit }), Is.EqualTo(19));
            Assert.That(getCost.Invoke(system, new object[] { null }), Is.EqualTo(15),
                "부하가 사라진 뒤에도 값을 물어볼 수 있어야 합니다.");
        }

        [Test]
        public void Revival_KeepsFatigueAndInjurySoDyingIsNotAShortcut()
        {
            var data = UnitData("tired", 40);
            var unit = Unit(data, 70f);
            Set(unit, "injury", Enum.Parse(Resolve("Code.Units.InjurySeverity"), "Severe"));
            Set(unit, "<IsIncapacitated>k__BackingField", true);

            unit.GetType().GetMethod("Revive").Invoke(unit, null);

            Assert.That(unit.GetType().GetField("fatigue", PrivateInstance).GetValue(unit), Is.EqualTo(70f),
                "되살아나도 피로는 남습니다.");
            Assert.That(unit.GetType().GetField("injury", PrivateInstance).GetValue(unit).ToString(),
                Is.EqualTo("Severe"), "되살아나도 부상은 남습니다.");
        }

        // ── 멈춤과 배속 ─────────────────────────────────────────────

        private Component SpeedController()
        {
            var controller = Component("Code.Manager.GameSpeedController", "Game speed");
            Call(controller, "Awake");
            return controller;
        }

        // ── 방문 목적 ────────────────────────────────────────────────
        // 목적은 어디로 걸을지만 가른다. 보물을 노린 자는 금고로 직진하고 나머지는 시설을 돈다.
        // 싸움은 누구와도 붙는다 — 전투까지 목적으로 가르면 경비가 순수 이득이 되어
        // 배치에 고를 것이 없어진다.

        private Component Visitor(string purposeName)
        {
            var enemy = Component("Code.Enemies.Enemy", "Visitor " + purposeName);
            var purpose = Enum.Parse(Resolve("Code.Enemies.AdventurerVisitPurpose"), purposeName);
            enemy.GetType().GetMethod("ConfigureVisitProfile").Invoke(enemy, new[] { purpose, (object)50 });
            return enemy;
        }

        [TestCase("TreasureHunt", true)]
        [TestCase("Shopping", false)]
        [TestCase("Rest", false)]
        [TestCase("EquipmentUpgrade", false)]
        public void Trespass_OnlyTheOnesAfterTheVaultCountAsIntruders(string purposeName, bool expected)
        {
            var visitor = Visitor(purposeName);

            Assert.That(Flag(visitor, "IsTrespasser"), Is.EqualTo(expected),
                $"{purposeName}으로 온 방문객의 경비 판정이 어긋납니다.");
        }

        [TestCase(0, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 1)]
        [TestCase(13, 3)]
        [TestCase(20, 5)]
        public void Forecast_TellsHowManyAreComingForTheVault(int visitors, int expectedHunters)
        {
            var rules = Resolve("Code.Enemies.AdventurerVisitRules");
            var hunt = Enum.Parse(Resolve("Code.Enemies.AdventurerVisitPurpose"), "TreasureHunt");
            var forecast = rules.GetMethod("ForecastCount");

            Assert.That(forecast.Invoke(null, new[] { hunt, (object)visitors }), Is.EqualTo(expectedHunters),
                "예보가 실제 배분과 어긋나면 경비를 세울 근거가 거짓말이 됩니다.");
        }

        [Test]
        public void Forecast_MatchesHowSpawningActuallyAssignsErrands()
        {
            var rules = Resolve("Code.Enemies.AdventurerVisitRules");
            var purposeType = Resolve("Code.Enemies.AdventurerVisitPurpose");
            var resolve = rules.GetMethod("ResolvePurpose");
            var forecast = rules.GetMethod("ForecastCount");
            var none = Enum.Parse(Resolve("Code.Enemies.AdventurerTrait"), "None");

            // 예보는 스폰이 실제로 나눠주는 방식과 같은 답을 내야 한다.
            // 특성이 없는 방문객만 세면 둘이 정확히 맞아야 한다.
            const int visitors = 14;
            var actual = new Dictionary<object, int>();
            for (var i = 0; i < visitors; i++)
            {
                var purpose = resolve.Invoke(null, new[] { none, (object)i });
                actual[purpose] = actual.TryGetValue(purpose, out var n) ? n + 1 : 1;
            }

            foreach (var value in Enum.GetValues(purposeType))
            {
                var expected = actual.TryGetValue(value, out var n) ? n : 0;
                Assert.That(forecast.Invoke(null, new[] { value, (object)visitors }), Is.EqualTo(expected),
                    $"{value} 예보가 스폰 배분과 다릅니다.");
            }
        }

        [Test]
        public void Trespass_ReconfiguringTheErrandChangesWhoGetsStopped()
        {
            var visitor = Visitor("Shopping");
            Assert.That(Flag(visitor, "IsTrespasser"), Is.False);

            // 같은 손님이라도 온 이유가 바뀌면 경비 대상이 바뀐다.
            var configure = visitor.GetType().GetMethod("ConfigureVisitProfile");
            var hunt = Enum.Parse(Resolve("Code.Enemies.AdventurerVisitPurpose"), "TreasureHunt");
            configure.Invoke(visitor, new[] { hunt, (object)50 });

            Assert.That(Flag(visitor, "IsTrespasser"), Is.True);
        }

        private static float Setting(object controller) =>
            (float)controller.GetType().GetProperty("Setting").GetValue(controller);

        private static bool Flag(object target, string property) =>
            (bool)target.GetType().GetProperty(property).GetValue(target);

        [Test]
        public void Speed_ModalReleaseReturnsToTheSpeedThePlayerChose()
        {
            var controller = SpeedController();
            var modal = new GameObject("Modal");
            created.Add(modal);

            controller.GetType().GetMethod("SetSetting").Invoke(controller, new object[] { 2f });
            controller.GetType().GetMethod("Suspend").Invoke(controller, new object[] { modal });
            Assert.That(Time.timeScale, Is.Zero, "창이 떠 있는 동안에는 멈춥니다.");

            // 창이 떠 있는 동안 배속을 바꿔도, 닫히면 옛 값이 아니라 새로 고른 값으로 돌아가야 한다.
            controller.GetType().GetMethod("SetSetting").Invoke(controller, new object[] { 1f });
            controller.GetType().GetMethod("Release").Invoke(controller, new object[] { modal });

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Setting(controller), Is.EqualTo(1f));
        }

        [Test]
        public void Speed_TwoModalsBothHaveToClose()
        {
            var controller = SpeedController();
            var first = new GameObject("First modal");
            var second = new GameObject("Second modal");
            created.Add(first);
            created.Add(second);

            var suspend = controller.GetType().GetMethod("Suspend");
            var release = controller.GetType().GetMethod("Release");

            suspend.Invoke(controller, new object[] { first });
            suspend.Invoke(controller, new object[] { second });
            release.Invoke(controller, new object[] { first });

            Assert.That(Time.timeScale, Is.Zero, "하나가 남아 있으면 아직 멈춰 있어야 합니다.");

            release.Invoke(controller, new object[] { second });
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Speed_ReportsItIsStoppedWhileAWindowHoldsTheClock()
        {
            var controller = SpeedController();
            var modal = new GameObject("Modal");
            created.Add(modal);

            Assert.That(Flag(controller, "IsPaused"), Is.False);

            controller.GetType().GetMethod("Suspend").Invoke(controller, new object[] { modal });

            // 창이 시간을 세워 둔 동안 "안 멈췄다"고 답하면, 아무도 움직이지 않는 화면을
            // 정상으로 읽게 된다. 이걸 믿고 디버깅하다 실제로 한참 헤맸다.
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Flag(controller, "IsPaused"), Is.True,
                "창이 세운 것도 멈춘 것입니다.");
            Assert.That(Flag(controller, "IsPausedByPlayer"), Is.False,
                "플레이어가 세운 것은 아닙니다.");
        }

        [Test]
        public void Speed_SpaceDoesNotResumeWhatAWindowStopped()
        {
            var controller = SpeedController();
            var modal = new GameObject("Modal");
            created.Add(modal);

            var toggle = controller.GetType().GetMethod("TogglePause");
            controller.GetType().GetMethod("Suspend").Invoke(controller, new object[] { modal });

            // 창이 떠 있는 동안 토글이 "재개"로 동작하면, 창을 닫는 순간
            // 플레이어가 고른 적 없는 배속이 남는다.
            toggle.Invoke(controller, null);
            Assert.That(Setting(controller), Is.Zero, "토글은 플레이어 배속을 0으로 내립니다.");

            controller.GetType().GetMethod("Release").Invoke(controller, new object[] { modal });
            Assert.That(Time.timeScale, Is.Zero, "창이 닫혀도 플레이어가 세운 멈춤은 남습니다.");
        }

        [Test]
        public void Speed_PauseSurvivesAnEffectThatFinishes()
        {
            var controller = SpeedController();
            controller.GetType().GetMethod("TogglePause").Invoke(controller, null);

            Assert.That(Setting(controller), Is.Zero);

            // 히트스톱이나 보스 연출이 끝나면 여기를 보고 되돌린다. 멈춤이 풀리면 안 된다.
            var restoreTarget = (float)controller.GetType()
                .GetProperty("RestoreTarget", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null);

            Assert.That(restoreTarget, Is.Zero, "연출이 끝나도 멈춘 상태로 돌아와야 합니다.");
        }

        [Test]
        public void Speed_ResetToNormalClearsEveryHold()
        {
            var controller = SpeedController();
            var modal = new GameObject("Modal");
            created.Add(modal);

            controller.GetType().GetMethod("Suspend").Invoke(controller, new object[] { modal });
            controller.GetType().GetMethod("ResetToNormal").Invoke(controller, null);

            // 씬을 떠나는 경로다. 잡고 있던 것이 정리될 틈 없이 사라져도 다음 판은 멀쩡해야 한다.
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Setting(controller), Is.EqualTo(1f));
        }
    }
}
