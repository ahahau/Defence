using System;
using System.Collections.Generic;
using Code.Skills;

namespace Code.Run
{
    /// <summary>
    /// 던전 FTL 실시간 침입 방어 한 판의 규칙. Unity 오브젝트에 기대지 않아 EditMode에서 그대로 검사한다.
    /// 규칙 출처: Docs/Design/DungeonFtlDesign.md 8장, DungeonFtlImplementationDesign.md "실시간 1단계 임시 수치".
    /// 같은 층의 십자 방을 사방 복도로 잇고 북쪽에 별도 굴을 둔다. 화면은 <see cref="Tick"/>에 흐른 시간만 넘긴다(멈춤은 0).
    /// </summary>
    public class RunBattle
    {
        private const float Epsilon = 0.0005f;

        private readonly List<RunRoom> _rooms = new();
        private readonly List<RunDoor> _doors = new();
        private readonly List<RunLink> _links = new();
        private readonly List<RunCombatant> _combatants = new();
        private readonly List<RunCombatant> _scratch = new();
        private int _nextId;
        private int _queuedIntruders;
        private readonly RunNavigation _navigation;
        private readonly RunTelegraphRules _telegraphRules;
        private readonly bool[] _connected;
        private readonly int[] _queue;
        private readonly List<RunCombatant> _allies = new();
        private readonly List<RunCombatant> _opponents = new();
        private RunCombatant _sortingCaster;
        private RunCombatant _focusTarget;
        private readonly IComparer<RunCombatant> _opponentComparer;

        private sealed class OpponentComparer : IComparer<RunCombatant>
        {
            private readonly RunBattle _battle;

            public OpponentComparer(RunBattle battle) => _battle = battle;

            public int Compare(RunCombatant a, RunCombatant b) => _battle.CompareOpponents(a, b);
        }

        public RunBattle(RunBattleSettings settings, IReadOnlyList<RunRoomLayout> layout, int entryRoom = 0)
            : this(settings, layout, entryRoom, false)
        {
        }

        internal RunBattle(RunBattleSettings settings, IReadOnlyList<RunRoomLayout> layout, int entryRoom, bool raid)
        {
            IsRaid = raid;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (layout == null || layout.Count == 0)
                throw new ArgumentException("방 배치가 비었습니다.", nameof(layout));

            if (entryRoom < 0 || entryRoom >= layout.Count) throw new ArgumentOutOfRangeException(nameof(entryRoom));
            EntryRoom = entryRoom;
            for (int i = 0; i < layout.Count; i++)
            {
                RunRoomLayout cell = layout[i];
                if (cell.Floor < 0 || cell.Row < 0 || cell.Column < 0)
                {
                    throw new ArgumentException("층·행·열은 음수일 수 없습니다.", nameof(layout));
                }
                for (int j = 0; j < i; j++)
                {
                    if (layout[j].Floor == cell.Floor && layout[j].Row == cell.Row && layout[j].Column == cell.Column)
                        throw new ArgumentException("격자 칸이 겹칩니다.", nameof(layout));
                }
                _rooms.Add(new RunRoom(i, cell, settings.RoomLength, settings.RoomDurability, MaxManaFor(cell.Kind)));
                if (cell.Kind == RunRoomKind.Trap) _rooms[i].SetTrapCooldown(settings.TrapCooldownSeconds);
                FloorCount = Math.Max(FloorCount, cell.Floor + 1);
                if (cell.Kind == RunRoomKind.Heart) HeartRoom = i;
                if (cell.Kind == RunRoomKind.Vault) VaultRoom = i;
            }
            if (HeartRoom < 0)
                throw new ArgumentException("배치에 심장이 없습니다.", nameof(layout));
            for (int i = 0; i < layout.Count; i++)
            {
                for (int j = i + 1; j < layout.Count; j++)
                {
                    RunRoom a = _rooms[i];
                    RunRoom b = _rooms[j];
                    if (a.Floor != b.Floor) continue;
                    bool horizontal = a.Row == b.Row && Math.Abs(a.Column - b.Column) == 1;
                    bool vertical = a.Column == b.Column && Math.Abs(a.Row - b.Row) == 1;
                    if (!horizontal && !vertical) continue;
                    if (raid && vertical && a.Column == 1) continue;
                    int from = horizontal ? (a.Column < b.Column ? i : j) : (a.Row < b.Row ? i : j);
                    int to = from == i ? j : i;
                    int index = _links.Count;
                    float length = settings.CorridorLength;
                    _links.Add(new RunLink(index, from, to, vertical, length,
                        a.Length, 0f));
                    // 문 제어실은 심장으로 이어진 복도의 문을 잠근다(2026-11-10 사용자 결정, 이전에는 함정 방 쪽 복도).
                    // 함정 방 쪽 문은 적을 문 앞에 모았다가 한꺼번에 들여보내 켜는 쪽이 손해였다.
                    int controller = a.Kind == RunRoomKind.DoorControl && b.Kind == RunRoomKind.Heart ? i
                        : b.Kind == RunRoomKind.DoorControl && a.Kind == RunRoomKind.Heart ? j : -1;
                    _doors.Add(new RunDoor(index, RunPosition.OnLink(index, length * 0.5f), settings.DoorDurability, controller));
                }
            }

            _navigation = new RunNavigation(this);
            _telegraphRules = new RunTelegraphRules(this, _navigation);
            _connected = new bool[_rooms.Count];
            _queue = new int[_rooms.Count];
            _opponentComparer = new OpponentComparer(this);

            HeartHealth = settings.HeartHealth;
            VaultGold = settings.VaultGold;
            ManaCapacity = settings.StartingManaCapacity;
        }

