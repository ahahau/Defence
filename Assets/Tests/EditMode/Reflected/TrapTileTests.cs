using System.Collections.Generic;
using System.Reflection;
using Code.Buildings;
using Code.MapCreateSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 함정의 칸 수·발동 방식·재사용 시간·시설 방 금지.
    ///
    /// 예전 함정은 모두 한 칸·한 명·방문당 한 번이라 종류가 피해량 차이일 뿐이었다. 여러 칸 함정이
    /// 격자를 제대로 점유·철거·저장하는지, 범위·지속 판정과 재사용 대기가 맞는지, 손님이 머무는
    /// 시설과 함정이 한 방에 겹치지 않는지를 지킨다.
    /// </summary>
    public class TrapTileTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private NodeTrapGrid NewGrid()
        {
            var host = new GameObject("TrapGridHost");
            _created.Add(host);
            return host.AddComponent<NodeTrapGrid>();
        }

        private static Trap LoadTrapPrefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/03.Prefabs/Buildings/Traps/{name}.prefab");
            Assert.That(prefab, Is.Not.Null, $"{name}.prefab 을 찾지 못했습니다.");
            return prefab.GetComponent<Trap>();
        }

        [Test]
        public void PersistentDamage_IsSplitAcrossTicks()
        {
            Assert.That(TrapRules.PersistentTickCount(3f), Is.EqualTo(3));
            Assert.That(TrapRules.PersistentTickDamage(12, 3f), Is.EqualTo(4));
            Assert.That(TrapRules.PersistentTickDamage(10, 3f), Is.EqualTo(4), "나눠 떨어지지 않으면 올림해 총량이 줄지 않습니다.");
            Assert.That(TrapRules.PersistentTickDamage(1, 3f), Is.EqualTo(1), "한 번 피해는 최소 1입니다.");
        }

        /// <summary>발동 효과가 한 번 돌고 사라지는지, 지속형은 끝까지 반복하는지. 멈추지 않는 효과는 함정이 계속 터지는 것처럼 보인다.</summary>
        [Test]
        public void TrapEffect_PlaysOnceOrLoopsThroughFrames()
        {
            Assert.That(TrapEffectRules.FrameAt(0f, 0.1f, 5, false), Is.EqualTo(0));
            Assert.That(TrapEffectRules.FrameAt(0.45f, 0.1f, 5, false), Is.EqualTo(4));
            Assert.That(TrapEffectRules.FrameAt(0.51f, 0.1f, 5, false), Is.EqualTo(-1), "한 번 도는 효과는 끝나면 사라집니다.");
            Assert.That(TrapEffectRules.FrameAt(0.51f, 0.1f, 5, true), Is.EqualTo(0), "반복 효과는 처음 프레임으로 돌아갑니다.");
            Assert.That(TrapEffectRules.FrameAt(0f, 0.1f, 0, true), Is.EqualTo(-1));

            var tiles = new Vector2Int(2, 1);
            Assert.That(TrapEffectRules.TileCenter(Vector2.zero, tiles, 2f, 0, 0), Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(TrapEffectRules.TileCenter(Vector2.zero, tiles, 2f, 1, 0), Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(TrapEffectRules.AreaWidthCells(new Vector2Int(2, 2), 1f), Is.EqualTo(4f), "폭발은 범위가 닿는 곳만큼 큽니다.");
        }

        /// <summary>함정마다 방식에 맞는 발동 효과가 배선돼 있는지. 비면 흔들림만 보여 무엇이 터졌는지 모른다.</summary>
        [TestCase("Trap"), TestCase("Trap_Spike"), TestCase("Trap_Snare"), TestCase("Generated/Trap_ArmorBreaker"),
         TestCase("Trap_Crusher"), TestCase("Generated/Trap_AmbushNet"), TestCase("Trap_Blade"),
         TestCase("Generated/Trap_Caltrop"), TestCase("Generated/Trap_ExecutionSaw"), TestCase("Trap_Arrow")]
        public void TrapPrefab_HasTriggerEffectForItsMode(string name)
        {
            var trap = LoadTrapPrefab(name);
            var visual = trap.GetComponent<TrapTriggerVisual>();
            Assert.That(visual, Is.Not.Null, $"{name} 에 TrapTriggerVisual 이 없습니다.");
            if (trap.TriggerMode == TrapTriggerMode.Projectile)
                Assert.That(visual.HasProjectile, Is.True, $"{name} 에 화살 그림이 없습니다.");
            else
                Assert.That(visual.HasFrames, Is.True, $"{name} 에 발동 프레임이 없습니다.");
        }

        [Test]
        public void Footprint_CoversAllTilesButNotBeyond()
        {
            var center = Vector2.zero;
            var twoByOne = new Vector2Int(2, 1);

            Assert.That(TrapRules.CoversFootprint(center, twoByOne, 2f, 0.8f, new Vector2(1.5f, 0f)), Is.True,
                "두 칸짜리는 옆 칸 위에서도 밟힙니다.");
            Assert.That(TrapRules.CoversFootprint(center, twoByOne, 2f, 0.8f, new Vector2(2.5f, 0f)), Is.False);
            Assert.That(TrapRules.CoversFootprint(center, Vector2Int.one, 2f, 0.8f, new Vector2(0.7f, 0f)), Is.True);
            Assert.That(TrapRules.CoversFootprint(center, Vector2Int.one, 2f, 0.8f, new Vector2(0.9f, 0f)), Is.False,
                "한 칸짜리는 예전처럼 중심 근처만 밟힙니다.");
        }

        [Test]
        public void Area_ReachesOneCellAroundTheFootprint()
        {
            var footprint = new Vector2Int(2, 2);
            Assert.That(TrapRules.InArea(Vector2.zero, footprint, 2f, 1f, new Vector2(3.9f, 0f)), Is.True);
            Assert.That(TrapRules.InArea(Vector2.zero, footprint, 2f, 1f, new Vector2(4.1f, 0f)), Is.False);
        }

        [Test]
        public void HoverAnchor_CentersTheFootprintOnTheCursor()
        {
            Assert.That(TrapRules.AnchorForHover(3, 3, Vector2Int.one), Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(TrapRules.AnchorForHover(3, 3, new Vector2Int(2, 2)), Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(TrapRules.AnchorForHover(3, 3, new Vector2Int(3, 1)), Is.EqualTo(new Vector2Int(2, 3)));
        }

        [Test]
        public void Grid_PlacesMultiTileTrapOnEveryCoveredCellAndRemovesItWhole()
        {
            var grid = NewGrid();
            var crusher = LoadTrapPrefab("Trap_Crusher");
            Assert.That(crusher.Footprint, Is.EqualTo(new Vector2Int(2, 2)));

            var placed = grid.TryPlace(0, 0, crusher);
            _created.Add(placed != null ? placed.gameObject : null);
            Assert.That(placed, Is.Not.Null);
            Assert.That(grid.BuildingAt(1, 1), Is.SameAs(placed), "덮은 칸 모두에서 같은 함정이 보여야 합니다.");
            Assert.That(grid.CanPlace(1, 0, Vector2Int.one), Is.False);
            Assert.That(grid.TryGetAnchor(placed, out var column, out var row), Is.True);
            Assert.That(new Vector2Int(column, row), Is.EqualTo(Vector2Int.zero));
            Assert.That(grid.PlacedCount, Is.EqualTo(1), "여러 칸이어도 함정은 하나입니다.");

            grid.Remove(1, 1);
            Assert.That(grid.IsCellFree(0, 0) && grid.IsCellFree(1, 0) && grid.IsCellFree(0, 1), Is.True,
                "어느 칸으로 철거하든 덮은 칸이 모두 비어야 합니다.");
        }

        [Test]
        public void Grid_RejectsFootprintThatHitsEdgeOrCentralSlotOrAnotherTrap()
        {
            var grid = NewGrid();
            var crusher = LoadTrapPrefab("Trap_Crusher");

            Assert.That(grid.CanPlace(5, 0, crusher.Footprint), Is.False, "격자 밖으로 삐져나가면 안 됩니다.");
            Assert.That(grid.CanPlace(1, 1, crusher.Footprint), Is.False, "중앙 시설 자리를 덮으면 안 됩니다.");

            var first = grid.TryPlace(0, 4, crusher);
            _created.Add(first.gameObject);
            Assert.That(grid.TryPlace(1, 4, crusher), Is.Null, "다른 함정과 한 칸이라도 겹치면 안 됩니다.");
            Assert.That(grid.TryGetNearestFreeCell(grid.CellWorldPosition(0, 4), crusher.Footprint, out var c, out var r), Is.True);
            Assert.That(grid.CanPlace(c, r, crusher.Footprint), Is.True);
        }

        [Test]
        public void Cooldown_BlocksRetriggerUntilItRunsOut()
        {
            var host = new GameObject("TrapHost");
            _created.Add(host);
            host.SetActive(false);
            var trap = host.AddComponent<Trap>();
            typeof(Trap).GetField("cooldownSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(trap, 3f);
            typeof(Trap).GetField("_cooldownRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(trap, 3f);

            Assert.That(trap.IsReady, Is.False);
            trap.TickTrap(2.9f);
            Assert.That(trap.IsReady, Is.False);
            trap.TickTrap(0.2f);
            Assert.That(trap.IsReady, Is.True, "재사용 시간이 지나면 다시 밟힙니다.");
        }

        [Test]
        public void FacilityAndTrap_NeverShareARoom()
        {
            var trapData = ScriptableObject.CreateInstance<BuildingDataSO>();
            var innData = ScriptableObject.CreateInstance<BuildingDataSO>();
            var vaultData = ScriptableObject.CreateInstance<BuildingDataSO>();
            _created.Add(trapData);
            _created.Add(innData);
            _created.Add(vaultData);
            typeof(BuildingDataSO).GetField("<Category>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(trapData, InstallCategory.Trap);
            typeof(BuildingDataSO).GetField("<DwellSeconds>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(innData, 3f);

            var innHost = new GameObject("Inn");
            _created.Add(innHost);
            innHost.SetActive(false);
            var inn = innHost.AddComponent<Building>();
            inn.Initialize(innData);

            Assert.That(TrapRules.ConflictsWithFacility(trapData, inn, false), Is.True, "손님이 머무는 방에는 함정을 못 깝니다.");
            Assert.That(TrapRules.ConflictsWithFacility(innData, null, true), Is.True, "함정이 있는 방에는 그 시설을 못 짓습니다.");
            Assert.That(TrapRules.ConflictsWithFacility(vaultData, null, true), Is.False, "머물지 않는 시설은 상관없습니다.");
            Assert.That(TrapRules.ConflictsWithFacility(trapData, null, false), Is.False);
        }

        /// <summary>
        /// 투사체(화살) 함정: 같은 가로줄(위아래 반 칸 안)에 선 모험가 중 서쪽 끝에서 날아온 화살에 가장 먼저 닿는,
        /// 가장 서쪽 대상을 맞힌다(사용자 결정). 줄 판정이 넓으면 다른 줄의 모험가가 맞는다.
        /// </summary>
        [Test]
        public void ProjectileTrap_HitsTheFirstAdventurerAlongItsRow()
        {
            var trap = new Vector2(0f, 0f);
            Assert.That(TrapRules.OnSameRow(trap, 1f, new Vector2(3f, 0.4f)), Is.True);
            Assert.That(TrapRules.OnSameRow(trap, 1f, new Vector2(-2f, -0.5f)), Is.True);
            Assert.That(TrapRules.OnSameRow(trap, 1f, new Vector2(0f, 0.6f)), Is.False, "위 줄은 맞지 않습니다.");

            Assert.That(TrapRules.FirstHitFromWest(new List<float> { 2f, -1f, 0.5f }), Is.EqualTo(1));
            Assert.That(TrapRules.FirstHitFromWest(new List<float>()), Is.EqualTo(-1));

            var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>("Assets/07.SO/Buildings/ArrowTrapBuildingData.asset");
            Assert.That(data, Is.Not.Null, "화살 함정 데이터를 찾지 못했습니다.");
            Assert.That(data.Category, Is.EqualTo(InstallCategory.Trap));
            var arrow = data.Prefab as Trap;
            Assert.That(arrow, Is.Not.Null, "화살 함정 프리팹이 Trap이어야 합니다.");
            Assert.That(arrow.TriggerMode, Is.EqualTo(TrapTriggerMode.Projectile));
        }
    }
}
