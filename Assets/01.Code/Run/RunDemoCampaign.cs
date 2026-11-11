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
        private readonly RunCombatantSO _raidArcher;

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
            // 대장 한 명만 잡으면 피해 없이 끝나던 원정을 다친 부하를 데려갈지 고민하게 만든다(2026-11-10 사용자 결정).
            _commander.ScaleDemoHealth(1.5f);
            // 침입 궁수는 부하를 지나쳐 치유 샘으로 가도록 체력·속도를 2배로 올렸다. 진지 경비실 궁수는 그 전 값을 쓴다(2026-11-11 사용자 결정).
            _raidArcher = _archer.Combatant.CreateDemoVariant(_archer.Combatant.DisplayName, healthBonus: 0f);
            _raidArcher.ScaleDemoHealth(0.5f);
            _raidArcher.ScaleDemoMoveSpeed(0.5f);
        }

        internal RunDemoRoute Route { get; private set; } = new();
        internal RunBattleCarryover Carryover { get; private set; }
        internal int TrapBonus { get; private set; }

        /// <summary>던전 정비에서 금화로 산 마력 용량(런 동안 유지).</summary>
        internal int ExtraCapacity { get; private set; }

        /// <summary>이번 상인 칸에서 산 항목(비트: 0 심장 수리, 1 함정 강화, 2 부하 고용). 항목마다 한 번만 산다.</summary>
        internal int MerchantBought { get; private set; }

        // 던전 정비 값(2026-11-10 사용자 승인, 시연 런 기획 "던전 정비 값 비교").
        internal const int RepairPrice = 20;
        internal const int HealPrice = 20;
        internal const int CapacityPrice = 60;
        internal const int MaxExtraCapacity = 2;

        // 도적·공병 침입은 같은 파티가 20초 간격으로 두 무리 들어오고, 정예 침입은 한 무리다(2026-11-10 사용자 결정).
        internal const float WaveGapSeconds = 20f;

        internal static int DefenseWaves(int number) => number == 3 ? 1 : 2;

        internal RunBattleSettings DefenseSettings(int number) => _encounter.Settings.ForDemo(
            (number == 3 && Route.RaidResult == RunDemoRoute.Result.Victory ? 4 : 3) + ExtraCapacity, TrapBonus, number != 3,
            DefenseWaves(number), WaveGapSeconds);

        internal RunIntruderEntry[] DefenseParty(int number) => number switch
        {
            1 => new[] { _sword, _archer, _thief },
            2 => new[] { _sword, _sapper, _mage, _archer },
            _ => new[] { _sword.WithCombatant(_elite), _archer, _sapper, _thief },
        };

        /// <summary>원정 진지를 지키는 적 이름(지도 미리보기용).</summary>
        internal string RaidDefenders => $"{_sword.Combatant.DisplayName}, {_archer.Combatant.DisplayName}, {_commander.DisplayName}";

        internal void ResolveDefense(RunBattle battle)
        {
            if (!Route.TryResolve(battle.Outcome == RunBattleOutcome.Victory ? RunDemoRoute.Result.Victory : RunDemoRoute.Result.Defeat)) return;
            Carryover = new RunBattleCarryover(battle);
        }

        /// <summary>상인 항목 하나의 금화 가격(심장 수리·함정 강화·부하 고용 공통).</summary>
        internal const int MerchantPrice = 30;

        /// <summary>상인이 고용으로 채울 쓰러진 부하(배치 순서상 첫 번째). 없으면 -1.</summary>
        internal int FirstLostMinion => Carryover == null ? -1 : Array.FindIndex(Carryover.MinionHealth, health => health <= 0);

        /// <summary>상인 항목을 지금 살 수 있는가(금화, 이미 샀는지, 항목 조건).</summary>
        internal bool CanBuyAtMerchant(int option)
        {
            if (Carryover == null || option is < 0 or > 2 || (MerchantBought & (1 << option)) != 0 || Carryover.Gold < MerchantPrice) return false;
            return option switch
            {
                0 => Carryover.HeartHealth < _encounter.Settings.HeartHealth,
                1 => TrapBonus == 0,
                _ => FirstLostMinion >= 0,
            };
        }

        internal bool HasBoughtAtMerchant(int option) => (MerchantBought & (1 << option)) != 0;

        // ── 던전 정비(전투·원정 사이) ──

        /// <summary>고칠 수 있는 방인가: 시스템 방(함정·치유 샘·문 제어실)이 고장 났거나 내구가 깎였다.</summary>
        internal bool IsRoomDamaged(int room)
        {
            if (Carryover == null || room < 0 || room >= Carryover.RoomBroken.Length) return false;
            RunRoomKind kind = _encounter.GridLayout[room].Kind;
            bool system = kind is RunRoomKind.Trap or RunRoomKind.HealingSpring or RunRoomKind.DoorControl;
            return system && (Carryover.RoomBroken[room] || Carryover.RoomDurability[room] < _encounter.Settings.RoomDurability);
        }

        /// <summary>치료할 수 있는 부하인가: 살아 있고 체력이 깎였다.</summary>
        internal bool IsMinionHurt(int index)
        {
            if (Carryover == null || index < 0 || index >= Carryover.MinionHealth.Length) return false;
            int health = Carryover.MinionHealth[index];
            return health > 0 && health < _encounter.Minions[index].Combatant.MaxHealth;
        }

        internal bool TryRepairRoom(int room)
        {
            if (!IsRoomDamaged(room) || Carryover.Gold < RepairPrice) return false;
            Carryover.RoomBroken[room] = false;
            Carryover.RoomDurability[room] = _encounter.Settings.RoomDurability;
            Carryover.Gold -= RepairPrice;
            return true;
        }

        internal bool TryHealMinion(int index)
        {
            if (!IsMinionHurt(index) || Carryover.Gold < HealPrice) return false;
            Carryover.MinionHealth[index] = _encounter.Minions[index].Combatant.MaxHealth;
            Carryover.Gold -= HealPrice;
            return true;
        }

        /// <summary>정비에서 쓰러진 부하를 되살리는 값. 상인(30)보다 비싸다(2026-11-11 사용자 결정).</summary>
        internal const int UpkeepHirePrice = 60;

        /// <summary>정비에서 되살릴 수 있는 부하인가: 쓰러졌다.</summary>
        internal bool IsMinionLost(int index) =>
            Carryover != null && index >= 0 && index < Carryover.MinionHealth.Length && Carryover.MinionHealth[index] <= 0;

        /// <summary>쓰러진 부하를 같은 종류로 체력 가득 되살린다.</summary>
        internal bool TryHireAtUpkeep(int index)
        {
            if (!IsMinionLost(index) || Carryover.Gold < UpkeepHirePrice) return false;
            Carryover.MinionHealth[index] = _encounter.Minions[index].Combatant.MaxHealth;
            Carryover.Gold -= UpkeepHirePrice;
            return true;
        }

        internal bool CanBuyCapacity => Carryover != null && ExtraCapacity < MaxExtraCapacity && Carryover.Gold >= CapacityPrice;

        internal bool TryBuyCapacity()
        {
            if (!CanBuyCapacity) return false;
            ExtraCapacity++;
            Carryover.Gold -= CapacityPrice;
            return true;
        }

        internal bool ResolveBranch(int option)
        {
            if (Route.CurrentResult != RunDemoRoute.Result.Pending || Carryover == null) return false;
            if (Route.Current == RunDemoRoute.Node.Merchant)
            {
                // 떠나기(-1)만 칸을 끝낸다. 항목은 금화가 되는 만큼 하나씩 사고, 같은 항목은 한 번만 산다(2026-11-10 사용자 승인).
                if (option == -1)
                {
                    MerchantBought = 0;
                    return Route.TryResolve(RunDemoRoute.Result.Completed);
                }
                if (!CanBuyAtMerchant(option)) return false;
                if (option == 0) Carryover.HeartHealth = Math.Min(_encounter.Settings.HeartHealth, Carryover.HeartHealth + 10);
                if (option == 1) TrapBonus = 2;
                // 고용: 쓰러진 부하 한 명을 같은 종류로 체력 가득 채운다(2026-11-10 사용자 결정, 금화 30).
                if (option == 2)
                {
                    int lost = FirstLostMinion;
                    Carryover.MinionHealth[lost] = _encounter.Minions[lost].Combatant.MaxHealth;
                }
                Carryover.Gold -= MerchantPrice;
                MerchantBought |= 1 << option;
                return true;
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
            raid.AddCombatant(_raidArcher, 1, _archer.OverrideAffinity ? _archer.Affinity : null);
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

        internal void Restore(RunDemoRoute route, RunBattleCarryover carryover, int trapBonus, int extraCapacity, int merchantBought)
        {
            Route = route;
            Carryover = carryover;
            TrapBonus = trapBonus;
            ExtraCapacity = Math.Clamp(extraCapacity, 0, MaxExtraCapacity);
            MerchantBought = merchantBought & 0b111;
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_elite);
                UnityEngine.Object.Destroy(_commander);
                UnityEngine.Object.Destroy(_raidArcher);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_elite);
                UnityEngine.Object.DestroyImmediate(_commander);
                UnityEngine.Object.DestroyImmediate(_raidArcher);
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
