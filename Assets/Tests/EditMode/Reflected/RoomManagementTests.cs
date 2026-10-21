using System.Collections.Generic;
using System.Reflection;
using Code.Buildings;
using Code.MapCreateSystem;
using Code.UI.RoomManagement;
using Code.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 방 관리 화면(Docs/Design/RoomManagementUiDesign.md)의 판정.
    ///
    /// 설치 창은 막힌 항목도 카드로 보이고 이유를 적는다. 이 판정이 실제 설치 경로와 어긋나면 "설치 가능"이라
    /// 보여 주고 결제한 뒤 환불로 끝나거나, 놓을 수 있는 것을 막는다.
    /// </summary>
    public class RoomManagementTests
    {
        private const string BuildingDataFolder = "Assets/07.SO/Buildings";

        private readonly List<GameObject> _hosts = new();

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

        /// <summary>막힌 이유가 상태마다 정확히 붙는다.</summary>
        [Test]
        public void InstallBlockReason_NamesEachBlockedState()
        {
            var store = LoadData("StoreBuildingData");
            var spike = LoadData("FireBrazierTrapBuildingData");
            var portal = LoadData("PortalBuildingData");
            // 라인(통로) 함정은 지금 데이터에 없다. 규칙만 보도록 라인 설치 표시를 켠 데이터를 만든다.
            var edge = ScriptableObject.Instantiate(spike);
            SetField(edge, "<InstallOnEdge>k__BackingField", true);

            Assert.That(Blocked(null, store), Does.Contain("방을 먼저"));
            Assert.That(Blocked(CreateNode(), portal), Does.Contain("입구"));
            Assert.That(InstallAvailabilityRules.TryGetBlockReason(null, edge, true, out _), Is.False, "빈 통로가 있으면 라인 함정은 됩니다.");
            Assert.That(InstallAvailabilityRules.TryGetBlockReason(null, edge, false, out var edgeReason), Is.True);
            Assert.That(edgeReason, Does.Contain("통로"));

            var building = CreateNode();
            building.BeginConstruction(4f, 0);
            Assert.That(Blocked(building, store), Does.Contain("공사 중"));

            var small = CreateNode(RoomSize.Small);
            Assert.That(Blocked(small, store), Does.Contain("소형"));

            var withStore = CreateNode();
            Assert.That(BuildingPlacement.InstallCentral(withStore, store), Is.Not.Null);
            Assert.That(Blocked(withStore, store), Does.Contain("이미 중앙 시설"));
            Assert.That(Blocked(withStore, spike), Does.Contain("손님이 머무는 시설"));

            var withTrap = CreateNode();
            Assert.That(PlaceOnAnyCell(withTrap, spike), Is.True, "빈 방에는 불 화로를 깔 수 있어야 합니다.");
            Assert.That(Blocked(withTrap, store), Does.Contain("함정이 있는 방"));

            var guarded = CreateNode();
            AssignUnit(guarded);
            Assert.That(Blocked(guarded, store), Does.Contain("유닛이 선 방"));
            Assert.That(InstallAvailabilityRules.TryGetBlockReason(guarded, spike, false, out _), Is.False,
                "함정은 유닛과 한 방을 나눠 씁니다.");
            Object.DestroyImmediate(edge);
        }

        /// <summary>
        /// 설치 창의 판정과 실제 설치 경로가 같은 답을 낸다. 판정이 통과시킨 것을 설치가 거절하면 결제 뒤 환불로 끝난다.
        /// 공사 중·잔해 정리 중은 판정 쪽이 더 엄격해(설치 경로는 그 상태를 보지 않는다) 여기서 비교하지 않는다.
        /// </summary>
        [Test]
        public void InstallBlockReason_AgreesWithActualPlacement()
        {
            var items = LoadRoomItems();
            Assert.That(items, Is.Not.Empty);
            var mismatches = new List<string>();

            foreach (var (setupName, setup) in Setups())
            foreach (var data in items)
            {
                var node = CreateNode();
                setup(node);
                var blocked = InstallAvailabilityRules.TryGetBlockReason(node, data, false, out var reason);
                var placed = TryPlace(node, data);
                if (blocked == placed)
                    mismatches.Add($"{setupName} / {data.name}: 판정 {(blocked ? $"막음({reason})" : "허용")} · 설치 {(placed ? "성공" : "실패")}");
            }

            Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
        }

        /// <summary>유닛 배치 판정: 입구·공사 중·건물 있는 방·정원 초과는 막고, 금고·함정 방은 지킬 수 있다.</summary>
        [Test]
        public void DeployBlockReason_GuardsOnlyRoomsWithoutFacilities()
        {
            Assert.That(DeployBlocked(CreateNode()), Is.Null, "빈 방에는 세울 수 있습니다.");

            var entrance = CreateNode();
            typeof(Node).GetProperty(nameof(Node.Data))?.GetSetMethod(true)?.Invoke(entrance,
                new object[] { new DungeonGraph().AddNode(DungeonNodeType.Entrance, Vector2Int.zero) });
            Assert.That(DeployBlocked(entrance), Does.Contain("입구"));

            var building = CreateNode();
            building.BeginConstruction(4f, 0);
            Assert.That(DeployBlocked(building), Does.Contain("공사 중"));

            var withStore = CreateNode();
            BuildingPlacement.InstallCentral(withStore, LoadData("StoreBuildingData"));
            Assert.That(DeployBlocked(withStore), Does.Contain("건물이 있는 방"));

            var withTreasury = CreateNode();
            Assert.That(BuildingPlacement.InstallCentral(withTreasury, LoadResourceData("TreasuryBuildingData")), Is.Not.Null);
            Assert.That(DeployBlocked(withTreasury), Is.Null, "금고는 지켜야 할 곳이라 유닛을 세울 수 있습니다.");

            var full = CreateNode(RoomSize.Small);
            AssignUnit(full);
            AssignUnit(full);
            Assert.That(full.AssignedUnitCount, Is.EqualTo(full.UnitCapacity), "소형 방 정원만큼 세웠어야 합니다.");
            Assert.That(DeployBlocked(full), Does.Contain("정원"));
        }

        /// <summary>시설 버튼: 수리는 수익 시설에만, 휴업은 운영비가 있을 때만, 관리 모드가 아니면 잠그고 이유를 적는다.</summary>
        [Test]
        public void RoomActions_ShowAndLockByFacilityState()
        {
            Assert.That(RoomActionRules.Repair(null, true).Visible, Is.False);
            Assert.That(RoomActionRules.Demolish(null, true).Visible, Is.False);

            var plain = NewFacility(dwellSeconds: 0f, upkeep: 0);
            Assert.That(RoomActionRules.Repair(plain, true).Visible, Is.False, "손님이 머물지 않는 건물은 수리 버튼이 없습니다.");
            Assert.That(RoomActionRules.Closure(plain, 1, true).Visible, Is.False, "운영비가 없으면 닫아도 아낄 것이 없습니다.");

            var shop = NewFacility(dwellSeconds: 3f, upkeep: 2);
            var intact = RoomActionRules.Repair(shop, true);
            Assert.That((intact.Visible, intact.Enabled, intact.Label), Is.EqualTo((true, false, "수리 불필요")));

            shop.ApplyWear(5);
            var repair = RoomActionRules.Repair(shop, true);
            Assert.That(repair.Enabled, Is.True);
            Assert.That(repair.Label, Is.EqualTo($"수리 {shop.RepairCost}G"));
            var lockedRepair = RoomActionRules.Repair(shop, false);
            Assert.That((lockedRepair.Enabled, lockedRepair.Reason), Is.EqualTo((false, RoomActionRules.ManagementOnlyReason)));

            var closure = RoomActionRules.Closure(shop, 1, true);
            Assert.That((closure.Enabled, closure.Label), Is.EqualTo((true, "휴업")));
            shop.Close();
            Assert.That(RoomActionRules.Closure(shop, 1, true).Label, Does.StartWith("다시 열기"));

            var demolish = RoomActionRules.Demolish(shop, false);
            Assert.That((demolish.Visible, demolish.Enabled, demolish.Reason), Is.EqualTo((true, false, RoomActionRules.ManagementOnlyReason)));
        }

        private static IEnumerable<(string, System.Action<Node>)> Setups()
        {
            yield return ("빈 중형 방", _ => { });
            yield return ("빈 소형 방", node => node.SetRoomSize(RoomSize.Small));
            yield return ("불 화로가 깔린 방", node => PlaceOnAnyCell(node, LoadData("FireBrazierTrapBuildingData")));
            yield return ("상점이 선 방", node => BuildingPlacement.InstallCentral(node, LoadData("StoreBuildingData")));
            yield return ("금고가 선 방", node => BuildingPlacement.InstallCentral(node, LoadResourceData("TreasuryBuildingData")));
        }

        // 라인(통로) 건물과 입구는 방 판정 대상이 아니다.
        private static List<BuildingDataSO> LoadRoomItems()
        {
            var items = new List<BuildingDataSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:BuildingDataSO", new[] { BuildingDataFolder }))
            {
                var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.Prefab != null && !data.InstallOnEdge && data.Prefab is not Portal)
                    items.Add(data);
            }

            return items;
        }

        private static bool TryPlace(Node node, BuildingDataSO data)
        {
            if (!BuildingPlacement.UsesGridCell(data))
                return BuildingPlacement.InstallCentral(node, data) != null;

            return PlaceOnAnyCell(node, data);
        }

        // 칸 건물은 어느 칸에든 하나라도 놓이면 설치 가능으로 본다.
        private static bool PlaceOnAnyCell(Node node, BuildingDataSO data)
        {
            var grid = node.TrapGrid;
            for (var row = 0; row < grid.Rows; row++)
            for (var column = 0; column < grid.Columns; column++)
            {
                if (BuildingPlacement.InstallOnCell(node, column, row, data) != null)
                    return true;
            }

            return false;
        }

        private static string Blocked(Node node, BuildingDataSO data)
        {
            Assert.That(data, Is.Not.Null);
            return InstallAvailabilityRules.TryGetBlockReason(node, data, false, out var reason) ? reason : null;
        }

        private static string DeployBlocked(Node node) =>
            InstallAvailabilityRules.TryGetDeployBlockReason(node, out var reason) ? reason : null;

        private void AssignUnit(Node node)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Unit>("Assets/03.Prefabs/Characters/Units/Unit.prefab");
            Assert.That(prefab, Is.Not.Null, "유닛 프리팹을 찾지 못했습니다.");
            var unit = Object.Instantiate(prefab);
            _hosts.Add(unit.gameObject);
            Assert.That(node.TryGetFirstFreeUnitSlot(out var column, out var row), Is.True, "빈 유닛 칸이 있어야 합니다.");
            Assert.That(node.TryAssignUnitToCell(null, unit, column, row), Is.True, "유닛을 세우지 못했습니다.");
        }

        private Node CreateNode(RoomSize size = RoomSize.Medium)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Node>("Assets/03.Prefabs/Map/Node.prefab");
            Assert.That(prefab, Is.Not.Null, "노드 프리팹을 찾지 못했습니다.");
            var node = Object.Instantiate(prefab);
            _hosts.Add(node.gameObject);
            node.SetRoomSize(size);
            return node;
        }

        private Building NewFacility(float dwellSeconds, int upkeep)
        {
            var data = ScriptableObject.CreateInstance<BuildingDataSO>();
            SetField(data, "<DwellSeconds>k__BackingField", dwellSeconds);
            SetField(data, "<DailyUpkeep>k__BackingField", upkeep);
            SetField(data, "<Cost>k__BackingField", 40);

            var host = new GameObject("Facility");
            host.SetActive(false);
            _hosts.Add(host);
            var building = host.AddComponent<Building>();
            SetField(building, "maxDurability", 20);
            SetField(building, "repairCostPerWear", 4);
            building.Initialize(data);
            return building;
        }

        private static BuildingDataSO LoadData(string name)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>($"{BuildingDataFolder}/{name}.asset");
            Assert.That(data, Is.Not.Null, $"{name} 데이터를 찾지 못했습니다.");
            return data;
        }

        private static BuildingDataSO LoadResourceData(string name)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>($"{BuildingDataFolder}/{name}.asset");
            Assert.That(data, Is.Not.Null, $"{name} 데이터를 찾지 못했습니다.");
            return data;
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name} 를 찾지 못했습니다.");
            field.SetValue(target, value);
        }
    }
}
