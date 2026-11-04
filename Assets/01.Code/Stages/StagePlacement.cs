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
        // 판 안 강화 단계(설치 = 1)와 강화를 뺀 피해 배율(영구 강화·강화 칸)
        private readonly Dictionary<Trap, int> _trapLevels = new();
        private readonly Dictionary<Trap, float> _trapBaseDamage = new();
        public IReadOnlyList<Trap> Traps => _traps;
        /// <summary>벽·바리케이드에 붙은 함정(2026-11-03).</summary>
        public StageWallTraps WallTraps { get; }
        public bool HasPlacements
        {
            get
            {
                if (_session.Board.Actors.Count > 0 || WallTraps.All.Count > 0)
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
            WallTraps = new StageWallTraps(session.Board, session.CheckTraps);
        }

        public bool HasPlaced(StageOption option)
        {
            foreach (StageActor actor in _session.Board.Actors)
                if (actor != null && actor.Option == option)
                    return true;
            foreach (Trap trap in _traps)
                if (trap != null && !trap.IsDestroyed && _trapOptions[trap] == option)
                    return true;
            foreach (StageWallTraps.Placed placed in WallTraps.All)
                if (placed.Option == option)
                    return true;
            return false;
        }

        /// <summary>미리보기와 실행이 함께 쓰는 전체 면적 검사. 상태/금화/메시지는 바꾸지 않는다.</summary>
        public StagePlacementCheck Check(Vector2Int cell, StageOption option, bool barricade)
        {
            Vector2Int footprint = !barricade && option?.Trap?.Prefab is Trap template ? template.Footprint : Vector2Int.one;
            int cost = barricade ? (_session.Definition?.BarricadeCost ?? 0) : (option?.Cost ?? 0);
            if (_session.Board == null || !_session.EditsTrapsNow)
                return new StagePlacementCheck(cell, footprint, cost, "준비 단계에서만 배치할 수 있습니다");
            bool fighting = _session.Phase == StagePhase.Fighting;
            if (fighting && (barricade || option == null || !option.IsAnyTrap))
                return new StagePlacementCheck(cell, footprint, cost, "전투 중에는 함정만 설치할 수 있습니다");
            if (!barricade && option == null)
                return new StagePlacementCheck(cell, footprint, cost, "배치할 종류를 선택하세요");
            if (!barricade && !_session.Profile.IsUnlocked(option))
                return new StagePlacementCheck(cell, footprint, cost, "아직 해금되지 않은 종류입니다");
            if (!barricade && !_session.IsSelected(option))
                return new StagePlacementCheck(cell, footprint, cost, "먼저 편성에 추가하세요");
            bool available = barricade ? _prefab != null
                : option.Unit != null ? option.Unit.Prefab != null : option.WallTrap != null || option.Trap?.Prefab is Trap;
            if (!available)
                return new StagePlacementCheck(cell, footprint, cost, "이 종류는 현재 배치할 수 없습니다");
            if (!barricade && option.WallTrap != null)
                return CheckWall(cell, cost);
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
                if (fighting && EnemyAt(occupied))
                    return new StagePlacementCheck(cell, footprint, cost, "적이 서 있는 칸입니다");
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

        private StagePlacementCheck CheckWall(Vector2Int cell, int cost)
        {
            if (!WallTraps.IsMount(cell, out _, out string reason))
                return new StagePlacementCheck(cell, Vector2Int.one, cost, reason);
            if (WallTraps.At(cell) != null)
                return new StagePlacementCheck(cell, Vector2Int.one, cost, "이미 벽 함정이 붙어 있습니다");
            if (_session.Gold < cost)
                return new StagePlacementCheck(cell, Vector2Int.one, cost, "금화가 부족합니다");
            return new StagePlacementCheck(cell, Vector2Int.one, cost);
        }

        private StageWallTraps.Placed CreateWall(Vector2Int cell, StageOption option, int cost, Vector2Int direction)
        {
            WallTraps.IsMount(cell, out bool onBarricade, out _);
            float upgrade = 1f + _session.Profile.Level(option.Id) * _session.Catalog.DamagePerUpgrade;
            return WallTraps.Add(option, cell, onBarricade, direction, cost, upgrade);
        }

        public bool TryPlace(Vector2Int cell, StageOption option, bool barricade)
        {
            StagePlacementCheck check = Check(cell, option, barricade);
            if (!check.Allowed)
            {
                _session.Tell(check.Reason);
                return false;
            }
            if (!barricade && option.WallTrap != null)
            {
                // 처음에는 아래부터 시계 방향으로 찾은 통로 쪽을 본다. 준비 단계에 R로 돌린다.
                CreateWall(cell, option, check.Cost, WallTraps.FacingFrom(cell, Vector2Int.down));
                _session.Pay(check.Cost);
                _session.RefreshUI();
                return true;
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
                FitTrapBody(trap, footprint);
                trap.BindStage(_session);
                // 덮은 칸 중 하나라도 강화 칸이면 영구 강화 배율 위에 곱한다. 판 안 강화 단계는 그 위에 곱한다.
                float upgrade = 1f + _session.Profile.Level(option.Id) * _session.Catalog.DamagePerUpgrade;
                float baseDamage = upgrade * (empowered ? _session.Definition.EmpoweredTrapMultiplier : 1f);
                trap.SetStageDamageMultiplier(baseDamage);
                _trapLevels.Add(trap, 1);
                _trapBaseDamage.Add(trap, baseDamage);
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
                    upgrade = _session.Profile.Level(_trapOptions[trap].Id), level = _trapLevels[trap]
                };
                trap.CaptureStageTiming(out saved.cooldown, out saved.active, out saved.tick, out saved.tickDamage);
                saved.pendingWear = trap.PendingWear;
                placements.Add(saved);
            }
            // 바리케이드에 붙은 벽 함정이 바리케이드 뒤에 복원되도록 맨 끝에 둔다
            foreach (StageWallTraps.Placed placed in WallTraps.All)
                placements.Add(new StageSavedPlacement
                {
                    kind = "wall", optionId = placed.Option.Id, cell = placed.Cell, paid = placed.Paid,
                    upgrade = _session.Profile.Level(placed.Option.Id), level = placed.Level, direction = placed.Direction
                });
        }

        internal void Restore(StageSavedPlacement saved, StageOption option)
        {
            if (saved.kind == "wall")
            {
                StageWallTraps.Placed wallTrap = CreateWall(saved.cell, option, saved.paid, saved.direction);
                wallTrap.Level = Mathf.Max(1, saved.level);
                return;
            }
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
                // 강화 단계가 없는 구 저장은 1단계다
                SetLevel(trap, Mathf.Max(1, saved.level));
                trap.RestoreDurability(saved.health);
                trap.RestoreStageTiming(saved.cooldown, saved.active, saved.tick, saved.tickDamage, saved.pendingWear);
            }
        }

        /// <summary>철거 대상의 실제 차지 범위. 함정 어느 칸을 가리켜도 같은 범위를 반환한다.</summary>
        public StagePlacementCheck CheckDemolition(Vector2Int cell)
        {
            if (_session.Board == null || !_session.EditsTrapsNow)
                return new StagePlacementCheck(cell, Vector2Int.one, 0, "준비 단계에서만 철거할 수 있습니다");
            bool fighting = _session.Phase == StagePhase.Fighting;
            // 바리케이드에 붙은 벽 함정이 먼저 떨어진다
            if (WallTraps.At(cell) != null)
                return new StagePlacementCheck(cell, Vector2Int.one, 0);
            foreach (Trap trap in _traps)
                if (trap != null && !trap.IsDestroyed && trap.Covers(_session.Board.World(cell), 0f))
                    return new StagePlacementCheck(TrapAnchor(trap), trap.Footprint, 0);
            if (fighting)
                return new StagePlacementCheck(cell, Vector2Int.one, 0, "전투 중에는 함정만 철거할 수 있습니다");
            if (_session.Board.Barricades.TryGetValue(cell, out StageBarricade wall) && wall != null && wall.IsAlive)
                return new StagePlacementCheck(cell, Vector2Int.one, 0);
            foreach (StageActor actor in _session.Board.Actors)
                if (actor != null && !actor.IsEnemy && _session.Board.Cell(actor.transform.position) == cell)
                    return new StagePlacementCheck(cell, Vector2Int.one, 0);
            return new StagePlacementCheck(cell, Vector2Int.one, 0, "이 칸에는 철거할 대상이 없습니다");
        }

        /// <summary>함정 철거 때 돌려줄 금화. 강화에 쓴 금화도 같은 비율이고, 전투 중에는 더 낮다(2026-11-03 사용자 결정).</summary>
        private int TrapRefund(int paid)
        {
            float ratio = _session.Definition.DemolishRefund
                * (_session.Phase == StagePhase.Fighting ? _session.Catalog.FightingRefundRatio : 1f);
            return Mathf.FloorToInt(Mathf.Max(0, paid) * ratio);
        }

        /// <summary>가리킨 칸의 바닥·벽 함정 강화 단계와 지금 철거 환급. 함정이 없으면 거짓.</summary>
        public bool TryDescribeTrap(Vector2Int cell, out int level, out int refund)
        {
            StageWallTraps.Placed wall = WallTraps.At(cell);
            Trap trap = wall == null ? TrapAt(cell) : null;
            level = wall != null ? wall.Level : trap != null ? _trapLevels[trap] : 0;
            refund = wall != null ? TrapRefund(wall.Paid) : trap != null ? TrapRefund(_trapPayments[trap]) : 0;
            return wall != null || trap != null;
        }

        /// <summary>그 칸에 놓인 바닥·벽 함정의 편성 후보. 없으면 null.</summary>
        public StageOption TrapOptionAt(Vector2Int cell)
        {
            StageWallTraps.Placed wall = WallTraps.At(cell);
            if (wall != null)
                return wall.Option;
            Trap trap = TrapAt(cell);
            return trap != null ? _trapOptions[trap] : null;
        }

        /// <summary>판 안 강화 가능 여부와 비용. 미리보기와 실행이 같은 검사를 쓴다.</summary>
        public StagePlacementCheck CheckTrapLevel(Vector2Int cell)
        {
            StageWallTraps.Placed wall = WallTraps.At(cell);
            Trap trap = wall == null ? TrapAt(cell) : null;
            if (wall == null && trap == null)
                return new StagePlacementCheck(cell, Vector2Int.one, 0, "강화할 함정이 없습니다");
            Vector2Int anchor = wall != null ? cell : TrapAnchor(trap);
            Vector2Int footprint = wall != null ? Vector2Int.one : trap.Footprint;
            if (!_session.Catalog.EditsTrapsInWaves || !_session.EditsTrapsNow)
                return new StagePlacementCheck(anchor, footprint, 0, "이 스테이지에서는 함정을 강화할 수 없습니다");
            int level = wall != null ? wall.Level : _trapLevels[trap];
            if (level >= _session.Catalog.TrapMaxLevel)
                return new StagePlacementCheck(anchor, footprint, 0, $"최고 단계(Lv{level})입니다");
            int cost = _session.Catalog.TrapLevelCost(wall != null ? wall.Option.Cost : _trapOptions[trap].Cost, level);
            if (_session.Gold < cost)
                return new StagePlacementCheck(anchor, footprint, cost, $"금화가 부족합니다 · Lv{level + 1} {cost}G");
            return new StagePlacementCheck(anchor, footprint, cost);
        }

        public bool TryLevelTrap(Vector2Int cell)
        {
            StagePlacementCheck check = CheckTrapLevel(cell);
            if (!check.Allowed)
            {
                _session.Tell(check.Reason);
                return false;
            }
            _session.Pay(check.Cost);
            StageWallTraps.Placed wall = WallTraps.At(cell);
            int level;
            string label;
            if (wall != null)
            {
                wall.Paid += check.Cost;
                level = ++wall.Level;
                label = wall.Option.Label;
            }
            else
            {
                Trap trap = TrapAt(cell);
                _trapPayments[trap] += check.Cost;
                SetLevel(trap, _trapLevels[trap] + 1);
                level = _trapLevels[trap];
                label = _trapOptions[trap].Label;
            }
            _session.Tell($"{label} Lv{level} · 피해 ×{_session.Catalog.TrapLevelMultiplier(level):0.##}");
            return true;
        }

        /// <summary>바리케이드가 부서지면 붙은 벽 함정도 떨어진다. 환급은 바리케이드 파괴와 같은 비율이다.</summary>
        internal void DetachWallTrap(Vector2Int cell)
        {
            StageWallTraps.Placed wall = WallTraps.At(cell);
            if (wall == null || !wall.OnBarricade)
                return;
            WallTraps.Remove(wall);
            _session.Refund(Mathf.FloorToInt(wall.Paid * Mathf.Clamp01(_session.Definition.DestroyRefund)));
            _session.Tell($"바리케이드가 부서져 {wall.Option.Label}도 떨어졌습니다");
        }

        private void SetLevel(Trap trap, int level)
        {
            _trapLevels[trap] = level;
            trap.SetStageDamageMultiplier(_trapBaseDamage[trap] * _session.Catalog.TrapLevelMultiplier(level));
        }

        /// <summary>
        /// 수정류처럼 칸보다 훨씬 작은 몸통 그림을 덮은 칸 높이의 약 85%까지 키운다(2026-11-04 가독성). 판정·연출 크기는 그대로다.
        /// </summary>
        private void FitTrapBody(Trap trap, Vector2Int footprint)
        {
            TrapBodyAnimation body = trap.GetComponentInChildren<TrapBodyAnimation>(true);
            SpriteRenderer sprite = body != null ? body.GetComponent<SpriteRenderer>() : null;
            if (sprite == null || sprite.sprite == null)
                return;
            float height = sprite.bounds.size.y;
            float target = _session.Definition.CellSize * footprint.y * TrapBodyFill;
            if (height <= 0f || height >= target)
                return;
            body.transform.localScale *= Mathf.Min(target / height, MaxTrapBodyGrowth);
        }

        private const float TrapBodyFill = 0.85f;
        private const float MaxTrapBodyGrowth = 2.2f;

        private bool EnemyAt(Vector2Int cell)
        {
            foreach (StageActor actor in _session.Board.Actors)
                if (actor != null && actor.IsEnemy && actor.IsAlive && _session.Board.Cell(actor.transform.position) == cell)
                    return true;
            return false;
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
            StageWallTraps.Placed wallTrap = WallTraps.At(cell);
            if (wallTrap != null)
            {
                _session.Refund(TrapRefund(wallTrap.Paid), false);
                WallTraps.Remove(wallTrap);
                return;
            }
            for (int i = _traps.Count - 1; i >= 0; i--)
            {
                Trap trap = _traps[i];
                if (trap == null || trap.IsDestroyed || !trap.Covers(_session.Board.World(cell), 0f))
                    continue;
                _session.Refund(TrapRefund(_trapPayments[trap]), false);
                _traps.RemoveAt(i);
                _trapOptions.Remove(trap);
                _trapPayments.Remove(trap);
                _trapLevels.Remove(trap);
                _trapBaseDamage.Remove(trap);
                Object.Destroy(trap.gameObject);
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
        }

        /// <summary>설치 면적의 어느 칸에서도 같은 함정을 돌려준다. 배치/발동 상태는 바꾸지 않는다.</summary>
        internal Trap TrapAt(Vector2Int cell)
        {
            foreach (Trap trap in _traps)
            {
                if (trap == null || trap.IsDestroyed)
                {
                    continue;
                }
                Vector2Int offset = cell - TrapAnchor(trap);
                if (offset.x >= 0 && offset.y >= 0 && offset.x < trap.Footprint.x && offset.y < trap.Footprint.y)
                {
                    return trap;
                }
            }
            return null;
        }

        public void CheckTraps(StageActor enemy)
        {
            Trap contact = null;
            Vector2Int cell = _session.Board.Cell(enemy.transform.position);
            foreach (Trap trap in _traps)
            {
                if (trap == null || trap.IsDestroyed)
                    continue;
                // 경로 재계산으로 중심을 건너뛰어도 설치 면적의 칸에 들어서면 밟은 것이다.
                Vector2Int offset = cell - TrapAnchor(trap);
                if (offset.x >= 0 && offset.y >= 0 && offset.x < trap.Footprint.x && offset.y < trap.Footprint.y)
                {
                    contact = trap;
                    break;
                }
            }
            if (enemy.EnterTrap(contact) && enemy.IsAlive
                && Random.value < Code.Enemies.MobilityRules.StepChance(contact.TriggerChance, enemy.Enemy.Data.Mobility))
                contact.TriggerBy(enemy.Enemy);
        }

    }
}
