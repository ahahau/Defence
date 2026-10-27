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
        /// <summary>적을 잡은 지원도 합류할 때는 아군 쪽으로 보고, 수직으로만 움직이면 방향을 유지한다.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Facing_SupportUsesActualRegroupMotion(bool horizontal)
        {
            GameObject root = new GameObject("SupportFacingTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                agent.ApplyRole(BattleRole.Support);
                EntityRender render = AttachFacingRender(agent);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right * 8f);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                BattleAgent ally = CreateAgent(root.transform, field, horizontal ? Vector3.left * 9f : Vector3.up * 9f);
                ally.Combatant.Health.SetCurrentRatio(0.5f);
                field.Allies(agent.Team).Add(ally);
                render.FaceTowards(horizontal ? 1f : -1f);
                agent.SupportWoundedAlly(0.1f);
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative, "합류는 실제 이동 방향, 수직 합류는 이전 방향이어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>이동하지 않은 접근과 정지는 지난 평활 속도나 멀리 있는 대상으로 돌아서면 안 된다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Facing_StationaryStepDiscardsOldMotion(bool attemptedMove)
        {
            GameObject root = new GameObject("StoppedFacingTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                EntityRender render = AttachFacingRender(agent);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right * 8f);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                render.FaceTowards(attemptedMove ? 1f : -1f);
                SetField(agent, "_smoothedMotionX", -10f);
                if (attemptedMove) agent.MoveToTarget(0f);
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.EqualTo(attemptedMove ? 1f : -1f));
                Assert.That(typeof(BattleAgent).GetField("_smoothedMotionX", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(agent), Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>대상이 없는 순회도 BattleAgent의 최종 방향 판단으로 실제 이동 쪽을 본다.</summary>
        [Test]
        public void Facing_TraversalFacesActualMovementWithoutTarget()
        {
            GameObject root = new GameObject("TraversalFacingTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                BattleAgent agent = CreateAgent(root.transform, root.AddComponent<NodeBattlefield>(), Vector3.zero);
                EntityRender render = AttachFacingRender(agent);
                agent.BeginTraversal();
                agent.transform.position = Vector3.left;
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative);
                Assert.That(render.FaceMotion, Is.False, "활성 BattleAgent와 그림이 서로 방향을 덮으면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>순간이동을 빠른 보행으로 읽으면 좌우 방향과 걷기 동작이 이동 후에 남는다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Facing_TeleportKeepsDirectionAndResetsMotion(bool withTarget)
        {
            GameObject root = new GameObject("TeleportFacingTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                EntityRender render = AttachFacingRender(agent);
                if (withTarget)
                {
                    BattleAgent target = CreateAgent(root.transform, field, Vector3.right * 8f);
                    SetField(target, "team", BattleTeam.Player);
                    SetField(agent, "_target", target);
                }
                render.FaceTowards(-1f);
                SetField(agent, "_smoothedMotionX", 10f);
                SetField(render, "_walkHold", 0.1f);
                agent.TeleportToCombatPosition(Vector2.left * 5f);
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative);
                Assert.That(typeof(EntityRender).GetField("_walkHold", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(render), Is.EqualTo(0f));
                Assert.That(typeof(BattleAgent).GetField("_smoothedMotionX", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(agent), Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>비주얼이 루트로 폴백한 캐릭터도 자세 복원이 이동한 루트를 원위치로 되돌리면 안 된다.</summary>
        [Test]
        public void Facing_PoseRestoreNeverMovesRoot()
        {
            GameObject root = new GameObject("RootPoseTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                SetField(agent, "body", agent.transform);
                SetField(agent, "_bodyBaseLocalPosition", Vector3.zero);
                agent.transform.position = Vector3.left * 2f;
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                MethodInfo face = typeof(BattleAgent).GetMethod("Face", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(face, Is.Not.Null);
                face.Invoke(agent, new object[] { 1f });
                Assert.That(agent.transform.position, Is.EqualTo(Vector3.left * 2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>넉백이 끝난 프레임과 겹침 해소를 대상 없는 보행으로 오해하지 않아야 한다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Facing_ForcedMovementWithoutTargetKeepsDirection(bool recoil)
        {
            GameObject root = new GameObject("ForcedFacingTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                EntityRender render = AttachFacingRender(agent);
                if (recoil)
                {
                    SetField(agent, "_recoilRemaining", Vector2.left);
                    SetField(agent, "_recoilTimeLeft", 0.05f);
                    InvokeAgent(agent, "TickHitRecoil", 0.1f);
                }
                else
                {
                    BattleAgent ally = CreateAgent(root.transform, field, Vector3.right * 0.1f);
                    field.Allies(agent.Team).Add(ally);
                    SetField(agent, "autoDrive", false);
                    SetField(agent, "personalSpaceRadius", 1f);
                    SetField(agent, "separationStrength", 2f);
                    InvokeAgent(agent, "ResolveCrowding", 0.1f);
                }
                Assert.That(agent.transform.position.x, Is.Negative, "검증에서 실제로 왼쪽으로 밀려야 합니다.");
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Positive, "강제 이동으로 등을 돌리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static EntityRender AttachFacingRender(BattleAgent agent)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(agent.transform, false);
            EntityRender render = visual.AddComponent<EntityRender>();
            SetField(agent, "_render", render);
            SetField(agent, "body", visual.transform);
            SetField(render, "_motionSource", agent.transform);
            SetField(render, "_lastPosition", agent.transform.position);
            return render;
        }

        /// <summary>치고 빠지기가 적을 놓친 뒤에도 공격·후퇴 시간을 채우며 멈춰 있던 동작을 검사한다.</summary>
        [TestCase("Attack", false)]
        [TestCase("Retreat", false)]
        [TestCase("Retreat", true)]
        public void Skirmish_ReapproachesAfterRangeLossOrTargetChange(string phase, bool changed)
        {
            GameObject root = new GameObject("SkirmishTransitionTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                BattleAgent oldTarget = CreateAgent(root.transform, field, Vector3.left * 9f);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right * (changed ? 1f : 8f));
                SetField(oldTarget, "team", BattleTeam.Player);
                SetField(target, "team", BattleTeam.Player);
                field.Opponents(agent.Team).Add(target);
                SetField(agent, "_target", target);
                SetField(agent, "_nextTargetReconsiderTime", float.PositiveInfinity);
                object action = MakeSkirmish(agent, phase);
                FieldInfo tracked = action.GetType().GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic);
                if (tracked != null) tracked.SetValue(action, changed ? oldTarget : target);

                TickAction(action);

                Assert.That(ReadActionField(action, "_phase").ToString(), Is.EqualTo(changed ? "Attack" : "Approach"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>움찔하거나 휘두르는 동안 공격·후퇴 타이머가 끝나 행동을 끊지 않아야 한다.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Skirmish_WaitsDuringCombatMoveLock(bool staggered)
        {
            GameObject root = new GameObject("SkirmishLockTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                SetField(agent, "_nextTargetReconsiderTime", float.PositiveInfinity);
                if (staggered) agent.Combatant.Stagger(1f);
                else SetField(agent.Combatant, "_windingUp", true);
                object action = MakeSkirmish(agent, "Attack");
                SetField(action, "_timer", 0f);
                TickAction(action);
                Assert.That(ReadActionField(action, "_phase").ToString(), Is.EqualTo("Attack"));
                Assert.That(ReadActionField(action, "_timer"), Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>휘두르기 중 체력이 낮은 다른 적이 생겼다고 공격 대상을 교체하면 예비 동작이 취소된다.</summary>
        [Test]
        public void BattleAgent_KeepsValidTargetDuringWindup()
        {
            GameObject root = new GameObject("WindupTargetTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right);
                BattleAgent weaker = CreateAgent(root.transform, field, Vector3.right * 2f);
                SetField(target, "team", BattleTeam.Player);
                SetField(weaker, "team", BattleTeam.Player);
                weaker.Combatant.Health.SetCurrentRatio(0.1f);
                field.Opponents(agent.Team).Add(target);
                field.Opponents(agent.Team).Add(weaker);
                SetField(agent, "_target", target);
                SetField(agent, "_nextTargetReconsiderTime", float.NegativeInfinity);
                SetField(agent.Combatant, "_windingUp", true);
                Assert.That(agent.FindTarget(TargetPriority.LowestHealth), Is.SameAs(target));
                target.gameObject.SetActive(false);
                Assert.That(agent.FindTarget(TargetPriority.LowestHealth), Is.SameAs(weaker), "유효하지 않은 대상은 고정하면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>탱커가 먼 전열만 쫓는 동안 후열을 때리는 적을 방치하지 않도록 위협 우선순위를 검사한다.</summary>
        [Test]
        public void BattleAgent_TankPrioritizesBacklineThreat()
        {
            GameObject root = new GameObject("TankPeelTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent tank = CreateAgent(root.transform, field, Vector3.zero);
                tank.ApplyRole(BattleRole.Tank);
                BattleAgent ally = CreateAgent(root.transform, field, Vector3.left);
                ally.ApplyRole(BattleRole.Support);
                BattleAgent front = CreateAgent(root.transform, field, Vector3.right);
                BattleAgent threat = CreateAgent(root.transform, field, Vector3.right * 4f);
                SetField(front, "team", BattleTeam.Player);
                SetField(threat, "team", BattleTeam.Player);
                threat.ApplyRole(BattleRole.Ranged);
                SetField(threat, "_target", ally);
                field.Opponents(tank.Team).Add(front);
                field.Opponents(tank.Team).Add(threat);
                Assert.That(tank.FindTarget(TargetPriority.Frontline), Is.SameAs(threat));
                SetField(tank, "_target", null);
                Assert.That(tank.FindTarget(TargetPriority.Nearest), Is.SameAs(front), "후열 보호 가중치는 Frontline 전술에만 적용합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>지원은 치유 범위 밖의 부상 아군에게 합류하되 이동 함수에서 추가 치유를 내지 않는다.</summary>
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void BattleAgent_SupportRegroupsWithoutExtraHealing(bool locked, bool healthy)
        {
            GameObject root = new GameObject("SupportRegroupTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent support = CreateAgent(root.transform, field, Vector3.zero);
                support.ApplyRole(BattleRole.Support);
                BattleAgent ally = CreateAgent(root.transform, field, Vector3.right * 9f);
                ally.Combatant.Health.SetCurrentRatio(healthy ? 1f : 0.5f);
                field.Allies(support.Team).Add(ally);
                if (locked) support.Combatant.Stagger(1f);
                MethodInfo method = typeof(BattleAgent).GetMethod("SupportWoundedAlly");
                Assert.That(method, Is.Not.Null, "BattleAgent.SupportWoundedAlly를 찾지 못했습니다.");
                int health = ally.Combatant.Health.CurrentHealth;
                bool assisting = (bool)method.Invoke(support, new object[] { 0.1f });
                Assert.That(assisting, Is.EqualTo(!healthy));
                Assert.That(support.transform.position.x > 0f, Is.EqualTo(!healthy && !locked));
                Assert.That(ally.Combatant.Health.CurrentHealth, Is.EqualTo(health), "주기 치유 외에 회복을 추가하면 안 됩니다.");
                if (!healthy && !locked)
                {
                    support.transform.position = ally.transform.position + Vector3.left * 2f;
                    Vector3 position = support.transform.position;
                    Assert.That((bool)method.Invoke(support, new object[] { 0.1f }), Is.True);
                    Assert.That(support.transform.position, Is.EqualTo(position), "치유 범위에 들어오면 지원 위치를 유지합니다.");
                    ally.BeginTraversal();
                    Assert.That((bool)method.Invoke(support, new object[] { 0.1f }), Is.False, "다른 방으로 가는 아군은 쫓지 않습니다.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>속박은 합류 이동만 막아야 하며 지원의 사거리 내 공격까지 끊으면 안 된다.</summary>
        [Test]
        public void BattleAgent_SnaredSupportKeepsAttackWhileUnableToRegroup()
        {
            GameObject root = new GameObject("SnaredSupportTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent support = CreateAgent(root.transform, field, Vector3.zero);
                support.ApplyRole(BattleRole.Support);
                BattleAgent ally = CreateAgent(root.transform, field, Vector3.right * 9f);
                ally.Combatant.Health.SetCurrentRatio(0.5f);
                field.Allies(support.Team).Add(ally);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.left);
                SetField(target, "team", BattleTeam.Player);
                SetField(support, "_target", target);
                SetField(support.Combatant, "_target", target.Combatant);
                SetField(support.Combatant, "_isAttacking", true);
                support.ApplySnare(1f);

                Assert.That(support.SupportWoundedAlly(0.1f), Is.True);
                Assert.That(support.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(support.Combatant.Target, Is.SameAs(target.Combatant));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object MakeSkirmish(BattleAgent agent, string phase)
        {
            Type type = Type.GetType("Code.BT.SkirmishTargetAction, DungeonKeeper.Runtime");
            Assert.That(type, Is.Not.Null);
            object action = Activator.CreateInstance(type);
            SetField(action, "_agent", agent);
            SetField(action, "_timer", 0.5f);
            FieldInfo target = type.GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic);
            if (target != null) target.SetValue(action, agent.CurrentTarget);
            FieldInfo field = type.GetField("_phase", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(action, Enum.Parse(field.FieldType, phase));
            return action;
        }

        private static object ReadActionField(object action, string name)
        {
            FieldInfo field = action.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return field.GetValue(action);
        }

        private static void TickAction(object action)
        {
            MethodInfo method = action.GetType().GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(action, null);
        }

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
            Assert.That(Code.BT.CombatMoveRules.CanTurn(0.1f), Is.False, "돌아선 직후 다시 돌아서지 않는다.");

            Assert.That(Code.BT.CombatMoveRules.ShouldGiveUpChase(2.9f), Is.False);
            Assert.That(Code.BT.CombatMoveRules.ShouldGiveUpChase(3f), Is.True, "3초 넘게 닿지 못하면 다른 상대로 바꾼다.");
        }

        /// <summary>
        /// 닿지 않은 채 대상을 쫓아 기둥·시설을 돌아갈 때는 가는 쪽을 본다. 예전에는 대상을 본 채 뒷걸음으로 돌아갔다
        /// (3일차 모험가 다가가기 이동의 31%). 닿는 거리와 넉백 중에는 그대로 대상을 본다.
        /// </summary>
        [Test]
        public void BattleAgent_FacesTravelOnlyWhileChasingOutOfReach()
        {
            GameObject root = new GameObject("CombatTravelFacingTest");
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

                // 멀리 있는 대상을 쫓는데 길이 왼쪽으로 꺾였다 → 가는 쪽(왼쪽).
                MethodInfo travel = typeof(BattleAgent).GetMethod("SetTravelPosition", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(travel, Is.Not.Null, "BattleAgent.SetTravelPosition을 찾지 못했습니다.");
                travel.Invoke(agent, new object[] { Vector3.left, true });
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative, "닿지 않은 채 돌아가는 동안은 가는 쪽을 봐야 합니다.");

                // 맞고 밀리는 중이면 쫓는 걸음이어도 대상을 본다.
                SetField(agent, "_recoilTimeLeft", 0.05f);
                travel.Invoke(agent, new object[] { Vector3.left * 2f, true });
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Positive, "밀리는 중에는 대상을 봐야 합니다.");
                SetField(agent, "_recoilTimeLeft", 0f);

                // 닿는 거리에서는 쫓는 걸음이어도 대상을 본다.
                render.FaceTowards(-1f);
                travel.Invoke(agent, new object[] { agent.transform.position + Vector3.left, true });
                target.transform.position = agent.transform.position + Vector3.right * 1f;
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Positive, "닿는 거리에서는 대상을 봐야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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

                agent.CombatBackStep(0.1f);
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);

                Assert.That(render.FacingX, Is.Positive, "오른쪽 대상을 등지고 왼쪽으로 돌아서면 안 됩니다.");
                Assert.That(render.FaceMotion, Is.False, "대상이 있으면 그림의 이동 방향 추적을 꺼야 합니다.");
                target.transform.position = Vector3.left * 8f;
                agent.FaceTarget(target);
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative, "대상이 반대로 이동하면 대상 쪽으로 돌아서야 합니다.");
                target.transform.position = agent.transform.position + Vector3.right * 0.05f;
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FacingX, Is.Negative, "거의 겹친 대상 때문에 방향이 떨리면 안 됩니다.");
                SetField(agent, "_target", null);
                InvokeAgent(agent, "UpdateCombatFacing", 0.1f);
                Assert.That(render.FaceMotion, Is.False, "대상이 없어도 활성 BattleAgent가 방향을 결정합니다.");
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

        [TestCase(false)]
        [TestCase(true)]
        public void RoomMovement_GuardAndRegroupReachBeyondPillar(bool regroup)
        {
            GameObject root = new GameObject("RoomMovementTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                Code.MapCreateSystem.NodeTrapGrid grid = root.AddComponent<Code.MapCreateSystem.NodeTrapGrid>();
                grid.ConfigureCells(new Code.MapCreateSystem.RoomShape("test", Code.MapCreateSystem.RoomSize.Medium,
                    false, Code.MapCreateSystem.RoomDoors.None,
                    new[] { ".......", "...#...", "...#...", "...#...", "...#...", "...#...", "......." }), 1f);
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, grid.CellWorldPosition(1, 3));
                BattleAgent other = CreateAgent(root.transform, field, grid.CellWorldPosition(5, 3));
                if (regroup)
                    field.Allies(agent.Team).Add(other);
                else
                {
                    SetField(other, "team", BattleTeam.Player);
                    field.Opponents(agent.Team).Add(other);
                }

                for (int i = 0; i < 400; i++)
                {
                    if (regroup) agent.RetreatTowardAllies(0.05f);
                    else agent.GuardBackline(0.05f);
                    Assert.That(grid.IsWalkableWorld(agent.transform.position), Is.True, "우회 중 막힌 칸을 밟으면 안 됩니다.");
                }
                Assert.That(Vector2.Distance(agent.transform.position, other.transform.position), Is.LessThan(0.3f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("GuardBacklineAction", false)]
        [TestCase("GuardBacklineAction", true)]
        [TestCase("FleeToAlliesAction", false)]
        [TestCase("FleeToAlliesAction", true)]
        [TestCase("StrafeTargetAction", false)]
        [TestCase("StrafeTargetAction", true)]
        [TestCase("TauntAction", true)]
        public void TimedAction_LockPreservesRemainingTime(string name, bool staggered)
        {
            GameObject root = new GameObject("TimedActionTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                if (staggered) agent.Combatant.Stagger(1f);
                else agent.ApplySnare(1f);
                object action = Activator.CreateInstance(Type.GetType("Code.BT." + name + ", DungeonKeeper.Runtime"));
                SetField(action, "_agent", agent);
                SetField(action, "_timer", -0.1f);
                MethodInfo update = action.GetType().GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(update, Is.Not.Null);
                Assert.That(update.Invoke(action, null).ToString(), Is.EqualTo("Running"));
                Assert.That(ReadActionField(action, "_timer"), Is.EqualTo(-0.1f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Charge_LockDoesNotReportArrival(int lockedBy)
        {
            GameObject root = new GameObject("ChargeArrivalTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right * 8f);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                SetField(agent, "_nextTargetReconsiderTime", float.PositiveInfinity);
                field.Opponents(agent.Team).Add(target);
                if (lockedBy == 0) agent.ApplySnare(1f);
                else if (lockedBy == 1) agent.Combatant.Stagger(1f);
                else SetField(agent.Combatant, "_windingUp", true);
                object action = Activator.CreateInstance(Type.GetType("Code.BT.ChargeTargetAction, DungeonKeeper.Runtime"));
                SetField(action, "_agent", agent);
                MethodInfo update = action.GetType().GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(update, Is.Not.Null);
                Assert.That(update.Invoke(action, null).ToString(), Is.EqualTo("Running"));
                Assert.That(agent.transform.position, Is.EqualTo(Vector3.zero));
                target.transform.position = Vector3.right * 0.5f;
                Assert.That(update.Invoke(action, null).ToString(), Is.EqualTo("Success"), "잠겨 있어도 이미 사거리 안이면 도착입니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Skirmish_SnareKeepsRetreatPhase()
        {
            GameObject root = new GameObject("SkirmishSnareTest");
            try
            {
                root.AddComponent<BoxCollider2D>();
                NodeBattlefield field = root.AddComponent<NodeBattlefield>();
                BattleAgent agent = CreateAgent(root.transform, field, Vector3.zero);
                BattleAgent target = CreateAgent(root.transform, field, Vector3.right);
                SetField(target, "team", BattleTeam.Player);
                SetField(agent, "_target", target);
                SetField(agent, "_nextTargetReconsiderTime", float.PositiveInfinity);
                agent.ApplySnare(1f);
                object action = MakeSkirmish(agent, "Retreat");
                SetField(action, "_timer", -0.1f);
                TickAction(action);
                Assert.That(ReadActionField(action, "_phase").ToString(), Is.EqualTo("Retreat"));
                Assert.That(ReadActionField(action, "_timer"), Is.EqualTo(-0.1f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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