        public RunBattleSettings Settings { get; }
        public IReadOnlyList<RunRoom> Rooms => _rooms;
        public IReadOnlyList<RunDoor> Doors => _doors;
        public IReadOnlyList<RunLink> Links => _links;
        public IReadOnlyList<RunCombatant> Combatants => _combatants;
        /// <summary>현재 진행 중인 예고. 목록과 예고 상태를 재사용하므로 화면은 매번 현재 값을 읽는다.</summary>
        public IReadOnlyList<RunTelegraph> Telegraphs => _telegraphRules.Active;
        public int HeartRoom { get; } = -1;
        public int VaultRoom { get; } = -1;
        public int EntryRoom { get; }
        public RunPosition EntrancePosition => RunPosition.AtEntrance(0f);
        public int FloorCount { get; }
        public int HeartHealth { get; private set; }
        public int VaultGold { get; private set; }

        internal void SetInitialResources(int heartHealth, int gold)
        {
            HeartHealth = Math.Min(Settings.HeartHealth, heartHealth);
            VaultGold = gold;
        }
        public int ManaCapacity { get; private set; }

        /// <summary>전투가 시작된 뒤 흐른 초.</summary>
        public float Elapsed { get; private set; }

        public RunBattleOutcome Outcome { get; private set; }
        internal void RestoreDemoOutcome(RunBattleOutcome outcome) => Outcome = outcome;
        public bool IsRaid { get; }
        public bool IsRaidRetreating { get; private set; }
        internal RunCombatant RaidCommander { get; set; }

        public void RequestRaidRetreat()
        {
            if (!IsRaid || Outcome != RunBattleOutcome.Ongoing) return;
            IsRaidRetreating = true;
        }

        public int ManaInUse
        {
            get
            {
                int total = 0;
                foreach (RunRoom room in _rooms)
                    total += room.Mana;
                return total;
            }
        }

        /// <summary>방에 인물을 바로 세운다. 부하는 지킬 자리에, 적은 방 가운데에 선다.</summary>
        public RunCombatant AddCombatant(RunCombatantSO definition, int roomIndex, RunAffinity? affinity = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (roomIndex < 0 || roomIndex >= _rooms.Count) throw new ArgumentOutOfRangeException(nameof(roomIndex));
            RunPosition position = definition.Side == RunSide.Minion ? PostPosition(definition, roomIndex) : CenterOf(roomIndex);
            RunCombatant combatant = new(_nextId++, definition, position, roomIndex, 0f, affinity);
            _combatants.Add(combatant);
            _telegraphRules.Register(combatant);
            _scratch.Capacity = _allies.Capacity = _opponents.Capacity = _combatants.Count;
            return combatant;
        }

        /// <summary>적을 입구 줄에 세운다. 넣은 순서대로 입장 간격마다 한 명씩 들어온다.</summary>
        public RunCombatant AddIntruderAtEntrance(RunCombatantSO definition, RunAffinity? affinity = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            float entryTime = _queuedIntruders++ * Settings.EntryIntervalSeconds;
            RunCombatant combatant = new(_nextId++, definition, EntrancePosition, EntryRoom, entryTime, affinity);
            _combatants.Add(combatant);
            _telegraphRules.Register(combatant);
            _scratch.Capacity = _allies.Capacity = _opponents.Capacity = _combatants.Count;
            return combatant;
        }

        // ── 마력 ──

        /// <summary>방에 넣을 마력을 정한다. 방 한도나 전체 용량을 넘으면 거절한다.</summary>
        public bool SetMana(int roomIndex, int amount)
        {
            if (roomIndex < 0 || roomIndex >= _rooms.Count) return false;
            RunRoom room = _rooms[roomIndex];
            if (amount < 0 || amount > room.MaxMana) return false;
            if (ManaInUse - room.Mana + amount > ManaCapacity) return false;
            room.SetMana(amount);
            _telegraphRules.CheckDoors();
            return true;
        }

        /// <summary>심장에서 이 방까지 고장 난 방 없이 이어져 있는가.</summary>
        public bool IsConnected(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= _rooms.Count) return false;
            _navigation.Connected(_connected, _queue);
            return _connected[roomIndex];
        }

        /// <summary>마력이 들어가 있고, 고장 나지 않았고, 심장과 이어져 있다.</summary>
        public bool IsPowered(int roomIndex)
        {
            RunRoom room = _rooms[roomIndex];
            return room.Mana > 0 && !room.IsBroken && IsConnected(roomIndex);
        }

        public bool IsDoorLocked(int doorIndex)
        {
            RunDoor door = _doors[doorIndex];
            return door.ControllerRoom >= 0 && !door.IsBrokenOpen && IsPowered(door.ControllerRoom);
        }

        // ── 명령 ──

