using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Code.Run
{
    /// <summary>예고의 준비·중단·발동 규칙. 상태와 경로 저장 공간은 전투 준비 때만 만든다.</summary>
    public class RunTelegraphRules
    {
        private const float Epsilon = 0.0005f;
        private readonly RunBattle _battle;
        private readonly RunNavigation _navigation;
        private readonly List<RunTelegraph> _slots = new();
        private readonly List<RunTelegraph> _active = new();
        private readonly ReadOnlyCollection<RunTelegraph> _view;

        public RunTelegraphRules(RunBattle battle, RunNavigation navigation)
        {
            _battle = battle;
            _navigation = navigation;
            _view = _active.AsReadOnly();
        }

        public IReadOnlyList<RunTelegraph> Active => _view;

        internal void Register(RunCombatant actor)
        {
            actor.Telegraph = new RunTelegraph(actor, _battle.Rooms.Count * 5 + _battle.Links.Count + 2);
            _slots.Add(actor.Telegraph);
            _active.Capacity = _slots.Count;
        }

        public bool TryBegin(RunCombatant caster, RunTelegraphSkillEffectSO effect)
        {
            if (caster == null || caster.Id < 0 || caster.Id >= _battle.Combatants.Count
                || !ReferenceEquals(_battle.Combatants[caster.Id], caster)) return false;
            RunTelegraph warning = caster.Telegraph;
            if (caster.Side != RunSide.Intruder || !caster.IsOnField || caster.IsEscaping || warning.IsActive
                || effect == null || warning.Effect != effect || !caster.IsSkillReady || caster.AttackTimer > Epsilon
                || _battle.Outcome != RunBattleOutcome.Ongoing) return false;

            int room = _battle.RoomOf(caster);
            RunCombatant target = null;
            RunPosition end;
            if (effect.Kind == RunTelegraphKind.PowderBlast)
            {
                if (!HasBlastTarget(room)) return false;
                end = _battle.CenterOf(room);
                // 폭약은 방 가운데에 놓는다. 들어오자마자 방 끝에서 시작하지 않아 이웃 방 부하가 닿을 시간이 생긴다(2026-11-09 사용자 결정).
                if (_battle.Distance(caster.Position, end) > Epsilon) return false;
            }
            else
            {
                float nearest = _battle.RangeOf(caster) + Epsilon;
                end = caster.Position;
                for (int i = 0; i < _battle.Combatants.Count; i++)
                {
                    RunCombatant other = _battle.Combatants[i];
                    if (!other.IsOnField || other.Side != RunSide.Minion) continue;
                    if (effect.Kind == RunTelegraphKind.Fireball && _battle.RoomOf(other) < 0) continue;
                    float distance = _battle.Distance(caster.Position, other.Position);
                    if (distance > nearest) continue;
                    RunPosition candidateEnd = other.Position;
                    if (effect.Kind == RunTelegraphKind.Charge && !ChargeEnd(caster.Position, other.Position, out candidateEnd)) continue;
                    if (target != null && distance >= nearest - Epsilon) continue;
                    nearest = distance;
                    target = other;
                    end = candidateEnd;
                }
                if (target == null) return false;
                room = _battle.RoomOf(target);
                if (effect.Kind == RunTelegraphKind.Fireball) end = _battle.CenterOf(room);
            }

            warning.Begin(room, target, end);
            if (effect.Kind is RunTelegraphKind.Fireball or RunTelegraphKind.Charge)
            {
                if (!CapturePath(warning))
                {
                    warning.Finish(RunTelegraphEndReason.TargetEscaped);
                    return false;
                }
                // 돌진은 예고 때 잠겨 있던 문을 기억해, 발동 전에 문이 풀려도 그 앞에서 멈춘다(2026-11-09 사용자 결정).
                if (effect.Kind == RunTelegraphKind.Charge) MarkLockedDoors(warning);
            }
            _active.Add(warning);
            caster.OnSkillUsed(warning.Skill.CooldownTurns * _battle.AttackInterval(caster));
            caster.Strike(_battle.AttackInterval(caster));
            caster.SetActivity(RunActionKind.Skill, target);
            CheckDoors();
            return true;
        }

        /// <summary>
        /// 폭파를 준비한 공병이 지금 방의 가운데로 걸어가야 하는가. 가는 동안 부하는 무시한다(2026-11-09 사용자 결정).
        /// </summary>
        internal bool ShouldApproachBlast(RunCombatant caster, out int room)
        {
            room = -1;
            RunTelegraph warning = caster.Telegraph;
            if (caster.Side != RunSide.Intruder || !caster.IsOnField || caster.IsEscaping || warning == null || warning.IsActive
                || warning.Effect == null || warning.Kind != RunTelegraphKind.PowderBlast || !caster.IsSkillReady
                || _battle.Outcome != RunBattleOutcome.Ongoing) return false;
            int current = _battle.RoomOf(caster);
            if (!HasBlastTarget(current)) return false;
            room = current;
            return true;
        }

        internal void Tick(float dt)
        {
            // 폭파가 다른 시전자를 쓰러뜨려 활성 목록을 바꿀 수 있으므로 준비된 슬롯을 순회한다.
            foreach (RunTelegraph warning in _slots)
            {
                if (!warning.IsActive) continue;
                if (!warning.Caster.IsOnField)
                {
                    Finish(warning, RunTelegraphEndReason.CasterLeft);
                    continue;
                }
                if (warning.Kind == RunTelegraphKind.Fireball && HasLockedDoor(warning))
                {
                    Finish(warning, RunTelegraphEndReason.LockedDoor);
                    continue;
                }
                warning.Tick(dt);
                if (warning.RemainingSeconds <= Epsilon) Resolve(warning);
            }
        }

        internal void OnDamage(RunCombatant target, int actualDamage)
        {
            RunTelegraph warning = target.Telegraph;
            if (warning == null || !warning.IsActive) return;
            if (!target.IsOnField) Finish(warning, RunTelegraphEndReason.CasterLeft);
            else if (actualDamage > 0 && (warning.Kind is RunTelegraphKind.PowderBlast or RunTelegraphKind.Fireball))
                Finish(warning, RunTelegraphEndReason.Damage);
        }

        internal void CheckDoors()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                RunTelegraph warning = _active[i];
                if (warning.Kind == RunTelegraphKind.Fireball && HasLockedDoor(warning))
                    Finish(warning, RunTelegraphEndReason.LockedDoor);
            }
        }

        internal void EndBattle()
        {
            while (_active.Count > 0) Finish(_active[_active.Count - 1], RunTelegraphEndReason.BattleEnded);
        }

        private bool HasBlastTarget(int room)
        {
            if (room < 0) return false;
            if (_battle.Rooms[room].HasSystem && !_battle.Rooms[room].IsBroken) return true;
            for (int i = 0; i < _battle.Combatants.Count; i++)
            {
                RunCombatant other = _battle.Combatants[i];
                if (other.IsOnField && other.Side == RunSide.Minion && _battle.RoomOf(other) == room) return true;
            }
            return false;
        }

        private bool CapturePath(RunTelegraph warning)
        {
            RunPosition cursor = warning.Origin;
            for (int i = 0; i < _battle.Rooms.Count * 5 + _battle.Links.Count + 2; i++)
            {
                if (_battle.Distance(cursor, warning.End) <= Epsilon) return true;
                if (!_navigation.NextStep(cursor, warning.End, false, out RunPosition start, out RunPosition end)
                    || !warning.AppendPath(start, end)) return false;
                cursor = end;
            }
            return false;
        }

        private void MarkLockedDoors(RunTelegraph warning)
        {
            for (int i = 0; i < warning.PathCount; i++)
            {
                RunPosition start = warning.PathStart(i);
                RunPosition end = warning.PathEnd(i);
                if (!start.IsInRoom && !start.IsAtEntrance && _battle.IsDoorLocked(start.Link)
                    && Between(_battle.Doors[start.Link].Position.Offset, start.Offset, end.Offset)) warning.MarkDoorLockedAtStart(i);
            }
        }

        private bool HasLockedDoor(RunTelegraph warning)
        {
            for (int i = 0; i < warning.PathCount; i++)
            {
                RunPosition start = warning.PathStart(i);
                RunPosition end = warning.PathEnd(i);
                if (!start.IsInRoom && !start.IsAtEntrance && _battle.IsDoorLocked(start.Link)
                    && Between(_battle.Doors[start.Link].Position.Offset, start.Offset, end.Offset)) return true;
            }
            return false;
        }

        private void Resolve(RunTelegraph warning)
        {
            Finish(warning, RunTelegraphEndReason.Completed);
            if (warning.Kind == RunTelegraphKind.Charge)
            {
                ResolveCharge(warning);
                return;
            }
            if (warning.Kind == RunTelegraphKind.AimedShot)
            {
                RunCombatant target = warning.Target;
                if (!target.IsOnField || _battle.RoomOf(target) != warning.TargetRoom
                    || _battle.Distance(warning.Caster.Position, target.Position) > _battle.RangeOf(warning.Caster) + Epsilon)
                {
                    warning.Finish(RunTelegraphEndReason.TargetEscaped);
                    return;
                }
                _battle.DealDamage(warning.Caster, target, warning.Effect.Damage);
                return;
            }
            if (warning.Kind == RunTelegraphKind.PowderBlast)
                _battle.Rooms[warning.TargetRoom].Damage(warning.Effect.SystemDamage);
            for (int i = 0; i < _battle.Combatants.Count; i++)
            {
                RunCombatant target = _battle.Combatants[i];
                if (!target.IsOnField || _battle.RoomOf(target) != warning.TargetRoom) continue;
                if (warning.Kind == RunTelegraphKind.Fireball && target.Side != RunSide.Minion) continue;
                _battle.DealDamage(warning.Caster, target, warning.Effect.Damage,
                    warning.Kind == RunTelegraphKind.Fireball ? RunAffinity.Magic : null);
            }
        }

        private void ResolveCharge(RunTelegraph warning)
        {
            int last = warning.PathCount - 1;
            RunPosition impactEnd = warning.End;
            for (int i = 0; i < warning.PathCount; i++)
            {
                RunPosition start = warning.PathStart(i);
                RunPosition end = warning.PathEnd(i);
                if (start.IsInRoom || start.IsAtEntrance || !(_battle.IsDoorLocked(start.Link) || warning.WasDoorLockedAtStart(i))) continue;
                float door = _battle.Doors[start.Link].Position.Offset;
                if (!Between(door, start.Offset, end.Offset)) continue;
                float gap = Math.Min(_battle.Settings.DoorStopGap, Math.Abs(door - start.Offset));
                impactEnd = RunPosition.OnLink(start.Link, door - Math.Sign(end.Offset - start.Offset) * gap);
                last = i;
                break;
            }
            warning.SetImpactEnd(impactEnd);
            for (int i = 0; i < _battle.Combatants.Count; i++)
            {
                RunCombatant target = _battle.Combatants[i];
                if (!target.IsOnField || target.Side != RunSide.Minion) continue;
                for (int segment = 0; segment <= last; segment++)
                {
                    RunPosition start = warning.PathStart(segment);
                    RunPosition end = segment == last ? impactEnd : warning.PathEnd(segment);
                    if (!OffsetOnSegment(start, target.Position, out float offset) || !Between(offset, start.Offset, end.Offset)) continue;
                    _battle.DealDamage(warning.Caster, target, warning.Effect.Damage);
                    break;
                }
            }
            warning.Caster.MoveTo(impactEnd);
        }

        private bool ChargeEnd(RunPosition origin, RunPosition target, out RunPosition end) =>
            ChargeOnAxis(origin, target, RunAxis.Horizontal, out end) || ChargeOnAxis(origin, target, RunAxis.Vertical, out end);

        private bool ChargeOnAxis(RunPosition origin, RunPosition target, RunAxis axis, out RunPosition end)
        {
            end = origin;
            if (!AxisCoordinate(origin, axis, out int floor, out int line, out float from)
                || !AxisCoordinate(target, axis, out int targetFloor, out int targetLine, out float to)
                || floor != targetFloor || line != targetLine || Math.Abs(to - from) <= Epsilon) return false;
            int direction = Math.Sign(to - from);
            int room = origin.IsInRoom ? origin.Room
                : direction > 0 ? _battle.Links[origin.Link].To : _battle.Links[origin.Link].From;
            // 같은 방 안의 대상에게는 그 방 끝까지만 달린다(2026-11-10 사용자 결정).
            bool sameRoom = origin.IsInRoom && target.IsInRoom && origin.Room == target.Room;
            while (!sameRoom)
            {
                int next = -1;
                for (int i = 0; i < _battle.Links.Count; i++)
                {
                    RunLink link = _battle.Links[i];
                    if (link.Axis != axis) continue;
                    if (direction > 0 && link.From == room) next = link.To;
                    else if (direction < 0 && link.To == room) next = link.From;
                    if (next >= 0) break;
                }
                if (next < 0) break;
                room = next;
            }
            end = RunPosition.InRoom(room, axis, direction > 0 ? _battle.Rooms[room].Length : 0f);
            AxisCoordinate(end, axis, out _, out _, out float edge);
            return Between(to, from, edge);
        }

        private bool AxisCoordinate(RunPosition position, RunAxis axis, out int floor, out int line, out float coordinate)
        {
            floor = line = -1;
            coordinate = 0f;
            if (position.IsAtEntrance) return false;
            int room = position.IsInRoom ? position.Room : _battle.Links[position.Link].From;
            RunRoom cell = _battle.Rooms[room];
            floor = cell.Floor;
            line = axis == RunAxis.Horizontal ? cell.Row : cell.Column;
            if (position.IsInRoom && position.Axis != axis && Math.Abs(position.Offset - cell.Center) > Epsilon) return false;
            if (!position.IsInRoom && _battle.Links[position.Link].Axis != axis) return false;
            float offset = position.IsInRoom ? (position.Axis == axis ? position.Offset : cell.Center)
                : _battle.Links[position.Link].FromOffset + position.Offset;
            coordinate = (axis == RunAxis.Horizontal ? cell.Column : cell.Row)
                * (_battle.Settings.RoomLength + _battle.Settings.CorridorLength) + offset;
            return true;
        }

        private bool OffsetOnSegment(RunPosition segment, RunPosition position, out float offset)
        {
            offset = position.Offset;
            if (SameSegment(segment, position)) return true;
            if (segment.IsInRoom && position.IsInRoom && segment.Room == position.Room
                && Math.Abs(position.Offset - _battle.Rooms[position.Room].Center) <= Epsilon)
            {
                offset = _battle.Rooms[position.Room].Center;
                return true;
            }
            return false;
        }

        private void Finish(RunTelegraph warning, RunTelegraphEndReason reason)
        {
            warning.Finish(reason);
            _active.Remove(warning);
        }

        private static bool Between(float value, float a, float b) => value >= Math.Min(a, b) - Epsilon && value <= Math.Max(a, b) + Epsilon;

        private static bool SameSegment(RunPosition a, RunPosition b) =>
            a.IsInRoom == b.IsInRoom && (a.IsInRoom ? a.Room == b.Room && a.Axis == b.Axis : a.Link == b.Link);
    }
}
