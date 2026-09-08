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
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
                UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();
            Resolve("_01.Code.Manager.HiredUnitRoster").GetProperty("Current").SetValue(null, previousRoster);
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
    }
}
