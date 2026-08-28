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
            Type.GetType("_01.Code.Buildings.Building, Assembly-CSharp");

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
