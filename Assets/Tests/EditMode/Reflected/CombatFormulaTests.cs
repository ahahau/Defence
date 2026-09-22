using System;
using System.Reflection;
using NUnit.Framework;

namespace Tests.EditMode.Combat
{
    /// <summary>
    /// 피해 산정 규칙을 지킨다. 방어 기준점을 100에서 30으로 내리면서
    /// "유닛 체감은 그대로, 방패 든 적만 단단해진다"를 성립시킨 것이 핵심이라
    /// 그 두 축을 각각 못 박아 둔다.
    /// 테스트 어셈블리는 DungeonKeeper.Runtime를 참조할 수 없어 리플렉션으로 접근한다.
    /// </summary>
    public class CombatFormulaTests
    {
        private static Type Formula
        {
            get
            {
                var type = Type.GetType("Code.Combat.CombatFormula, DungeonKeeper.Runtime");
                Assert.That(type, Is.Not.Null, "CombatFormula 타입을 찾지 못했습니다.");
                return type;
            }
        }

        private static object CallStatic(string method, params object[] args)
        {
            var m = Formula.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"CombatFormula.{method} 정적 메서드를 찾지 못했습니다.");
            return m.Invoke(null, args);
        }

        private static int ApplyDefense(int damage, int defense) =>
            (int)CallStatic("ApplyDefense", damage, defense);

        private static float Reduction(int defense) =>
            (float)CallStatic("GetDefenseReduction", defense);

        private static float HalvingPoint =>
            (float)Formula.GetField("DefenseHalvingPoint", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null);

        // ── 곡선 자체 ────────────────────────────────────────────────

        [Test]
        public void Defense_AtHalvingPoint_CutsDamageInHalf()
        {
            var point = (int)HalvingPoint;
            Assert.That(ApplyDefense(10, point), Is.EqualTo(5), "기준점과 같은 방어는 피해를 정확히 절반으로 줄여야 합니다.");
            Assert.That(Reduction(point), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Defense_NeverRaisesDamageAndNeverReachesZero()
        {
            for (var damage = 1; damage <= 10; damage++)
            {
                var previous = ApplyDefense(damage, 0);
                Assert.That(previous, Is.EqualTo(damage), "방어 0은 피해를 그대로 통과시켜야 합니다.");

                for (var defense = 1; defense <= 60; defense++)
                {
                    var current = ApplyDefense(damage, defense);
                    Assert.That(current, Is.LessThanOrEqualTo(previous),
                        $"방어를 {defense}로 올렸는데 피해가 늘었습니다(피해 {damage}).");
                    Assert.That(current, Is.GreaterThanOrEqualTo(1), "아무리 단단해도 1은 들어가야 합니다.");
                    previous = current;
                }
            }
        }

        // ── 유닛 쪽 체감은 그대로여야 한다 ───────────────────────────

        [Test]
        public void UnitDefenseBonuses_KeepTheirOldReduction()
        {
            // 기준점을 x0.3으로 낮추면서 특성·명령 보정도 같은 비율로 줄였다.
            // 20/(20+100)과 6/(6+30)은 같은 값이라 수호자의 체감이 유지된다.
            Assert.That(Reduction(6), Is.EqualTo(20f / 120f).Within(0.0005f), "수호자 특성의 감소율이 예전과 달라졌습니다.");
            Assert.That(Reduction(11), Is.EqualTo(35f / 135f).Within(0.01f), "수호자+방어 명령의 감소율이 예전과 달라졌습니다.");
        }

        // ── 방패 든 적은 실제로 단단해져야 한다 ──────────────────────

        [Test]
        public void ShieldedEnemy_NoLongerTakesFullDamage()
        {
            // 예전에는 방패병(방어 14)이 창병의 3과 수호자의 2를 무감소로 맞았다.
            const int swordEnemyDefense = 14;
            Assert.That(ApplyDefense(3, swordEnemyDefense), Is.LessThan(3), "방패병이 창병의 피해를 그대로 맞고 있습니다.");
            Assert.That(ApplyDefense(2, swordEnemyDefense), Is.LessThan(2), "방패병이 수호자의 피해를 그대로 맞고 있습니다.");
            Assert.That(ApplyDefense(5, swordEnemyDefense), Is.EqualTo(3), "석궁수 5는 방패병에게 3으로 들어가야 합니다.");

            // 치유병(방어 10)도 마찬가지. 예전에는 석궁수의 5를 5로 그대로 맞았다.
            Assert.That(ApplyDefense(5, 10), Is.LessThan(5), "치유병이 석궁수의 피해를 그대로 맞고 있습니다.");
        }

        // ── 치명타 ───────────────────────────────────────────────────

        [Test]
        public void Critical_MultipliesBeforeDefenseAndNeverShrinksDamage()
        {
            Assert.That(CallStatic("ApplyCritical", 5, 2f), Is.EqualTo(10));
            Assert.That(CallStatic("ApplyCritical", 3, 0.5f), Is.EqualTo(3), "1보다 작은 배율은 피해를 줄이지 못해야 합니다.");
        }
    }
}
