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

        /// <summary>미리보기와 실행이 함께 쓰는 전체 면적 검사. 상태/금화/메시지는 바꾸지 않는다.</summary>
        public StagePlacementCheck Check(Vector2Int cell, StageOption option, bool barricade)
        {
            Vector2Int footprint = !barricade && option?.Trap?.Prefab is Trap template ? template.Footprint : Vector2Int.one;
            int cost = barricade ? (_session.Definition?.BarricadeCost ?? 0) : (option?.Cost ?? 0);
            if (_session.Phase != StagePhase.Preparing || _session.Board == null)
                return new StagePlacementCheck(cell, footprint, cost, "준비 단계에서만 배치할 수 있습니다");
            if (!barricade && option == null)
                return new StagePlacementCheck(cell, footprint, cost, "배치할 종류를 선택하세요");
            if (!barricade && !_session.Profile.IsUnlocked(option))
                return new StagePlacementCheck(cell, footprint, cost, "아직 해금되지 않은 종류입니다");
            if (!barricade && !_session.IsSelected(option))
                return new StagePlacementCheck(cell, footprint, cost, "먼저 편성에 추가하세요");
            bool available = barricade ? _prefab != null
                : option.Unit != null ? option.Unit.Prefab != null : option.Trap?.Prefab is Trap;
            if (!available)
                return new StagePlacementCheck(cell, footprint, cost, "이 종류는 현재 배치할 수 없습니다");
            for (int y = 0; y < footprint.y; y++)
            for (int x = 0; x < footprint.x; x++)
            {
                Vector2Int occupied = cell + new Vector2Int(x, y);
                if (!_session.Definition.IsFloor(occupied))
                    return new StagePlacementCheck(cell, footprint, cost, "배치 범위에 벽이나 맵 밖이 포함됩니다");
                if (!_session.Definition.IsBuildable(occupied))
                    return new StagePlacementCheck(cell, footprint, cost, "배치 범위에 설치 제한 칸이 포함됩니다");
                if (_session.Board.Reserved(occupied))
                    return new StagePlacementCheck(cell, footprint, cost, "코어·던전문·환경 장치는 비워두세요");
                if (_session.Board.BlockerAt(occupied, null) != null)
                    return new StagePlacementCheck(cell, footprint, cost, "유닛이나 바리케이드가 있는 칸입니다");
                foreach (Trap installed in _traps)
                    if (installed != null && !installed.IsDestroyed && installed.Covers(_session.Board.World(occupied), 0f))
                        return new StagePlacementCheck(cell, footprint, cost, "이미 함정이 있는 칸입니다");
            }
            // 덮는 칸 중 하나라도 이 함정을 막는 지형이면 놓을 수 없다. 부하·바리케이드는 지형을 가리지 않는다.
            string terrain = !barricade && option.Trap != null ? _session.Board.Terrain.BlockReason(cell, footprint, option.Trap) : string.Empty;
            if (terrain.Length > 0)
                return new StagePlacementCheck(cell, footprint, cost, terrain);
            if (_session.Gold < cost)
                return new StagePlacementCheck(cell, footprint, cost, "금화가 부족합니다");
            return new StagePlacementCheck(cell, footprint, cost);
        }

        public bool TryPlace(Vector2Int cell, StageOption option, bool barricade)
        {
            StagePlacementCheck check = Check(cell, option, barricade);
            if (!check.Allowed)
            {
                _session.Tell(check.Reason);
                return false;
            }
            Create(cell, option, barricade, check.Cost);
            _session.Pay(check.Cost);
            _session.RefreshUI();
            return true;
        }

        private Component Create(Vector2Int cell, StageOption option, bool barricade, int cost)
        {
            Vector2Int footprint = !barricade && option?.Trap?.Prefab is Trap template ? template.Footprint : Vector2Int.one;
            bool empowered = false;
            for (int y = 0; y < footprint.y; y++)
            for (int x = 0; x < footprint.x; x++)
                empowered |= _session.Definition.IsEmpowered(cell + new Vector2Int(x, y));
            if (barricade)
            {
                StageBarricade wall = Object.Instantiate(_prefab, _session.Board.World(cell), Quaternion.identity, _root);
                wall.Initialize(_session, cell, cost);
                _session.Board.Barricades.Add(cell, wall);
                return wall;
            }
            else if (option.Unit != null)
            {
                Unit unit = Object.Instantiate(option.Unit.Prefab, _session.Board.World(cell), Quaternion.identity, _root);
                unit.Initialize(option.Unit);
                StageActor actor = unit.gameObject.AddComponent<StageActor>();
                actor.Initialize(_session, cell, option, cost);
                unit.Combatant.SetAttackModifier(actor, 0f, 1f + _session.Profile.Level(option.Id) * _session.Catalog.DamagePerUpgrade);
                _session.Artifacts?.Apply(actor);
                _session.Merchant.Apply(actor);
                return actor;
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
                // 지형 강화는 적에게 주는 피해에만, 감속은 적을 맞혔을 때만 건다(2026-11-02 사용자 결정)
                trap.SetStageEnemyMultiplier(_session.Board.Terrain.TrapMultiplier(cell, footprint, option.Trap));
                if (_session.Catalog.IsSlowingTrap(option.Trap))
                    trap.SetStageMoveSlow(_session.Catalog.TrapSlowMultiplier, _session.Catalog.TrapSlowSeconds);
                _traps.Add(trap);
                _trapOptions.Add(trap, option);
                _trapPayments.Add(trap, cost);
                return trap;
            }
        }

        internal void Capture(List<StageSavedPlacement> placements)
        {
            foreach (StageBarricade wall in _session.Board.Barricades.Values)
            {
                if (wall == null || !wall.IsAlive)
                    continue;
                StageSavedPlacement saved = new() { kind = "barricade", cell = wall.Cell, paid = wall.PaidGold };
                wall.Capture(saved);
                placements.Add(saved);
            }
            foreach (StageActor actor in _session.Board.Actors)
            {
                if (actor == null || actor.IsEnemy)
                    continue;
                placements.Add(new StageSavedPlacement
                {
                    kind = "unit", optionId = actor.Option.Id, cell = actor.Home,
                    position = actor.transform.position, paid = actor.PaidGold,
                    upgrade = _session.Profile.Level(actor.Option.Id), condition = actor.Unit.CaptureConditionState(),
                    cooldown = actor.Skills != null ? actor.Skills.CaptureStageCooldown() : 0,
                    command = (int)actor.Command
                });
            }
            foreach (Trap trap in _traps)
            {
                if (trap == null || trap.IsDestroyed)
                    continue;
                StageSavedPlacement saved = new()
                {
                    kind = "trap", optionId = _trapOptions[trap].Id, cell = TrapAnchor(trap),
                    paid = _trapPayments[trap], health = trap.CurrentDurability,
                    upgrade = _session.Profile.Level(_trapOptions[trap].Id)
                };
                trap.CaptureStageTiming(out saved.cooldown, out saved.active, out saved.tick, out saved.tickDamage);
                placements.Add(saved);
            }
        }

        internal void Restore(StageSavedPlacement saved, StageOption option)
        {
            Component placed = Create(saved.cell, option, saved.kind == "barricade", saved.paid);
            if (placed is StageBarricade wall)
                wall.Restore(saved);
            else if (placed is StageActor actor)
            {
                actor.transform.position = saved.position;
                actor.Unit.ApplyConditionState(saved.condition);
                actor.Skills?.RestoreStageCooldown(saved.cooldown);
                actor.SetCommand((StageUnitCommand)saved.command);
            }
            else if (placed is Trap trap)
            {
                trap.RestoreDurability(saved.health);
                trap.RestoreStageTiming(saved.cooldown, saved.active, saved.tick, saved.tickDamage);
            }
        }

        /// <summary>철거 대상의 실제 차지 범위. 함정 어느 칸을 가리켜도 같은 범위를 반환한다.</summary>
        public StagePlacementCheck CheckDemolition(Vector2Int cell)
        {
            if (_session.Phase != StagePhase.Preparing || _session.Board == null)
                return new StagePlacementCheck(cell, Vector2Int.one, 0, "준비 단계에서만 철거할 수 있습니다");
            if (_session.Board.Barricades.TryGetValue(cell, out StageBarricade wall) && wall != null && wall.IsAlive)
                return new StagePlacementCheck(cell, Vector2Int.one, 0);
            foreach (StageActor actor in _session.Board.Actors)
                if (actor != null && !actor.IsEnemy && _session.Board.Cell(actor.transform.position) == cell)
                    return new StagePlacementCheck(cell, Vector2Int.one, 0);
            foreach (Trap trap in _traps)
                if (trap != null && !trap.IsDestroyed && trap.Covers(_session.Board.World(cell), 0f))
                    return new StagePlacementCheck(TrapAnchor(trap), trap.Footprint, 0);
            return new StagePlacementCheck(cell, Vector2Int.one, 0, "이 칸에는 철거할 대상이 없습니다");
        }

        private Vector2Int TrapAnchor(Trap trap)
        {
            Vector3 corner = trap.transform.position - new Vector3(trap.Footprint.x - 1, trap.Footprint.y - 1, 0)
                * (_session.Definition.CellSize * 0.5f);
            return _session.Board.Cell(corner);
        }

        public void Demolish(Vector2Int cell)
        {
            StagePlacementCheck check = CheckDemolition(cell);
            if (!check.Allowed)
            {
                _session.Tell(check.Reason);
                return;
            }
            if (_session.Board.Barricades.TryGetValue(cell, out StageBarricade wall) && wall != null && wall.IsAlive)
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
                if (trap == null || trap.IsDestroyed || !trap.Covers(_session.Board.World(cell), 0f))
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
