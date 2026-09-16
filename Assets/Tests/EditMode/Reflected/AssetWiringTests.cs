using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.EditMode.Gameplay
{
    /// <summary>
    /// 데이터가 가리키는 그림이 실제로 붙어 있는지 본다.
    ///
    /// 이 프로젝트에서 반복해서 터진 자리다. 값이 비어 있는 경우도 있었고, 값은 들어 있는데
    /// 가리키는 파일이 지워져 끊긴 경우도 있었다. 둘 다 게임이 조용히 돌아가서 눈으로 보기 전엔
    /// 모른다 — 적이 안 보이거나, 승급한 건물이 승급 전 그림으로 나오거나, 함정 아홉이 전부
    /// 같은 돌덩이로 보이거나.
    ///
    /// 에디터에서는 끊긴 참조도 null 로 읽히므로 null 검사 하나로 둘 다 걸린다.
    /// </summary>
    public class AssetWiringTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>아직 그림이 없기로 되어 있는 건물. 그림이 들어오면 여기서 빼면 된다.</summary>
        private static readonly string[] ArtPendingBuildings = System.Array.Empty<string>();

        [Test]
        public void EveryAdventurer_HasAllThreePosesAndPrefab()
        {
            var missing = new List<string>();

            foreach (var (asset, path) in LoadAll("_01.Code.Enemies.EnemyDataSO"))
            {
                foreach (var field in new[] { "IdleSprite", "AttackSprite", "DefeatedSprite", "BoardSprite" })
                    if (ReadProperty(asset, field) == null)
                        missing.Add(asset.name + " · " + field);

                if (ReadProperty(asset, "Prefab") == null)
                    missing.Add(asset.name + " · Prefab");

                Assert.That(path, Does.StartWith("Assets/"));
            }

            Assert.That(missing, Is.Empty,
                "그림이나 프리팹이 비었거나 끊긴 모험가:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void EveryBuildingAndTrap_HasBoardSprite_ExceptArtPending()
        {
            var missing = new List<string>();

            foreach (var (asset, _) in LoadAll("_01.Code.Buildings.BuildingDataSO"))
            {
                if (ArtPendingBuildings.Contains(asset.name))
                    continue;

                if (ReadProperty(asset, "BoardSprite") == null)
                    missing.Add(asset.name);
            }

            Assert.That(missing, Is.Empty,
                "보드에 올릴 그림이 비었거나 끊긴 건물·함정:\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// 함정 아홉이 서로 다른 그림을 써야 한다. 하나를 돌려 쓰면 무엇이 터졌는지 구분이 안 된다.
        /// </summary>
        [Test]
        public void Traps_DoNotShareTheSameArt()
        {
            var byArt = new Dictionary<UnityEngine.Object, List<string>>();

            foreach (var (asset, _) in LoadAll("_01.Code.Buildings.BuildingDataSO"))
            {
                if (!asset.name.Contains("Trap"))
                    continue;

                var art = ReadProperty(asset, "BoardSprite");
                if (art == null)
                    continue;

                if (!byArt.TryGetValue(art, out var users))
                    byArt[art] = users = new List<string>();
                users.Add(asset.name);
            }

            var shared = byArt.Where(pair => pair.Value.Count > 1)
                .Select(pair => pair.Key.name + " <- " + string.Join(", ", pair.Value))
                .ToList();

            Assert.That(shared, Is.Empty, "한 그림을 나눠 쓰는 함정:\n  " + string.Join("\n  ", shared));
        }

        [Test]
        public void WaveConfig_RunsForeverWithFourWeeksOfAuthoredDays()
        {
            var configType = RequireType("_01.Code.Manager.WaveConfigSO");
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/03.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig 에셋을 찾지 못했습니다.");

            var finalDay = configType.GetProperty("FinalDay").GetValue(config);
            Assert.That(finalDay, Is.Zero, "끝나는 날이 있으면 무한 진행이 아닙니다.");

            // 4주치는 손으로 짠 날이어야 한다. 없으면 defaultWave 하나로 매일이 같아진다.
            var getWave = configType.GetMethod("GetWaveForDay");
            var entryType = RequireType("_01.Code.Manager.WaveConfigSO+WaveEntry");
            var targetDayField = entryType.GetField("targetDay");
            var unauthored = new List<int>();

            for (var day = 1; day <= 28; day++)
            {
                var entry = getWave.Invoke(config, new object[] { day });
                if (entry == null || (int)targetDayField.GetValue(entry) != day)
                    unauthored.Add(day);
            }

            Assert.That(unauthored, Is.Empty,
                "일차별 수치가 없는 날: " + string.Join(", ", unauthored));
        }

        [TestCase(7)]
        [TestCase(14)]
        [TestCase(21)]
        [TestCase(28)]
        public void WaveConfig_PutsABossOnEverySettlementDay(int day)
        {
            var configType = RequireType("_01.Code.Manager.WaveConfigSO");
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/03.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig 에셋을 찾지 못했습니다.");

            // 보스와 청산이 같은 날이라 한 주가 하나의 고비로 끝난다.
            // 둘이 어긋나면 주의 리듬이 사라지고 보스날은 그냥 힘든 날이 된다.
            Assert.That(configType.GetMethod("IsBossDay").Invoke(config, new object[] { day }), Is.True,
                $"{day}일은 청산일인데 보스가 없습니다.");
            Assert.That(configType.GetMethod("GetBossForDay").Invoke(config, new object[] { day }), Is.Not.Null,
                $"{day}일 보스는 전용 정의가 있어야 공용 보스와 구분됩니다.");
        }

        [Test]
        public void GoldPanel_ListensToBothTheCostAndTheDayChannel()
        {
            var viewType = RequireType("_01.Code.UI.GoldCostView");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04.Prefab/UI/Hud/GoldCostPanel.prefab");
            Assert.That(prefab, Is.Not.Null, "금화 패널 프리팹을 찾지 못했습니다.");

            var view = prefab.GetComponentInChildren(viewType, true);
            Assert.That(view, Is.Not.Null, "금화 패널에 GoldCostView가 없습니다.");

            // 날짜 채널이 끊기면 빚은 보이는데 청산일까지 며칠인지가 멈춘다.
            // 화면은 멀쩡해 보이므로 플레이로는 잡기 어렵다.
            foreach (var field in new[] { "costEventChannel", "dayEventChannel" })
            {
                var info = viewType.GetField(field, Instance);
                Assert.That(info, Is.Not.Null, field + " 필드를 찾지 못했습니다.");
                Assert.That(info.GetValue(view), Is.Not.Null, field + "이 비어 있습니다.");
            }
        }

        [Test]
        public void EveryCharacterPrefab_HasAllThreePoses()
        {
            var renderType = RequireType("_01.Code.Entities.EntityRender");
            var missing = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/04.Prefab/Characters" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    continue;

                var render = root.GetComponentInChildren(renderType, true);
                if (render == null)
                    continue;

                foreach (var field in new[] { "idleSprite", "attackSprite", "defeatedSprite" })
                {
                    var info = renderType.GetField(field, Instance);
                    Assert.That(info, Is.Not.Null, field + " 필드를 찾지 못했습니다.");
                    if (info.GetValue(render) == null)
                        missing.Add(root.name + " · " + field);
                }
            }

            Assert.That(missing, Is.Empty,
                "포즈 그림이 비었거나 끊긴 프리팹:\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// 유물은 목록에서 그림으로 구분된다. 하나라도 비면 그 칸만 빈 상자로 나온다.
        /// </summary>
        [Test]
        public void EveryArtifact_HasIcon()
        {
            var missing = new List<string>();

            foreach (var (asset, _) in LoadAll("_01.Code.Artifacts.ArtifactDataSO"))
                if (ReadProperty(asset, "Icon") == null)
                    missing.Add(asset.name);

            Assert.That(missing, Is.Empty,
                "그림이 비었거나 끊긴 유물:\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// 스탯 표식은 유닛 정보창에서 수치 옆에 붙는다. 하나가 비면 그 줄만 표식 없이 나와
        /// 줄이 어긋나는데, 화면을 열어 보기 전까지 아무도 모른다.
        /// </summary>
        [Test]
        public void EveryStat_HasIcon()
        {
            var missing = new List<string>();

            foreach (var (asset, _) in LoadAll("_01.Code.Core.Stats.StatSO"))
                if (ReadProperty(asset, "Icon") == null)
                    missing.Add(asset.name);

            Assert.That(missing, Is.Empty,
                "그림이 비었거나 끊긴 스탯:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void ServiceBuildings_UseCentralRoomSlot()
        {
            var paths = new[]
            {
                "Assets/Resources/Buildings/BlacksmithBuildingData.asset",
                "Assets/03.SO/Buildings/StoreBuildingData.asset",
                "Assets/03.SO/Buildings/ArmoryStoreBuildingData.asset",
                "Assets/03.SO/Buildings/InnBuildingData.asset",
                "Assets/03.SO/Buildings/GrandInnBuildingData.asset"
            };

            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(asset, Is.Not.Null, path);
                Assert.That((bool)asset.GetType().GetProperty("CentralOnly", Instance).GetValue(asset), Is.True, path);
                Assert.That((bool)asset.GetType().GetProperty("InstallOnEdge", Instance).GetValue(asset), Is.False, path);
            }
        }

        [Test]
        public void OpeningScene_HasNoPlayerAndBlacksmithInstallsAtCentre()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var controllerType = RequireType("_01.Code.MapCreateSystem.DungeonGraphController");
                Component controller = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    controller = root.GetComponentInChildren(controllerType, true);
                    if (controller != null)
                        break;
                }

                Assert.That(controller, Is.Not.Null);
                controllerType.GetMethod("EditorBakeInitialScenePreview").Invoke(controller, null);

                var unitsRoot = (Transform)controllerType.GetField("unitsRoot", Instance).GetValue(controller);
                Assert.That(unitsRoot.childCount, Is.Zero, "시작 배치에 플레이어가 생성되었습니다.");

                var nodeManager = (Component)controllerType.GetField("nodeManager", Instance).GetValue(controller);
                var nodeType = RequireType("_01.Code.MapCreateSystem.Node");
                var entrance = nodeManager.GetComponentInChildren(nodeType, true);
                Assert.That(entrance, Is.Not.Null);

                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/BlacksmithBuildingData.asset");
                var placementType = RequireType("_01.Code.Buildings.BuildingPlacement");
                var install = placementType.GetMethod("InstallCentral", BindingFlags.Public | BindingFlags.Static);
                var building = (Component)install.Invoke(null, new object[] { entrance, data, 0.92f });
                Assert.That(building, Is.Not.Null);

                var grid = nodeType.GetProperty("TrapGrid", Instance).GetValue(entrance);
                var centre = (Vector3)grid.GetType().GetMethod("CentralBuildingWorldPosition").Invoke(grid, null);
                Assert.That(Vector3.Distance(building.transform.position, centre), Is.LessThan(0.01f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void OpeningScene_DoesNotLaunchTutorial()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var dialogueType = RequireType("_01.Code.Dialogue.DialogueRunner");
                var tutorialType = RequireType("_01.Code.UI.PlayTutorialView");
                Component dialogue = null;
                Component tutorial = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    dialogue ??= root.GetComponentInChildren(dialogueType, true);
                    tutorial ??= root.GetComponentInChildren(tutorialType, true);
                }

                Assert.That(dialogue, Is.Not.Null);
                Assert.That(tutorial, Is.Not.Null);
                Assert.That(new SerializedObject(dialogue).FindProperty("playOnStart").boolValue, Is.False);
                Assert.That(new SerializedObject(dialogue).FindProperty("useGuidedStartTutorial").boolValue, Is.False);
                Assert.That(tutorial.gameObject.activeSelf, Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void OpeningRoom_HasFourDoorsAndUsesEastDoorForIntruders()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var controllerType = RequireType("_01.Code.MapCreateSystem.DungeonGraphController");
                var nodeType = RequireType("_01.Code.MapCreateSystem.Node");
                Component controller = null;
                foreach (var root in scene.GetRootGameObjects())
                    controller ??= root.GetComponentInChildren(controllerType, true);
                Assert.That(controller, Is.Not.Null);
                controllerType.GetMethod("EditorBakeInitialScenePreview").Invoke(controller, null);

                var manager = (Component)controllerType.GetField("nodeManager", Instance).GetValue(controller);
                var entrance = manager.GetComponentInChildren(nodeType, true);
                Assert.That(entrance, Is.Not.Null);
                Assert.That((bool)nodeType.GetProperty("IsEnemySpawnNode").GetValue(entrance), Is.True);

                var doors = entrance.transform.Find("Room Doors");
                Assert.That(doors, Is.Not.Null);
                Assert.That(doors.childCount, Is.EqualTo(4));
                foreach (var side in new[] { "North", "South", "East", "West" })
                    Assert.That(doors.Find(side), Is.Not.Null, side);

                var marker = doors.Find("East/EntrySpawn");
                Assert.That(marker, Is.Not.Null);
                Assert.That(nodeType.GetProperty("EntryDoorSpawnPoint").GetValue(entrance), Is.SameAs(marker));
                Assert.That(marker.position.x, Is.GreaterThan(entrance.transform.position.x + 5f));
                Assert.That(marker.position.y, Is.EqualTo(entrance.transform.position.y).Within(0.01f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AdjacentOperatingFacilities_IncreaseIncomeAndAddUpkeep()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var nodeManagerType = RequireType("_01.Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("_01.Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("_01.Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("_01.Code.Buildings.BuildingPlacement");
                var economyType = RequireType("_01.Code.Manager.FacilityEconomyRules");
                Component manager = null;
                foreach (var root in scene.GetRootGameObjects())
                    manager ??= root.GetComponentInChildren(nodeManagerType, true);
                Assert.That(manager, Is.Not.Null);

                var graph = Activator.CreateInstance(graphType);
                var kind = Enum.Parse(nodeKindType, "Corridor");
                var addNode = graphType.GetMethod("AddNode");
                var createNode = nodeManagerType.GetMethod("CreateNode");
                var leftModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1000, 0) });
                var rightModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1001, 0) });
                var treasuryModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1002, 0) });
                var left = (Component)createNode.Invoke(manager, new[] { leftModel });
                var right = (Component)createNode.Invoke(manager, new[] { rightModel });
                var treasuryNode = (Component)createNode.Invoke(manager, new[] { treasuryModel });

                var blacksmith = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/BlacksmithBuildingData.asset");
                var mine = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/03.SO/Buildings/MineBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/TreasuryBuildingData.asset");
                var install = placementType.GetMethod("InstallCentral", BindingFlags.Public | BindingFlags.Static);
                var upkeepBefore = (int)economyType.GetMethod("CalculateDailyUpkeep").Invoke(null, null);
                var forge = install.Invoke(null, new object[] { left, blacksmith, 0.92f });
                var mineBuilding = install.Invoke(null, new object[] { right, mine, 0.92f });
                var treasury = install.Invoke(null, new object[] { treasuryNode, treasuryData, 0.92f });
                Assert.That(forge, Is.Not.Null);
                Assert.That(mineBuilding, Is.Not.Null);
                Assert.That(treasury, Is.Not.Null);
                treasury.GetType().GetMethod("RestoreStoredGold").Invoke(treasury, new object[] { 100 });

                Assert.That((int)economyType.GetMethod("CountAdjacentFacilities").Invoke(null, new[] { forge }), Is.EqualTo(1));
                Assert.That((int)economyType.GetMethod("ScaleIncome").Invoke(null, new[] { forge, (object)20 }), Is.EqualTo(24));
                Assert.That((int)treasury.GetType().GetProperty("ProjectedInterest").GetValue(treasury), Is.EqualTo(10));
                Assert.That((int)economyType.GetMethod("CalculateDailyUpkeep").Invoke(null, null) - upkeepBefore,
                    Is.EqualTo(6));

                nodeManagerType.GetMethod("ClearAll").Invoke(manager, null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Fame_CountsTheGoldInTheVaultAndTheFacilitiesAroundIt()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var nodeManagerType = RequireType("_01.Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("_01.Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("_01.Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("_01.Code.Buildings.BuildingPlacement");
                var fameType = RequireType("_01.Code.Manager.DungeonFameRules");
                Component manager = null;
                foreach (var root in scene.GetRootGameObjects())
                    manager ??= root.GetComponentInChildren(nodeManagerType, true);
                Assert.That(manager, Is.Not.Null);

                var graph = Activator.CreateInstance(graphType);
                var kind = Enum.Parse(nodeKindType, "Corridor");
                var addNode = graphType.GetMethod("AddNode");
                var createNode = nodeManagerType.GetMethod("CreateNode");
                var storeModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1100, 0) });
                var vaultModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1101, 0) });
                var storeNode = (Component)createNode.Invoke(manager, new[] { storeModel });
                var vaultNode = (Component)createNode.Invoke(manager, new[] { vaultModel });

                var storeData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/03.SO/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/TreasuryBuildingData.asset");
                var install = placementType.GetMethod("InstallCentral", BindingFlags.Public | BindingFlags.Static);

                var facilityFame = fameType.GetMethod("FameFromFacilities");
                var goldFame = fameType.GetMethod("FameFromStoredGold");
                var facilitiesBefore = (int)facilityFame.Invoke(null, null);
                var goldBefore = (int)goldFame.Invoke(null, null);

                install.Invoke(null, new object[] { storeNode, storeData, 0.92f });
                var treasury = install.Invoke(null, new object[] { vaultNode, treasuryData, 0.92f });
                Assert.That(treasury, Is.Not.Null);

                // 상점 2 + 금고 건물 4.
                Assert.That((int)facilityFame.Invoke(null, null) - facilitiesBefore, Is.EqualTo(6),
                    "지어 둔 시설이 소문을 만듭니다.");

                // 금고에 넣어 둔 돈만 보인다. 40G마다 1점.
                treasury.GetType().GetMethod("RestoreStoredGold").Invoke(treasury, new object[] { 120 });
                Assert.That((int)goldFame.Invoke(null, null) - goldBefore, Is.EqualTo(3),
                    "금고에 쌓을수록 털 만해 보입니다.");

                nodeManagerType.GetMethod("ClearAll").Invoke(manager, null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 온 목적이 갈 길을 가르는지 본다.
        ///
        /// 예전에는 누구든 금고로 직진했다. 그러면 경비를 어디에 세우든 모두가 그 앞을 지나서,
        /// 길을 막으면 손님이 막히고 열면 도둑이 지나갔다 — 배치에 고를 것이 없었다.
        /// 보물을 노린 자만 금고로 가고 나머지는 볼일 보러 시설로 가야 선택이 생긴다.
        /// </summary>
        [Test]
        public void RouteGoal_ErrandsLeadToFacilitiesAndOnlyHuntersHeadForTheVault()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            GameObject visitorObject = null;
            try
            {
                var nodeManagerType = RequireType("_01.Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("_01.Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("_01.Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("_01.Code.Buildings.BuildingPlacement");
                var enemyType = RequireType("_01.Code.Enemies.Enemy");
                var purposeType = RequireType("_01.Code.Enemies.AdventurerVisitPurpose");
                Component manager = null;
                foreach (var root in scene.GetRootGameObjects())
                    manager ??= root.GetComponentInChildren(nodeManagerType, true);
                Assert.That(manager, Is.Not.Null);

                var graph = Activator.CreateInstance(graphType);
                var kind = Enum.Parse(nodeKindType, "Corridor");
                var addNode = graphType.GetMethod("AddNode");
                var createNode = nodeManagerType.GetMethod("CreateNode");
                var storeModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1200, 0) });
                var vaultModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1201, 0) });
                var storeNode = (Component)createNode.Invoke(manager, new[] { storeModel });
                var vaultNode = (Component)createNode.Invoke(manager, new[] { vaultModel });

                var storeData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/03.SO/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/TreasuryBuildingData.asset");
                var install = placementType.GetMethod("InstallCentral", BindingFlags.Public | BindingFlags.Static);
                var store = install.Invoke(null, new object[] { storeNode, storeData, 0.92f });
                var treasury = install.Invoke(null, new object[] { vaultNode, treasuryData, 0.92f });
                Assert.That(store, Is.Not.Null);
                Assert.That(treasury, Is.Not.Null);
                treasury.GetType().GetMethod("RestoreStoredGold").Invoke(treasury, new object[] { 100 });

                // 비활성 상태로 만들어 Awake를 재운다. 프리팹이 아니라 부품만 필요하다.
                visitorObject = new GameObject("Route visitor");
                visitorObject.SetActive(false);
                visitorObject.transform.position = storeNode.transform.position;
                var visitor = visitorObject.AddComponent(enemyType);
                var configure = enemyType.GetMethod("ConfigureVisitProfile");
                var resolveGoal = enemyType.GetMethod("ResolveRouteGoal");

                configure.Invoke(visitor, new[] { Enum.Parse(purposeType, "Shopping"), (object)50 });
                Assert.That(resolveGoal.Invoke(visitor, null), Is.SameAs(storeNode),
                    "쇼핑하러 온 모험가는 상점으로 향해야 합니다.");

                configure.Invoke(visitor, new[] { Enum.Parse(purposeType, "TreasureHunt"), (object)50 });
                Assert.That(resolveGoal.Invoke(visitor, null), Is.SameAs(vaultNode),
                    "보물을 노리고 온 자만 금고로 직진합니다.");

                // 대장간이 없으니 갈 곳이 없다. 그래도 금고로 새면 안 된다.
                configure.Invoke(visitor, new[] { Enum.Parse(purposeType, "EquipmentUpgrade"), (object)50 });
                Assert.That(resolveGoal.Invoke(visitor, null), Is.Not.SameAs(vaultNode),
                    "볼일 볼 곳이 없다고 금고로 향하면 길이 갈라지지 않습니다.");

                // 예산을 다 쓰면 더 들를 이유가 없다.
                configure.Invoke(visitor, new[] { Enum.Parse(purposeType, "Shopping"), (object)0 });
                Assert.That(resolveGoal.Invoke(visitor, null), Is.Null,
                    "쓸 돈이 없는 모험가는 목적지 없이 돌아다니다 나가야 합니다.");

                nodeManagerType.GetMethod("ClearAll").Invoke(manager, null);
            }
            finally
            {
                if (visitorObject != null)
                    UnityEngine.Object.DestroyImmediate(visitorObject);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 방 하나는 벌이든 경비든 하나만 한다.
        ///
        /// 경영의 선택이 여기서 나온다. 상점을 지으면 그 방은 지킬 수 없게 되고, 게다가
        /// 그 상점이 매력도를 올려 다음 날 더 많은 모험가를 부른다 — 벌수록 막을 자리가
        /// 줄어든다. 금고와 광산은 사람이 머무는 곳이 아니라 그대로 지킬 수 있다.
        /// </summary>
        [Test]
        public void Room_EarnsOrDefendsButNotBoth()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var nodeManagerType = RequireType("_01.Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("_01.Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("_01.Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("_01.Code.Buildings.BuildingPlacement");
                var nodeType = RequireType("_01.Code.MapCreateSystem.Node");
                Component manager = null;
                foreach (var root in scene.GetRootGameObjects())
                    manager ??= root.GetComponentInChildren(nodeManagerType, true);
                Assert.That(manager, Is.Not.Null);

                var graph = Activator.CreateInstance(graphType);
                var kind = Enum.Parse(nodeKindType, "Corridor");
                var addNode = graphType.GetMethod("AddNode");
                var createNode = nodeManagerType.GetMethod("CreateNode");
                var storeModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1300, 0) });
                var vaultModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1301, 0) });
                var storeNode = (Component)createNode.Invoke(manager, new[] { storeModel });
                var vaultNode = (Component)createNode.Invoke(manager, new[] { vaultModel });

                var canAccept = nodeType.GetProperty("CanAcceptAdditionalUnit");
                Assert.That((bool)canAccept.GetValue(storeNode), Is.True, "빈 방은 유닛을 받아야 합니다.");

                var storeData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/03.SO/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/TreasuryBuildingData.asset");
                var install = placementType.GetMethod("InstallCentral", BindingFlags.Public | BindingFlags.Static);

                Assert.That(install.Invoke(null, new object[] { storeNode, storeData, 0.92f }), Is.Not.Null);
                Assert.That((bool)canAccept.GetValue(storeNode), Is.False,
                    "상점이 선 방에는 유닛을 세울 수 없어야 합니다.");

                // 금고는 모험가가 머무는 곳이 아니다. 지킬 수 없으면 지킬 것이 없어진다.
                Assert.That(install.Invoke(null, new object[] { vaultNode, treasuryData, 0.92f }), Is.Not.Null);
                Assert.That((bool)canAccept.GetValue(vaultNode), Is.True,
                    "금고 방은 그대로 지킬 수 있어야 합니다.");

                nodeManagerType.GetMethod("ClearAll").Invoke(manager, null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ── 도구 ───────────────────────────────────────────────

        private static IEnumerable<(ScriptableObject asset, string path)> LoadAll(string typeName)
        {
            var type = RequireType(typeName);
            var found = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/03.SO") && !path.StartsWith("Assets/Resources"))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null || !type.IsInstanceOfType(asset))
                    continue;

                found++;
                yield return (asset, path);
            }

            Assert.That(found, Is.GreaterThan(0), typeName + " 에셋을 하나도 찾지 못했습니다.");
        }

        private static UnityEngine.Object ReadProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, Instance);
            Assert.That(property, Is.Not.Null, propertyName + " 프로퍼티를 찾지 못했습니다.");
            return property.GetValue(target) as UnityEngine.Object;
        }

        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName + " 타입을 찾지 못했습니다.");
            return type;
        }
    }
}
