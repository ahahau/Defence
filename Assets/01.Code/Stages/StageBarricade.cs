using Code.Combat;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>체력 0에서 즉시 차단 해제. 회복과 환급은 실제 지불 금액에만 적용한다.</summary>
    public sealed class StageBarricade : MonoBehaviour
    {
        private StageSession _session;
        private float _lastHit = float.NegativeInfinity;
        private float _recovery;
        private bool _removed;
        public Combatant Combat { get; private set; }
        public Vector2Int Cell { get; private set; }
        public int PaidGold { get; private set; }
        public bool IsAlive => !_removed && Combat != null && Combat.IsAlive;

        public void Initialize(StageSession session, Vector2Int cell, int paid)
        {
            _session = session;
            Cell = cell;
            PaidGold = paid;
            Combat = GetComponent<Combatant>();
            Combat.Health.SetMaxHealth(session.Definition.BarricadeHealth, true);
            Combat.Health.Damaged += HandleDamaged;
        }

        public void Demolish()
        {
            if (_session.Phase != StagePhase.Preparing)
                return;
            Remove(_session.Definition.DemolishRefund);
        }

        private void Update()
        {
            if (_removed || _session == null)
                return;
            if (!Combat.IsAlive)
            {
                Remove(_session.Definition.DestroyRefund);
                return;
            }
            if (_session.Phase != StagePhase.Preparing && _session.Phase != StagePhase.Fighting)
                return;
            if (Time.time - _lastHit < _session.Definition.RecoveryDelay)
                return;
            if (Combat.Health.CurrentHealth >= Combat.Health.MaxHealth)
            {
                _recovery = 0;
                return;
            }
            _recovery += Time.deltaTime * _session.Definition.RecoveryPerSecond;
            int amount = Mathf.FloorToInt(_recovery);
            if (amount > 0)
            {
                _recovery -= amount;
                Combat.Health.Heal(amount);
            }
        }

        private void HandleDamaged(int amount)
        {
            _lastHit = Time.time;
            _recovery = 0;
            if (!Combat.IsAlive)
                Remove(_session.Definition.DestroyRefund);
        }

        private void Remove(float rate)
        {
            if (_removed)
                return;
            _removed = true;
            _session.Board.Barricades.Remove(Cell);
            _session.Refund(Mathf.FloorToInt(PaidGold * Mathf.Clamp01(rate)));
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Combat != null && Combat.Health != null)
                Combat.Health.Damaged -= HandleDamaged;
        }
    }
}
