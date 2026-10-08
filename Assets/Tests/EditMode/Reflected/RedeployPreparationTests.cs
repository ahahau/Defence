using System.Reflection;
using Code.Combat;
using Code.Units;
using GameLib.Entity;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 재배치 준비: 옮긴 방 수 × 1초, 준비 중 받는 피해 절반.
    ///
    /// 준비가 빠지면 멈춰 놓고 경비를 적이 오는 방마다 돌려 막는 것이 정답이 되고,
    /// 피해 감소가 빠지면 옮기자마자 맞아 녹아서 재배치 자체를 안 쓰게 된다.
    /// </summary>
    public class RedeployPreparationTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                Object.DestroyImmediate(_host);
        }

        [TestCase(0, 1f)]
        [TestCase(1, 1f)]
        [TestCase(3, 3f)]
        public void Preparation_IsOneSecondPerRoomMoved(int rooms, float expected)
        {
            Assert.That(RedeployRules.SecondsFor(rooms), Is.EqualTo(expected));
        }

        [TestCase(10, 5)]
        [TestCase(7, 4)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        public void PreparationDamage_IsHalvedButNeverBelowOne(int damage, int expected)
        {
            Assert.That(RedeployRules.ApplyPreparationDamage(damage), Is.EqualTo(expected));
        }

        [Test]
        public void Health_TakesHalfDamageWhilePreparing()
        {
            var (unit, health) = CreateUnit(20);
            unit.BeginRedeployPreparation(2f);

            health.TakeDamage(10);

            Assert.That(unit.IsPreparingRedeploy, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(15));
        }

        [Test]
        public void Health_TakesFullDamageWhenNotPreparing()
        {
            var (_, health) = CreateUnit(20);

            health.TakeDamage(10);

            Assert.That(health.CurrentHealth, Is.EqualTo(10));
        }

        [Test]
        public void Preparation_KeepsTheLongerRemainingTime()
        {
            var (unit, _) = CreateUnit(20);
            unit.BeginRedeployPreparation(3f);
            unit.BeginRedeployPreparation(1f);

            Assert.That(unit.RedeployPreparationRemaining, Is.EqualTo(3f));
        }

        private (Unit unit, Health health) CreateUnit(int currentHealth)
        {
            _host = new GameObject("Unit");
            var health = _host.AddComponent<Health>();
            // 에디트 모드에서는 Awake가 돌지 않아 체력을 직접 채운다.
            typeof(HealthModule).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(health, currentHealth);
            var unit = _host.AddComponent<Unit>();
            return (unit, health);
        }
    }
}
