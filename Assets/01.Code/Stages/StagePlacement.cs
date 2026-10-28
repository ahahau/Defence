using System.Collections.Generic;
using Code.Buildings;
using Code.Units;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>준비 전용 설치/철거와 실제 지불 금액. 스테이지 진행과 책임을 분리한다.</summary>
    public sealed class StagePlacement
    {
        private readonly StageSession _session;
        private readonly Transform _root;
        private readonly StageBarricade _prefab;
        private readonly List<Trap> _traps = new();
        private readonly Dictionary<Trap, StageOption> _trapOptions = new();
        private readonly Dictionary<Trap, int> _trapPayments = new();
        public IReadOnlyList<Trap> Traps => _traps;
        public bool HasPlacements
        {
            get
            {
                if (_session.Board.Actors.Count > 0)
                    return true;
                foreach (Trap trap in _traps)
                    if (trap != null && !trap.IsDestroyed)
                        return true;
                return false;
            }
        }

        public StagePlacement(StageSession session, Transform root, StageBarricade prefab)
        {
            _session = session;
            _root = root;
            _prefab = prefab;
        }

        public bool HasPlaced(StageOption option)
        {
            foreach (StageActor actor in _session.Board.Actors)
                if (actor != null && actor.Option == option)
                    return true;
            foreach (Trap trap in _traps)
                if (trap != null && !trap.IsDestroyed && _trapOptions[trap] == option)
                    return true;
            return false;
        }

        public bool TryPlace(Vector2Int cell, StageOption option, bool barricade)
        {
            if (_session.Phase != StagePhase.Preparing || !_session.Definition.IsBuildable(cell) || _session.Board.Reserved(cell)
                || _session.Board.BlockerAt(cell, null) != null)
                return false;
            if (!barricade && (!_session.IsSelected(option) || !_session.Profile.IsUnlocked(option)))
                return false;
            Vector2Int footprint = !barricade && option?.Trap?.Prefab is Trap template ? template.Footprint : Vector2Int.one;
            bool empowered = false;
            for (int y = 0; y < footprint.y; y++)
            for (int x = 0; x < footprint.x; x++)
            {
                Vector2Int occupied = cell + new Vector2Int(x, y);
                empowered |= _session.Definition.IsEmpowered(occupied);
                if (!_session.Definition.IsBuildable(occupied) || _session.Board.Reserved(occupied)
                    || _session.Board.BlockerAt(occupied, null) != null)
                    return false;
                foreach (Trap installed in _traps)
                    if (installed != null && !installed.IsDestroyed && installed.Covers(_session.Board.World(occupied), 0f))
                        return false;
            }
            int cost = barricade ? _session.Definition.BarricadeCost : option.Cost;
            if (_session.Gold < cost)
            {
                _session.Tell("금화가 부족합니다");
                return false;
            }
            if (!barricade && option.Unit == null && (option.Trap == null || option.Trap.Prefab is not Trap))
                return false;
            if (barricade)
            {
                StageBarricade wall = Object.Instantiate(_prefab, _session.Board.World(cell), Quaternion.identity, _root);
                wall.Initialize(_session, cell, cost);
                _session.Board.Barricades.Add(cell, wall);
            }
            else if (option.Unit != null)
            {
                Unit unit = Object.Instantiate(option.Unit.Prefab, _session.Board.World(cell), Quaternion.identity, _root);
                unit.Initialize(option.Unit);
                StageActor actor = unit.gameObject.AddComponent<StageActor>();
                actor.Initialize(_session, cell, option, cost);
                unit.Combatant.SetAttackModifier(actor, 0f, 1f + _session.Profile.Level(option.Id) * _session.Catalog.DamagePerUpgrade);
            }
            else
            {
                Vector3 center = _session.Board.World(cell) + new Vector3(footprint.x - 1, footprint.y - 1, 0) * (_session.Definition.CellSize * 0.5f);
                Trap trap = Object.Instantiate((Trap)option.Trap.Prefab, center, Quaternion.identity, _root);
                // 여러 칸 함정은 덮은 칸만큼 늘린다(방 격자 NodeTrapGrid.TryPlace와 같다). 한 칸 크기로 두면 2×2를 치는데 그림은 한 칸이다.
                Vector3 scale = trap.transform.localScale * _session.Definition.CellSize;
                trap.transform.localScale = new Vector3(scale.x * footprint.x, scale.y * footprint.y, scale.z);
                trap.Initialize(option.Trap);
                trap.BindStage(_session);
                // 덮은 칸 중 하나라도 강화 칸이면 영구 강화 배율 위에 곱한다
                float upgrade = 1f + _session.Profile.Level(option.Id) * _session.Catalog.DamagePerUpgrade;
                trap.SetStageDamageMultiplier(upgrade * (empowered ? _session.Definition.EmpoweredTrapMultiplier : 1f));
                _traps.Add(trap);
                _trapOptions.Add(trap, option);
                _trapPayments.Add(trap, cost);
            }
            _session.Pay(cost);
            _session.RefreshUI();
            return true;
        }

        public void Demolish(Vector2Int cell)
        {
            if (_session.Phase != StagePhase.Preparing)
                return;
            if (_session.Board.Barricades.TryGetValue(cell, out StageBarricade wall))
            {
                wall.Demolish();
                return;
            }
            for (int i = _session.Board.Actors.Count - 1; i >= 0; i--)
            {
                StageActor actor = _session.Board.Actors[i];
                if (actor != null && !actor.IsEnemy && _session.Board.Cell(actor.transform.position) == cell)
                {
                    _session.Refund(Mathf.FloorToInt(actor.PaidGold * _session.Definition.DemolishRefund));
                    actor.Stop();
                    _session.Board.Actors.RemoveAt(i);
                    Object.Destroy(actor.gameObject);
                    return;
                }
            }
            for (int i = _traps.Count - 1; i >= 0; i--)
            {
                Trap trap = _traps[i];
                if (trap == null || !trap.Covers(_session.Board.World(cell), 0f))
                    continue;
                _session.Refund(Mathf.FloorToInt(_trapPayments[trap] * _session.Definition.DemolishRefund));
                _traps.RemoveAt(i);
                _trapOptions.Remove(trap);
                _trapPayments.Remove(trap);
                Object.Destroy(trap.gameObject);
                return;
            }
        }

        public void CheckTraps(StageActor enemy)
        {
            Trap contact = null;
            foreach (Trap trap in _traps)
                if (trap != null && !trap.IsDestroyed && trap.Covers(enemy.transform.position, 0f))
                {
                    contact = trap;
                    break;
                }
            if (enemy.EnterTrap(contact) && enemy.IsAlive
                && Random.value < Code.Enemies.MobilityRules.StepChance(contact.TriggerChance, enemy.Enemy.Data.Mobility))
                contact.TriggerBy(enemy.Enemy);
        }

    }
}