        /// <summary>부하에게 목적지 방을 준다. 싸우던 중이어도 바로 빠져 걸어간다(사용자 답).</summary>
        public bool OrderMove(RunCombatant minion, int roomIndex)
        {
            if (minion == null || minion.Side != RunSide.Minion || !minion.IsActive) return false;
            if (roomIndex < 0 || roomIndex >= _rooms.Count) return false;
            if (_rooms[roomIndex].Floor != _rooms[minion.HomeRoom].Floor) return false;
            if (float.IsPositiveInfinity(Distance(minion.Position, CenterOf(roomIndex)))) return false;
            if (minion.IsRetreating) minion.EndRetreat(false);
            // 체력이 낮아도 플레이어 명령이 우선한다. 기준 이상으로 회복할 때까지 자동으로 물러나지 않는다(2026-11-10 사용자 결정).
            if (minion.Health < minion.MaxHealth * Settings.RetreatHealthRatio) minion.SetRetreatSuppressed(true);
            minion.SetMoveTarget(roomIndex);
            minion.BeginSlipBack();
            return true;
        }

        // ── 진행 ──

        /// <summary>시간을 흘린다. 배속이 달라도 결과가 같게 정해진 걸음으로 나눠 진행한다.</summary>
        public void Tick(float deltaTime)
        {
            float left = deltaTime;
            while (left > Epsilon && Outcome == RunBattleOutcome.Ongoing)
            {
                float step = Math.Min(left, Settings.SimulationStep);
                Step(step);
                left -= step;
            }
        }

        /// <summary>서 있는 방. 복도·입구 굴 위라면 -1이다.</summary>
        public int RoomOf(RunCombatant combatant) => combatant == null ? -1 : combatant.Position.Room;
        public RunPosition CenterOf(int room) => RunPosition.InRoom(room, _rooms[room].Center);
        public float Distance(RunPosition from, RunPosition to) => _navigation.Distance(from, to);
        public float RouteCost(RunPosition from, int room) => _navigation.Distance(from, CenterOf(room), true);

        /// <summary>이동 명령 중이 아닌 부하가 실제로 지키는 방인가.</summary>
        public bool IsDefended(int room)
        {
            foreach (RunCombatant minion in _combatants)
            {
                if (minion.IsOnField && minion.Side == RunSide.Minion && !minion.HasMoveOrder && RoomOf(minion) == room) return true;
            }
            return false;
        }

        /// <summary>도적은 금고, 공병은 가장 가까운 켜진 방, 나머지는 심장을 노린다.</summary>
        public int GoalRoomOf(RunCombatant intruder)
        {
            if (intruder == null || intruder.Side != RunSide.Intruder) return -1;
            if (intruder.IsEscaping) return -1;
            if (intruder.Definition.IntruderRole == RunIntruderRole.Sapper)
            {
                int nearest = -1;
                float best = float.PositiveInfinity;
                foreach (RunRoom room in _rooms)
                {
                    if (!IsPowered(room.Index)) continue;
                    float distance = Distance(intruder.Position, CenterOf(room.Index));
                    if (distance < best - Epsilon)
                    {
                        nearest = room.Index;
                        best = distance;
                    }
                }
                if (nearest >= 0) return nearest;
            }
            return (intruder.Definition.IntruderRole == RunIntruderRole.Thief || intruder.Definition.Goal == RunGoal.Vault)
                && VaultRoom >= 0 ? VaultRoom : HeartRoom;
        }

        public float AttackInterval(RunCombatant combatant) =>
            Settings.AttackIntervalFactor / Math.Max(1, combatant.Definition.Speed);

        public float RangeOf(RunCombatant combatant) =>
            combatant.Definition.IsRanged ? Settings.RangedRange : Settings.MeleeRange;

        /// <summary>모든 부하의 기준 자리는 방 중앙이다. 화면 겹침은 별도로 보정한다.</summary>
        public RunPosition PostPosition(RunCombatantSO definition, int roomIndex) => CenterOf(roomIndex);

        /// <summary>다음에 이 함정 방이 줄 피해. 꺼져 있으면 0. 화면 표시용.</summary>
        public int TrapDamage(int roomIndex)
        {
            RunRoom room = _rooms[roomIndex];
            if (room.Kind != RunRoomKind.Trap || !IsPowered(roomIndex)) return 0;
            return Boost(room, Settings.TrapDamagePerMana * room.Mana + Settings.TrapDamageBonus);
        }

        // ── 대상 ──

        /// <summary>지원 거리 안 아군(자신 포함). 반환 목록은 다음 아군 조회 때 재사용한다.</summary>
        public List<RunCombatant> AlliesNear(RunCombatant caster)
        {
            List<RunCombatant> allies = _allies;
            allies.Clear();
            foreach (RunCombatant other in _combatants)
                if (other.IsOnField && other.Side == caster.Side
                    && Distance(other.Position, caster.Position) <= Settings.SupportRadius + Epsilon)
                    allies.Add(other);
            return allies;
        }

        public RunCombatant LowestHealthAlly(RunCombatant caster)
        {
            RunCombatant best = null;
            foreach (RunCombatant ally in AlliesNear(caster))
                if (best == null || ally.Health < best.Health || (ally.Health == best.Health && ally.Id < best.Id))
                    best = ally;
            return best;
        }

