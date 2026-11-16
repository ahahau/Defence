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
        [SerializeField] private RunRouteMapView routeMap;
        [SerializeField, Tooltip("상인 좌판(RunMerchantBuilder). 비어 있으면 예전 두 단계 팝업을 쓴다.")]
        private RunMerchantView merchantWindow;
        [SerializeField, Tooltip("원정 파견 판(RunRaidSelectBuilder). 비어 있으면 예전처럼 한 명씩 묻는 팝업을 쓴다.")]
        private RunRaidSelectView raidSelectWindow;
        private RunRouteMapPresenter _map;
        private RunMerchantPresenter _merchant;
        private RunRaidSelectPresenter _raidSelect;
        private readonly RunExchangePresenter _exchange = new();
        private int _trapRoom = -1;
        private int _queuedTrap = -1;
        private bool _trapCancelled;
        private readonly RunDeploymentMemory _memory = new();
        private readonly StringBuilder _text = new();
        private readonly StringBuilder _notice = new();
        private RunBattleFlow _flow;
        private RunDemoCampaign _demo;
        private RunBattle _raid;
        private readonly List<int> _raiders = new(3);
        private bool _saveBlocked;
        // 끝난 방어·원정 칸에서 던전 정비 창을 이미 지나갔는가. 새 칸에 들어가면 다시 띄운다.
        private bool _upkeepDone;
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

        /// <summary>함정 조작 패널과 그 방. 전투 화면이 패널을 함정 방 옆에 붙인다.</summary>
        public RectTransform TrapPanel => trapControl != null ? trapControl.Panel : null;
        public int TrapRoom => _trapRoom;

        /// <summary>상인에서 살린 부하인가(다음 전투가 끝나기 전까지). 준비 화면 자리 안내에 쓴다.</summary>
        public bool WasRevived(int minion) => _demo != null && _demo.WasRevived(minion);

        /// <summary>첫 판 안내가 가리킬 부하 준비 카드.</summary>
        public RectTransform PreparationCard(int minion) => preparation != null ? preparation.MinionCardRect(minion) : null;
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
            if (routeMap != null)
            {
                routeMap.NodeClicked -= SelectMapNode;
                routeMap.GoClicked -= EnterSelectedNode;
            }
            if (_merchant != null)
            {
                _merchant.Bought -= HandleMerchantBought;
                _merchant.Left -= HandleMerchantLeft;
            }
            if (_raidSelect != null) _raidSelect.Confirmed -= HandleRaidersConfirmed;
            _exchange.Finished -= HandleExchangeFinished;
            _exchange.Exchanged -= HandleExchanged;
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
            if (routeMap != null)
            {
                _map ??= new RunRouteMapPresenter(routeMap);
                routeMap.NodeClicked += SelectMapNode;
                routeMap.GoClicked += EnterSelectedNode;
            }
            if (merchantWindow != null)
            {
                _merchant ??= new RunMerchantPresenter(merchantWindow);
                _merchant.Bought += HandleMerchantBought;
                _merchant.Left += HandleMerchantLeft;
            }
            if (raidSelectWindow != null)
            {
                _raidSelect ??= new RunRaidSelectPresenter(raidSelectWindow);
                _raidSelect.Confirmed += HandleRaidersConfirmed;
            }
            _exchange.Finished += HandleExchangeFinished;
            _exchange.Exchanged += HandleExchanged;
            _bound = true;
        }

        private void SelectMapNode(int index) => _map.Select(index);

        private void EnterSelectedNode()
        {
            if (_map.SelectedEnterable is not RunDemoRoute.Node node) return;
            routeMap.Hide();
            EnterDemoNode(node);
        }

        public void Prepare(RunEncounterSO encounter)
        {
            Bind();
            _demo?.Dispose();
            _raid = null;
            // 원정과 정예 침입 사이에도 상인/글 사건 갈래를 둔다(2026-11-11 사용자 결정). 예전 저장은 저장된 지도대로 이어진다.
            _demo = new RunDemoCampaign(encounter, postRaidBranch: true);
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
            if (PopupController.IsOpen || (routeMap != null && routeMap.IsOpen) || (_merchant != null && _merchant.IsOpen)
                || (_raidSelect != null && _raidSelect.IsOpen)) return;
            if (_raid != null && !IsResult)
            {
                if (!_raid.IsRaidRetreating)
                    PopupController.Show(PopupRequest.Confirm("원정 후퇴", "부하가 진지 입구까지 걸어 나갑니다. 이동 중에도 공격받습니다.",
                        _raid.RequestRaidRetreat, confirmLabel: "후퇴").PauseGame().WithAccent(HudPalette.Danger, null));
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
            if (!_upkeepDone && route.Current is not (RunDemoRoute.Node.Merchant or RunDemoRoute.Node.StoryEvent))
            {
                ShowUpkeep();
                return;
            }
            if (routeMap == null)
            {
                Debug.LogWarning("구역 지도 창이 연결되지 않았습니다(RunRouteMapBuilder.BuildAll).", this);
                return;
            }
            _map.Show(route, _demo, Deployment.Encounter);
        }

        private void EnterDemoNode(RunDemoRoute.Node node)
        {
            if (!_demo.EnterNode(node)) return;
            _upkeepDone = false;
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
            // 글 사건은 약초꾼 대신 교환 "굴 속 거래"다(2026-11-11 사용자 결정).
            if (_demo.Route.Current == RunDemoRoute.Node.StoryEvent)
            {
                _exchange.Show(_demo, Deployment.Encounter);
                return;
            }
            ShowMerchant();
        }

        // 범용 팝업 버튼이 3개까지라 상인은 두 단계로 보인다(2026-11-10 사용자 "두 단계 팝업").
        // 가격·가진 금화는 비용 줄, 살 수 없는 이유는 경고 줄에 둔다(2026-11-10 사용자 목업 승인).
        // 항목마다 한 번, 방문마다 두 개까지 산다(2026-11-11 사용자 결정). 떠나기를 눌러야 칸이 끝난다(2026-11-10 던전 정비 목업 승인).
        private void ShowMerchant()
        {
            // 좌판 창이 놓여 있으면 그것을 쓴다(2026-11-11 사용자 결정 "B 전용 좌판").
            if (_merchant != null)
            {
                _merchant.Show(_demo, Deployment.Encounter);
                return;
            }
            RunBattleCarryover state = _demo.Carryover;
            int lost = _demo.FirstLostMinion;
            bool affordable = state.Gold >= RunDemoCampaign.MerchantPrice;
            string warning = _demo.MerchantLimitReached ? "이번에는 두 개를 다 샀습니다" : affordable ? null : "금화가 모자라 살 수 없습니다";
            string hire = _demo.HasBoughtAtMerchant(2) ? "고용함" : lost >= 0 ? $"쓰러진 {_demo.Roster[lost].Combatant.DisplayName} 복귀" : "쓰러진 부하 없음";
            PopupController.Show(PopupRequest.Choice("상인",
                $"심장·함정 · 심장 수리나 함정 강화\n부하 고용 · {hire}\n한 번 들를 때 두 개까지 살 수 있습니다.",
                new[]
                {
                    new PopupButtonSpec("떠나기", PopupButtonStyle.Secondary, () => CompleteBranch(-1)),
                    new PopupButtonSpec("심장·함정", PopupButtonStyle.Primary, ShowMerchantUpkeep),
                    new PopupButtonSpec("부하 고용", PopupButtonStyle.Primary, ShowMerchantHire, _demo.CanBuyAtMerchant(2)),
                }, () => { })
                .WithLines($"가진 금화 {state.Gold} · 각 금화 {RunDemoCampaign.MerchantPrice}", warning)
                .WithAccent(HudPalette.Accent, "두 개까지 삽니다"));
        }

        private void ShowMerchantUpkeep()
        {
            RunBattleCarryover state = _demo.Carryover;
            int maxHeart = Deployment.Encounter.Settings.HeartHealth;
            bool affordable = state.Gold >= RunDemoCampaign.MerchantPrice;
            bool heartFull = state.HeartHealth >= maxHeart;
            string warning = _demo.MerchantLimitReached ? "이번에는 두 개를 다 샀습니다"
                : !affordable ? "금화가 모자라 살 수 없습니다"
                : _demo.HasBoughtAtMerchant(0) && _demo.HasBoughtAtMerchant(1) ? "둘 다 샀습니다"
                : heartFull && !_demo.HasBoughtAtMerchant(0) ? "심장이 가득 차 수리할 수 없습니다" : null;
            PopupController.Show(PopupRequest.Choice("상인 · 심장·함정",
                $"심장 수리 · 심장 +10 (지금 {state.HeartHealth}/{maxHeart}){(_demo.HasBoughtAtMerchant(0) ? " · 삼" : string.Empty)}\n함정 강화 · 이번 런 함정 기본 피해 +2{(_demo.HasBoughtAtMerchant(1) ? " · 삼" : string.Empty)}",
                new[]
                {
                    new PopupButtonSpec("뒤로", PopupButtonStyle.Secondary, ShowMerchant),
                    new PopupButtonSpec("심장 수리", PopupButtonStyle.Primary, () => CompleteBranch(0), _demo.CanBuyAtMerchant(0)),
                    new PopupButtonSpec("함정 강화", PopupButtonStyle.Primary, () => CompleteBranch(1), _demo.CanBuyAtMerchant(1)),
                }, ShowMerchant)
                .WithLines($"가진 금화 {state.Gold} · 가격 금화 {RunDemoCampaign.MerchantPrice}", warning)
                .WithAccent(HudPalette.Accent, null));
        }

        private void ShowMerchantHire()
        {
            int lost = _demo.FirstLostMinion;
            if (lost < 0 || _demo.HasBoughtAtMerchant(2))
            {
                ShowMerchant();
                return;
            }
            RunCombatantSO minion = _demo.Roster[lost].Combatant;
            int gold = _demo.Carryover.Gold;
            bool affordable = gold >= RunDemoCampaign.MerchantPrice;
            PopupController.Show(PopupRequest.Choice("상인 · 부하 고용",
                $"돌아올 부하 · {minion.DisplayName}\n체력 {minion.MaxHealth}/{minion.MaxHealth} · 원래 자리에 섭니다",
                new[]
                {
                    new PopupButtonSpec("뒤로", PopupButtonStyle.Secondary, ShowMerchant),
                    new PopupButtonSpec("고용", PopupButtonStyle.Primary, () => CompleteBranch(2), _demo.CanBuyAtMerchant(2)),
                }, ShowMerchant)
                .WithLines($"가진 금화 {gold} · 가격 금화 {RunDemoCampaign.MerchantPrice}",
                    _demo.MerchantLimitReached ? "이번에는 두 개를 다 샀습니다" : affordable ? null : "금화가 모자라 살 수 없습니다")
                .WithAccent(HudPalette.Accent, null));
        }

        // ── 던전 정비: 방어·원정이 끝날 때마다 지도보다 먼저 뜬다(2026-11-10 사용자 목업 승인) ──

        private void ShowUpkeep()
        {
            // 본문은 '이름 · 값' 대신 문장으로 쓴다(2026-11-10 사용자 목업 승인).
            RunBattleCarryover state = _demo.Carryover;
            _text.Clear();
            AppendRoomSentence(state, true, "고장 났습니다.");
            AppendRoomSentence(state, false, "내구가 깎였습니다.");
            bool hasDamagedRoom = _text.Length > 0;
            if (!hasDamagedRoom) _text.Append("고칠 방이 없습니다.");
            _text.Append('\n');
            int hurt = 0;
            string last = null;
            for (int i = 0; i < state.MinionHealth.Length; i++)
            {
                if (!_demo.IsMinionHurt(i)) continue;
                RunCombatantSO minion = _demo.Roster[i].Combatant;
                if (hurt++ > 0) _text.Append(", ");
                _text.Append(minion.DisplayName).Append('(').Append(state.MinionHealth[i]).Append('/').Append(minion.MaxHealth).Append(')');
                last = minion.DisplayName;
            }
            _text.Append(hurt == 0 ? "다친 부하가 없습니다." : Particle(last, "이", "가") + " 다쳤습니다.");
            // 정비 창은 수리·치료만 한다(2026-11-11 사용자 결정). 상인 안내 줄과 머리 글은 뺐다(2026-11-12 사용자 목업 승인).
            bool anyTarget = UpkeepTargetCount() > 0;
            PopupController.Show(PopupRequest.Choice("던전 정비", _text.ToString(),
                    new[]
                    {
                        new PopupButtonSpec("지도로", PopupButtonStyle.Secondary, LeaveUpkeep),
                        new PopupButtonSpec("수리·치료", PopupButtonStyle.Primary, () => ShowUpkeepTarget(0), anyTarget),
                    }, LeaveUpkeep)
                .WithLines(UpkeepPriceLine(state.Gold, hasDamagedRoom, hurt > 0), null)
                .WithAccent(HudPalette.Gain, null));
        }

        // 할 수 있는 일의 가격만 적는다(다친 부하가 없으면 치료 가격을 빼는 식).
        private static string UpkeepPriceLine(int gold, bool repair, bool heal)
        {
            string line = $"금화 {gold}";
            if (repair) line += $" · 고치기 {RunDemoCampaign.RepairPrice}";
            if (heal) line += $" · 치료 {RunDemoCampaign.HealPrice}";
            return line;
        }

        // "함정 방, 문 제어실이 고장 났습니다." 처럼 같은 상태의 방을 한 문장으로 잇는다.
        private void AppendRoomSentence(RunBattleCarryover state, bool broken, string predicate)
        {
            string last = null;
            int lineStart = _text.Length;
            for (int i = 0; i < state.RoomBroken.Length; i++)
            {
                if (!_demo.IsRoomDamaged(i) || state.RoomBroken[i] != broken) continue;
                if (last != null) _text.Append(", ");
                else if (lineStart > 0) _text.Append('\n');
                last = RunBattleText.RoomName(Deployment.Encounter.GridLayout[i].Kind);
                _text.Append(last);
            }
            if (last != null) _text.Append(broken ? Particle(last, "이", "가") : Particle(last, "은", "는")).Append(' ').Append(predicate);
        }

        // 수리·치료 대상: 고장·깎인 시스템 방, 체력이 깎인 부하 순(각각 배치 순서).
        private int UpkeepTargetCount()
        {
            int count = 0;
            for (int i = 0; i < _demo.Carryover.RoomBroken.Length; i++) if (_demo.IsRoomDamaged(i)) count++;
            for (int i = 0; i < _demo.Carryover.MinionHealth.Length; i++) if (_demo.IsMinionHurt(i)) count++;
            return count;
        }

        // n번째 대상을 돌려준다. 방이면 room, 부하면 minion이 0 이상이다.
        private void UpkeepTarget(int ordinal, out int room, out int minion)
        {
            room = minion = -1;
            for (int i = 0; i < _demo.Carryover.RoomBroken.Length; i++)
                if (_demo.IsRoomDamaged(i) && ordinal-- == 0) { room = i; return; }
            for (int i = 0; i < _demo.Carryover.MinionHealth.Length; i++)
                if (_demo.IsMinionHurt(i) && ordinal-- == 0) { minion = i; return; }
        }

        private void ShowUpkeepTarget(int ordinal)
        {
            int count = UpkeepTargetCount();
            if (count == 0)
            {
                ShowUpkeep();
                return;
            }
            ordinal %= count;
            UpkeepTarget(ordinal, out int room, out int minionIndex);
            RunBattleCarryover state = _demo.Carryover;
            int price;
            string line;
            System.Action buy;
            if (room >= 0)
            {
                int max = Deployment.Encounter.Settings.RoomDurability;
                string name = RunBattleText.RoomName(Deployment.Encounter.GridLayout[room].Kind);
                line = state.RoomBroken[room] ? $"{name} · 고장 → 내구 가득" : $"{name} · 내구 {Mathf.CeilToInt(state.RoomDurability[room])}/{max} → {max}/{max}";
                price = RunDemoCampaign.RepairPrice;
                buy = () => BuyUpkeep(_demo.TryRepairRoom(room));
            }
            else
            {
                RunCombatantSO minion = _demo.Roster[minionIndex].Combatant;
                line = $"{minion.DisplayName} · 체력 {state.MinionHealth[minionIndex]}/{minion.MaxHealth} → {minion.MaxHealth}/{minion.MaxHealth}";
                price = RunDemoCampaign.HealPrice;
                buy = () => BuyUpkeep(_demo.TryHealMinion(minionIndex));
            }
            bool affordable = state.Gold >= price;
            PopupController.Show(PopupRequest.Choice("던전 정비 · 수리·치료", $"{line} ({ordinal + 1}/{count})",
                    new[]
                    {
                        new PopupButtonSpec("뒤로", PopupButtonStyle.Secondary, ShowUpkeep),
                        new PopupButtonSpec("다음 대상", PopupButtonStyle.Secondary, () => ShowUpkeepTarget(ordinal + 1), count > 1),
                        new PopupButtonSpec(room >= 0 ? "고치기" : "치료", PopupButtonStyle.Primary, buy, affordable),
                    }, ShowUpkeep)
                .WithLines($"가진 금화 {state.Gold} · 가격 금화 {price}", affordable ? null : "금화가 모자라 살 수 없습니다")
                .WithAccent(HudPalette.Gain, null));
        }

        private void BuyUpkeep(bool bought)
        {
            if (bought)
            {
                // 끝난 전투가 지도 뒤 화면을 그리므로 고친 방·치료한 부하·금화를 바로 보인다.
                _demo.Carryover.ApplyTo(Battle);
                SaveCheckpoint();
            }
            ShowUpkeep();
        }

        private void LeaveUpkeep()
        {
            _upkeepDone = true;
            OpenNextEncounter();
        }

        // 유물 알림이 떠 있는 동안에도 왼쪽 부하 카드가 내준 체력을 보이게 한다(2026-11-16 한 판 점검 1번).
        private void HandleExchanged() => _demo.Carryover.ApplyTo(Battle);

        // 글 사건 "굴 속 거래"가 끝났다(교환 또는 떠나기). 칸은 이미 끝났으므로 이어지는 상태를 보이고 저장한 뒤 다음으로 간다.
        private void HandleExchangeFinished()
        {
            _demo.Carryover.ApplyTo(Battle);
            SaveCheckpoint();
            OpenNextEncounter();
        }

        private void HandleMerchantBought()
        {
            // 끝난 전투가 지도 뒤 화면을 그리므로 고친 심장·되살린 부하·금화를 바로 보인다.
            _demo.Carryover.ApplyTo(Battle);
            SaveCheckpoint();
        }

        private void HandleMerchantLeft() => CompleteBranch(-1);

        private void CompleteBranch(int choice)
        {
            bool merchantPurchase = _demo.Route.Current == RunDemoRoute.Node.Merchant && choice >= 0;
            if (!_demo.ResolveBranch(choice)) return;
            // 끝난 전투가 지도 뒤 화면을 그리므로 고용·치료한 부하 체력까지 이어지는 값으로 맞춘다.
            _demo.Carryover.ApplyTo(Battle);
            SaveCheckpoint();
            if (merchantPurchase) ShowMerchant();
            else OpenNextEncounter();
        }

        private void PrepareDefense(int number)
        {
            _raid = null;
            _flow.PrepareDemoDefense(number, _demo.DefenseSettings(number), _demo.DefenseParty(number), _demo.Carryover, _demo.Roster);
            ShowPreparation(Deployment.Encounter);
        }

        private void BeginRaidSelection()
        {
            // 파견 판이 놓여 있으면 카드로 한 번에 고른다(2026-11-16 사용자 A안 승인). 살아 있는 부하가 없으면 예전 경로가 실패를 알린다.
            if (_raidSelect != null && Array.Exists(_demo.Carryover.MinionHealth, health => health > 0))
            {
                _raidSelect.Show(_demo, Deployment.Encounter.Settings.RetreatHealthRatio, _raiders);
                return;
            }
            _raiders.Clear();
            SelectRaider(0);
        }

        private void HandleRaidersConfirmed(IReadOnlyList<int> picked)
        {
            _raiders.Clear();
            _raiders.AddRange(picked);
            ShowRaidRoute();
        }

        private void SelectRaider(int index)
        {
            RunPlacement[] minions = _demo.Roster;
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
            if (_raiders.Count > 0) _text.Append("지금까지 · ").Append(RaiderNames());
            bool weak = health < minion.MaxHealth * Deployment.Encounter.Settings.RetreatHealthRatio;
            // 잃는다는 경고와 승리 보상 줄은 뺐다(2026-11-12 사용자 목업 승인 "전투 뒤 창" 글 줄이기).
            PopupController.Show(PopupRequest.Confirm($"원정 · 파견 ({_raiders.Count}/3)", _text.ToString(),
                    () => { _raiders.Add(index); SelectRaider(index + 1); }, () => SelectRaider(index + 1), "파견", "남기기")
                .WithLines(null, weak ? "체력이 30% 아래입니다" : null)
                .WithAccent(HudPalette.Danger, null));
        }

        private void ShowRaidRoute()
        {
            // 아래 줄은 한 줄 칸이라 세 이름이 잘렸다. 이름은 본문 끝 줄로 옮기고 아래 줄은 인원만 적는다(2026-11-16 한 판 점검 2번).
            PopupController.Show(PopupRequest.Choice("원정 · 진입 경로",
                    "정면 · 가깝지만 검사와 궁수가 지킵니다\n우회 · 경비실을 피하지만 길이 깁니다\n진입 뒤 부하를 골라 다음 방으로 보냅니다.\n파견 · " + RaiderNames(),
                    new[]
                    {
                        new PopupButtonSpec("다시 선택", PopupButtonStyle.Secondary, BeginRaidSelection),
                        new PopupButtonSpec("정면", PopupButtonStyle.Primary, () => StartRaid(false)),
                        new PopupButtonSpec("우회", PopupButtonStyle.Primary, () => StartRaid(true)),
                    }, () => { })
                .WithLines($"파견 {_raiders.Count}명", null)
                .WithAccent(HudPalette.Danger, "진지 대장을 노립니다"));
        }

        private string RaiderNames()
        {
            string names = string.Empty;
            foreach (int raider in _raiders)
                names += (names.Length > 0 ? ", " : string.Empty) + _demo.Roster[raider].Combatant.DisplayName;
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
            // 원정은 할 일 안내가 필요해 아래 띠를 연 채로 시작한다(상성 버튼으로 닫을 수 있다).
            crisisView.SetBattleMode(true, true);
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
            if (route.SecondBranch is RunDemoRoute.Node second && route.Current == RunDemoRoute.Node.EliteDefense)
                _text.Append(" → ").Append(NodeLabel(second));
            AppendRouteStep(RunDemoRoute.Node.EliteDefense, route);
            _text.Append("\n남은 부하 · ");
            AppendMinions(state, alive: true);
            _text.Append("\n잃은 부하 · ");
            AppendMinions(state, alive: false);
            int maxHeart = Deployment.Encounter.Settings.HeartHealth;
            string warning = won ? null : state.HeartHealth <= 0 ? "심장이 무너졌습니다" : "방어에 실패했습니다";
            PopupController.Show(PopupRequest.Notice(won ? "시연 런 방어 성공" : "시연 런 패배", _text.ToString(),
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene("Start"), "타이틀로")
                .WithLines($"심장 {state.HeartHealth}/{maxHeart} · 금화 {state.Gold}", warning)
                .WithAccent(HudPalette.Accent, "1구역 시연 런 결과"));
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
                RunCombatantSO minion = _demo.Roster[i].Combatant;
                if (_text.Length > start) _text.Append(", ");
                _text.Append(minion.DisplayName);
                // 상인에서 들인 다섯째는 같은 이름의 처음 부하와 구별한다(2026-11-16 한 판 점검 7번).
                if (i >= Deployment.Encounter.Minions.Length) _text.Append("(새로 들임)");
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
            int minions = encounter.Minions.Length + (checkpoint.ExtraMinionKind >= 0 ? 1 : 0);
            if (carryover.MinionHealth.Length != minions || carryover.RoomDurability.Length != encounter.GridLayout.Length)
            {
                _saveBlocked = true;
                Debug.LogWarning("현재 던전 구성과 다른 시연 저장을 보존합니다.", this);
                return;
            }
            RunDemoRoute route = checkpoint.GetRoute();
            _demo.Restore(route, checkpoint);
            // 원정 뒤 갈래(2026-11-11)는 원정 칸처럼 공병전 결과 화면 뒤에서 이어진다.
            int number = route.Current switch
            {
                RunDemoRoute.Node.ThiefDefense => 1,
                RunDemoRoute.Node.Merchant or RunDemoRoute.Node.StoryEvent => route.IsSecondBranch ? 2 : 1,
                RunDemoRoute.Node.EliteDefense => 3,
                _ => 2,
            };
            _flow.PrepareDemoDefense(number, _demo.DefenseSettings(number), _demo.DefenseParty(number), carryover, _demo.Roster);
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
            crisisView.SetBattleMode(!IsPreparing);
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
                // 카드는 "이름 ● → 노리는 곳" 한 줄이다. 큰 기술·상성 이름은 뺐다(2026-11-12 사용자 목업 승인 "준비 화면 양쪽 글").
                preparation.SetParty(i, data.DisplayName + " " + HudPalette.Paint("●", RunVisualPalette.Affinity(affinity)) + " → " + GoalTarget(data));
            }
            RefreshPreparation();
            Prepared?.Invoke();
        }

        /// <summary>마지막 글자에 받침이 있으면 withFinal, 없으면 withoutFinal 조사를 붙인다.</summary>
        private static string Josa(string word, string withFinal, string withoutFinal) => word + Particle(word, withFinal, withoutFinal);

        private static string Particle(string word, string withFinal, string withoutFinal)
        {
            if (string.IsNullOrEmpty(word)) return withoutFinal;
            char last = word[^1];
            bool final = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
            return final ? withFinal : withoutFinal;
        }

        private static string GoalTarget(RunCombatantSO data) => data.IntruderRole == RunIntruderRole.Sapper ? "켜진 방"
            : data.Goal == RunGoal.Vault ? "금고"
            : data.Goal == RunGoal.Room ? RunBattleText.RoomName(data.GoalRoomKind)
            : "심장";

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
                RunCombatantSO data = _demo.Roster[i].Combatant;
                // 쓰러진 부하도 카드를 흐리게 남겨 목록에 틈이 생기지 않고 잃은 것이 보이게 한다(2026-11-10 사용자 목업 승인).
                if (!CanPlaceMinion(i))
                {
                    preparation.SetMinion(i, HudPalette.Paint(data.DisplayName + "\n쓰러짐", HudPalette.TextMuted),
                        HudPalette.TextMuted, false);
                    continue;
                }
                // 자리는 평면도에서 보이므로 카드에는 이름과 체력만 둔다(2026-11-12 사용자 목업 승인).
                // 상인에서 살린 부하는 체력이 가득이라 체력 대신 "지난 판 쓰러짐"을 보인다(2026-11-12 사용자 결정).
                string second = _demo.WasRevived(i) ? HudPalette.Paint("지난 판 쓰러짐", HudPalette.Warn)
                    : $"체력 {Battle.Combatants[i].Health}/{data.MaxHealth}";
                preparation.SetMinion(i, data.DisplayName + "\n" + second,
                    RunVisualPalette.Affinity(data.Affinity), i == _selection);
            }
            // 카드는 5장이다(상인 새 부하, 2026-11-11). 부하가 넷이면 남는 카드를 숨긴다.
            for (int i = Deployment.MinionCount; i < preparation.MinionCapacity; i++)
                preparation.SetMinion(i, string.Empty, HudPalette.TextMuted, false, false);
            // 조작 안내는 위 제목줄 한 곳에만 둔다(2026-11-10 사용자 목업 승인).
            string detail = string.Empty;
            if (_selection >= 0)
            {
                RunCombatantSO data = _demo.Roster[_selection].Combatant;
                detail = data.DisplayName + $" · 체력 {Battle.Combatants[_selection].Health}/{data.MaxHealth} · 공격 {data.Attack}\n"
                    + (data.Skill != null ? data.Skill.DisplayName + ": " + data.Skill.Description : "큰 기술 없음");
            }
            // 칸 수 안내 줄은 뺐다(2026-11-12 사용자 목업 승인).
            preparation.SetSummary($"마력 {Deployment.Capacity - Deployment.ManaInUse} 남음",
                $"침입 {EncounterNumber}/{RunBattleFlow.EncounterLimit} · 심장 {Battle.HeartHealth} · 금화 {Battle.VaultGold}\n{detail}", _memory.HasPrevious);
        }

        public void StartBattle()
        {
            if (!_flow.StartBattle()) return;
            _monitor = new RunCrisisMonitor(Battle);
            BindTrap();
            preparation.Show(false);
            crisisView.SetBattleMode(true);
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
                // 칸을 끝내면 지도(런이 끝났으면 정산)를 바로 띄운다. 닫아도 결과 띠의 버튼으로 다시 연다.
                OpenNextEncounter();
                return;
            }
            _monitor.Scan();
            bool pauses = false;
            _text.Clear();
            _notice.Clear();
            for (int i = 0; i < _monitor.Events.Count; i++)
            {
                RunCrisis item = _monitor.Events[i];
                // 심장 진입과 공병 폭파 예고만 멈춘다. 나머지는 한 줄 알림만 띄운다(2026-11-12 사용자 "큰 위기만 멈춤").
                if (!StopsBattle(item))
                {
                    if (_notice.Length > 0) _notice.Append('\n');
                    AppendNotice(item);
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
            if (_notice.Length > 0) crisisView.Notify(_notice.ToString());
            if (!pauses) return;
            IsCrisis = true;
            crisisView.ShowCrisis(_text.ToString());
            CrisisStarted?.Invoke();
        }

        // 자동 멈춤은 없앴다. 심장 진입·공병 폭파 예고도 알림만 띄우고, 멈춤은 플레이어가 Space로 한다
        // (2026-11-12 사용자 결정 "자동 멈춤 없애기", 그 전에는 이 둘만 멈췄다).
        private static bool StopsBattle(RunCrisis item) => false;

        /// <summary>멈춤 카드: 누가 · 어디서 · 무엇을 한 줄(해결 방법 줄은 2026-11-12 사용자 목업 승인으로 뺐다).</summary>
        private void AppendCrisis(RunCrisis item)
        {
            if (_text.Length > 0) _text.Append('\n');
            string room = item.Room >= 0 ? _roomNames[item.Room] : "복도";
            RunCombatant actor = Battle.Combatants[item.Actor];
            _text.Append(actor.Definition.DisplayName).Append(" · ");
            if (item.Kind == RunCrisisKind.HeartEntered) _text.Append("심장 진입");
            else _text.Append(room).Append(" 폭파 ").Append(actor.Telegraph.RemainingSeconds.ToString("0.0")).Append('초');
        }

        /// <summary>멈추지 않는 위기의 한 줄 알림.</summary>
        private void AppendNotice(RunCrisis item)
        {
            string room = item.Room >= 0 ? _roomNames[item.Room] : "복도";
            if (item.Kind == RunCrisisKind.RoomBroken)
            {
                _notice.Append(room).Append(" 고장");
                return;
            }
            RunCombatant actor = Battle.Combatants[item.Actor];
            _notice.Append(actor.Definition.DisplayName).Append(" · ");
            switch (item.Kind)
            {
                case RunCrisisKind.LowHealth: _notice.Append("체력 낮음"); break;
                case RunCrisisKind.HeartEntered: _notice.Append("심장 진입"); break;
                case RunCrisisKind.ThiefAtVault: _notice.Append("금고 도착"); break;
                case RunCrisisKind.RoomUnderAttack: _notice.Append(room).Append(" 공격"); break;
                case RunCrisisKind.DoorUnderAttack: _notice.Append(room).Append(" 문"); break;
                default:
                    _notice.Append(actor.Telegraph.Skill.DisplayName).Append(' ')
                        .Append(actor.Telegraph.RemainingSeconds.ToString("0.0")).Append('초');
                    break;
            }
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
