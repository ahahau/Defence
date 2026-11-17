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

        private static readonly RunDemoRelic[] RelicOrder =
            { RunDemoRelic.QuickTrap, RunDemoRelic.ThickBones, RunDemoRelic.ManaCrystal, RunDemoRelic.HeavyLock };

        private readonly RunEncounterSO _encounter;
        private readonly RunIntruderEntry _sword;
        private readonly RunIntruderEntry _archer;
        private readonly RunIntruderEntry _thief;
        private readonly RunIntruderEntry _sapper;
        private readonly RunIntruderEntry _mage;
        private readonly RunCombatantSO _elite;
        private readonly RunCombatantSO _commander;
        private readonly RunCombatantSO _raidArcher;
        // 강화한 부하의 실행 중 사본. 런이 끝날 때 지운다.
        private readonly List<RunCombatantSO> _variants = new();
        private RunPlacement[] _roster;
        private int[] _minionUpgrades;

        internal RunDemoCampaign(RunEncounterSO encounter, bool postRaidBranch = false, int seed = 0)
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
            Route = new RunDemoRoute(postRaidBranch);
            Seed = seed != 0 ? seed : NewSeed();
            _minionUpgrades = new int[encounter.Minions.Length + MaxExtraMinions];
        }

        internal RunDemoRoute Route { get; private set; }
        internal RunBattleCarryover Carryover { get; private set; }
        internal int TrapBonus { get; private set; }

        /// <summary>상인에서 금화로 산 마력 용량(런 동안 유지).</summary>
        internal int ExtraCapacity { get; private set; }

        /// <summary>이번 상인 칸에서 산 항목(비트 = <see cref="MerchantItem"/>). 항목마다 한 번, 방문마다 두 개까지 산다.</summary>
        internal int MerchantBought { get; private set; }

        /// <summary>
        /// 상인에서 살린 부하(부하 번호 비트). 다음 준비 화면에서 "지난 판 쓰러짐"과 자리 안내를 보이고,
        /// 다음 전투·원정이 끝나면 지운다(2026-11-12 사용자 결정 "준비 화면에서 자리 안내").
        /// </summary>
        internal int RevivedMinions { get; private set; }

        internal bool WasRevived(int index) => index is >= 0 and < 31 && (RevivedMinions & (1 << index)) != 0;

        /// <summary>상인 물건·글 사건을 뽑는 런 시드. 저장해 다시 불러도 같은 제안이 나온다.</summary>
        internal int Seed { get; private set; }

        internal RunDemoRelic Relics { get; private set; }

        /// <summary>상인에서 들인 5번째 부하의 종류(에셋 부하 목록 번호). 없으면 -1.</summary>
        internal int ExtraMinionKind { get; private set; } = -1;

        /// <summary>지금 상인·글 사건 칸의 제안 두 개(상인은 유물 값, 글 사건은 <see cref="RunDemoExchange"/> 값). 없으면 -1.</summary>
        internal int Offer0 { get; private set; } = -1;
        internal int Offer1 { get; private set; } = -1;

        // ── 값(2026-11-11 사용자 "이 표로 시뮬레이션", 시연 런 기획 "지도와 경제") ──

        // 정비 20 → 30(처치 금화 10 → 5와 함께, 2026-11-17 사용자 결정 "G3"). 금화가 남아돌아 정비에서 고를 일이 없었다.
        internal const int RepairPrice = 30;
        internal const int HealPrice = 30;
        internal const int MaxExtraCapacity = 2;
        internal const int MaxExtraMinions = 1;
        internal const float QuickTrapCooldownSeconds = 3f;
        internal const int ThickBonesHealth = 4;
        internal const float HeavyLockCarryRatio = 0.5f;

        /// <summary>상인 새 항목·교환 값. 기본값이 게임 값이다.</summary>
        internal RunDemoEconomy Economy { get; } = new();

        /// <summary>상인 항목 번호. <see cref="MerchantBought"/>의 비트 자리이므로 바꾸지 않는다.</summary>
        internal enum MerchantItem
        {
            HeartRepair = 0,
            TrapUpgrade = 1,
            Revive = 2,
            Capacity = 3,
            NewMinion = 4,
            Relic0 = 5,
            Relic1 = 6,
        }

        internal const int MerchantItemMask = 0b111_1111;

        // 세 침입 모두 같은 파티가 20초 간격으로 두 무리 들어온다(2026-11-10 사용자 결정).
        // 정예 침입은 한 무리였으나 약 17초 만에 끝나 공병 침입보다 쉬워서 두 무리로 늘렸다(2026-11-16 사용자 결정 "S4 + E1").
        internal const float WaveGapSeconds = 20f;

        // 공병 침입만 세 무리다(2026-11-17 사용자 결정 "P2"). 두 무리면 대응하지 않아도 런을 다 이겼다.
        internal static int DefenseWaves(int number) => number == 2 ? 3 : 2;

        internal bool Has(RunDemoRelic relic) => (Relics & relic) != 0;

        internal RunBattleSettings DefenseSettings(int number) => _encounter.Settings.ForDemo(
                (number == 3 && Route.RaidResult == RunDemoRoute.Result.Victory ? 4 : 3) + ExtraCapacity
                + (Has(RunDemoRelic.ManaCrystal) ? 1 : 0), TrapBonus, number != 3, DefenseWaves(number), WaveGapSeconds)
            .WithDemoRelics(Has(RunDemoRelic.QuickTrap) ? QuickTrapCooldownSeconds : null, Has(RunDemoRelic.HeavyLock) ? HeavyLockCarryRatio : 1f);

        internal RunIntruderEntry[] DefenseParty(int number) => number switch
        {
            1 => new[] { _sword, _archer, _thief },
            2 => new[] { _sword, _sapper, _mage, _archer },
            _ => new[] { _sword.WithCombatant(_elite), _archer, _sapper, _thief },
        };

        /// <summary>원정 진지를 지키는 적 이름(지도 미리보기용).</summary>
        internal string RaidDefenders => $"{_sword.Combatant.DisplayName}, {_archer.Combatant.DisplayName}, {_commander.DisplayName}";

        // ── 부하 목록: 에셋 부하 + 새 부하, 유물·강화를 얹은 사본 ──

        /// <summary>이번 런의 부하 목록. 배치·전투·이월이 같은 번호를 쓴다.</summary>
        internal RunPlacement[] Roster => _roster ??= BuildRoster();

        internal int MinionCount => Roster.Length;

        internal RunCombatantSO Minion(int index) => Roster[index].Combatant;

        /// <summary>교환 "금화 → 부하 강화"를 받은 횟수.</summary>
        internal int UpgradesOf(int index) => index >= 0 && index < _minionUpgrades.Length ? _minionUpgrades[index] : 0;

        internal void ResolveDefense(RunBattle battle)
        {
            if (!Route.TryResolve(battle.Outcome == RunBattleOutcome.Victory ? RunDemoRoute.Result.Victory : RunDemoRoute.Result.Defeat)) return;
            Carryover = new RunBattleCarryover(battle);
            RevivedMinions = 0;
        }

        /// <summary>지도에서 칸에 들어간다. 상인·글 사건이면 이번 방문의 제안 두 개를 뽑는다.</summary>
        internal bool EnterNode(RunDemoRoute.Node node)
        {
            if (!Route.TryEnter(node)) return false;
            Offer0 = Offer1 = -1;
            MerchantBought = 0;
            if (node == RunDemoRoute.Node.Merchant) DrawRelicOffer();
            else if (node == RunDemoRoute.Node.StoryEvent) DrawExchangeOffer();
            return true;
        }

        // ── 상인 ──

        /// <summary>상인 항목 하나의 기본 금화 가격(심장 수리·함정 강화·부하 고용).</summary>
        internal const int MerchantPrice = 30;

        internal int PriceOf(MerchantItem item) => item switch
        {
            MerchantItem.Capacity => Economy.MerchantCapacityPrice,
            MerchantItem.NewMinion => Economy.NewMinionPrice,
            MerchantItem.Relic0 or MerchantItem.Relic1 => Economy.RelicPrice,
            _ => MerchantPrice,
        };

        /// <summary>이번 상인 방문에서 살 수 있는 만큼 다 샀는가.</summary>
        internal bool MerchantLimitReached => Economy.MerchantPurchaseLimit > 0 && MerchantBoughtCount >= Economy.MerchantPurchaseLimit;

        /// <summary>이번 상인 방문에서 산 항목 수.</summary>
        internal int MerchantBoughtCount
        {
            get
            {
                int count = 0;
                for (int bits = MerchantBought; bits != 0; bits &= bits - 1) count++;
                return count;
            }
        }

        /// <summary>상인이 고용으로 채울 쓰러진 부하(배치 순서상 첫 번째). 없으면 -1.</summary>
        internal int FirstLostMinion => Carryover == null ? -1 : Array.FindIndex(Carryover.MinionHealth, health => health <= 0);

        /// <summary>상인 유물 칸(0·1)의 유물. 없으면 None.</summary>
        internal RunDemoRelic OfferedRelic(int slot)
        {
            if (Route.Current != RunDemoRoute.Node.Merchant) return RunDemoRelic.None;
            int value = slot == 0 ? Offer0 : slot == 1 ? Offer1 : -1;
            return value > 0 ? (RunDemoRelic)value : RunDemoRelic.None;
        }

        internal bool CanBuyAtMerchant(int option) => CanBuyAtMerchant((MerchantItem)option);

        /// <summary>상인 항목을 지금 살 수 있는가(금화, 이미 샀는지, 항목 조건).</summary>
        internal bool CanBuyAtMerchant(MerchantItem item)
        {
            if (Carryover == null || Route.Current != RunDemoRoute.Node.Merchant || Route.CurrentResult != RunDemoRoute.Result.Pending) return false;
            if (item < MerchantItem.HeartRepair || item > MerchantItem.Relic1 || HasBoughtAtMerchant(item) || Carryover.Gold < PriceOf(item)) return false;
            if (MerchantLimitReached) return false;
            return item switch
            {
                MerchantItem.HeartRepair => Carryover.HeartHealth < _encounter.Settings.HeartHealth,
                MerchantItem.TrapUpgrade => TrapBonus == 0,
                MerchantItem.Revive => FirstLostMinion >= 0,
                MerchantItem.Capacity => ExtraCapacity < MaxExtraCapacity,
                MerchantItem.NewMinion => ExtraMinionKind < 0,
                MerchantItem.Relic0 => OfferedRelic(0) != RunDemoRelic.None && !Has(OfferedRelic(0)),
                _ => OfferedRelic(1) != RunDemoRelic.None && !Has(OfferedRelic(1)),
            };
        }

        internal bool HasBoughtAtMerchant(int option) => (MerchantBought & (1 << option)) != 0;
        internal bool HasBoughtAtMerchant(MerchantItem item) => HasBoughtAtMerchant((int)item);

        /// <summary>상인 항목 하나를 산다. 새 부하는 <paramref name="kind"/>(에셋 부하 목록 번호)를 고른다.</summary>
        internal bool TryBuyAtMerchant(MerchantItem item, int kind = -1)
        {
            if (!CanBuyAtMerchant(item)) return false;
            switch (item)
            {
                case MerchantItem.HeartRepair:
                    Carryover.HeartHealth = Math.Min(_encounter.Settings.HeartHealth, Carryover.HeartHealth + 10);
                    break;
                case MerchantItem.TrapUpgrade:
                    TrapBonus = 2;
                    break;
                // 고용: 쓰러진 부하 한 명을 같은 종류로 체력 가득 채운다(2026-11-10 사용자 결정, 금화 30).
                case MerchantItem.Revive:
                    int lost = FirstLostMinion;
                    Carryover.MinionHealth[lost] = Minion(lost).MaxHealth;
                    RevivedMinions |= 1 << lost;
                    break;
                case MerchantItem.Capacity:
                    ExtraCapacity++;
                    break;
                // 새 부하: 4종 중 고른 하나가 5번째로 체력 가득 들어온다. 자리는 다음 준비 화면에서 정한다(2026-11-11 사용자 결정).
                case MerchantItem.NewMinion:
                    if (kind < 0 || kind >= _encounter.Minions.Length || _encounter.Minions[kind].Combatant == null) return false;
                    ExtraMinionKind = kind;
                    InvalidateRoster();
                    int[] health = Carryover.MinionHealth;
                    Array.Resize(ref health, MinionCount);
                    health[^1] = Minion(MinionCount - 1).MaxHealth;
                    Carryover.MinionHealth = health;
                    break;
                default:
                    GainRelic(OfferedRelic(item == MerchantItem.Relic0 ? 0 : 1));
                    break;
            }
            Carryover.Gold -= PriceOf(item);
            MerchantBought |= 1 << (int)item;
            return true;
        }

        // ── 던전 정비(전투·원정 사이) ──

        /// <summary>고칠 수 있는 방인가: 시스템 방(함정·치유 샘·문 제어실)이 고장 났거나 내구가 깎였다.</summary>
        internal bool IsRoomDamaged(int room)
        {
            if (Carryover == null || !IsSystemRoom(room)) return false;
            return Carryover.RoomBroken[room] || Carryover.RoomDurability[room] < _encounter.Settings.RoomDurability;
        }

        /// <summary>치료할 수 있는 부하인가: 살아 있고 체력이 깎였다.</summary>
        internal bool IsMinionHurt(int index)
        {
            if (!IsMinionAlive(index)) return false;
            return Carryover.MinionHealth[index] < Minion(index).MaxHealth;
        }

        internal bool IsMinionAlive(int index) =>
            Carryover != null && index >= 0 && index < Carryover.MinionHealth.Length && Carryover.MinionHealth[index] > 0;

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
            Carryover.MinionHealth[index] = Minion(index).MaxHealth;
            Carryover.Gold -= HealPrice;
            return true;
        }

        // 정비 창의 잃은 부하 고용(60)과 마력 용량(60)은 상인으로 옮겨 지웠다(2026-11-11 사용자 결정 "매번 뜨되 일부만").

        // ── 글 사건: 대가 있는 교환 ──

        /// <summary>글 사건 칸(0·1)의 교환. 없으면 null.</summary>
        internal RunDemoExchange? OfferedExchange(int slot)
        {
            if (Route.Current != RunDemoRoute.Node.StoryEvent) return null;
            int value = slot == 0 ? Offer0 : slot == 1 ? Offer1 : -1;
            return value >= 0 ? (RunDemoExchange)value : null;
        }

        /// <summary>이 교환을 이 대상으로 할 수 있는가. 부하 교환은 부하 번호, 방 교환은 방 번호다.</summary>
        internal bool CanExchange(RunDemoExchange exchange, int target = -1)
        {
            if (Carryover == null || Route.Current != RunDemoRoute.Node.StoryEvent || Route.CurrentResult != RunDemoRoute.Result.Pending) return false;
            if (OfferedExchange(0) != exchange && OfferedExchange(1) != exchange) return false;
            return exchange switch
            {
                RunDemoExchange.HeartForUpgrade => Carryover.HeartHealth > Economy.HeartUpgradeCost && IsMinionAlive(target),
                RunDemoExchange.GoldForUpgrade => Carryover.Gold >= Economy.UpgradePrice && IsMinionAlive(target),
                RunDemoExchange.HealthForRelic => IsMinionAlive(target) && Relics != RunDemoRelic.All,
                _ => IsSystemRoom(target) && !Carryover.RoomBroken[target] && Relics != RunDemoRelic.All,
            };
        }

        /// <summary>교환을 하고 칸을 끝낸다. 떠나기는 <see cref="ResolveBranch"/>(-1).</summary>
        internal bool TryExchange(RunDemoExchange exchange, int target = -1)
        {
            if (!CanExchange(exchange, target)) return false;
            switch (exchange)
            {
                // 금화 대신 심장으로 내는 강화(2026-11-11 사용자 결정, "심장 → 금화"는 글 사건 길에서 금화 쓸 곳이 없어 손해만 났다).
                case RunDemoExchange.HeartForUpgrade:
                    Carryover.HeartHealth -= Economy.HeartUpgradeCost;
                    UpgradeMinion(target);
                    break;
                case RunDemoExchange.GoldForUpgrade:
                    Carryover.Gold -= Economy.UpgradePrice;
                    UpgradeMinion(target);
                    break;
                // 최대 체력의 일부를 잃되 쓰러지지는 않는다(시뮬레이션용 임시 해석).
                case RunDemoExchange.HealthForRelic:
                    Carryover.MinionHealth[target] = Math.Max(1, Carryover.MinionHealth[target] - HealthForRelicLoss(target));
                    GainRelic(DrawMissingRelic(17));
                    break;
                // 마력 칸이 거의 차 있어 용량 +1이 쓸모없었다. 방을 고장 낸 대가로 유물을 준다(2026-11-11 사용자 결정).
                default:
                    Carryover.RoomBroken[target] = true;
                    Carryover.RoomDurability[target] = 0f;
                    GainRelic(DrawMissingRelic(23));
                    break;
            }
            return Route.TryResolve(RunDemoRoute.Result.Completed);
        }

        // 최대 체력이 오른 만큼 지금 체력도 오른다(시뮬레이션용 임시 해석, 시연 런 기획에 적음).
        private void UpgradeMinion(int target)
        {
            _minionUpgrades[target]++;
            InvalidateRoster();
            Carryover.MinionHealth[target] = Math.Min(Minion(target).MaxHealth, Carryover.MinionHealth[target] + Economy.UpgradeHealth);
        }

        /// <summary>
        /// 상인·글 사건 칸의 선택. -1은 떠나기(칸을 끝냄), 상인의 0~2는 예전 항목이다.
        /// 글 사건은 떠나기만 받는다(교환은 <see cref="TryExchange"/>). 약초꾼 치료는 교환으로 바꿨다(2026-11-11 사용자 결정).
        /// </summary>
        internal bool ResolveBranch(int option)
        {
            if (Route.CurrentResult != RunDemoRoute.Result.Pending || Carryover == null) return false;
            if (Route.Current == RunDemoRoute.Node.Merchant)
            {
                // 떠나기(-1)만 칸을 끝낸다. 항목은 하나씩 사고, 같은 항목은 한 번만 산다(2026-11-10 사용자 승인).
                // 한 방문에서는 두 개까지다(2026-11-11 사용자 결정 "두 개로 제한").
                if (option == -1)
                {
                    MerchantBought = 0;
                    return Route.TryResolve(RunDemoRoute.Result.Completed);
                }
                return option is >= 0 and <= 2 && TryBuyAtMerchant((MerchantItem)option);
            }
            if (Route.Current != RunDemoRoute.Node.StoryEvent || option != -1) return false;
            return Route.TryResolve(RunDemoRoute.Result.Completed);
        }

        /// <summary>
        /// 지금 상인·글 사건 칸에 제안이 없으면 뽑는다. 제안을 저장하기 전의 저장으로 칸 안에서 이어 할 때를 위한 것이다.
        /// </summary>
        internal void EnsureBranchOffers()
        {
            if (Route.CurrentResult != RunDemoRoute.Result.Pending || Offer0 >= 0 || Offer1 >= 0) return;
            if (Route.Current == RunDemoRoute.Node.Merchant) DrawRelicOffer();
            else if (Route.Current == RunDemoRoute.Node.StoryEvent) DrawExchangeOffer();
        }

        /// <summary>교환 대상 부하가 잃는 체력("부하 체력 → 유물").</summary>
        internal int HealthForRelicLoss(int target) => (int)Math.Round(Minion(target).MaxHealth * Economy.HealthForRelicRatio);

        // ── 원정 ──

        internal RunBattle CreateRaid(IReadOnlyList<int> party, bool detour)
        {
            RunBattle raid = new(_encounter.Settings.ForDemo(0, 0, false), RaidLayout, 0, true);
            raid.SetInitialResources(Carryover.HeartHealth, Carryover.Gold);
            foreach (int index in party)
            {
                RunCombatant minion = raid.AddCombatant(Minion(index), 0);
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
            RevivedMinions = 0;
        }

        // ── 저장 ──

        internal void Restore(RunDemoRoute route, RunDemoSave.Checkpoint checkpoint)
        {
            Route = route;
            Carryover = checkpoint.Carryover;
            TrapBonus = checkpoint.TrapBonus;
            ExtraCapacity = Math.Clamp(checkpoint.ExtraCapacity, 0, MaxExtraCapacity);
            MerchantBought = checkpoint.MerchantBought & MerchantItemMask;
            RevivedMinions = Math.Max(0, checkpoint.RevivedMinions);
            if (checkpoint.Seed != 0) Seed = checkpoint.Seed;
            Relics = (RunDemoRelic)checkpoint.Relics & RunDemoRelic.All;
            ExtraMinionKind = checkpoint.ExtraMinionKind is >= 0 && checkpoint.ExtraMinionKind < _encounter.Minions.Length ? checkpoint.ExtraMinionKind : -1;
            Offer0 = checkpoint.Offer0;
            Offer1 = checkpoint.Offer1;
            Array.Clear(_minionUpgrades, 0, _minionUpgrades.Length);
            if (checkpoint.MinionUpgrades != null)
                Array.Copy(checkpoint.MinionUpgrades, _minionUpgrades, Math.Min(_minionUpgrades.Length, checkpoint.MinionUpgrades.Length));
            InvalidateRoster();
        }

        internal int[] CopyUpgrades() => (int[])_minionUpgrades.Clone();

        public void Dispose()
        {
            DestroyVariants();
            Destroy(_elite);
            Destroy(_commander);
            Destroy(_raidArcher);
        }

        // ── 보조 ──

        private bool IsSystemRoom(int room)
        {
            if (Carryover == null || room < 0 || room >= Carryover.RoomBroken.Length) return false;
            return _encounter.GridLayout[room].Kind is RunRoomKind.Trap or RunRoomKind.HealingSpring or RunRoomKind.DoorControl;
        }

        private void GainRelic(RunDemoRelic relic)
        {
            if (relic == RunDemoRelic.None || Has(relic)) return;
            Relics |= relic;
            if (relic != RunDemoRelic.ThickBones) return;
            InvalidateRoster();
            // 최대 체력이 오른 만큼 살아 있는 부하의 지금 체력도 오른다(시뮬레이션용 임시 해석).
            for (int i = 0; i < Carryover.MinionHealth.Length; i++)
                if (Carryover.MinionHealth[i] > 0)
                    Carryover.MinionHealth[i] = Math.Min(Minion(i).MaxHealth, Carryover.MinionHealth[i] + ThickBonesHealth);
        }

        // 방문(첫 갈래 0, 원정 뒤 1)마다 정해지는 순서. 저장한 시드라 다시 불러도 같다.
        private System.Random VisitRandom(int salt) => new(unchecked(Seed * 31 + (Route.IsSecondBranch ? 1 : 0) * 7919 + salt));

        private void DrawRelicOffer()
        {
            List<RunDemoRelic> pool = new(RelicOrder.Length);
            foreach (RunDemoRelic relic in RelicOrder) if (!Has(relic)) pool.Add(relic);
            Shuffle(pool, VisitRandom(1));
            Offer0 = pool.Count > 0 ? (int)pool[0] : -1;
            Offer1 = pool.Count > 1 ? (int)pool[1] : -1;
        }

        private void DrawExchangeOffer()
        {
            List<int> pool = new() { 0, 1, 2, 3 };
            Shuffle(pool, VisitRandom(2));
            Offer0 = pool[0];
            Offer1 = pool[1];
        }

        private RunDemoRelic DrawMissingRelic(int salt)
        {
            List<RunDemoRelic> pool = new(RelicOrder.Length);
            foreach (RunDemoRelic relic in RelicOrder) if (!Has(relic)) pool.Add(relic);
            return pool.Count == 0 ? RunDemoRelic.None : pool[VisitRandom(salt).Next(pool.Count)];
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private RunPlacement[] BuildRoster()
        {
            RunPlacement[] assets = _encounter.Minions;
            int count = assets.Length + (ExtraMinionKind >= 0 ? 1 : 0);
            RunPlacement[] roster = new RunPlacement[count];
            for (int i = 0; i < count; i++)
            {
                RunPlacement source = i < assets.Length ? assets[i] : new RunPlacement(assets[ExtraMinionKind].Combatant, HeartRoom());
                int health = (Has(RunDemoRelic.ThickBones) ? ThickBonesHealth : 0) + _minionUpgrades[i] * Economy.UpgradeHealth;
                int attack = _minionUpgrades[i] * Economy.UpgradeAttack;
                if (source.Combatant == null || (health == 0 && attack == 0))
                {
                    roster[i] = source;
                    continue;
                }
                RunCombatantSO variant = source.Combatant.CreateDemoVariant(source.Combatant.DisplayName, healthBonus: 0f);
                variant.AddDemoStats(health, attack);
                _variants.Add(variant);
                roster[i] = new RunPlacement(variant, source.Room);
            }
            return roster;
        }

        private int HeartRoom()
        {
            for (int i = 0; i < _encounter.GridLayout.Length; i++)
                if (_encounter.GridLayout[i].Kind == RunRoomKind.Heart) return i;
            return 0;
        }

        // 끝난 전투 화면이 예전 사본을 아직 그릴 수 있어 사본은 런이 끝날 때(Dispose) 한꺼번에 지운다.
        private void InvalidateRoster() => _roster = null;

        private void DestroyVariants()
        {
            foreach (RunCombatantSO variant in _variants) Destroy(variant);
            _variants.Clear();
        }

        private static void Destroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        private static int NewSeed()
        {
            int seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
            return seed != 0 ? seed : 1;
        }

        private RunIntruderEntry Find(string assetName)
        {
            foreach (RunIntruderEntry entry in _encounter.Party)
                if (entry.Combatant != null && entry.Combatant.name == assetName) return entry;
            throw new InvalidOperationException($"시연 전투 데이터가 없습니다 ({assetName}).");
        }
    }
}