        /// <summary>공격 거리 안 상대를 가까운 순으로 고른다. 반환 목록은 다음 상대 조회 때 재사용한다.</summary>
        public List<RunCombatant> PickOpponents(RunCombatant caster, RunTargetRule rule, int maxTargets)
        {
            List<RunCombatant> opponents = _opponents;
            opponents.Clear();
            float range = RangeOf(caster);
            foreach (RunCombatant other in _combatants)
                if (other.IsOnField && other.Side != caster.Side
                    && Distance(other.Position, caster.Position) <= range + Epsilon)
                    opponents.Add(other);
            _sortingCaster = caster;
            opponents.Sort(_opponentComparer);
            if (rule != RunTargetRule.AllInRange && opponents.Count > maxTargets)
                opponents.RemoveRange(maxTargets, opponents.Count - maxTargets);
            return opponents;
        }

        /// <summary>침입자 처치 시 금화를 더하고 훔친 금화를 되찾는다. 전투 중 마력 용량은 유지한다.</summary>
        public void DealDamage(RunCombatant source, RunCombatant target, int amount, RunAffinity? affinity = null)
        {
            if (target == null || !target.IsActive) return;
            if (source != null)
            {
                target.NoteAttacker(source);
                amount = RunAffinityRules.Damage(amount, affinity ?? source.Affinity, target.Affinity);
            }
            int actualDamage = target.TakeDamage(amount);
            _telegraphRules.OnDamage(target, actualDamage);
            if (!IsRaid && target.IsDown && target.Side == RunSide.Intruder)
            {
                VaultGold += Settings.GoldPerKill + target.DropGold();
            }
        }

        // ── 한 걸음 ──

        /// <summary>연결된 예고 효과를 시작한다. 효과의 Apply와 자동 행동이 함께 사용하는 경로다.</summary>
        public bool TryBeginTelegraph(RunCombatant caster, RunTelegraphSkillEffectSO effect) => _telegraphRules.TryBegin(caster, effect);

        private void Step(float dt)
        {
            Elapsed += dt;
            foreach (RunCombatant combatant in _combatants)
                if (!combatant.HasEntered && combatant.IsActive && Elapsed + Epsilon >= combatant.EntryTime)
                    combatant.Enter();

            UpdateRooms(dt);
            _telegraphRules.Tick(dt);
            if (UpdateOutcome()) return;

            foreach (RunCombatant combatant in _combatants)
            {
                if (!combatant.IsOnField) continue;
                combatant.TickTimers(dt);
                if (combatant.Side == RunSide.Minion) UpdateMinion(combatant, dt);
                else UpdateIntruder(combatant, dt);
                _telegraphRules.CheckDoors();
                if (UpdateOutcome()) return;
            }

            if (Elapsed + Epsilon >= Settings.TimeLimitSeconds)
            {
                // 제한 시간을 버티면 남은 적은 물러난다. 들고 있던 금화는 잃는다.
                foreach (RunCombatant combatant in _combatants)
                    if (combatant.Side == RunSide.Intruder && combatant.IsActive)
                        combatant.Leave();
                UpdateOutcome();
            }
        }

        private void UpdateRooms(float dt)
        {
            foreach (RunRoom room in _rooms)
            {
                switch (room.Kind)
                {
                    case RunRoomKind.Trap:
                        UpdateTrap(room, dt);
                        break;
                    case RunRoomKind.HealingSpring:
                        UpdateSpring(room, dt);
                        break;
                }
            }
        }

        private void UpdateTrap(RunRoom room, float dt)
        {
            if (room.TrapCooldown > 0f && IsPowered(room.Index)) room.SetTrapCooldown(room.TrapCooldown - dt);
        }

        /// <summary>수동 발동 조건. 전력·충전·대상을 발동 순간 다시 확인한다.</summary>
        public bool CanActivateTrap(int roomIndex)
        {
            if (Outcome != RunBattleOutcome.Ongoing || roomIndex < 0 || roomIndex >= _rooms.Count) return false;
            RunRoom room = _rooms[roomIndex];
            if (room.Kind != RunRoomKind.Trap || !IsPowered(roomIndex) || room.TrapCooldown > Epsilon) return false;
            for (int i = 0; i < _combatants.Count; i++)
            {
                RunCombatant actor = _combatants[i];
                if (actor.IsOnField && actor.Side == RunSide.Intruder && IsInTrapReach(actor, roomIndex)) return true;
            }
            return false;
        }

        private bool IsInTrapReach(RunCombatant actor, int roomIndex)
        {
            if (RoomOf(actor) == roomIndex) return true;
            if (!Settings.TrapReachesLinks || actor.Position.IsInRoom || actor.Position.Link < 0) return false;
            RunLink link = _links[actor.Position.Link];
            return link.From == roomIndex || link.To == roomIndex;
        }

        /// <summary>함정 방의 적 전원을 공격한다. 실패하면 충전을 소비하지 않는다.</summary>
        public bool TryActivateTrap(int roomIndex)
        {
            if (!CanActivateTrap(roomIndex)) return false;
            RunRoom room = _rooms[roomIndex];

            // Boost도 같은 임시 목록을 쓰므로 침입자 목록보다 먼저 계산한다.
            int damage = TrapDamage(roomIndex);

            _scratch.Clear();
            foreach (RunCombatant actor in _combatants)
                if (actor.IsOnField && actor.Side == RunSide.Intruder && IsInTrapReach(actor, room.Index))
                    _scratch.Add(actor);

            foreach (RunCombatant intruder in _scratch)
                DealDamage(null, intruder, RunAffinityRules.Damage(damage, room.TrapAffinity, intruder.Affinity));
            room.SetTrapCooldown(Settings.TrapCooldownSeconds);
            return true;
        }

