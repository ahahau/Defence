using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Buildings
{
    public class BuildingSerializationTests
    {
        private static readonly Type BuildingType =
            Type.GetType("Code.Buildings.Building, DungeonKeeper.Runtime");

        [Test]
        public void CentralOnlyBuildings_UseCentralSlots_WhileTrapsKeepCells()
        {
            var placement = Type.GetType("Code.Buildings.BuildingPlacement, DungeonKeeper.Runtime", true);
            var usesCell = placement.GetMethod("UsesGridCell");
            var isCentral = placement.GetMethod("IsCentralBuilding");
            foreach (var path in new[]
            {
                "Assets/GameModules/Data/Buildings/PortalBuildingData.asset",
                "Assets/GameModules/Data/Buildings/RecoveryRoomBuildingData.asset",
                "Assets/GameModules/Data/Buildings/MineBuildingData.asset",
                "Assets/GameModules/Data/Buildings/DeepMineBuildingData.asset",
                "Assets/Resources/Buildings/TreasuryBuildingData.asset"
            })
            {
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(data, Is.Not.Null, path);
                Assert.That(usesCell.Invoke(null, new object[] { data }), Is.False, path);
                Assert.That(isCentral.Invoke(null, new object[] { data }), Is.True, path);
            }
            var trap = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/GameModules/Data/Buildings/SpikeTrapBuildingData.asset");
            Assert.That(usesCell.Invoke(null, new object[] { trap }), Is.True);
        }

        [Test]
        public void BuildingHierarchy_DoesNotReuseSerializedFieldNames()
        {
            Assert.That(BuildingType, Is.Not.Null);

            foreach (var type in BuildingType.Assembly.GetTypes())
            {
                if (type.IsAbstract || !BuildingType.IsAssignableFrom(type))
                    continue;

                var ownersByFieldName = new Dictionary<string, Type>();
                for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
                {
                    var fields = current.GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    foreach (var field in fields)
                    {
                        if (!IsSerializedByUnity(field))
                            continue;

                        Assert.That(
                            ownersByFieldName.TryGetValue(field.Name, out var existingOwner),
                            Is.False,
                            $"{type.FullName} serializes '{field.Name}' in both " +
                            $"{existingOwner?.FullName} and {current.FullName}.");

                        ownersByFieldName[field.Name] = current;
                    }
                }
            }
        }

        private static bool IsSerializedByUnity(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral || field.IsNotSerialized)
                return false;

            return field.IsPublic
                   || field.IsDefined(typeof(SerializeField), true)
                   || field.IsDefined(typeof(SerializeReference), true);
        }
    }
}
