using System;
using Code.Units;
using GameLib.Entity;

namespace Code.Combat
{
    /// <summary>
    /// 프리팹에 실제로 붙는 체력 컴포넌트. 계산과 상태는 전부 <see cref="HealthModule"/>(Core)에 있고,
    /// 여기에는 전투 쪽 사정만 남는다.
    ///
    /// 클래스 이름과 파일을 그대로 둔 것은 일부러다 — 프리팹 16종이 이 스크립트를 GUID로 물고 있고,
    /// 호출부도 27개 파일에 흩어져 있다. 껍데기를 남기면 그 배선을 하나도 건드리지 않고
    /// 구현만 Core로 옮길 수 있다. 직렬화 필드(maxHealth)는 기반 클래스로 갔지만
    /// Unity가 기반 클래스 필드도 같은 키로 직렬화하므로 프리팹 값은 그대로 유지된다.
    /// </summary>
    public class Health : HealthModule, IDamageable
    {
        /// <summary>
        /// 누구든 맞으면 울리는 전역 방송. 웨이브 집계(WaveManager)가 이걸로 가한 피해를 센다.
        /// Core로 올리지 않은 이유는 이게 체력의 성질이 아니라 전투 집계의 사정이기 때문이다.
        /// </summary>
        public static event Action<Health, int, bool> AnyDamaged;

        private Unit _unit;
        private Code.BT.BattleAgent _battleAgent;
        private bool _unitResolved;

        protected override void OnDamageApplied(int appliedDamage, bool isCritical) =>
            AnyDamaged?.Invoke(this, appliedDamage, isCritical);

        // 재배치 준비 중인 부하는 받는 피해가 줄어들고, 요새 방에 선 부하도 덜 아프다.
        protected override int ModifyIncomingDamage(int damage)
        {
            if (!_unitResolved)
            {
                _unit = GetComponentInParent<Unit>();
                _battleAgent = _unit != null ? _unit.GetComponent<Code.BT.BattleAgent>() : null;
                _unitResolved = true;
            }

            if (_unit == null)
                return damage;

            if (_unit.IsPreparingRedeploy)
                damage = RedeployRules.ApplyPreparationDamage(damage, _unit.Trait);
            if (damage <= 0)
                return 0;

            var battlefield = _battleAgent != null ? _battleAgent.Battlefield : null;
            return battlefield != null && battlefield.TryGetComponent<Code.MapCreateSystem.Node>(out var node)
                ? Code.MapCreateSystem.RoomFeatureRules.ScaleUnitDamage(damage, node.Feature)
                : damage;
        }
    }
}
