using System.Collections.Generic;
using System.Linq;
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
        [TestCase("Trap_FireBrazier"), TestCase("Trap_BloodPool"), TestCase("Trap_ManaCrystal")]
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

        /// <summary>
        /// 함정 몸체는 팩의 애니메이션 그림(화로 불꽃·웅덩이 물결·수정 반짝임)이고 함정마다 다르다. 예전에는 노란 임시 네모였고,
        /// 같은 네모를 쓰는 테두리(Border)가 몸체를 덮었다. 몸체는 바닥 타일과 같은 텍셀 크기(25.6/유닛)라 확대 배율에서 바닥과
        /// 함께 또렷하다. 그림은 빈 여백 없이 잘라 카드에서 작게 보이지 않게 하고, 덮은 칸 가운데에 놓인다. 몸체는 바닥·기둥 타일
        /// 위에 그려 기둥 받침이 내려온 아래 칸에서도 함정이 보인다(캐릭터는 그보다 위). 프레임은 게임 시간으로 반복한다.
        /// </summary>
        [TestCase("Trap_FireBrazier"), TestCase("Trap_BloodPool"), TestCase("Trap_ManaCrystal")]
        public void TrapPrefab_ShowsItsOwnDungeonBody(string name)
        {
            var trap = LoadTrapPrefab(name);
            var renderers = trap.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(renderers.Any(r => r.name == "Border"), Is.False, $"{name} 에 노란 테두리가 남아 몸체를 덮습니다.");

            var body = renderers.FirstOrDefault(r => r.name == "Visual");
            Assert.That(body, Is.Not.Null, $"{name} 에 몸체(Visual)가 없습니다.");
            Assert.That(body.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(body.sprite), Does.StartWith("Assets/04.Sprites/Tilesets/Generated/TrapBody_"),
                $"{name} 몸체가 아직 임시 그림입니다.");
            Assert.That(body.sprite.pixelsPerUnit, Is.EqualTo(25.6f).Within(0.001f), "바닥 타일과 같은 텍셀 크기여야 합니다.");

            var footprint = trap.Footprint;
            var scale = Vector2.Scale(body.transform.localScale, new Vector2(footprint.x, footprint.y));
            Assert.That(scale.x, Is.EqualTo(1f).Within(0.001f), "설치하면 그림이 늘어나지 않고 텍셀 크기 그대로입니다.");
            Assert.That(scale.y, Is.EqualTo(1f).Within(0.001f));

            // 기준점이 덮은 칸 가운데라, 잘라 낸 그림은 그 둘레 안에 있어야 한다(조각이 좌우 대칭이 아니라 정확히 가운데는 아니다).
            var bounds = body.sprite.bounds;
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-footprint.x * 0.625f - 0.001f), "덮은 칸 밖으로 나가지 않습니다.");
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(footprint.x * 0.625f + 0.001f));
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-footprint.y * 0.625f - 0.001f));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(footprint.y * 0.625f + 0.001f));
            Assert.That(((Vector2)bounds.center).magnitude, Is.LessThan(0.2f), "덮은 칸 가운데쯤에 놓입니다.");

            // 프레임은 모두 같은 사각형으로 잘렸으니(흔들리지 않게) 여백은 프레임 전체를 합쳐서 본다.
            var firstPath = AssetDatabase.GetAssetPath(body.sprite);
            Assert.That(firstPath, Does.EndWith("_0.png"));
            bool top = false, bottom = false, left = false, right = false;
            for (var frame = 0; System.IO.File.Exists(firstPath.Replace("_0.png", $"_{frame}.png")); frame++)
            {
                var png = new Texture2D(2, 2);
                _created.Add(png);
                png.LoadImage(System.IO.File.ReadAllBytes(firstPath.Replace("_0.png", $"_{frame}.png")));
                var pixels = png.GetPixels32();
                var width = png.width;
                var height = png.height;
                bool Opaque(int x, int y) => pixels[y * width + x].a > 0;
                bottom |= Enumerable.Range(0, width).Any(x => Opaque(x, 0));
                top |= Enumerable.Range(0, width).Any(x => Opaque(x, height - 1));
                left |= Enumerable.Range(0, height).Any(y => Opaque(0, y));
                right |= Enumerable.Range(0, height).Any(y => Opaque(width - 1, y));
            }

            Assert.That(top && bottom && left && right, Is.True, $"{name} 그림에 빈 여백이 남아 설치 카드에서 작게 보입니다.");

            Assert.That(body.sortingOrder, Is.EqualTo(Trap.BodySortingOrder), "몸체는 바닥·기둥 타일 위, 캐릭터 아래입니다.");

            // 닳을 때 흔드는 대상이 비면 루트를 흔드는데, 기준 위치를 노드(배율 15)에 붙기 전 월드 좌표로 잡아 함정이 멀리 튀었다.
            var shake = typeof(Building).GetField("damageAnimationTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(shake, Is.Not.Null, "Building.damageAnimationTarget 를 찾지 못했습니다.");
            Assert.That(shake.GetValue(trap), Is.SameAs(body.transform), $"{name} 은 닳을 때 몸체만 흔들어야 합니다.");

            var animation = body.GetComponent<TrapBodyAnimation>();
            Assert.That(animation, Is.Not.Null, $"{name} 몸체가 움직이지 않습니다.");
            Assert.That(animation.FrameCount, Is.EqualTo(8), "팩 애니메이션 8프레임을 돌립니다.");
        }

        /// <summary>
        /// 맞는 건물은 흔들리는데, 흔들 대상이 루트면 기준 위치를 Awake(노드에 붙기 전 월드 좌표)에서 잡아
        /// 배율 15인 노드 아래에서 첫 피해에 방 밖 수백 유닛으로 튀었다. 흔들기 시작할 때의 자리를 기준으로 삼아야 한다.
        /// </summary>
        [Test]
        public void DamagedBuilding_ShakesInPlaceUnderAScaledNode()
        {
            var node = new GameObject("Node");
            _created.Add(node);
            node.transform.position = new Vector3(-30f, -30f, 0f);
            node.transform.localScale = new Vector3(15f, 15f, 3f);

            var host = new GameObject("Building");
            _created.Add(host);
            host.transform.position = new Vector3(-27f, -31f, 0f);
            var building = host.AddComponent<Building>();
            typeof(Building).GetField("destructible", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(building, true);
            // 실제 순서: 생성(Awake) → 노드에 붙이기(월드 자리 유지) → 피해.
            typeof(Building).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(building, null);
            host.transform.SetParent(node.transform, true);
            var before = host.transform.position;

            typeof(Building).GetField("maxDurability", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(building, 20);
            typeof(Building).GetField("currentDurability", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(building, 20);
            Assert.That(building.TakeBuildingDamage(1), Is.False, "한 번 맞아서는 부서지지 않습니다.");
            Assert.That(Vector3.Distance(host.transform.position, before), Is.LessThan(0.5f), "맞아도 제자리에서 흔들려야 합니다.");
        }

        /// <summary>
        /// 함정 판정을 매 프레임 굴려 발동 확률을 낮춰도 서 있는 동안 결국 밟혔다. 칸에 들어선 순간 한 번만 굴리고,
        /// 피했으면 나갈 때까지 다시 굴리지 않는다. 다시 들어서면 새로 굴린다.
        /// </summary>
        [Test]
        public void TrapStep_RollsOncePerEntry()
        {
            var host = new GameObject("Trap");
            _created.Add(host);
            var trap = host.AddComponent<Trap>();
            var memory = new TrapStepMemory();

            Assert.That(memory.Step(trap, 0.6f, 0.9f), Is.False, "굴림이 확률보다 크면 피합니다.");
            for (var frame = 0; frame < 100; frame++)
                Assert.That(memory.Step(trap, 0.6f, 0f), Is.False, "피한 칸 위에서는 다시 굴리지 않습니다.");

            memory.Leave(trap);
            Assert.That(memory.Step(trap, 0.6f, 0.1f), Is.True, "칸을 나갔다 다시 들어오면 새로 굴립니다.");
        }

        /// <summary>기동성 1점마다 회피 +2%p, 함정 피하기 +4%p, 이동 속도 +5%(0~10).</summary>
        [Test]
        public void Mobility_ScalesEvasionTrapAvoidAndSpeed()
        {
            Assert.That(Code.Enemies.MobilityRules.StepChance(0.6f, 8), Is.EqualTo(0.6f * 0.68f).Within(0.0001f));
            Assert.That(Code.Enemies.MobilityRules.StepChance(0.6f, 0), Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(Code.Enemies.MobilityRules.EvasionBonus(8), Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(Code.Enemies.MobilityRules.SpeedMultiplier(8), Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(Code.Enemies.MobilityRules.StepChance(0.6f, 99), Is.EqualTo(0.6f * 0.6f).Within(0.0001f), "10에서 멈춥니다.");
        }

        /// <summary>설치된 함정은 쉽게 밟히지 않는다(기본 60%), 적마다 기동성이 정해져 있다.</summary>
        [Test]
        public void TrapsAndAdventurers_UseAgreedMobilityValues()
        {
            foreach (var name in new[] { "Trap_FireBrazier", "Trap_BloodPool", "Trap_ManaCrystal" })
                Assert.That(LoadTrapPrefab(name).TriggerChance, Is.EqualTo(0.6f).Within(0.0001f), name);

            var expected = new Dictionary<string, int>
            {
                { "ThiefEnemy", 8 }, { "ScoutEnemy", 7 }, { "BasicEnemy", 3 }, { "ArcherEnemy", 4 }, { "SapperEnemy", 2 },
                { "HealterEnemy", 2 }, { "SwordEnemy", 2 }, { "PaladinEnemy", 1 }, { "Enemy_Tourist_Coward", 3 },
                { "Enemy_Tourist_Priest", 3 }, { "Enemy_Tourist_Shopaholic", 3 }, { "Boss_GreedKnight", 2 },
                { "Boss_Houndmaster", 2 }, { "Boss_KnightCommander", 2 }, { "Boss_Subjugator", 2 }
            };
            foreach (var guid in AssetDatabase.FindAssets("t:Code.Enemies.EnemyDataSO", new[] { "Assets/07.SO/Enemies" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<Code.Enemies.EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(expected.ContainsKey(data.name), Is.True, $"{data.name} 의 기동성을 정하지 않았습니다.");
                Assert.That(data.Mobility, Is.EqualTo(expected[data.name]), data.name);
            }
        }

        /// <summary>그림이 없는 함정은 지웠다. 남은 함정은 팩 그림이 있는 셋뿐이고, 각자 해금 목록에 올라 있다.</summary>
        [Test]
        public void TrapRoster_IsOnlyTrapsWithPackArt()
        {
            var names = AssetDatabase.FindAssets("TrapBuildingData t:ScriptableObject", new[] { "Assets/07.SO/Buildings" })
                .Select(guid => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            Assert.That(names, Is.EquivalentTo(new[]
            {
                "FireBrazierTrapBuildingData", "BloodPoolTrapBuildingData", "ManaCrystalTrapBuildingData"
            }));

            var catalog = AssetDatabase.LoadAssetAtPath<Code.Progression.DungeonUnlockCatalogSO>(
                "Assets/07.SO/Progression/DungeonUnlockCatalog.asset");
            var listed = catalog.Entries.Where(entry => entry?.Building != null && entry.Building.Prefab is Trap)
                .Select(entry => entry.Building.name).ToArray();
            Assert.That(listed, Is.EquivalentTo(names), "해금 목록의 함정이 남은 함정과 같아야 합니다.");
            Assert.That(catalog.Entries.Any(entry => entry?.Building != null && entry.Building.name == "FireBrazierTrapBuildingData"
                && entry.StartsUnlocked), Is.True, "처음부터 깔 수 있는 함정이 하나는 있어야 합니다.");
        }

        /// <summary>함정 데이터의 보드 그림이 프리팹 몸체와 같고 함정끼리 겹치지 않는다(설치 뒤 Initialize가 보드 그림으로 바꾼다).</summary>
        [Test]
        public void TrapData_BoardSpriteMatchesPrefabBodyAndIsUnique()
        {
            var seen = new Dictionary<Sprite, string>();
            foreach (var guid in AssetDatabase.FindAssets("TrapBuildingData t:ScriptableObject", new[] { "Assets/07.SO/Buildings" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || data.Prefab == null)
                    continue;

                var body = data.Prefab.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name == "Visual");
                Assert.That(body, Is.Not.Null, data.name);
                Assert.That(data.BoardSprite, Is.EqualTo(body.sprite), $"{data.name} 보드 그림이 프리팹 몸체와 다릅니다.");
                Assert.That(seen.ContainsKey(data.BoardSprite), Is.False,
                    $"{data.name} 와 {(seen.TryGetValue(data.BoardSprite, out var other) ? other : "")} 가 같은 몸체를 씁니다.");
                seen[data.BoardSprite] = data.name;
            }

            Assert.That(seen.Count, Is.EqualTo(3));
        }

        /// <summary>
        /// 설치 카드·미리보기는 프리팹의 첫 SpriteRenderer 그림을 쓴다. 칸막이는 방에 기둥 타일로 그려져 몸체가 꺼져 있는데,
        /// 그 몸체에 임시 건물 그림이 남아 카드에 노란 네모가 나왔다. 카드 그림은 보드 그림(기둥 조각)과 같아야 한다.
        /// </summary>
        [Test]
        public void Partition_CardShowsItsBoardArt()
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>("Assets/07.SO/Buildings/PartitionBuildingData.asset");
            Assert.That(data, Is.Not.Null);
            var cardSprite = data.Prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite;
            Assert.That(cardSprite, Is.EqualTo(data.BoardSprite), "칸막이 카드가 임시 그림을 보여 줍니다.");
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
            var crusher = LoadTrapPrefab("Trap_BloodPool");
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
            var crusher = LoadTrapPrefab("Trap_BloodPool");

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
        }
    }
}
