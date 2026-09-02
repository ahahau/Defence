using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Gameplay
{
    public class AdventurerTraitTests
    {
        private static Type TraitType => RequireType("_01.Code.Enemies.AdventurerTrait");
        private static Type RulesType => RequireType("_01.Code.Enemies.AdventurerTraitRules");

        [Test]
        public void Shopaholic_PaysMoreAndGainsMoreGreed()
        {
            var trait = Enum.Parse(TraitType, "Shopaholic");
            Assert.That(Call<int>("ResolveFacilityGold", 20, trait), Is.EqualTo(30));
            Assert.That(Call<int>("ResolveGreedGain", 2, trait), Is.EqualTo(4));
        }

        [Test]
        public void Coward_GainsMoreFear()
        {
            var trait = Enum.Parse(TraitType, "Coward");
            Assert.That(Call<int>("ResolveFearGain", 3, trait), Is.EqualTo(5));
        }

        [Test]
        public void Priest_ResistsFearAndCalmsParty()
        {
            var trait = Enum.Parse(TraitType, "Priest");
            Assert.That(Call<int>("ResolveFearGain", 4, trait), Is.EqualTo(3));
            Assert.That(Call<int>("GetPartyCalmAmount", trait), Is.EqualTo(2));
        }

        [TestCase(0, 0)]
        [TestCase(19, 0)]
        [TestCase(20, 1)]
        [TestCase(45, 2)]
        public void GreedKnight_GainsAttackPerTwentyFacilityGold(int gold, int expected)
        {
            Assert.That(Call<int>("ResolveGreedKnightAttackBonus", gold), Is.EqualTo(expected));
        }

        [Test]
        public void AuthoredBossEntry_MakesAnUnscheduledDayABossDay()
        {
            var configType = RequireType("_01.Code.Manager.WaveConfigSO");
            var bossEntryType = RequireType("_01.Code.Manager.WaveConfigSO+BossEntry");
            var config = ScriptableObject.CreateInstance(configType);
            try
            {
                var entry = Activator.CreateInstance(bossEntryType);
                bossEntryType.GetField("targetDay")?.SetValue(entry, 12);
                var entries = Array.CreateInstance(bossEntryType, 1);
                entries.SetValue(entry, 0);
                configType.GetField("bossEntries", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(config, entries);

                var isBossDay = configType.GetMethod("IsBossDay", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(isBossDay?.Invoke(config, new object[] { 12 }), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void BlacksmithContent_IsAvailableFromResources()
        {
            var buildingDataType = RequireType("_01.Code.Buildings.BuildingDataSO");
            var data = Resources.Load("Buildings/BlacksmithBuildingData", buildingDataType);
            Assert.That(data, Is.Not.Null);
            Assert.That(buildingDataType.GetProperty("DisplayName")?.GetValue(data), Is.EqualTo("대장간"));
            Assert.That(buildingDataType.GetProperty("Prefab")?.GetValue(data), Is.Not.Null);
        }

        private static T Call<T>(string methodName, params object[] args)
        {
            var method = RulesType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName + " 메서드를 찾지 못했습니다.");
            return (T)method.Invoke(null, args);
        }

        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName + " 타입을 찾지 못했습니다.");
            return type;
        }
    }
}