        private void UpdateSpring(RunRoom room, float dt)
        {
            if (!IsPowered(room.Index))
            {
                room.SpringTimer = 0f;
                return;
            }
            room.SpringTimer += dt;
            while (room.SpringTimer + Epsilon >= 1f)
            {
                room.SpringTimer -= 1f;
                int heal = Boost(room, Settings.SpringHealPerSecond * room.Mana);
                foreach (RunCombatant minion in CombatantsIn(room.Index, RunSide.Minion))
                    minion.Heal(heal);
            }
        }

        private void UpdateMinion(RunCombatant minion, float dt)
        {
            if (IsRaidRetreating)
            {
                Walk(minion, EntrancePosition, dt, false, passOpponents: true);
                if (Distance(minion.Position, EntrancePosition) <= Epsilon) minion.Leave();
                return;
            }
            UpdateRetreat(minion);
            if (minion.HasMoveOrder)
            {
                RunPosition destination = PostPosition(minion.Definition, minion.MoveTarget);
                // 끊을 수 있는 예고의 시전자가 목적지 방에 있으면 길의 적을 지나쳐 그 시전자부터 친다(2026-11-09 사용자 결정).
                RunCombatant caster = InterruptibleCasterIn(minion.MoveTarget, minion.Position);
                if (caster != null && Distance(minion.Position, caster.Position) <= RangeOf(minion) + Epsilon)
                {
                    // 스킬도 가까운 적 대신 이 시전자를 먼저 맞힌다.
                    _focusTarget = caster;
                    Fight(minion, caster);
                    _focusTarget = null;
                    return;
                }
                RunCombatant blocker = OpponentOnRoute(minion, destination);
                if (caster != null && blocker != null) minion.MarkSlippedPast();
                // 시전자에게 가는 길과, 지나친 뒤 새 명령으로 돌아오는 길에서는 적을 지나친다(2026-11-09 사용자 결정).
                bool slip = caster != null || minion.IsSlippingBack;
                if (!slip && blocker != null)
                {
                    Fight(minion, blocker);
                    return;
                }
                Walk(minion, destination, dt, false, 0f, slip);
                if (Distance(minion.Position, destination) <= Epsilon)
                {
                    minion.MoveTo(destination);
                    minion.Arrive();
                    minion.Face(-1);
                }
                return;
            }
            RunCombatant foe = NearestOpponent(minion);
            if (foe != null)
            {
                Fight(minion, foe);
                return;
            }
            if (!minion.Definition.IsRanged)
            {
                foe = ApproachTarget(minion);
                if (foe != null)
                {
                    Walk(minion, foe.Position, dt, false, Settings.MeleeRange);
                    minion.SetActivity(RunActionKind.Move, foe);
                    return;
                }
            }
            RunPosition post = PostPosition(minion.Definition, minion.HomeRoom);
            if (Distance(minion.Position, post) > Epsilon)
            {
                Walk(minion, post, dt, false);
                return;
            }
            int room = RoomOf(minion);
            if (room >= 0 && _rooms[room].IsDamaged && minion.Definition.RepairPerSecond > 0f
                && !(Settings.BlocksRepairOfBrokenRooms && _rooms[room].IsBroken))
            {
                _rooms[room].RepairBy(minion.Definition.RepairPerSecond * dt);
                minion.SetActivity(RunActionKind.Repair);
                return;
            }
            minion.SetActivity(RunActionKind.Idle);
        }

        private RunCombatant ApproachTarget(RunCombatant minion)
        {
            RunCombatant best = null;
            float distance = float.PositiveInfinity;
            foreach (RunCombatant other in _combatants)
            {
                if (!other.IsOnField || other.Side == minion.Side) continue;
                bool sameRoom = RoomOf(other) == minion.HomeRoom;
                bool attacking = other.Target == minion && (other.Activity is RunActionKind.Attack or RunActionKind.Skill);
                bool lastAttacker = minion.LastAttacker == other && Distance(minion.Position, other.Position) <= RangeOf(other) + Epsilon;
                if (!sameRoom && !attacking && !lastAttacker) continue;
                float candidate = Distance(minion.Position, other.Position);
                if (candidate < distance - Epsilon)
                {
                    best = other;
                    distance = candidate;
                }
            }
            return best;
        }

        private void UpdateRetreat(RunCombatant minion)
        {
            if (minion.IsRetreating)
            {
                if (!minion.HasMoveOrder && minion.Health >= minion.MaxHealth) minion.EndRetreat(true);
                return;
            }
            if (minion.Health >= minion.MaxHealth * Settings.RetreatHealthRatio)
            {
                minion.SetRetreatSuppressed(false);
                return;
            }
            if (minion.IsRetreatSuppressed) return;
            int best = -1;
            float distance = float.PositiveInfinity;
            foreach (RunRoom room in _rooms)
            {
                if (room.Kind != RunRoomKind.HealingSpring || !IsPowered(room.Index)) continue;
                if (room.Floor != _rooms[minion.HomeRoom].Floor) continue;
                if (minion.HomeRoom == room.Index && !minion.HasMoveOrder) continue;
                RunPosition target = PostPosition(minion.Definition, room.Index);
                float candidate = Distance(minion.Position, target);
                if (candidate >= distance || IntruderBetween(minion.Position, target)) continue;
                best = room.Index;
                distance = candidate;
            }
            if (best >= 0) minion.BeginRetreat(best);
        }

