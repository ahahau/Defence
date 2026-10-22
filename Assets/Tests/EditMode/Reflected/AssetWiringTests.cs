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
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/07.SO/WaveConfig.asset");
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
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/07.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig 에셋을 찾지 못했습니다.");

            // 보스와 청산이 같은 날이라 한 주가 하나의 고비로 끝난다.
            // 둘이 어긋나면 주의 리듬이 사라지고 보스날은 그냥 힘든 날이 된다.
            Assert.That(configType.GetMethod("IsBossDay").Invoke(config, new object[] { day }), Is.True,
                $"{day}일은 청산일인데 보스가 없습니다.");
            Assert.That(configType.GetMethod("GetBossForDay").Invoke(config, new object[] { day }), Is.Not.Null,
                $"{day}일 보스는 전용 정의가 있어야 공용 보스와 구분됩니다.");
        }


        [Test]
        public void EveryCharacterPrefab_HasAllThreePoses()
        {
            var renderType = RequireType("Code.Entities.EntityRender");
            var missing = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/03.Prefabs/Characters" }))
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
        /// 고용할 수 있는 부하는 모두 진짜 그림과 움직임(애니메이션 묶음)을 가져야 한다.
        /// 슬라임 셋이 임시 네모 그림으로 남아 시작 부하부터 사각형으로 싸웠다(2026-10-22 해골 검사 셋으로 교체).
        /// </summary>
        [Test]
        public void EveryUnit_UsesRealAnimatedArt()
        {
            var renderType = RequireType("Code.Entities.EntityRender");
            var setField = renderType.GetField("animationSet", Instance);
            Assert.That(setField, Is.Not.Null, "EntityRender.animationSet 를 찾지 못했습니다.");
            var missing = new List<string>();

            foreach (var (asset, _) in LoadAll("Code.Units.UnitDataSO"))
            {
                var sprite = ReadProperty(asset, "Sprite");
                if (sprite == null || IsPlaceholder(sprite))
                    missing.Add(asset.name + " · 고용 그림");

                var prefab = ReadProperty(asset, "Prefab") as Component;
                var render = prefab != null ? prefab.GetComponentInChildren(renderType, true) : null;
                var set = render != null ? setField.GetValue(render) as Code.Entities.CharacterAnimationSetSO : null;
                if (set == null)
                    missing.Add(asset.name + " · 애니메이션 묶음");

                // 화면 아이콘(배치 카드·상태 창·지원자 줄)은 대기 첫 프레임이다. 보드 그림(임시 네모)으로 떨어지면 빈 상자가 보였다.
                var icon = ((Code.Units.UnitDataSO)asset).IconSprite;
                if (icon == null || IsPlaceholder(icon) || (set != null && icon != set.FirstIdleFrame))
                    missing.Add(asset.name + " · 아이콘(대기 첫 프레임)");
            }

            Assert.That(missing, Is.Empty, "진짜 그림이 없는 부하:\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// 관광객(겁쟁이·쇼핑광)은 맨손 민간인 그림이다. 척후병 프리팹을 같이 써서 "매복 기습·연막 독무"를 쓰던 것을
        /// 스킬 없는 관광객 전용 프리팹으로 나눴다(2026-10-22). 다시 공용 프리팹으로 돌리면 민간인이 암살 기술을 쓴다.
        /// </summary>
        [Test]
        public void Tourists_FightWithoutCombatSkills()
        {
            var casterType = RequireType("Code.Skills.SkillCaster");
            var armed = new List<string>();

            foreach (var (asset, _) in LoadAll("Code.Enemies.EnemyDataSO"))
            {
                // 탐욕의 기사(보스)도 쇼핑광 특성이라 특성만으로 고르지 않는다. 성직자 관광객은 치유사 프리팹이 맞다.
                var trait = asset.GetType().GetProperty("Trait", Instance).GetValue(asset).ToString();
                if (!asset.name.StartsWith("Enemy_Tourist_") || trait == "Priest")
                    continue;

                var prefab = ReadProperty(asset, "Prefab") as Component;
                Assert.That(prefab, Is.Not.Null, asset.name + " 프리팹이 없습니다.");
                if (prefab.GetComponent(casterType) != null)
                    armed.Add(asset.name + " ← " + prefab.name);
            }

            Assert.That(armed, Is.Empty, "스킬을 든 관광객:\n  " + string.Join("\n  ", armed));
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
                "Assets/07.SO/Buildings/BlacksmithBuildingData.asset",
                "Assets/07.SO/Buildings/StoreBuildingData.asset",
                "Assets/07.SO/Buildings/ArmoryStoreBuildingData.asset",
                "Assets/07.SO/Buildings/InnBuildingData.asset",
                "Assets/07.SO/Buildings/GrandInnBuildingData.asset"
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
                    "Assets/07.SO/Buildings/BlacksmithBuildingData.asset");
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
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
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

        /// <summary>
        /// 상인 버튼을 누르면 그릴 화면이 씬에 있어야 한다. 예전에는 상인 판단(<c>MerchantPanelView</c>)만 있고 화면이 없어
        /// 버튼을 눌러도 아무것도 뜨지 않았다.
        /// </summary>
        [Test]
        public void MerchantScreen_IsPlacedAndWiredToTheMerchant()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                Code.UI.MerchantScreenView screen = null;
                foreach (var root in scene.GetRootGameObjects())
                    screen ??= root.GetComponentInChildren<Code.UI.MerchantScreenView>(true);

                Assert.That(screen, Is.Not.Null, "상인 창이 씬에 없습니다. MerchantBuilder.BuildAll()을 돌리세요.");
                var serialized = new SerializedObject(screen);
                Assert.That(serialized.FindProperty("merchant").objectReferenceValue, Is.Not.Null, "상인 판단과 이어져야 합니다.");
                Assert.That(serialized.FindProperty("cards").arraySize, Is.EqualTo(Code.UI.MerchantPanelView.MaxOffers));
                Assert.That(serialized.FindProperty("buyButton").objectReferenceValue, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 도크의 고용 버튼이 고용 창을 열 수 있어야 한다. 예전에는 고용이 청산일 정산표에서만 돼 첫 주에 부하를 둘 방법이 없었다.
        /// </summary>
        [Test]
        public void RecruitScreen_IsPlacedAndOpenedFromTheDock()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                Code.UI.RecruitScreenView recruit = null;
                Code.UI.RoomManagement.RoomManagementScreen roomScreen = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    recruit ??= root.GetComponentInChildren<Code.UI.RecruitScreenView>(true);
                    roomScreen ??= root.GetComponentInChildren<Code.UI.RoomManagement.RoomManagementScreen>(true);
                }

                Assert.That(recruit, Is.Not.Null, "고용 창이 씬에 없습니다. RecruitBuilder.BuildAll()을 돌리세요.");
                var shop = new SerializedObject(recruit).FindProperty("shop").objectReferenceValue as Code.UI.SettlementShopPane;
                Assert.That(shop, Is.Not.Null);
                Assert.That(new SerializedObject(shop).FindProperty("roster").objectReferenceValue, Is.Not.Null, "고용 명단과 이어져야 합니다.");
                var shopSo = new SerializedObject(shop);
                var icons = shopSo.FindProperty("icons");
                Assert.That(icons.arraySize, Is.EqualTo(shopSo.FindProperty("slots").arraySize), "지원자 줄마다 유닛 아이콘 칸이 있어야 합니다.");
                for (var i = 0; i < icons.arraySize; i++)
                    Assert.That(icons.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, $"지원자 {i}번 줄의 아이콘 칸이 비었습니다.");

                Assert.That(roomScreen, Is.Not.Null);
                Assert.That(new SerializedObject(roomScreen).FindProperty("recruit").objectReferenceValue, Is.EqualTo(recruit));
                var dock = roomScreen.GetComponentInChildren<Code.UI.RoomManagement.ActionDockView>(true);
                Assert.That(new SerializedObject(dock).FindProperty("recruitButton").objectReferenceValue, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// ESC는 라우터 하나가 받아 위 창부터 하나씩 닫고, 닫을 창이 없으면 설정을 연다. 예전에는 상인·고용·상태·금고 창이
        /// ESC로 닫히지 않았고 게임 화면에 설정이 없었다. 창이 라우터 목록에서 빠지면 그 창만 ESC가 안 먹는다.
        /// </summary>
        [Test]
        public void SettingsScreen_IsPlacedAndEveryWindowClosesWithEscape()
        {
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
            try
            {
                Code.UI.UiEscapeRouter router = null;
                Code.UI.TopHudView hud = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    router ??= root.GetComponentInChildren<Code.UI.UiEscapeRouter>(true);
                    hud ??= root.GetComponentInChildren<Code.UI.TopHudView>(true);
                }

                Assert.That(router, Is.Not.Null, "ESC 라우터가 씬에 없습니다. SettingsBuilder.BuildAll()을 돌리세요.");
                var settings = new SerializedObject(router).FindProperty("settings").objectReferenceValue;
                Assert.That(settings, Is.Not.Null, "닫을 창이 없을 때 열 설정 창이 없습니다.");
                Assert.That(new SerializedObject(hud).FindProperty("settings").objectReferenceValue, Is.EqualTo(settings), "톱니 버튼이 설정 창을 열어야 합니다.");
                Assert.That(new SerializedObject(hud).FindProperty("settingsButton").objectReferenceValue, Is.Not.Null);

                var windows = new SerializedObject(router).FindProperty("windows");
                var wired = new List<Type>();
                for (var i = 0; i < windows.arraySize; i++)
                {
                    var window = windows.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(window, Is.Not.Null, $"ESC 목록 {i}번이 비었습니다.");
                    Assert.That(window, Is.InstanceOf<Code.UI.IEscapeClosable>(), window + "은 ESC로 닫을 수 없는 컴포넌트입니다.");
                    wired.Add(window.GetType());
                }

                Assert.That(wired, Is.EquivalentTo(new[]
                {
                    typeof(Code.UI.SettingsScreenView), typeof(Code.UI.SettlementScreenView), typeof(Code.UI.MerchantScreenView),
                    typeof(Code.UI.RecruitScreenView), typeof(Code.UI.TreasuryView), typeof(Code.UI.UnitStatusView),
                    typeof(Code.UI.EnemyStatusView), typeof(Code.UI.RoomManagement.RoomManagementScreen)
                }));
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
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
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
                    "Assets/07.SO/Buildings/BlacksmithBuildingData.asset");
                var mine = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/07.SO/Buildings/MineBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/07.SO/Buildings/TreasuryBuildingData.asset");
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
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
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
                    "Assets/07.SO/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/07.SO/Buildings/TreasuryBuildingData.asset");
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
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
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
                    "Assets/07.SO/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/07.SO/Buildings/TreasuryBuildingData.asset");
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
            var scene = EditorSceneManager.OpenScene("Assets/00.Scenes/SampleScene.unity", OpenSceneMode.Additive);
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
                    "Assets/07.SO/Buildings/StoreBuildingData.asset");
                var treasuryData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/07.SO/Buildings/TreasuryBuildingData.asset");
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
                    "Assets/07.SO/Buildings/FireBrazierTrapBuildingData.asset");
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

        private const string PlaceholderRoot = "Assets/04.Sprites/Placeholder";

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
                if (!path.StartsWith("Assets/07.SO") && !path.StartsWith("Assets/Resources"))
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

        /// <summary>
        /// 흰 피격 점멸은 실루엣 재질이, 근접 베기 섬광은 그림이 전투 연출 표에 걸려 있어야 나온다.
        /// 비면 조용히 예전 붉은 칠(흰색이면 아무 변화 없음)로 돌아가 눈으로 보기 전엔 모른다.
        /// </summary>
        [Test]
        public void CombatFxCatalog_HasHitFlashMaterialAndMeleeSlash()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<Code.Combat.CombatFxCatalogSO>("Assets/07.SO/Combat/CombatFxCatalog.asset");
            Assert.That(catalog, Is.Not.Null, "전투 연출 표가 없다.");
            Assert.That(catalog.HitFlashMaterial, Is.Not.Null, "피격 점멸 재질이 비어 있다.");
            Assert.AreEqual("Defence/Sprite Flash", catalog.HitFlashMaterial.shader.name);
            Assert.That(catalog.MeleeSlashSprite, Is.Not.Null, "근접 베기 섬광 그림이 비어 있다.");
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
