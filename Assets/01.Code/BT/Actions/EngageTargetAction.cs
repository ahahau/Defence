using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Random = UnityEngine.Random;

namespace _01.Code.BT.Actions
{
    /// <summary>찾기→(원거리면 거리유지)→사거리면 공격, 아니면 접근 — 전투 전체를 노드 하나로.
    /// 사거리 안에서는 공격 쿨이 도는 동안 좌우 스트레이프/백스텝을 무작위로 섞고(살아있는 교전),
    /// 가끔 표적을 다른 적으로 바꾼다(난전). 적이 없으면 Failure(다음 분기 Idle/Traverse로).</summary>
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
        [Tooltip("난전 중 가끔 다른 적으로 표적을 바꾼다. Focused 우선순위에서는 무시(협동 화력 유지).")]
        [SerializeReference] public BlackboardVariable<bool> SwitchTargets = new(true);

        private BattleAgent _agent;

        // 기동 상태 — 에이전트별 노드 인스턴스에 보존. 매 프레임 흔들리지 않게 한 동작을 잠깐 유지.
        private enum Maneuver { Hold, StrafeLeft, StrafeRight, BackStep }
        private Maneuver _maneuver;
        private float _maneuverUntil;
        private float _nextSwitchTime;

        protected override Status OnStart()
        {
            _agent = Resolve();
            _maneuverUntil = 0f;
            _nextSwitchTime = Time.time + Random.Range(2f, 5f);
            return _agent == null ? Status.Failure : Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            var priority = Priority != null ? Priority.Value : TargetPriority.Nearest;
            
            // 난전 표적 변경 — 집중공격(Focused) 역할은 협동 화력을 위해 제외.
            if (SwitchOn && priority != TargetPriority.Focused && Time.time >= _nextSwitchTime)
            {
                _agent.SwitchTargetRandomly();
                _nextSwitchTime = Time.time + Random.Range(3f, 6f);
            }
            
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
            if (VariedOn && !_agent.AttackReady)
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
