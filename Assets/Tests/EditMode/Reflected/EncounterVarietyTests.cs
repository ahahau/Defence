using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Gameplay
{
    public class EncounterVarietyTests
    {
        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void ThreatPreview_AuthoredTextOverridesRoleInference()
        {
            var profile = RequireType("Code.Manager.WaveThreatProfile");
            var build = profile.GetMethod("BuildForRoleCounts", StaticFlags);
            Assert.That(build, Is.Not.Null);

            var preview = build.Invoke(null, new object[]
            {
                "정찰병 돌격", "경계 명령으로 막으세요", 4, 0, 0, 0
            });

            Assert.That(ReadProperty<string>(preview, "Title"), Is.EqualTo("정찰병 돌격"));
            Assert.That(ReadProperty<string>(preview, "CounterHint"), Is.EqualTo("경계 명령으로 막으세요"));
        }

        [Test]
        public void ThreatPreview_InfersHealerAndRangedCounters()
        {
            var profile = RequireType("Code.Manager.WaveThreatProfile");
            var build = profile.GetMethod("BuildForRoleCounts", StaticFlags);

            var healer = build.Invoke(null, new object[] { null, null, 1, 0, 1, 1 });
            Assert.That(ReadProperty<string>(healer, "Title"), Does.Contain("치유사"));
            Assert.That(ReadProperty<string>(healer, "CounterHint"), Does.Contain("먼저"));

            var ranged = build.Invoke(null, new object[] { null, null, 0, 2, 0, 1 });
            Assert.That(ReadProperty<string>(ranged, "Title"), Does.Contain("원거리"));
            Assert.That(ReadProperty<string>(ranged, "CounterHint"), Does.Contain("경계"));
        }

        [TestCase("Guard", "Nearest", "Frontline")]
        [TestCase("Assault", "Nearest", "Backline")]
        [TestCase("Standby", "LowestHealth", "LowestHealth")]
        [TestCase("Rest", "Focused", "Focused")]
        public void UnitCommand_ChangesTargetPriority(
            string commandName,
            string requestedName,
            string expectedName)
        {
            var utility = RequireType("Code.Units.UnitCommandUtility");
            var commandType = RequireType("Code.Units.UnitCommand");
            var priorityType = RequireType("Code.BT.TargetPriority");
            var resolve = utility.GetMethod("ResolveTargetPriority", StaticFlags);
            Assert.That(resolve, Is.Not.Null);

            var result = resolve.Invoke(null, new[]
            {
                Enum.Parse(commandType, commandName),
                Enum.Parse(priorityType, requestedName)
            });

            Assert.That(result.ToString(), Is.EqualTo(expectedName));
        }

        [Test]
        public void PolicyCombatTradeoffs_HaveNeutralSafeDefaults()
        {
            var policyType = RequireType("Code.Manager.PolicyDataSO");
            var policy = ScriptableObject.CreateInstance(policyType);
            try
            {
                Assert.That(ReadProperty<float>(policy, "UnitDamageMultiplier"), Is.EqualTo(1f));
                Assert.That(ReadProperty<int>(policy, "UnitDefenseBonus"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(policy);
            }
        }

        [Test]
        public void BossReinforcement_ReservesConfiguredEnemiesButKeepsBossInOpeningGroup()
        {
            var bossEntryType = RequireType("Code.Manager.WaveConfigSO+BossEntry");
            var partyType = RequireType("Code.Manager.AdventurerPartySO");
            var enemyDataType = RequireType("Code.Enemies.EnemyDataSO");
            var entry = Activator.CreateInstance(bossEntryType);
            var party = ScriptableObject.CreateInstance(partyType);
            var enemy = ScriptableObject.CreateInstance(enemyDataType);

            try
            {
                bossEntryType.GetField("enableReinforcementPhase").SetValue(entry, true);
                bossEntryType.GetField("reinforcementCount").SetValue(entry, 5);
                partyType.GetProperty("Members").SetValue(party, Array.CreateInstance(enemyDataType, 1));
                ((Array)partyType.GetProperty("Members").GetValue(party)).SetValue(enemy, 0);
                bossEntryType.GetField("reinforcementParty").SetValue(entry, party);

                var reserve = bossEntryType.GetMethod("GetReservedReinforcementCount");
                Assert.That(reserve.Invoke(entry, new object[] { 4 }), Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(party);
            }
        }

        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", DungeonKeeper.Runtime");
            Assert.That(type, Is.Not.Null, fullName + " 타입을 찾지 못했습니다.");
            return type;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName + " 프로퍼티를 찾지 못했습니다.");
            return (T)property.GetValue(target);
        }
    }
}
