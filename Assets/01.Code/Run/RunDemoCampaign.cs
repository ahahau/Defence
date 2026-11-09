using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Run
{
    /// <summary>시연의 던전 상태와 콘텐츠를 지도 규칙에 연결한다.</summary>
    internal sealed class RunDemoCampaign : IDisposable
    {
        private static readonly RunRoomLayout[] RaidLayout =
        {
            new(RunRoomKind.Entrance, 0, 0),
            new(RunRoomKind.Watch, 0, 1),
            new(RunRoomKind.Heart, 0, 2),
            new(RunRoomKind.Entrance, 1, 0),
            new(RunRoomKind.Entrance, 1, 1),
            new(RunRoomKind.Entrance, 1, 2),
        };

        private readonly RunEncounterSO _encounter;
        private readonly RunIntruderEntry _sword;
        private readonly RunIntruderEntry _archer;
        private readonly RunIntruderEntry _thief;
        private readonly RunIntruderEntry _sapper;
        private readonly RunIntruderEntry _mage;
        private readonly RunCombatantSO _elite;
        private readonly RunCombatantSO _commander;

        internal RunDemoCampaign(RunEncounterSO encounter)
        {
            _encounter = encounter;
            _sword = Find("RunIntruder_Sword");
            _archer = Find("RunIntruder_Archer");
            _thief = Find("RunIntruder_Thief");
            _sapper = Find("RunIntruder_Sapper");
            _mage = Find("RunIntruder_ApprenticeMage");
            _elite = _sword.Combatant.CreateDemoVariant("정예 방패병", encounter.DemoEliteAppearance);
            _commander = _sword.Combatant.CreateDemoVariant("진지 대장");
        }

        internal RunDemoRoute Route { get; private set; } = new();
        internal RunBattleCarryover Carryover { get; private set; }
        internal int TrapBonus { get; private set; }

        internal RunBattleSettings DefenseSettings(int number) => _encounter.Settings.ForDemo(
            number == 3 && Route.RaidResult == RunDemoRoute.Result.Victory ? 4 : 3, TrapBonus, number != 3);

        internal RunIntruderEntry[] DefenseParty(int number) => number switch
        {
            1 => new[] { _sword, _archer, _thief },
            2 => new[] { _sword, _sapper, _mage },
            _ => new[] { _sword.WithCombatant(_elite), _archer, _sapper, _thief, _mage },
        };

        internal void ResolveDefense(RunBattle battle)
        {
            if (!Route.TryResolve(battle.Outcome == RunBattleOutcome.Victory ? RunDemoRoute.Result.Victory : RunDemoRoute.Result.Defeat)) return;
            Carryover = new RunBattleCarryover(battle);
        }

        internal bool ResolveBranch(int option)
        {
            if (Route.CurrentResult != RunDemoRoute.Result.Pending || Carryover == null) return false;
            if (Route.Current == RunDemoRoute.Node.Merchant)
            {
                if (option is < -1 or > 1 || (option >= 0 && Carryover.Gold < 30)) return false;
                if (option == 0)
                {
                    if (Carryover.HeartHealth >= _encounter.Settings.HeartHealth) return false;
                    Carryover.HeartHealth = Math.Min(_encounter.Settings.HeartHealth, Carryover.HeartHealth + 10);
                }
                if (option == 1) TrapBonus = 2;
                if (option >= 0) Carryover.Gold -= 30;
            }
            else if (Route.Current == RunDemoRoute.Node.StoryEvent)
            {
                if (option < -1 || option >= Carryover.MinionHealth.Length) return false;
                if (option >= 0)
                {
                    int max = _encounter.Minions[option].Combatant.MaxHealth;
                    int health = Carryover.MinionHealth[option];
                    if (health <= 0 || health >= max) return false;
                    Carryover.MinionHealth[option] = Math.Min(max, health + (max + 1) / 2);
                }
            }
            else return false;
            return Route.TryResolve(RunDemoRoute.Result.Completed);
        }

        internal RunBattle CreateRaid(IReadOnlyList<int> party, bool detour)
        {
            RunBattle raid = new(_encounter.Settings.ForDemo(0, 0, false), RaidLayout, 0, true);
            raid.SetInitialResources(Carryover.HeartHealth, Carryover.Gold);
            foreach (int index in party)
            {
                RunCombatant minion = raid.AddCombatant(_encounter.Minions[index].Combatant, 0);
                minion.SetInitialHealth(Carryover.MinionHealth[index]);
                minion.MoveTo(raid.EntrancePosition);
                raid.OrderMove(minion, detour ? 3 : 1);
            }
            raid.AddCombatant(_sword.Combatant, 1, _sword.OverrideAffinity ? _sword.Affinity : null);
            raid.AddCombatant(_archer.Combatant, 1, _archer.OverrideAffinity ? _archer.Affinity : null);
            raid.RaidCommander = raid.AddCombatant(_commander, 2);
            return raid;
        }

        internal void ResolveRaid(RunBattle raid, IReadOnlyList<int> party)
        {
            RunDemoRoute.Result result = raid.Outcome switch
            {
                RunBattleOutcome.Victory => RunDemoRoute.Result.Victory,
                RunBattleOutcome.Retreat => RunDemoRoute.Result.Retreat,
                _ => RunDemoRoute.Result.Defeat,
            };
            if (!Route.TryResolve(result)) return;
            for (int i = 0; i < party.Count; i++) Carryover.MinionHealth[party[i]] = raid.Combatants[i].Health;
        }

        internal void Restore(RunDemoRoute route, RunBattleCarryover carryover, int trapBonus)
        {
            Route = route;
            Carryover = carryover;
            TrapBonus = trapBonus;
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_elite);
                UnityEngine.Object.Destroy(_commander);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_elite);
                UnityEngine.Object.DestroyImmediate(_commander);
            }
        }

        private RunIntruderEntry Find(string assetName)
        {
            foreach (RunIntruderEntry entry in _encounter.Party)
                if (entry.Combatant != null && entry.Combatant.name == assetName) return entry;
            throw new InvalidOperationException($"시연 전투 데이터가 없습니다 ({assetName}).");
        }
    }
}
