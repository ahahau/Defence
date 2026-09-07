using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        private static readonly string[] ArtPendingBuildings = { "PortalBuildingData", "TreasuryBuildingData" };

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
