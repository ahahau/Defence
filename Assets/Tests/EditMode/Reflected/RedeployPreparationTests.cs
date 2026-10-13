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

        /// <summary>
        /// 특성마다 재배치 추가 반응이 하나씩 있다(사용자 결정). 강인함 준비 절반, 신중함 1.5배·피해 없음,
        /// 수호자 피해 75% 감소, 공격적 첫 공격 +50%, 야전 의무병 도착 방 회복 10%.
        /// </summary>
        [Test]
        public void TraitReactions_FollowTheRules()
        {
            Assert.That(RedeployRules.SecondsFor(2, UnitTrait.Tireless), Is.EqualTo(1f));
            Assert.That(RedeployRules.SecondsFor(2, UnitTrait.Cautious), Is.EqualTo(3f));
            Assert.That(RedeployRules.SecondsFor(2, UnitTrait.None), Is.EqualTo(2f));

            Assert.That(RedeployRules.ApplyPreparationDamage(10, UnitTrait.Cautious), Is.Zero);
            Assert.That(RedeployRules.ApplyPreparationDamage(10, UnitTrait.Guardian), Is.EqualTo(3));
            Assert.That(RedeployRules.ApplyPreparationDamage(1, UnitTrait.Guardian), Is.EqualTo(1), "수호자도 최소 1은 받습니다.");
            Assert.That(RedeployRules.ApplyPreparationDamage(10, UnitTrait.Aggressive), Is.EqualTo(5));

            Assert.That(RedeployRules.ApplyFirstStrike(4), Is.EqualTo(6));
            Assert.That(RedeployRules.FieldMedicHeal(30), Is.EqualTo(3));
            Assert.That(RedeployRules.FieldMedicHeal(5), Is.EqualTo(1));
        }

        /// <summary>공격적인 부하는 준비가 끝난 뒤 첫 공격 한 번만 세게 친다. 준비 중에는 소모되지 않는다.</summary>
        [Test]
        public void Aggressive_FirstStrikeIsConsumedOnceAfterPreparation()
        {
            var (unit, _) = CreateUnit(20);
            typeof(Unit).GetField("trait", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, UnitTrait.Aggressive);

            unit.BeginRedeployPreparation(1f);
            Assert.That(unit.ConsumeRedeployFirstStrike(), Is.False, "준비 중에는 공격하지 못합니다.");

            typeof(Unit).GetField("_redeployPreparationRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, 0f);
            Assert.That(unit.ConsumeRedeployFirstStrike(), Is.True);
            Assert.That(unit.ConsumeRedeployFirstStrike(), Is.False, "첫 공격 한 번뿐입니다.");
        }

        /// <summary>신중한 부하는 준비 중 맞아도 체력이 줄지 않는다(실제 체력 경로).</summary>
        [Test]
        public void Cautious_TakesNoDamageWhilePreparing()
        {
            var (unit, health) = CreateUnit(20);
            typeof(Unit).GetField("trait", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, UnitTrait.Cautious);
            unit.BeginRedeployPreparation(2f);

            health.TakeDamage(10);
            Assert.That(health.CurrentHealth, Is.EqualTo(20));
        }

        /// <summary>
        /// 부활 마력 비용 = (기본 + 레벨 + 정가 10G당 1) × 특성(사용자 결정: 정가 + 특성).
        /// 예전에는 레벨만 봐서 비싼 상급 부하와 졸개가 같은 값에 일어났다.
        /// </summary>
        [Test]
        public void RevivalManaCost_UsesHireCostAndTrait()
        {
            Assert.That(Code.Manager.RevivalManaCostRules.Cost(15, 8, 1, 0, UnitTrait.None), Is.EqualTo(15), "정가·레벨이 없으면 예전과 같습니다.");
            Assert.That(Code.Manager.RevivalManaCostRules.Cost(15, 8, 2, 50, UnitTrait.None), Is.EqualTo(28));
            Assert.That(Code.Manager.RevivalManaCostRules.Cost(15, 8, 2, 50, UnitTrait.Guardian), Is.EqualTo(34));
            Assert.That(Code.Manager.RevivalManaCostRules.Cost(15, 8, 2, 50, UnitTrait.FieldMedic), Is.EqualTo(22));
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
