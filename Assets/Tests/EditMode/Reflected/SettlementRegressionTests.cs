using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

        private static Type Resolve(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private Component Component(string typeName, string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            created.Add(go);
            if (typeName == "_01.Code.MapCreateSystem.Node")
                go.AddComponent<BoxCollider2D>();
            var component = go.AddComponent(Resolve(typeName));
            Assert.That(component, Is.Not.Null);
            return component;
        }

        private ScriptableObject UnitData(string name, int cost)
        {
            var data = ScriptableObject.CreateInstance(Resolve("_01.Code.Units.UnitDataSO"));
            created.Add(data);
            Set(data, "<Name>k__BackingField", name);
            Set(data, "<Cost>k__BackingField", cost);
            return data;
        }

        private Component Unit(ScriptableObject data, float fatigue)
        {
            var unit = Component("_01.Code.Units.Unit", data.name);
            Set(unit, "<Data>k__BackingField", data);
            Set(unit, "fatigue", fatigue);
            return unit;
        }

        private static object Event(string name, params object[] args) =>
            Activator.CreateInstance(Resolve("_01.Code.Events." + name), args);

        [SetUp]
        public void SetUp()
        {
            previousRoster = Resolve("_01.Code.Manager.HiredUnitRoster").GetProperty("Current").GetValue(null);
            previousCost = Resolve("_01.Code.Manager.CostManager").GetProperty("Current").GetValue(null);
            previousSettlement = Resolve("_01.Code.Manager.ManagementSettlementManager").GetProperty("Current").GetValue(null);
            previousTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
                UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();
            Resolve("_01.Code.Manager.HiredUnitRoster").GetProperty("Current").SetValue(null, previousRoster);
            Resolve("_01.Code.Manager.CostManager").GetProperty("Current").SetValue(null, previousCost);
            Resolve("_01.Code.Manager.ManagementSettlementManager").GetProperty("Current").SetValue(null, previousSettlement);

            // 배속 테스트는 전역 시간을 건드린다. 되돌리지 않으면 뒤따르는 테스트가 멈춘 채로 돈다.
            Time.timeScale = previousTimeScale;
        }

        [Test]
        public void Upkeep_AfterRosterRestore_ChargesAvailableAndDeployedUnitsButNotApplicants()
        {
            var roster = Component("_01.Code.Manager.HiredUnitRoster", "Roster");
            var manager = Component("_01.Code.Manager.ManagementSettlementManager", "Settlement");
            var unit = UnitData("Restored unit", 14);
            Resolve("_01.Code.Manager.HiredUnitRoster").GetProperty("Current").SetValue(null, roster);
            // Equivalent to the restored roster: one reserve, two deployed, five unhired candidates.
            ((IList)roster.GetType().GetField("_availableUnits", PrivateInstance).GetValue(roster)).Add(unit);
            ((IDictionary)roster.GetType().GetField("_deployedUnits", PrivateInstance).GetValue(roster)).Add(unit, 2);
            ((IDictionary)roster.GetType().GetField("_ownedUnits", PrivateInstance).GetValue(roster)).Add(unit, 5);
            Assert.That(Call(manager, "CalculateDailyUpkeep"), Is.EqualTo(9),
                "Restoring a run must not make the wages of already hired units disappear.");
        }

        [Test]
        public void Fatigue_RestoredPlacementWithoutDeployEvent_IsReported()
        {
            var manager = Component("_01.Code.Manager.ManagementSettlementManager", "Settlement");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04.Prefab/Map/Node.prefab");
            var go = UnityEngine.Object.Instantiate(prefab);
            created.Add(go);
            var node = go.GetComponent(Resolve("_01.Code.MapCreateSystem.Node"));
            var modelType = Resolve("_01.Code.MapCreateSystem.DungeonNode");
            var model = Activator.CreateInstance(modelType,
                Enum.Parse(Resolve("_01.Code.MapCreateSystem.DungeonNodeType"), "Corridor"),
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
            Assert.That((string)Call(manager, "BuildFatigueText"), Does.Contain("73"));
        }

        [Test]
        public void Fatigue_SameRoom_ReportsEveryUnitIncludingSameType()
        {
            var manager = Component("_01.Code.Manager.ManagementSettlementManager", "Settlement");
            var node = Component("_01.Code.MapCreateSystem.Node", "Room");
            var data = UnitData("Slime", 14);
            var tired = Unit(data, 80);
            var fresh = Unit(data, 20);
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, tired));
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, fresh));
            Call(manager, "ApplyBattleFatigue");
            var report = (string)Call(manager, "BuildFatigueText");
            Assert.That(report, Does.Contain("80"));
            Assert.That(report, Does.Contain("20"));
        }

        [Test]
        public void Fatigue_ReturningOneUnit_DoesNotRemoveItsRoommate()
        {
            var manager = Component("_01.Code.Manager.ManagementSettlementManager", "Settlement");
            var node = Component("_01.Code.MapCreateSystem.Node", "Room");
            var data = UnitData("Slime", 14);
            var remaining = Unit(data, 80);
            var returning = Unit(data, 20);
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, remaining));
            Call(manager, "HandleUnitAssigned", Event("UnitAssignedToNodeEvent", node, data, returning));
            Call(manager, "HandleUnitReturned", Event("UnitReturnedFromNodeEvent", node, data, returning));
            Call(manager, "ApplyBattleFatigue");
            var report = (string)Call(manager, "BuildFatigueText");
            Assert.That(report, Does.Contain("80"));
            Assert.That(report, Does.Not.Contain("20"));
        }

        [Test]
        public void FailedConstruction_RefundsChargedGoldAndRestoresConsumedDiscount()
        {
            var channel = ScriptableObject.CreateInstance(Resolve("_01.Code.Core.GameEventChannelSO"));
            created.Add(channel);
            var cost = Component("_01.Code.Manager.CostManager", "Construction cost");
            var settlement = Component("_01.Code.Manager.ManagementSettlementManager", "Construction ledger");
            var panel = Component("_01.Code.UI.NodePanelView", "Construction panel");
            var node = Component("_01.Code.MapCreateSystem.Node", "Construction room");
            var buildingData = ScriptableObject.CreateInstance(Resolve("_01.Code.Buildings.BuildingDataSO"));
            created.Add(buildingData);

            Set(cost, "costEventChannel", channel);
            Set(settlement, "costEventChannel", channel);
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
            var cost = Component("_01.Code.Manager.CostManager", "Revival cost");
            Set(cost, "initialGold", 30);
            Set(cost, "weeklyDebtInterest", 0.1f);
            Call(cost, "Awake");

            var borrowed = cost.GetType().GetMethod("ChargeOrBorrow").Invoke(cost, new object[] { 50 });

            Assert.That(borrowed, Is.EqualTo(20), "모자란 만큼만 빚이 됩니다.");
            Assert.That(cost.GetType().GetProperty("CurrentGold").GetValue(cost), Is.Zero, "가진 금화를 먼저 씁니다.");
            Assert.That(cost.GetType().GetProperty("CurrentDebt").GetValue(cost), Is.EqualTo(20));
        }

        [Test]
        public void Revival_IsNeverRefusedForLackOfGold()
        {
            var cost = Component("_01.Code.Manager.CostManager", "Revival cost");
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
            var system = Component("_01.Code.Manager.UnitRevivalSystem", "Revival system");
            Set(system, "baseRevivalCost", 15);
            Set(system, "costPerLevel", 8);

            var getCost = system.GetType().GetMethod("GetRevivalCost");
            var data = UnitData("veteran", 40);
            var unit = Unit(data, 0f);

            // 레벨 컴포넌트가 없으면 1레벨로 본다. 기본값만 나와야 한다.
            Assert.That(getCost.Invoke(system, new object[] { unit }), Is.EqualTo(15));
            Assert.That(getCost.Invoke(system, new object[] { null }), Is.EqualTo(15),
                "부하가 사라진 뒤에도 값을 물어볼 수 있어야 합니다.");
        }

        [Test]
        public void Revival_KeepsFatigueAndInjurySoDyingIsNotAShortcut()
        {
            var data = UnitData("tired", 40);
            var unit = Unit(data, 70f);
            Set(unit, "injury", Enum.Parse(Resolve("_01.Code.Units.InjurySeverity"), "Severe"));
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
            var controller = Component("_01.Code.Manager.GameSpeedController", "Game speed");
            Call(controller, "Awake");
            return controller;
        }

        private static float Setting(object controller) =>
            (float)controller.GetType().GetProperty("Setting").GetValue(controller);

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
