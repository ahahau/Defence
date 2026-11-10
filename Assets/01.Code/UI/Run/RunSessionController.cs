using System;
using System.Collections.Generic;
using System.Text;
using Code.Run;
using Code.UI.Popup;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>준비와 위기 화면의 중계. 상태 감지는 순수 규칙에 맡기고 바뀐 순간에만 문구를 만든다.</summary>
    public class RunSessionController : MonoBehaviour
    {
        [SerializeField] private RunPreparationView preparation;
        [SerializeField] private RunCrisisView crisisView;
        [SerializeField] private RunTrapControlView trapControl;
        private int _trapRoom = -1;
        private int _queuedTrap = -1;
        private bool _trapCancelled;
        private readonly RunDeploymentMemory _memory = new();
        private readonly StringBuilder _text = new();
        private RunBattleFlow _flow;
        private RunDemoCampaign _demo;
        private RunBattle _raid;
        private readonly List<int> _raiders = new(3);
        private bool _saveBlocked;
        private RunCrisisMonitor _monitor;
        private bool[] _highlighted;
        private bool[] _danger;
        private string[] _dangerLabels;
        private string[] _roomNames;
        private int _selection = -1;
        private bool _bound;

        public event Action Started;
        public event Action Prepared;
        public event Action PlacementChanged;
        public event Action<int> MinionSelected;
        public event Action<int, Vector2> MinionDropped;
        public event Action CrisisStarted;
        public RunBattle Battle => _raid ?? _flow.Battle;
        public RunDeployment Deployment => _flow.Deployment;
        public bool IsPreparing => _raid == null && _flow.Phase == RunBattlePhase.Preparation;
        public bool IsResult => _raid == null ? _flow.Phase == RunBattlePhase.Result : _raid.Outcome != RunBattleOutcome.Ongoing;
        public int EncounterNumber => _flow.EncounterNumber;
        public bool CanPrepareNext => IsResult || (_raid != null && !_raid.IsRaidRetreating);
        public string NextLabel => _raid != null && !IsResult ? "후퇴" : _demo.Route.IsFinished ? "정산" : "구역 지도";
        public bool IsRaidNode => _demo.Route.Current == RunDemoRoute.Node.OutpostRaid;
        public RunDemoRoute.Result RaidResult => _demo.Route.RaidResult;
        public bool IsCrisis { get; private set; }
        public bool[] Highlighted => _highlighted;
        public string RoomName(int room) => _raid == null ? RunBattleText.RoomName(Battle.Rooms[room].Kind) : room switch
        {
            0 => "진지 입구",
            1 => "경비실",
            2 => "대장실",
            3 => "옆 통로",
            4 => "보급실",
            _ => "뒷 통로",
        };

        private void OnEnable() => Bind();
        private void OnDestroy() => _demo?.Dispose();
        private void OnDisable()
        {
            if (!_bound) return;
            preparation.StartClicked -= StartBattle;
            preparation.RestoreClicked -= Restore;
            preparation.Selected -= Select;
            preparation.Dropped -= Drop;
            if (trapControl != null) trapControl.Activated -= RequestTrap;
            _bound = false;
        }

        private void Bind()
        {
            if (_bound || preparation == null) return;
            preparation.StartClicked += StartBattle;
            preparation.RestoreClicked += Restore;
            preparation.Selected += Select;
            preparation.Dropped += Drop;
            if (trapControl != null) trapControl.Activated += RequestTrap;
            _bound = true;
        }

        public void Prepare(RunEncounterSO encounter)
        {
            Bind();
            _demo?.Dispose();
            _raid = null;
            _demo = new RunDemoCampaign(encounter);
            _flow = new RunBattleFlow(encounter, _memory, _demo.DefenseSettings(1), _demo.DefenseParty(1));
            RestoreCheckpoint(encounter);
            ShowPreparation(encounter);
            if (_saveBlocked)
                PopupController.Show(PopupRequest.Notice("시연 저장 확인", "기존 저장을 보존했습니다. 이번 진행은 자동 저장되지 않습니다."));
            else if (IsPreparing) SaveCheckpoint();
        }

        public void PrepareNext() => OpenNextEncounter();

        public void OpenNextEncounter()
        {
            if (PopupController.IsOpen) return;
            if (_raid != null && !IsResult)
            {
                if (!_raid.IsRaidRetreating)
                    PopupController.Show(PopupRequest.Confirm("원정 후퇴", "부하가 진지 입구까지 걸어 나갑니다. 이동 중에도 공격받습니다.",
                        _raid.RequestRaidRetreat, confirmLabel: "후퇴").PauseGame());
                return;
            }
            if (!IsResult) return;
            RunDemoRoute route = _demo.Route;
            if (route.IsFinished)
            {
                ShowSettlement();
                return;
            }
            if (route.CurrentResult == RunDemoRoute.Result.Pending)
            {
                if (route.Current == RunDemoRoute.Node.OutpostRaid) BeginRaidSelection();
                else ShowDemoBranch();
                return;
            }
            if (route.Current == RunDemoRoute.Node.ThiefDefense)
            {
                PopupController.Show(PopupRequest.Choice("구역 지도 · 도적 침입",
                    "도적 침입 방어 완료\n상인 · 심장 수리 또는 함정 강화\n글 사건 · 부상 부하 한 명 회복\n이후 · 공병 침입 → 원정 → 정예 침입",
                    new[]
                    {
                        new PopupButtonSpec("닫기", PopupButtonStyle.Secondary, null),
                        new PopupButtonSpec("상인", PopupButtonStyle.Primary, () => EnterDemoNode(RunDemoRoute.Node.Merchant)),
                        new PopupButtonSpec("글 사건", PopupButtonStyle.Primary, () => EnterDemoNode(RunDemoRoute.Node.StoryEvent)),
                    }, () => { }));
                return;
            }
            RunDemoRoute.Node next = route.Current switch
            {
                RunDemoRoute.Node.Merchant or RunDemoRoute.Node.StoryEvent => RunDemoRoute.Node.SapperDefense,
                RunDemoRoute.Node.SapperDefense => RunDemoRoute.Node.OutpostRaid,
                _ => RunDemoRoute.Node.EliteDefense,
            };
            string label = NodeLabel(next);
            string current = NodeLabel(route.Current);
            string effect = next == RunDemoRoute.Node.EliteDefense
                ? $"마력 용량 {_demo.DefenseSettings(3).StartingManaCapacity} · 남은 전력으로 정예 침입 방어"
                : next == RunDemoRoute.Node.OutpostRaid ? "최대 3명 파견 · 진지 대장 처치" : "공병의 화약 폭파에 대비";
            PopupController.Show(PopupRequest.Confirm($"구역 지도 · {current}",
                $"다음 칸 · {label}\n{effect}", () => EnterDemoNode(next), confirmLabel: "이동"));
        }

        private void EnterDemoNode(RunDemoRoute.Node node)
        {
            if (!_demo.Route.TryEnter(node)) return;
            SaveCheckpoint();
            switch (node)
            {
                case RunDemoRoute.Node.Merchant:
                case RunDemoRoute.Node.StoryEvent:
                    ShowDemoBranch();
                    break;
                case RunDemoRoute.Node.SapperDefense:
                    PrepareDefense(2);
                    break;
                case RunDemoRoute.Node.EliteDefense:
                    PrepareDefense(3);
                    break;
                case RunDemoRoute.Node.OutpostRaid:
                    BeginRaidSelection();
                    break;
            }
        }

        private void ShowDemoBranch()
        {
            if (_demo.Route.Current == RunDemoRoute.Node.StoryEvent)
            {
                ShowHerbalist(0);
                return;
            }
            ShowMerchant();
        }

        // 범용 팝업 버튼이 3개까지라 상인은 두 단계로 보인다(2026-11-10 사용자 "두 단계 팝업").
        // 가격·가진 금화는 비용 줄, 살 수 없는 이유는 경고 줄에 둔다(2026-11-10 사용자 목업 승인).
        private void ShowMerchant()
        {
            RunBattleCarryover state = _demo.Carryover;
            int lost = _demo.FirstLostMinion;
            bool affordable = state.Gold >= RunDemoCampaign.MerchantPrice;
            string hire = lost >= 0 ? $"쓰러진 {Deployment.Encounter.Minions[lost].Combatant.DisplayName} 복귀" : "쓰러진 부하 없음";
            PopupController.Show(PopupRequest.Choice("상인",
                $"던전 정비 · 심장 수리나 함정 강화\n부하 고용 · {hire}\n한 가지만 살 수 있습니다.",
                new[]
                {
                    new PopupButtonSpec("떠나기", PopupButtonStyle.Secondary, () => CompleteBranch(-1)),
                    new PopupButtonSpec("던전 정비", PopupButtonStyle.Primary, ShowMerchantUpkeep),
                    new PopupButtonSpec("부하 고용", PopupButtonStyle.Primary, ShowMerchantHire, affordable && lost >= 0),
                }, () => { })
                .WithLines($"가진 금화 {state.Gold} · 각 금화 {RunDemoCampaign.MerchantPrice}", affordable ? null : "금화가 모자라 살 수 없습니다"));
        }

        private void ShowMerchantUpkeep()
        {
            RunBattleCarryover state = _demo.Carryover;
            int maxHeart = Deployment.Encounter.Settings.HeartHealth;
            bool affordable = state.Gold >= RunDemoCampaign.MerchantPrice;
            bool heartFull = state.HeartHealth >= maxHeart;
            string warning = !affordable ? "금화가 모자라 살 수 없습니다" : heartFull ? "심장이 가득 차 수리할 수 없습니다" : null;
            PopupController.Show(PopupRequest.Choice("상인 · 던전 정비",
                $"심장 수리 · 심장 +10 (지금 {state.HeartHealth}/{maxHeart})\n함정 강화 · 이번 런 함정 기본 피해 +2",
                new[]
                {
                    new PopupButtonSpec("뒤로", PopupButtonStyle.Secondary, ShowMerchant),
                    new PopupButtonSpec("심장 수리", PopupButtonStyle.Primary, () => CompleteBranch(0), affordable && !heartFull),
                    new PopupButtonSpec("함정 강화", PopupButtonStyle.Primary, () => CompleteBranch(1), affordable),
                }, ShowMerchant)
                .WithLines($"가진 금화 {state.Gold} · 가격 금화 {RunDemoCampaign.MerchantPrice}", warning));
        }

        private void ShowMerchantHire()
        {
            int lost = _demo.FirstLostMinion;
            if (lost < 0)
            {
                ShowMerchant();
                return;
            }
            RunCombatantSO minion = Deployment.Encounter.Minions[lost].Combatant;
            int gold = _demo.Carryover.Gold;
            bool affordable = gold >= RunDemoCampaign.MerchantPrice;
            PopupController.Show(PopupRequest.Choice("상인 · 부하 고용",
                $"돌아올 부하 · {minion.DisplayName}\n체력 {minion.MaxHealth}/{minion.MaxHealth} · 원래 자리에 섭니다",
                new[]
                {
                    new PopupButtonSpec("뒤로", PopupButtonStyle.Secondary, ShowMerchant),
                    new PopupButtonSpec("고용", PopupButtonStyle.Primary, () => CompleteBranch(2), affordable),
                }, ShowMerchant)
                .WithLines($"가진 금화 {gold} · 가격 금화 {RunDemoCampaign.MerchantPrice}", affordable ? null : "금화가 모자라 살 수 없습니다"));
        }

        private void ShowHerbalist(int from)
        {
            RunPlacement[] minions = Deployment.Encounter.Minions;
            int index = -1;
            int injured = 0;
            for (int offset = 0; offset < minions.Length; offset++)
            {
                int candidate = (from + offset) % minions.Length;
                int health = _demo.Carryover.MinionHealth[candidate];
                if (health <= 0 || health >= minions[candidate].Combatant.MaxHealth) continue;
                if (index < 0) index = candidate;
                injured++;
            }
            // 다친 부하 중 몇 번째인지(배치 순서 기준). "다른 부하"로 넘길 때 위치를 보인다.
            int ordinal = 0;
            for (int i = 0; index >= 0 && i <= index; i++)
            {
                int health = _demo.Carryover.MinionHealth[i];
                if (health > 0 && health < minions[i].Combatant.MaxHealth) ordinal++;
            }
            if (index < 0)
            {
                PopupController.Show(PopupRequest.Notice("굴 근처 약초꾼", "회복할 부하가 없습니다.",
                    () => CompleteBranch(-1), "떠나기"));
                return;
            }
            RunCombatantSO minion = minions[index].Combatant;
            int current = _demo.Carryover.MinionHealth[index];
            // 회복량은 RunDemoCampaign.ResolveBranch와 같은 식(최대 체력의 절반, 올림)이다.
            int healed = Math.Min(minion.MaxHealth, current + (minion.MaxHealth + 1) / 2);
            // 상인과 같은 형식: 항목마다 한 줄, 비용·인원은 비용 줄(2026-11-10 사용자 목업 승인).
            PopupController.Show(PopupRequest.Choice("굴 근처 약초꾼",
                $"치료할 부하 · {minion.DisplayName} ({ordinal}/{injured})\n체력 {current}/{minion.MaxHealth} → {healed}/{minion.MaxHealth}\n최대 체력의 절반만큼 회복합니다.",
                new[]
                {
                    new PopupButtonSpec("떠나기", PopupButtonStyle.Secondary, () => CompleteBranch(-1)),
                    new PopupButtonSpec("다른 부하", PopupButtonStyle.Secondary, () => ShowHerbalist(index + 1), injured > 1),
                    new PopupButtonSpec("치료", PopupButtonStyle.Primary, () => CompleteBranch(index)),
                }, () => { })
                .WithLines($"비용 없음 · 다친 부하 {injured}명", null));
        }

        private void CompleteBranch(int choice)
        {
            if (!_demo.ResolveBranch(choice)) return;
            // 끝난 전투가 지도 뒤 화면을 그리므로 고용·치료한 부하 체력까지 이어지는 값으로 맞춘다.
            _demo.Carryover.ApplyTo(Battle);
            SaveCheckpoint();
            OpenNextEncounter();
        }

        private void PrepareDefense(int number)
        {
            _raid = null;
            _flow.PrepareDemoDefense(number, _demo.DefenseSettings(number), _demo.DefenseParty(number), _demo.Carryover);
            ShowPreparation(Deployment.Encounter);
        }

        private void BeginRaidSelection()
        {
            _raiders.Clear();
            SelectRaider(0);
        }

        private void SelectRaider(int index)
        {
            RunPlacement[] minions = Deployment.Encounter.Minions;
            while (index < minions.Length && _demo.Carryover.MinionHealth[index] <= 0) index++;
            if (index == minions.Length || _raiders.Count == 3)
            {
                if (_raiders.Count != 0) ShowRaidRoute();
                else if (Array.Exists(_demo.Carryover.MinionHealth, health => health > 0)) BeginRaidSelection();
                else
                {
                    _demo.Route.TryResolve(RunDemoRoute.Result.Defeat);
                    SaveCheckpoint();
                    PopupController.Show(PopupRequest.Notice("원정 실패", "파견할 부하가 없습니다.", OpenNextEncounter));
                }
                return;
            }
            RunCombatantSO minion = minions[index].Combatant;
            int health = _demo.Carryover.MinionHealth[index];
            // 보상·위험을 비용 줄·경고 줄에 둔다(2026-11-10 사용자 목업 승인).
            _text.Clear().Append(minion.DisplayName).Append(" · 체력 ").Append(health).Append('/').Append(minion.MaxHealth).Append('\n');
            if (_raiders.Count > 0) _text.Append("지금까지 · ").Append(RaiderNames()).Append('\n');
            _text.Append("쓰러지면 이 런에서 잃습니다.");
            bool weak = health < minion.MaxHealth * Deployment.Encounter.Settings.RetreatHealthRatio;
            PopupController.Show(PopupRequest.Confirm($"원정 · 파견 인원 ({_raiders.Count}/3 선택)", _text.ToString(),
                    () => { _raiders.Add(index); SelectRaider(index + 1); }, () => SelectRaider(index + 1), "파견", "남기기")
                .WithLines("승리 보상 · 정예 침입 마력 용량 +1", weak ? "체력이 30% 아래입니다" : null));
        }

        private void ShowRaidRoute()
        {
            PopupController.Show(PopupRequest.Choice("원정 · 진입 경로",
                    "정면 · 가깝지만 검사와 궁수가 지킵니다\n우회 · 경비실을 피하지만 길이 깁니다\n진입 뒤 부하를 골라 다음 방으로 보냅니다.",
                    new[]
                    {
                        new PopupButtonSpec("다시 선택", PopupButtonStyle.Secondary, BeginRaidSelection),
                        new PopupButtonSpec("정면", PopupButtonStyle.Primary, () => StartRaid(false)),
                        new PopupButtonSpec("우회", PopupButtonStyle.Primary, () => StartRaid(true)),
                    }, () => { })
                .WithLines("파견 · " + RaiderNames(), null));
        }

        private string RaiderNames()
        {
            string names = string.Empty;
            foreach (int raider in _raiders)
                names += (names.Length > 0 ? ", " : string.Empty) + Deployment.Encounter.Minions[raider].Combatant.DisplayName;
            return names;
        }

        private void StartRaid(bool detour)
        {
            _raid = _demo.CreateRaid(_raiders, detour);
            _trapRoom = -1;
            IsCrisis = false;
            _monitor = new RunCrisisMonitor(_raid);
            _highlighted = new bool[_raid.Rooms.Count];
            _danger = new bool[_raid.Combatants.Count];
            _dangerLabels = new string[_danger.Length];
            _roomNames = new string[_raid.Rooms.Count];
            for (int i = 0; i < _roomNames.Length; i++) _roomNames[i] = RoomName(i);
            crisisView.ShowCrisis(null);
            crisisView.ClearNotice();
            BindTrap();
            Started?.Invoke();
        }

        private void ShowSettlement()
        {
            // 지나온 칸·남은 부하·잃은 부하를 줄마다, 심장·금화는 비용 줄, 패배 이유는 경고 줄에 둔다(2026-11-10 사용자 목업 승인).
            RunBattleCarryover state = _demo.Carryover;
            RunDemoRoute route = _demo.Route;
            bool won = route.CurrentResult == RunDemoRoute.Result.Victory;
            _text.Clear();
            AppendRouteStep(RunDemoRoute.Node.ThiefDefense, route);
            if (route.ChosenBranch is RunDemoRoute.Node branch && route.Current != RunDemoRoute.Node.ThiefDefense)
                _text.Append(" → ").Append(NodeLabel(branch));
            AppendRouteStep(RunDemoRoute.Node.SapperDefense, route);
            AppendRouteStep(RunDemoRoute.Node.OutpostRaid, route);
            AppendRouteStep(RunDemoRoute.Node.EliteDefense, route);
            _text.Append("\n남은 부하 · ");
            AppendMinions(state, alive: true);
            _text.Append("\n잃은 부하 · ");
            AppendMinions(state, alive: false);
            int maxHeart = Deployment.Encounter.Settings.HeartHealth;
            string warning = won ? null : state.HeartHealth <= 0 ? "심장이 무너졌습니다" : "방어에 실패했습니다";
            PopupController.Show(PopupRequest.Notice(won ? "시연 런 방어 성공" : "시연 런 패배", _text.ToString(),
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene("Start"), "타이틀로")
                .WithLines($"심장 {state.HeartHealth}/{maxHeart} · 금화 {state.Gold}", warning));
        }

        // 지나온 칸만 "도적 승리"처럼 짧게 붙인다. 앞 칸은 이겨야 다음으로 가므로 지금 칸만 결과를 따로 읽는다.
        private void AppendRouteStep(RunDemoRoute.Node node, RunDemoRoute route)
        {
            if (node > route.Current) return;
            string name = node switch
            {
                RunDemoRoute.Node.ThiefDefense => "도적",
                RunDemoRoute.Node.SapperDefense => "공병",
                RunDemoRoute.Node.OutpostRaid => "원정",
                _ => "정예",
            };
            string result = node == RunDemoRoute.Node.OutpostRaid
                ? route.RaidResult switch
                {
                    RunDemoRoute.Result.Victory => "승리",
                    RunDemoRoute.Result.Retreat => "후퇴",
                    _ => "실패",
                }
                : node == route.Current && route.CurrentResult != RunDemoRoute.Result.Victory ? "패배" : "승리";
            if (_text.Length > 0) _text.Append(" → ");
            _text.Append(name).Append(' ').Append(result);
        }

        private void AppendMinions(RunBattleCarryover state, bool alive)
        {
            int start = _text.Length;
            for (int i = 0; i < state.MinionHealth.Length; i++)
            {
                if (state.MinionHealth[i] > 0 != alive) continue;
                RunCombatantSO minion = Deployment.Encounter.Minions[i].Combatant;
                if (_text.Length > start) _text.Append(", ");
                _text.Append(minion.DisplayName);
                if (alive) _text.Append(' ').Append(state.MinionHealth[i]).Append('/').Append(minion.MaxHealth);
            }
            if (_text.Length == start) _text.Append("없음");
        }

        private static string NodeLabel(RunDemoRoute.Node node) => node switch
        {
            RunDemoRoute.Node.ThiefDefense => "도적 침입",
            RunDemoRoute.Node.Merchant => "상인",
            RunDemoRoute.Node.StoryEvent => "글 사건",
            RunDemoRoute.Node.SapperDefense => "공병 침입",
            RunDemoRoute.Node.OutpostRaid => "모험가 진지 원정",
            _ => "정예 침입",
        };

        private void RestoreCheckpoint(RunEncounterSO encounter)
        {
            RunDemoSave.Checkpoint checkpoint = RunDemoSave.Load(out _saveBlocked);
            if (checkpoint == null || checkpoint.Carryover == null) return;
            RunBattleCarryover carryover = checkpoint.Carryover;
            if (carryover.MinionHealth.Length != encounter.Minions.Length || carryover.RoomDurability.Length != encounter.GridLayout.Length)
            {
                _saveBlocked = true;
                Debug.LogWarning("현재 던전 구성과 다른 시연 저장을 보존합니다.", this);
                return;
            }
            RunDemoRoute route = checkpoint.GetRoute();
            _demo.Restore(route, carryover, checkpoint.TrapBonus);
            int number = route.Current switch
            {
                RunDemoRoute.Node.ThiefDefense or RunDemoRoute.Node.Merchant or RunDemoRoute.Node.StoryEvent => 1,
                RunDemoRoute.Node.EliteDefense => 3,
                _ => 2,
            };
            _flow.PrepareDemoDefense(number, _demo.DefenseSettings(number), _demo.DefenseParty(number), carryover);
            bool defense = route.Current is RunDemoRoute.Node.ThiefDefense or RunDemoRoute.Node.SapperDefense or RunDemoRoute.Node.EliteDefense;
            if (!defense || route.CurrentResult != RunDemoRoute.Result.Pending)
                _flow.RestoreDemoResult(route.IsFinished && route.CurrentResult == RunDemoRoute.Result.Defeat
                    ? RunBattleOutcome.Defeat : RunBattleOutcome.Victory);
        }

        private void SaveCheckpoint()
        {
            if (_saveBlocked || RunDemoSave.Save(_demo)) return;
            _saveBlocked = true;
            PopupController.Show(PopupRequest.Notice("시연 자동 저장 중단", "기존 저장을 보존했습니다. 이번 진행은 이어하기에 반영되지 않습니다."));
        }

        private void ShowPreparation(RunEncounterSO encounter)
        {
            _trapRoom = -1;
            for (int i = 0; i < Battle.Rooms.Count; i++)
                if (Battle.Rooms[i].Kind == RunRoomKind.Trap) { _trapRoom = i; break; }
            BindTrap();
            _selection = -1;
            _highlighted = new bool[Battle.Rooms.Count];
            _danger = new bool[Battle.Combatants.Count];
            _dangerLabels = new string[_danger.Length];
            _roomNames = new string[Battle.Rooms.Count];
            for (int i = 0; i < _roomNames.Length; i++) _roomNames[i] = RunBattleText.RoomName(Battle.Rooms[i].Kind);
            IsCrisis = false;
            crisisView.ShowCrisis(null);
            crisisView.ClearNotice();
            preparation.Show(IsPreparing);
            RunIntruderEntry[] party = _flow.CurrentParty;
            for (int i = 0; i < preparation.PartyCapacity; i++)
            {
                if (i >= party.Length)
                {
                    preparation.SetParty(i, string.Empty);
                    continue;
                }
                RunIntruderEntry entry = party[i];
                RunCombatantSO data = entry.Combatant;
                RunAffinity affinity = entry.OverrideAffinity ? entry.Affinity : data.Affinity;
                string effect = data.Skill != null ? data.Skill.DisplayName : "큰 기술 없음";
                if (data.Skill != null)
                    foreach (RunTurnSkillEffectSO skillEffect in data.Skill.TurnEffects)
                        if (skillEffect is RunTelegraphSkillEffectSO warning)
                        {
                            string area = warning.Kind switch
                            {
                                RunTelegraphKind.PowderBlast => "방 전체 폭파",
                                RunTelegraphKind.Fireball => "목표 방 화염구",
                                RunTelegraphKind.Charge => "일자 줄 돌진",
                                _ => "지정 부하 사격",
                            };
                            effect = $"{warning.WarningSeconds:0.#}초 뒤 {area}";
                            break;
                        }
                string goal = data.IntruderRole == RunIntruderRole.Sapper ? "마력 켜진 방 시스템"
                    : data.Goal == RunGoal.Vault ? "금고" : "심장";
                preparation.SetParty(i, data.DisplayName + "  " + HudPalette.Paint(AffinityName(affinity), RunVisualPalette.Affinity(affinity))
                    + "\n" + effect + "\n목표 · " + goal);
            }
            RefreshPreparation();
            Prepared?.Invoke();
        }

        private static string AffinityName(RunAffinity affinity) => affinity switch
        {
            RunAffinity.Magic => "● 마법",
            RunAffinity.Curse => "● 저주",
            _ => "● 물리",
        };

        public void Select(int index)
        {
            if (!CanPlaceMinion(index)) return;
            _selection = index;
            RefreshPreparation();
            MinionSelected?.Invoke(index);
        }

        private void Drop(int index, Vector2 point) => MinionDropped?.Invoke(index, point);

        public bool Place(int index, int room)
        {
            if (!CanPlaceMinion(index) || !Deployment.Place(index, room)) return false;
            RefreshPreparation();
            PlacementChanged?.Invoke();
            return true;
        }

        private bool CanPlaceMinion(int index) => IsPreparing && index >= 0 && index < Deployment.MinionCount
            && index < Battle.Combatants.Count && Battle.Combatants[index].Side == RunSide.Minion
            && Battle.Combatants[index].IsActive;

        public bool ChangeMana(int room, int amount)
        {
            if (!IsPreparing) return Battle.SetMana(room, amount);
            if (!Deployment.SetMana(room, amount)) return false;
            Battle.SetMana(room, amount);
            RefreshPreparation();
            return true;
        }

        private void Restore()
        {
            if (!_flow.RestorePrevious()) return;
            for (int i = 0; i < Battle.Rooms.Count; i++) Battle.SetMana(i, 0);
            for (int i = 0; i < Battle.Rooms.Count; i++) Battle.SetMana(i, Deployment.ManaAt(i));
            RefreshPreparation();
            PlacementChanged?.Invoke();
        }

        private void RefreshPreparation()
        {
            for (int i = 0; i < Deployment.MinionCount; i++)
            {
                RunCombatantSO data = Deployment.Encounter.Minions[i].Combatant;
                bool available = CanPlaceMinion(i);
                preparation.SetMinion(i, data.DisplayName + "\n배치됨 · " + _roomNames[Deployment.RoomOf(i)],
                    RunVisualPalette.Affinity(data.Affinity), i == _selection, available);
            }
            string detail = "부하 카드 또는 지도 위 부하를\n끌어 놓거나 눌러 방을 고르세요.";
            if (_selection >= 0)
            {
                RunCombatantSO data = Deployment.Encounter.Minions[_selection].Combatant;
                detail = data.DisplayName + $" · 체력 {Battle.Combatants[_selection].Health}/{data.MaxHealth} · 공격 {data.Attack}\n"
                    + (data.Skill != null ? data.Skill.DisplayName + ": " + data.Skill.Description : "큰 기술 없음");
            }
            int slots = 0;
            for (int i = 0; i < Deployment.RoomCount; i++) slots += Deployment.MaxMana(i);
            preparation.SetSummary($"남은 마력 {Deployment.Capacity - Deployment.ManaInUse}/{Deployment.Capacity}\n칸 {slots}개 중 {Deployment.Capacity}개만",
                $"침입 {EncounterNumber}/{RunBattleFlow.EncounterLimit} · 심장 {Battle.HeartHealth} · 금화 {Battle.VaultGold}\n{detail}", _memory.HasPrevious);
        }

        public void StartBattle()
        {
            if (!_flow.StartBattle()) return;
            _monitor = new RunCrisisMonitor(Battle);
            BindTrap();
            preparation.Show(false);
            Started?.Invoke();
        }

        public void Advance(float seconds)
        {
            if (IsPreparing || IsResult || IsCrisis || seconds <= 0f) return;
            if (_queuedTrap >= 0)
            {
                _trapCancelled = !Battle.TryActivateTrap(_queuedTrap);
                _queuedTrap = -1;
            }
            if (_raid != null) _raid.Tick(seconds);
            else _flow.Advance(seconds);
            if (IsResult)
            {
                if (_raid != null) _demo.ResolveRaid(_raid, _raiders);
                else _demo.ResolveDefense(Battle);
                SaveCheckpoint();
                return;
            }
            _monitor.Scan();
            bool pauses = false;
            _text.Clear();
            for (int i = 0; i < _monitor.Events.Count; i++)
            {
                RunCrisis item = _monitor.Events[i];
                if (!item.Pauses)
                {
                    string name = Battle.Combatants[item.Actor].Definition.DisplayName;
                    crisisView.Notify(item.Kind == RunCrisisKind.LowHealth ? name + " · 체력 30% 미만\n치유 샘의 마력을 확인하세요"
                        : name + " · 심장 방 진입\n심장을 지킬 부하를 보내세요");
                    continue;
                }
                pauses = true;
                Highlight(item);
                AppendCrisis(item);
                for (int actor = 0; actor < Battle.Combatants.Count; actor++)
                    if (_monitor.IsEndangered(Battle.Combatants[actor], item))
                    {
                        _danger[actor] = true;
                        int room = Battle.RoomOf(Battle.Combatants[actor]);
                        _dangerLabels[actor] = "위험 · " + (room >= 0 ? _roomNames[room] : "복도");
                    }
            }
            if (!pauses) return;
            IsCrisis = true;
            crisisView.ShowCrisis(_text.ToString());
            CrisisStarted?.Invoke();
        }

        private void AppendCrisis(RunCrisis item)
        {
            if (_text.Length > 0) _text.Append("\n\n");
            string room = item.Room >= 0 ? _roomNames[item.Room] : "복도";
            if (item.Kind == RunCrisisKind.RoomBroken)
            {
                _text.Append(room).Append(" · 방 고장\n마력을 다른 방으로 옮기기\n부하 보내 수리하기");
                return;
            }
            RunCombatant actor = Battle.Combatants[item.Actor];
            _text.Append(actor.Definition.DisplayName).Append(" · ").Append(room);
            if (item.Kind == RunCrisisKind.ThiefAtVault)
            {
                _text.Append(" · 금고 도착\n부하를 금고로 보내기\n금화를 들고 나가기 전에 막으세요");
                return;
            }
            RunTelegraph warning = actor.Telegraph;
            _text.Append(" · ").Append(warning.Skill.DisplayName).Append(' ').Append(warning.RemainingSeconds.ToString("0.0")).Append("초\n");
            _text.Append(warning.Kind switch
            {
                RunTelegraphKind.PowderBlast => "그 방 부하 빼기\n시전자 때려 끊기",
                RunTelegraphKind.Fireball => "그 방 부하 빼기\n시전자 때려 끊기 또는 문 잠그기",
                RunTelegraphKind.Charge => "그 줄에서 비키기\n문 잠그기",
                _ => "대상 부하를 방 밖으로 빼기\n방을 나가거나 사거리 밖이면 빗나갑니다",
            });
        }

        /// <summary>복도의 사격 대상과 일자 돌진도 이어진 방을 강조한다.</summary>
        private void Highlight(RunCrisis item)
        {
            if (item.Room >= 0) _highlighted[item.Room] = true;
            if (item.Kind != RunCrisisKind.Telegraph) return;
            RunTelegraph warning = Battle.Combatants[item.Actor].Telegraph;
            if (warning.Scope == RunTelegraphScope.StraightLine)
                for (int i = 0; i < warning.PathCount; i++)
                {
                    HighlightPosition(warning.PathStart(i));
                    HighlightPosition(warning.PathEnd(i));
                }
            else if (item.Room < 0 && warning.Target != null) HighlightPosition(warning.Target.Position);
        }

        private void HighlightPosition(RunPosition position)
        {
            if (position.IsInRoom) _highlighted[position.Room] = true;
            else if (position.IsAtEntrance) _highlighted[Battle.EntryRoom] = true;
            else
            {
                RunLink link = Battle.Links[position.Link];
                _highlighted[link.From] = true;
                _highlighted[link.To] = true;
            }
        }

        public string DangerLabel(int actor) => IsCrisis && _danger[actor] ? _dangerLabels[actor] : null;

        public void Resume()
        {
            IsCrisis = false;
            Array.Clear(_highlighted, 0, _highlighted.Length);
            Array.Clear(_danger, 0, _danger.Length);
            crisisView.ShowCrisis(null);
        }

        private void BindTrap()
        {
            _queuedTrap = -1;
            _trapCancelled = false;
            if (trapControl != null) trapControl.Bind(Battle, _trapRoom);
        }

        /// <summary>멈춤 중에도 명령만 예약한다. 실제 피해는 다음 시간 진행 직전에 판정한다.</summary>
        public void RequestTrap()
        {
            if (IsPreparing || IsResult || !Battle.CanActivateTrap(_trapRoom)) return;
            _queuedTrap = _trapRoom;
            _trapCancelled = false;
        }

        public void RefreshDisplay(float seconds)
        {
            crisisView.AdvanceDisplay(seconds);
            if (trapControl != null) trapControl.Render(!IsPreparing && !IsResult, _queuedTrap >= 0, _trapCancelled);
        }
        public void LayoutCrisis(Camera camera, RunGridView grid) => crisisView.LayoutLinks(camera, grid, _highlighted);
        public void Drag(Vector2 point) => preparation.MoveGhost(point);
        public void EndDrag() => preparation.HideGhost();
    }
}
