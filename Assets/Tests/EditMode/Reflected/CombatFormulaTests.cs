using System;
using System.Reflection;
using NUnit.Framework;
using Code.BT;
using Code.Combat;
using Code.Entities;
using GameLib.Entity;
using UnityEngine;

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
        /// <summary>
        /// 근접이 회복 구간 내내 물러나거나, 닿는 대상을 무작위로 바꾸던 동작의 제한을 유지한다.
        /// </summary>
        [Test]
        public void CombatMove_KeepsRecoveryAndTargetSwitchLimits()
        {
            Assert.That(Code.BT.CombatMoveRules.InMeleeRecoveryStep(0.05f), Is.True);
            Assert.That(Code.BT.CombatMoveRules.InMeleeRecoveryStep(0.2f), Is.False, "근접 뒷걸음은 0.15초만 — 회복 구간(0.3초) 내내 물러나지 않는다.");
            Assert.That(Code.BT.CombatMoveRules.MeleeRecoveryStepSpeedShare, Is.LessThan(0.5f), "반 걸음이라 걷는 속도보다 한참 느리다.");

            Assert.That(Code.BT.CombatMoveRules.KeepsMeleeReach(1.1f, 1.3f), Is.True);
            Assert.That(Code.BT.CombatMoveRules.KeepsMeleeReach(1.25f, 1.3f), Is.False, "사거리 끝에서는 더 물러나지 않는다 — 나갔다 다시 붙으며 떨었다.");
            Assert.That(Code.BT.CombatMoveRules.ShouldGiveUpChase(2.9f), Is.False);
            Assert.That(Code.BT.CombatMoveRules.ShouldGiveUpChase(3f), Is.True, "3초 넘게 닿지 못하면 다른 상대로 바꾼다.");
        }

        /// <summary>대상을 가진 채 반대쪽으로 이동해도 등을 돌리지 않아야 한다. 실제 방향 갱신 경로를 검사한다.</summary>
        [Test]
        public void BattleAgent_FacesTargetWhileMovingAway()
        {
            GameObject root = new GameObject("CombatFacingTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield battlefield = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, battlefield, Vector3.zero);
                BattleAgent target = CreateAgent(root.transform, battlefield, Vector3.right * 8f);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(agent.transform);
                EntityRender render = visual.AddComponent<EntityRender>();
                SetField(agent, "_render", render);
                render.FaceTowards(1f);

                agent.transform.position = Vector3.left;
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);

                Assert.That(render.FacingX, Is.Positive, "오른쪽 대상을 등지고 왼쪽으로 돌아서면 안 됩니다.");
                Assert.That(render.FaceMotion, Is.False, "대상이 있으면 그림의 이동 방향 추적을 꺼야 합니다.");
                target.transform.position = Vector3.left * 8f;
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative, "대상이 반대로 이동하면 대상 쪽으로 돌아서야 합니다.");
                target.transform.position = agent.transform.position + Vector3.right * 0.05f;
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative, "거의 겹친 대상 때문에 방향이 떨리면 안 됩니다.");
                SetField(agent, "_target", null);
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FaceMotion, Is.True, "대상이 없어지면 다시 이동 방향을 따라야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>그래프의 이동을 멈춰도 LateUpdate 겹침 해소가 공격 예비 동작·피격 경직 중 발을 움직였던 경로를 검사한다.</summary>
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void BattleAgent_CrowdingRespectsCombatMoveLock(bool windingUp, bool staggered)
        {
            GameObject root = new GameObject("CombatCrowdingTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield battlefield = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, battlefield, Vector3.zero);
                BattleAgent ally = CreateAgent(root.transform, battlefield, Vector3.right * 0.1f);
                battlefield.Allies(agent.Team).Add(ally);
                SetField(agent, "autoDrive", false);
                SetField(agent, "personalSpaceRadius", 1f);
                SetField(agent, "separationStrength", 1f);
                SetField(agent.GetComponent<Combatant>(), "_windingUp", windingUp);
                if (staggered)
                    agent.GetComponent<Combatant>().Stagger(1f);

                InvokeAgent(agent, "ResolveCrowding", 0.1f);

                if (windingUp || staggered)
                    Assert.That(agent.transform.position, Is.EqualTo(Vector3.zero), "공격 예비 동작·피격 경직 중에는 겹침 해소도 이동을 멈춰야 합니다.");
                else
                    Assert.That(agent.transform.position.x, Is.Negative, "이동 잠금이 없을 때는 실제로 겹침을 풀어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static BattleAgent CreateAgent(Transform parent, NodeBattlefield battlefield, Vector3 position)
        {
            GameObject host = new GameObject("Agent");
            host.transform.SetParent(parent);
            host.transform.position = position;
            Health health = host.AddComponent<Health>();
            FieldInfo currentHealth = typeof(HealthModule).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(currentHealth, Is.Not.Null, "HealthModule.currentHealth를 찾지 못했습니다.");
            currentHealth.SetValue(health, 20);
            BattleAgent agent = host.AddComponent<BattleAgent>();
            Combatant combatant = host.GetComponent<Combatant>();
            SetField(combatant, "health", health);
            SetField(agent, "combatant", combatant);
            agent.SetArena(battlefield, Vector2.zero, 20f);
            return agent;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name}을 찾지 못했습니다.");
            field.SetValue(target, value);
        }

        private static void InvokeAgent(BattleAgent agent, string name, float deltaTime)
        {
            MethodInfo method = typeof(BattleAgent).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"BattleAgent.{name}을 찾지 못했습니다.");
            method.Invoke(agent, new object[] { deltaTime });
        }

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

        /// <summary>
        /// 평타는 게이지가 (간격 − 예비 동작)에 닿으면 휘두르기 시작해 다 찼을 때 닿는다. 예전에는 맞은 뒤에야 공격 동작이 나왔고,
        /// 한 번 교전을 시작하면 사거리 밖으로 쫓아가면서도 계속 맞았다. 사거리 밖·움찔하는 중에는 휘두르지 않고 게이지가 예비 동작 직전에서 기다린다.
        /// </summary>
        [Test]
        public void Swing_StartsBeforeImpactOnlyInRangeAndNotStaggered()
        {
            Assert.That(Code.Combat.CombatTimingRules.ShouldStartSwing(0.79f, 1f, 0.2f, true, false), Is.False);
            Assert.That(Code.Combat.CombatTimingRules.ShouldStartSwing(0.8f, 1f, 0.2f, true, false), Is.True);
            Assert.That(Code.Combat.CombatTimingRules.ShouldStartSwing(0.9f, 1f, 0.2f, false, false), Is.False, "사거리 밖에서는 휘두르지 않는다.");
            Assert.That(Code.Combat.CombatTimingRules.ShouldStartSwing(0.9f, 1f, 0.2f, true, true), Is.False, "맞고 움찔하는 중에는 휘두르지 않는다.");
            Assert.That(Code.Combat.CombatTimingRules.HoldTimer(0.95f, 1f, 0.2f), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(Code.Combat.CombatTimingRules.HoldTimer(0.5f, 1f, 0.2f), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Swing_ToleratesKnockbackButWhiffsWhenTargetEscapes()
        {
            Assert.That(Code.Combat.CombatTimingRules.InSwingRange(1.45f, 1.3f), Is.True, "밀려난 직후 조금 벗어난 것은 봐준다.");
            Assert.That(Code.Combat.CombatTimingRules.InSwingRange(1.6f, 1.3f), Is.False);
            Assert.That(Code.Combat.CombatTimingRules.InStrikeReach(1.65f, 1.3f), Is.True);
            Assert.That(Code.Combat.CombatTimingRules.InStrikeReach(1.8f, 1.3f), Is.False, "휘두르는 사이 멀어졌으면 헛친다.");
        }
    }
}
