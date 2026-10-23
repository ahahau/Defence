using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Random = UnityEngine.Random;

namespace Code.BT.Actions
{
    /// <summary>찾기→(원거리면 거리유지)→사거리면 공격, 아니면 접근 — 전투 전체를 노드 하나로.
    /// 사거리 안에서 근접은 대상을 마주 보고 버티다 휘두르고, 휘두른 직후 반 걸음만 느리게 물러난다.
    /// 원거리는 공격 쿨이 도는 동안 좌우 스트레이프/백스텝을 섞는다. 휘두르는 중·맞고 움찔하는 중에는
    /// <see cref="BattleAgent"/>가 발을 묶는다. 쫓아도 닿지 못하는 표적만 가까운 다른 적으로 바꾼다. 적이 없으면 Failure.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Engage Target",
        description: "Finds, approaches, and attacks the best target with varied combat movement. One node = full combat. Fails when no enemy.",
        story: "[Agent] engages the enemy",
        category: "Action/Battle",
        id: "b3e9c1d72f5a4c08a9e6d4b15c270e84")]
    public partial class EngageTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("타깃 우선순위: Nearest/Focused/Frontline/Backline/LowestHealth.")]
        [SerializeReference] public BlackboardVariable<TargetPriority> Priority = new(TargetPriority.Nearest);
        [Tooltip("공격 쿨이 도는 동안 제자리 대신 좌우 스트레이프/백스텝을 무작위로 섞는다(살아있는 교전). 끄면 제자리.")]
        [SerializeReference] public BlackboardVariable<bool> VariedMovement = new(true);
        [Tooltip("쫓아도 3초 넘게 닿지 못하는 표적은 가까운 다른 적으로 바꾼다. Focused 우선순위에서는 무시(협동 화력 유지).")]
        [SerializeReference] public BlackboardVariable<bool> SwitchTargets = new(true);

        private BattleAgent _agent;

        // 기동 상태 — 에이전트별 노드 인스턴스에 보존. 매 프레임 흔들리지 않게 한 동작을 잠깐 유지.
        private enum Maneuver { Hold, StrafeLeft, StrafeRight, BackStep }
        private Maneuver _maneuver;
        private float _maneuverUntil;

        protected override Status OnStart()
        {
            _agent = Resolve();
            _maneuverUntil = 0f;
            return _agent == null ? Status.Failure : Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            var priority = Priority != null ? Priority.Value : TargetPriority.Nearest;
            
            // 쫓아도 닿지 못하는 대상만 바꾼다 — 집중공격(Focused) 역할은 협동 화력을 위해 제외.
            // 예전에는 3~6초마다 무작위로 바꿔, 싸우던 중에 엉뚱한 쪽으로 돌아섰다.
            if (SwitchOn && priority != TargetPriority.Focused && _agent.IsChaseStalled)
                _agent.SwitchToNearestOtherTarget();

            if (_agent.FindTarget(priority) == null)
                return Status.Failure; // 적 없음 → 다음 분기(Idle/Traverse)로

            _agent.RegisterFocus();
            var dt = Time.deltaTime;

            // 원거리가 너무 가까우면 물러나며 쏨(근접/탱커는 false라 통과)
            if (_agent.MaintainRange(dt))
                return Status.Running;

            if (!_agent.TargetInRange())
            {
                _agent.MoveToTarget(dt); // 사거리 밖 → 붙는다
                return Status.Running;
            }

            // 사거리 안: 공격 루프는 항상 돌려둔다(쿨 충전 유지). 쿨 도는 동안만 기동을 섞어
            // 움직임을 주되, Attack()/CombatStrafe 모두 StopCombat을 부르지 않아 공격 타이밍은 그대로다.
            _agent.Attack();
            if (_agent.InAttackRecovery)
            {
                // 원거리는 쏜 직후 제자리에 선다(근접이 파고들 틈). 예전에는 여기서 좌우를 프레임마다 새로 뽑아
                // 제자리에서 떨었다(실측: 모험가 이동 떨림의 91%가 치유사·궁수의 이 구간).
                if (!_agent.UsesRangedKiting)
                    _agent.MeleeRecoveryStep(dt);
                return Status.Running;
            }

            // 근접은 대기 시간에도 대상을 마주 보고 버틴다(휘두른 직후의 반 걸음 물러남은 위 회복 구간이 맡는다).
            // 무작위 좌우 이동을 섞으면 칼을 맞대는 대신 제자리에서 미끄러지며 떠는 것처럼 보였다.
            if (VariedOn && _agent.UsesRangedKiting && !_agent.AttackReady)
                PerformManeuver(dt);

            return Status.Running;
        }

        /// <summary>쿨다운 동안 좌우 스트레이프/백스텝/제자리를 잠깐씩 무작위로 — 공격 타이밍은 건드리지 않는다.</summary>
        private void PerformManeuver(float dt)
        {
            if (Time.time >= _maneuverUntil)
                RollManeuver();

            switch (_maneuver)
            {
                case Maneuver.StrafeLeft: _agent.CombatStrafe(dt, -1f); break;
                case Maneuver.StrafeRight: _agent.CombatStrafe(dt, 1f); break;
                case Maneuver.BackStep: _agent.CombatBackStep(dt); break;
                // Hold: 이동 없음 — 공격 루프가 제자리에서 충전/발사.
            }
        }

        private void RollManeuver()
        {
            _maneuverUntil = Time.time + Random.Range(0.3f, 0.7f);
            var r = Random.value;
            _maneuver = r < 0.40f ? Maneuver.Hold        // 40% 제자리
                      : r < 0.65f ? Maneuver.StrafeLeft  // 25%
                      : r < 0.90f ? Maneuver.StrafeRight // 25%
                      : Maneuver.BackStep;               // 10%
        }

        private bool VariedOn => VariedMovement == null || VariedMovement.Value;
        private bool SwitchOn => SwitchTargets == null || SwitchTargets.Value;

        protected override void OnEnd() => _agent?.StopAttack();

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