        private void UpdateIntruder(RunCombatant intruder, float dt)
        {
            if (IsRaid)
            {
                UpdateRaidDefender(intruder, dt);
                return;
            }
            if (TryBeginTelegraph(intruder, intruder.Telegraph.Effect)) return;
            if (intruder.Telegraph.IsActive)
            {
                RunCombatant opponent = NearestOpponent(intruder);
                if (opponent != null) Fight(intruder, opponent);
                else intruder.SetActivity(RunActionKind.Skill, intruder.Telegraph.Target);
                return;
            }
            if (_telegraphRules.ShouldApproachBlast(intruder, out int blastRoom))
            {
                RunPosition center = CenterOf(blastRoom);
                if (Distance(intruder.Position, center) > Epsilon) Walk(intruder, center, dt, false, 0f, true);
                else intruder.SetActivity(RunActionKind.Idle);
                return;
            }
            int room = RoomOf(intruder);
            int goalRoom = GoalRoomOf(intruder);
            RunCombatant foe = NearestOpponent(intruder);
            if (foe != null)
            {
                Fight(intruder, foe);
                return;
            }
            if (!intruder.IsEscaping && intruder.Definition.IntruderRole == RunIntruderRole.Sapper && room == goalRoom && IsPowered(room))
            {
                intruder.SetActivity(RunActionKind.AttackRoom);
                if (intruder.AttackTimer <= Epsilon)
                {
                    _rooms[room].Damage(intruder.Definition.Attack * Settings.SapperRoomDamageMultiplier);
                    intruder.Strike(AttackInterval(intruder));
                }
                return;
            }
            foe = DefenderInRoom(room, intruder);
            if (foe != null)
            {
                Walk(intruder, foe.Position, dt, false, RangeOf(intruder));
                return;
            }
            RunPosition goal = intruder.IsEscaping ? EntrancePosition : CenterOf(goalRoom);
            if (Distance(intruder.Position, goal) <= Epsilon)
            {
                if (intruder.IsEscaping)
                {
                    intruder.SetActivity(RunActionKind.Escape);
                    intruder.Leave();
                }
                else if (goalRoom == HeartRoom)
                {
                    HeartHealth = Math.Max(0, HeartHealth - intruder.Definition.Attack);
                    intruder.SetActivity(RunActionKind.HitHeart);
                    intruder.Leave();
                }
                else if (goalRoom == VaultRoom) Steal(intruder, dt);
                return;
            }
            Walk(intruder, goal, dt, true);
            if (intruder.IsEscaping && intruder.Activity == RunActionKind.Move) intruder.SetActivity(RunActionKind.Escape);
        }

        private void Steal(RunCombatant thief, float dt)
        {
            thief.SetActivity(RunActionKind.Steal);
            thief.StealProgress += Settings.ThiefStealPerSecond * dt;
            int amount = (int)Math.Floor(thief.StealProgress + Epsilon);
            if (amount > 0)
            {
                thief.StealProgress -= amount;
                int space = Settings.ThiefCarryLimit - thief.CarriedGold;
                int taken = Math.Max(0, Math.Min(amount, Math.Min(space, VaultGold)));
                VaultGold -= taken;
                thief.Carry(taken);
            }
            if (thief.CarriedGold >= Settings.ThiefCarryLimit || VaultGold <= 0)
                thief.StartEscape();
        }

        private void UpdateRaidDefender(RunCombatant defender, float dt)
        {
            RunCombatant foe = NearestOpponent(defender, frontOnly: defender != RaidCommander);
            if (foe != null)
            {
                if (TryBeginTelegraph(defender, defender.Telegraph.Effect)) return;
                Fight(defender, foe);
                return;
            }
            if (defender.Telegraph.IsActive) return;
            RunPosition post = CenterOf(defender.HomeRoom);
            if (Distance(defender.Position, post) > Epsilon) Walk(defender, post, dt, false);
            else defender.SetActivity(RunActionKind.Idle);
        }

