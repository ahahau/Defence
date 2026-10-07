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

            foreach (var (asset, path) in LoadAll("Code.Enemies.EnemyDataSO"))
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

            foreach (var (asset, _) in LoadAll("Code.Buildings.BuildingDataSO"))
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
        ///
        /// 임시 그림은 예외다. 진짜 도트가 오기 전까지는 함정 전부가 같은 노란 사각형을
        /// 쓰는 것이 정상이고, 그걸 실패로 치면 아트 교체가 끝날 때까지 이 테스트를 꺼 둬야
        /// 한다. 꺼 두면 그사이 진짜 아트끼리 겹치는 것도 못 잡는다.
        /// </summary>
        [Test]
        public void Traps_DoNotShareTheSameArt()
        {
            var byArt = new Dictionary<UnityEngine.Object, List<string>>();

            foreach (var (asset, _) in LoadAll("Code.Buildings.BuildingDataSO"))
            {
                if (!asset.name.Contains("Trap"))
                    continue;

                var art = ReadProperty(asset, "BoardSprite");
                if (art == null || IsPlaceholder(art))
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
            var configType = RequireType("Code.Manager.WaveConfigSO");
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/GameModules/Data/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig 에셋을 찾지 못했습니다.");

            var finalDay = configType.GetProperty("FinalDay").GetValue(config);
            Assert.That(finalDay, Is.Zero, "끝나는 날이 있으면 무한 진행이 아닙니다.");

            // 4주치는 손으로 짠 날이어야 한다. 없으면 defaultWave 하나로 매일이 같아진다.
            var getWave = configType.GetMethod("GetWaveForDay");
            var entryType = RequireType("Code.Manager.WaveConfigSO+WaveEntry");
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
            var configType = RequireType("Code.Manager.WaveConfigSO");
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/GameModules/Data/WaveConfig.asset");
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
            // 구독은 표시(GoldCostView)가 아니라 씬의 GoldHudPresenter가 맡는다.
            var presenterType = RequireType("Code.UI.GoldHudPresenter");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                Component presenter = null;
                foreach (var root in scene.GetRootGameObjects())
                    presenter ??= root.GetComponentInChildren(presenterType, true);
                Assert.That(presenter, Is.Not.Null, "씬에 GoldHudPresenter가 없습니다.");

                // 날짜 채널이 끊기면 빚은 보이는데 청산일까지 며칠인지가 멈춘다.
                // 화면은 멀쩡해 보이므로 플레이로는 잡기 어렵다.
                foreach (var field in new[] { "costEventChannel", "dayEventChannel", "view" })
                {
                    var info = presenterType.GetField(field, Instance);
                    Assert.That(info, Is.Not.Null, field + " 필드를 찾지 못했습니다.");
                    Assert.That(info.GetValue(presenter) as UnityEngine.Object, Is.Not.Null, field + "이 비어 있습니다.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EveryCharacterPrefab_HasAllThreePoses()
        {
            var renderType = RequireType("Code.Entities.EntityRender");
            var missing = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/GameModules/Prefabs/Characters" }))
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

            foreach (var (asset, _) in LoadAll("Code.Artifacts.ArtifactDataSO"))
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

            foreach (var (asset, _) in LoadAll("GameLib.Entity.Stats.StatSO"))
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
                "Assets/GameModules/Data/Buildings/StoreBuildingData.asset",
                "Assets/GameModules/Data/Buildings/ArmoryStoreBuildingData.asset",
                "Assets/GameModules/Data/Buildings/InnBuildingData.asset",
                "Assets/GameModules/Data/Buildings/GrandInnBuildingData.asset"
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
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var controllerType = RequireType("Code.MapCreateSystem.DungeonGraphController");
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
                var nodeType = RequireType("Code.MapCreateSystem.Node");
                var entrance = nodeManager.GetComponentInChildren(nodeType, true);
                Assert.That(entrance, Is.Not.Null);

                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/BlacksmithBuildingData.asset");
                var placementType = RequireType("Code.Buildings.BuildingPlacement");
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
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var dialogueType = RequireType("Code.Dialogue.DialogueRunner");
                var tutorialType = RequireType("Code.UI.PlayTutorialView");
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
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var controllerType = RequireType("Code.MapCreateSystem.DungeonGraphController");
                var nodeType = RequireType("Code.MapCreateSystem.Node");
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
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var nodeManagerType = RequireType("Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("Code.Buildings.BuildingPlacement");
                var economyType = RequireType("Code.Manager.FacilityEconomyRules");
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
                    "Assets/GameModules/Data/Buildings/MineBuildingData.asset");
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
        public void Grade_CountsTheGoldInTheVaultAndTheBuildingsAroundIt()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var nodeManagerType = RequireType("Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("Code.Buildings.BuildingPlacement");
                var gradeType = RequireType("Code.Manager.DungeonGradeRules");
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
                    "Assets/GameModules/Data/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/TreasuryBuildingData.asset");
                var install = placementType.GetMethod("InstallCentral", BindingFlags.Public | BindingFlags.Static);

                var buildingGrade = gradeType.GetMethod("GradeFromBuildings");
                var goldGrade = gradeType.GetMethod("GradeFromStoredGold");
                var facilitiesBefore = (int)buildingGrade.Invoke(null, null);
                var goldBefore = (int)goldGrade.Invoke(null, null);

                install.Invoke(null, new object[] { storeNode, storeData, 0.92f });
                var treasury = install.Invoke(null, new object[] { vaultNode, treasuryData, 0.92f });
                Assert.That(treasury, Is.Not.Null);

                // 상점 2 + 금고 건물 4.
                Assert.That((int)buildingGrade.Invoke(null, null) - facilitiesBefore, Is.EqualTo(6),
                    "지어 둔 시설이 소문을 만듭니다.");

                // 금고에 넣어 둔 돈만 보인다. 40G마다 1점.
                treasury.GetType().GetMethod("RestoreStoredGold").Invoke(treasury, new object[] { 120 });
                Assert.That((int)goldGrade.Invoke(null, null) - goldBefore, Is.EqualTo(3),
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
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            GameObject visitorObject = null;
            try
            {
                var nodeManagerType = RequireType("Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("Code.Buildings.BuildingPlacement");
                var enemyType = RequireType("Code.Enemies.Enemy");
                var purposeType = RequireType("Code.Enemies.AdventurerVisitPurpose");
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
                    "Assets/GameModules/Data/Buildings/StoreBuildingData.asset");
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
        /// 방 하나는 건물이 쓰거나 경비가 쓰거나 둘 중 하나다. 함정만 유닛과 자리를 나눠 쓴다.
        ///
        /// 경영의 선택이 여기서 나온다. 상점을 지으면 그 방은 유닛으로 지킬 수 없게 되고,
        /// 게다가 그 상점이 등급을 올려 다음 날 더 많은 모험가를 부른다 — 벌수록 막을
        /// 자리가 줄어든다. 건물로 채운 방에 남는 수단은 함정뿐이다.
        ///
        /// 금고만 예외다. 금고는 벌이가 아니라 지켜야 할 것이라, 경비가 곧 자물쇠다.
        /// </summary>
        [Test]
        public void Room_EarnsOrDefendsButNotBoth()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                var nodeManagerType = RequireType("Code.MapCreateSystem.DungeonNodeManager");
                var graphType = RequireType("Code.MapCreateSystem.DungeonGraph");
                var nodeKindType = RequireType("Code.MapCreateSystem.DungeonNodeType");
                var placementType = RequireType("Code.Buildings.BuildingPlacement");
                var nodeType = RequireType("Code.MapCreateSystem.Node");
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
                    "Assets/GameModules/Data/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/Resources/Buildings/TreasuryBuildingData.asset");
                var install = placementType.GetMethod("InstallCentral", BindingFlags.Public | BindingFlags.Static);

                Assert.That(install.Invoke(null, new object[] { storeNode, storeData, 0.92f }), Is.Not.Null);
                Assert.That((bool)canAccept.GetValue(storeNode), Is.False,
                    "상점이 선 방에는 유닛을 세울 수 없어야 합니다.");

                // 금고는 예외다. 지켜야 할 곳이라 유닛을 못 세우면 지킬 방법이 없어진다.
                Assert.That(install.Invoke(null, new object[] { vaultNode, treasuryData, 0.92f }), Is.Not.Null);
                Assert.That((bool)canAccept.GetValue(vaultNode), Is.True,
                    "금고 방에는 경비를 세울 수 있어야 합니다.");

                // 함정은 건물이 아니다. 유닛과 한 방을 나눠 쓴다.
                var trapModel = addNode.Invoke(graph, new object[] { kind, new Vector2Int(1302, 0) });
                var trapNode = (Component)createNode.Invoke(manager, new[] { trapModel });
                var trapData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/GameModules/Data/Buildings/SpikeTrapBuildingData.asset");
                Assert.That(trapData, Is.Not.Null, "함정 데이터를 찾지 못했습니다.");
                var installCell = placementType.GetMethod("InstallOnCell", BindingFlags.Public | BindingFlags.Static);
                Assert.That(installCell.Invoke(null, new object[] { trapNode, 0, 0, trapData }), Is.Not.Null);
                Assert.That((bool)canAccept.GetValue(trapNode), Is.True,
                    "함정이 있다고 유닛을 막으면 함정과 경비를 겹쳐 둘 수 없습니다.");

                nodeManagerType.GetMethod("ClearAll").Invoke(manager, null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 진짜 도트가 오기 전까지 자리를 지키는 임시 그림이 성하게 있는지 본다.
        ///
        /// 아트를 전부 걷어낸 뒤로 이 아홉 장이 화면의 거의 전부다. 여기가 깨지면 게임이
        /// 흰 사각형 무더기가 되어 배치도 밸런스도 눈으로 볼 수 없다. 필터까지 보는 것은
        /// Point가 아니면 도트가 뭉개져 임시 그림으로도 못 쓰기 때문이다.
        ///
        /// 남은 숙제는 실패로 만들지 않고 적어만 둔다. 임시 그림을 쓰는 것 자체는 지금
        /// 잘못이 아니라 예정된 상태이고, 이 숫자가 줄어드는 것이 아트 작업의 진척이다.
        /// </summary>
        [Test]
        public void PlaceholderArt_StandsInUntilTheRealPixelArtArrives()
        {
            foreach (var kind in new[]
                     {
                         "Adventurer", "Unit", "Boss", "Building",
                         "Trap", "Node", "Icon", "Ui", "Background"
                     })
            {
                var path = $"{PlaceholderRoot}/{kind}.png";
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null,
                    $"{path} 임시 그림이 없습니다. Defence/Art/Generate Placeholder Art로 다시 만들 수 있습니다.");

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path + " 임포터를 찾지 못했습니다.");
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite),
                    kind + ": 스프라이트가 아니면 붙일 수 없습니다.");
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point),
                    kind + ": 필터가 Point가 아니면 도트가 뭉개집니다.");
            }

            var pending = new List<string>();
            foreach (var (asset, path) in LoadAll("Code.Buildings.BuildingDataSO"))
                if (UsesPlaceholder(path))
                    pending.Add(asset.name);
            foreach (var (asset, path) in LoadAll("Code.Enemies.EnemyDataSO"))
                if (UsesPlaceholder(path))
                    pending.Add(asset.name);

            // Debug.Log로 남긴다. TestContext.WriteLine은 러너 UI에만 들어가서
            // 이 프로젝트가 결과를 읽는 경로(unity cmd console)로는 올라오지 않는다.
            Debug.Log(pending.Count == 0
                ? "[임시 아트] 임시 그림을 쓰는 건물·모험가가 없습니다. 아트 교체가 끝났습니다."
                : $"[임시 아트] 아직 진짜 도트를 기다리는 자산 {pending.Count}개:\n  "
                  + string.Join("\n  ", pending));
        }

        // ── 도구 ───────────────────────────────────────────────

        private const string PlaceholderRoot = "Assets/_Graphics/Placeholder";

        /// <summary>이 그림이 자리만 지키는 임시 그림인가.</summary>
        private static bool IsPlaceholder(UnityEngine.Object sprite) =>
            sprite != null && AssetDatabase.GetAssetPath(sprite).StartsWith(PlaceholderRoot);

        private static bool UsesPlaceholder(string assetPath)
        {
            foreach (var dependency in AssetDatabase.GetDependencies(assetPath, true))
                if (dependency.StartsWith(PlaceholderRoot))
                    return true;
            return false;
        }

        private static IEnumerable<(ScriptableObject asset, string path)> LoadAll(string typeName)
        {
            var type = RequireType(typeName);
            var found = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/GameModules/Data") && !path.StartsWith("Assets/Resources"))
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

        // 게임 코드는 Runtime, 스탯·모듈 같은 재사용 기반은 GameLib 어셈블리에 있다. 둘 다 찾아본다.
        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", DungeonKeeper.Runtime")
                       ?? Type.GetType(fullName + ", DungeonKeeper.GameLib");
            Assert.That(type, Is.Not.Null, fullName + " 타입을 찾지 못했습니다.");
            return type;
        }
    }
}