        private void Walk(RunCombatant actor, RunPosition target, float dt, bool tactical, float stopRange = 0f, bool passOpponents = false)
        {
            float remaining = Math.Min(actor.Definition.MoveSpeed * dt, Math.Max(0f, Distance(actor.Position, target) - stopRange));
            actor.SetActivity(RunActionKind.Move);
            for (int iteration = 0; remaining > Epsilon && iteration < _rooms.Count * 4 + _links.Count * 2; iteration++)
            {
                if (!_navigation.NextStep(actor.Position, target, tactical, out RunPosition start, out RunPosition end)) break;
                int direction = Math.Sign(end.Offset - start.Offset);
                float amount = Math.Min(remaining, Math.Abs(end.Offset - start.Offset));
                if (direction == 0) break;
                if (actor.Side == RunSide.Intruder && !start.IsInRoom && !start.IsAtEntrance && IsDoorLocked(start.Link))
                {
                    RunDoor door = _doors[start.Link];
                    float stop = door.Position.Offset - direction * Settings.DoorStopGap;
                    float gap = (stop - start.Offset) * direction;
                    if ((door.Position.Offset - start.Offset) * direction >= -Epsilon && gap <= amount + Epsilon)
                    {
                        if (gap > Epsilon)
                        {
                            actor.MoveTo(RunPosition.OnLink(start.Link, stop));
                            return;
                        }
                        actor.SetActivity(RunActionKind.AttackDoor);
                        if (actor.AttackTimer <= Epsilon)
                        {
                            door.Damage(actor.Definition.Attack);
                            actor.Strike(AttackInterval(actor));
                        }
                        return;
                    }
                }
                // 한 걸음 안에 상대를 통과하지 않도록 접촉 위치를 자른다.
                if (!passOpponents)
                {
                    foreach (RunCombatant other in _combatants)
                    {
                        if (!other.IsOnField || other.Side == actor.Side || !OffsetOnSegment(start, other.Position, out float otherOffset)) continue;
                        if (CanPassEngagedDefender(actor, other)) continue;
                        float gap = (otherOffset - start.Offset) * direction;
                        if (gap > Epsilon) amount = Math.Min(amount, Math.Max(0f, gap - RangeOf(actor)));
                    }
                }
                RunPosition next = start.IsInRoom
                    ? RunPosition.InRoom(start.Room, start.Axis, start.Offset + direction * amount)
                    : start.IsAtEntrance ? RunPosition.AtEntrance(start.Offset + direction * amount)
                    : RunPosition.OnLink(start.Link, start.Offset + direction * amount);
                actor.Face(direction);
                actor.MoveTo(next);
                if (next.IsAtEntrance)
                {
                    if (next.Offset >= Settings.EntranceLength - Epsilon)
                        actor.MoveTo(RunPosition.InRoom(EntryRoom, RunAxis.Vertical, 0f));
                }
                else if (!next.IsInRoom)
                {
                    RunLink link = _links[next.Link];
                    if (next.Offset <= Epsilon) actor.MoveTo(RunPosition.InRoom(link.From, link.Axis, link.FromOffset));
                    else if (next.Offset >= link.Length - Epsilon) actor.MoveTo(RunPosition.InRoom(link.To, link.Axis, link.ToOffset));
                }
                remaining -= amount;
                if (amount <= Epsilon) break;
            }
        }

        private static bool SameSegment(RunPosition a, RunPosition b) =>
            a.IsInRoom == b.IsInRoom && (a.IsInRoom ? a.Room == b.Room && a.Axis == b.Axis : a.Link == b.Link);

        private bool OffsetOnSegment(RunPosition segment, RunPosition position, out float offset)
        {
            offset = position.Offset;
            if (SameSegment(segment, position)) return true;
            if (segment.IsInRoom && position.IsInRoom && segment.Room == position.Room
                && Math.Abs(position.Offset - _rooms[position.Room].Center) <= Epsilon)
            {
                offset = _rooms[position.Room].Center;
                return true;
            }
            return false;
        }

        /// <summary>공격 간격이 돌면 스킬(준비됐으면)이나 평타를 쓴다. 스킬 쿨은 턴제 쿨 × 공격 간격이다.</summary>
        private void Fight(RunCombatant actor, RunCombatant foe)
        {
            if (SameSegment(actor.Position, foe.Position)) actor.Face(Math.Sign(foe.Position.Offset - actor.Position.Offset));
            if (actor.AttackTimer > Epsilon)
            {
                actor.SetActivity(RunActionKind.Attack, foe);
                return;
            }

            float interval = AttackInterval(actor);
            SkillDataSO skill = actor.Definition.Skill;
            if (actor.Telegraph.Effect == null && skill != null && actor.SkillCooldownLeft <= Epsilon
                && skill.TurnEffects != null && skill.TurnEffects.Length > 0)
            {
                skill.ExecuteTurn(this, actor);
                actor.OnSkillUsed(skill.CooldownTurns * interval);
                actor.SetActivity(RunActionKind.Skill, foe);
            }
            else
            {
                DealDamage(actor, foe, actor.Definition.Attack);
                actor.SetActivity(RunActionKind.Attack, foe);
            }
            actor.Strike(interval);
        }

        // ── 보조 ──

        private int CompareOpponents(RunCombatant a, RunCombatant b)
        {
            if (!ReferenceEquals(a, b) && (ReferenceEquals(a, _focusTarget) || ReferenceEquals(b, _focusTarget)))
                return ReferenceEquals(a, _focusTarget) ? -1 : 1;
            int distance = Distance(a.Position, _sortingCaster.Position).CompareTo(Distance(b.Position, _sortingCaster.Position));
            return distance != 0 ? distance : a.Id.CompareTo(b.Id);
        }

        private RunCombatant NearestOpponent(RunCombatant actor, bool frontOnly = false)
        {
            RunCombatant best = null;
            float nearest = RangeOf(actor) + Epsilon;
            foreach (RunCombatant other in _combatants)
            {
                if (!other.IsOnField || other.Side == actor.Side) continue;
                if (frontOnly && !IsOnRaidFront(other.Position)) continue;
                if (CanPassEngagedDefender(actor, other)) continue;
                float distance = Distance(actor.Position, other.Position);
                if (distance > nearest) continue;
                if (best != null && Math.Abs(distance - nearest) <= Epsilon && other.Id > best.Id) continue;
                best = other;
                nearest = distance;
            }
            return best;
        }

        private bool CanPassEngagedDefender(RunCombatant actor, RunCombatant defender)
        {
            return actor.Definition.IntruderRole == RunIntruderRole.Thief && !actor.IsEscaping && VaultRoom >= 0
                && RoomOf(defender) != VaultRoom && defender.Activity is RunActionKind.Attack or RunActionKind.Skill
                && defender.Target != null && defender.Target != actor && defender.Target.Side == actor.Side && defender.Target.IsOnField;
        }

        private RunCombatant DefenderInRoom(int room, RunCombatant intruder)
        {
            if (room < 0) return null;
            RunCombatant best = null;
            float distance = float.PositiveInfinity;
            foreach (RunCombatant minion in _combatants)
            {
                if (!minion.IsOnField || minion.Side != RunSide.Minion || minion.HasMoveOrder || RoomOf(minion) != room) continue;
                if (CanPassEngagedDefender(intruder, minion)) continue;
                float candidate = Distance(intruder.Position, minion.Position);
                if (candidate >= distance) continue;
                best = minion;
                distance = candidate;
            }
            return best;
        }

        private RunCombatant OpponentOnRoute(RunCombatant actor, RunPosition target)
        {
            float length = Distance(actor.Position, target);
            RunCombatant best = null;
            float nearest = RangeOf(actor) + Epsilon;
            foreach (RunCombatant other in _combatants)
            {
                if (!other.IsOnField || other.Side == actor.Side) continue;
                float distance = Distance(actor.Position, other.Position);
                if (distance <= Epsilon) continue;
                if (distance > nearest || distance + Distance(other.Position, target) > length + Epsilon) continue;
                nearest = distance;
                best = other;
            }
            return best;
        }

        /// <summary>그 방에서 피해로 끊을 수 있는 예고(폭파·화염구)를 하는 시전자 중 가장 가까운 쪽.</summary>
        private RunCombatant InterruptibleCasterIn(int room, RunPosition from)
        {
            RunCombatant best = null;
            float nearest = float.PositiveInfinity;
            IReadOnlyList<RunTelegraph> active = _telegraphRules.Active;
            for (int i = 0; i < active.Count; i++)
            {
                RunTelegraph warning = active[i];
                if (warning.Kind is not (RunTelegraphKind.PowderBlast or RunTelegraphKind.Fireball)) continue;
                RunCombatant caster = warning.Caster;
                if (!caster.IsOnField || RoomOf(caster) != room) continue;
                float distance = Distance(from, caster.Position);
                if (distance >= nearest) continue;
                nearest = distance;
                best = caster;
            }
            return best;
        }

        private bool IntruderBetween(RunPosition a, RunPosition b)
        {
            float length = Distance(a, b);
            foreach (RunCombatant other in _combatants)
            {
                if (!other.IsOnField || other.Side != RunSide.Intruder) continue;
                float start = Distance(a, other.Position);
                float end = Distance(other.Position, b);
                if (start > Epsilon && end > Epsilon && start + end <= length + Epsilon) return true;
            }
            return false;
        }

        /// <summary>방을 잘 맞는 부하가 맡고 있으면 효과를 키운다(올림).</summary>
        private int Boost(RunRoom room, int value)
        {
            foreach (RunCombatant minion in CombatantsIn(room.Index, RunSide.Minion))
                if (minion.Definition.HasFavoredRoom && minion.Definition.FavoredRoom == room.Kind)
                    return (int)Math.Ceiling(value * Settings.FavoredRoomMultiplier);
            return value;
        }

        private bool UpdateOutcome()
        {
            if (Outcome != RunBattleOutcome.Ongoing) return true;
            if (IsRaid)
            {
                bool alive = false;
                bool escaped = false;
                foreach (RunCombatant combatant in _combatants)
                {
                    if (combatant.Side != RunSide.Minion) continue;
                    alive |= combatant.IsActive;
                    escaped |= combatant.HasLeft;
                }
                if (!IsRaidRetreating && RaidCommander != null && RaidCommander.IsDown)
                    Outcome = RunBattleOutcome.Victory;
                else if (!alive)
                    Outcome = IsRaidRetreating && escaped ? RunBattleOutcome.Retreat : RunBattleOutcome.Defeat;
                if (Outcome == RunBattleOutcome.Ongoing) return false;
                _telegraphRules.EndBattle();
                return true;
            }
            if (HeartHealth <= 0)
            {
                Outcome = RunBattleOutcome.Defeat;
                _telegraphRules.EndBattle();
                return true;
            }
            foreach (RunCombatant combatant in _combatants)
                if (combatant.Side == RunSide.Intruder && combatant.IsActive)
                    return false;
            Outcome = RunBattleOutcome.Victory;
            _telegraphRules.EndBattle();
            return true;
        }

        private bool IsOnRaidFront(RunPosition position)
        {
            if (position.IsAtEntrance) return true;
            if (position.IsInRoom) return _rooms[position.Room].Row == 0;
            RunLink link = _links[position.Link];
            return _rooms[link.From].Row == 0 && _rooms[link.To].Row == 0;
        }

        private List<RunCombatant> CombatantsIn(int roomIndex, RunSide side)
        {
            _scratch.Clear();
            foreach (RunCombatant combatant in _combatants)
                if (combatant.IsOnField && combatant.Side == side && RoomOf(combatant) == roomIndex)
                    _scratch.Add(combatant);
            return _scratch;
        }

        private int MaxManaFor(RunRoomKind kind) => kind switch
        {
            RunRoomKind.Trap => Settings.TrapMaxMana,
            RunRoomKind.HealingSpring => 1,
            RunRoomKind.DoorControl => 1,
            _ => 0,
        };
    }
}
