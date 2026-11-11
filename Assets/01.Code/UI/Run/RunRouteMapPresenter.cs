using System.Text;
using Code.Run;

namespace Code.UI.Run
{
    /// <summary>
    /// 시연 지도의 칸 상태와 설명 문구를 정해 <see cref="RunRouteMapView"/>에 넘긴다.
    /// 칸 0~5는 <see cref="RunDemoRoute.Node"/> 순서(도적·상인·글 사건·공병·원정·정예), 6·7은 원정 뒤 갈래(상인·글 사건)다
    /// (2026-11-11 사용자 결정 "원정 뒤에도 갈래").
    /// </summary>
    internal sealed class RunRouteMapPresenter
    {
        private const int NodeCount = 8;
        private const int SecondMerchant = 6;
        private const int SecondEvent = 7;

        private readonly RunRouteMapView _view;
        private readonly StringBuilder _text = new();
        private RunDemoRoute _route;
        private RunDemoCampaign _demo;
        private RunEncounterSO _encounter;
        private int _selected = -1;

        internal RunRouteMapPresenter(RunRouteMapView view) => _view = view;

        /// <summary>고른 칸이 지금 들어갈 수 있는 칸이면 그 칸.</summary>
        internal RunDemoRoute.Node? SelectedEnterable => _route != null && _selected >= 0 && CanEnter(_selected) ? NodeOf(_selected) : null;

        internal void Show(RunDemoRoute route, RunDemoCampaign demo, RunEncounterSO encounter)
        {
            _route = route;
            _demo = demo;
            _encounter = encounter;
            _selected = SlotOfCurrent();
            for (int i = 0; i < NodeCount; i++)
                if (CanEnter(i))
                {
                    _selected = i;
                    break;
                }
            _view.SetResources(Resources());
            Refresh();
            _view.Open();
        }

        internal void Select(int index)
        {
            if (_route == null || index < 0 || index >= NodeCount || (IsSecond(index) && !_route.PostRaidBranch)) return;
            _selected = index;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < NodeCount; i++)
            {
                bool shown = !IsSecond(i) || _route.PostRaidBranch;
                _view.SetNodeVisible(i, shown);
                if (!shown) continue;
                RunRouteNodeState state = StateOf(i);
                _view.SetNode(i, Name(NodeOf(i)), StateLabel(i, state), state, i == _selected);
            }
            RunDemoRoute.Node chosen = NodeOf(_selected);
            _view.SetDetail(Name(chosen), Kind(chosen), Body(chosen), CanEnter(_selected) ? Name(chosen) + " 들어가기" : null);
        }

        // ── 칸 번호 ↔ 지도 칸 ──

        private static bool IsSecond(int slot) => slot >= SecondMerchant;

        private static RunDemoRoute.Node NodeOf(int slot) => slot switch
        {
            SecondMerchant => RunDemoRoute.Node.Merchant,
            SecondEvent => RunDemoRoute.Node.StoryEvent,
            _ => (RunDemoRoute.Node)slot,
        };

        private static bool IsBranch(int slot) => NodeOf(slot) is RunDemoRoute.Node.Merchant or RunDemoRoute.Node.StoryEvent;

        // 같은 상인·글 사건이라도 원정 전 칸과 원정 뒤 칸을 가른다.
        private bool CanEnter(int slot) => _route.CanEnter(NodeOf(slot)) && (!IsBranch(slot) || IsSecond(slot) == (_route.RaidResult != RunDemoRoute.Result.Pending));

        private int SlotOfCurrent()
        {
            if (!_route.IsSecondBranch) return (int)_route.Current;
            return _route.Current == RunDemoRoute.Node.Merchant ? SecondMerchant : SecondEvent;
        }

        private static int Column(int slot) => slot switch
        {
            0 => 0,
            1 or 2 => 1,
            3 => 2,
            4 => 3,
            SecondMerchant or SecondEvent => 4,
            _ => 5,
        };

        private RunRouteNodeState StateOf(int slot)
        {
            if (CanEnter(slot)) return RunRouteNodeState.Open;
            int current = SlotOfCurrent();
            if (slot == current)
                return _route.CurrentResult == RunDemoRoute.Result.Pending ? RunRouteNodeState.Open : RunRouteNodeState.Done;
            int column = Column(slot);
            int currentColumn = Column(current);
            if (IsBranch(slot) && column <= currentColumn)
            {
                RunDemoRoute.Node? chosen = IsSecond(slot) ? _route.SecondBranch : _route.ChosenBranch;
                return chosen == NodeOf(slot) ? RunRouteNodeState.Done : RunRouteNodeState.Skipped;
            }
            return column < currentColumn ? RunRouteNodeState.Done : RunRouteNodeState.Locked;
        }

        private string StateLabel(int slot, RunRouteNodeState state) => state switch
        {
            RunRouteNodeState.Open => slot == SlotOfCurrent() ? "진행 중" : "고를 수 있음",
            RunRouteNodeState.Skipped => "지나침",
            RunRouteNodeState.Done => ResultLabel(slot),
            _ => NodeOf(slot) switch
            {
                RunDemoRoute.Node.Merchant or RunDemoRoute.Node.StoryEvent => "들르기",
                RunDemoRoute.Node.OutpostRaid => "원정",
                _ => "방어",
            },
        };

        private string ResultLabel(int slot)
        {
            RunDemoRoute.Node node = NodeOf(slot);
            RunDemoRoute.Result result = slot == SlotOfCurrent() ? _route.CurrentResult
                : node == RunDemoRoute.Node.OutpostRaid ? _route.RaidResult
                : IsBranch(slot) ? RunDemoRoute.Result.Completed
                : RunDemoRoute.Result.Victory;
            return result switch
            {
                RunDemoRoute.Result.Victory => "승리",
                RunDemoRoute.Result.Defeat => "패배",
                RunDemoRoute.Result.Retreat => "후퇴",
                _ => "다녀옴",
            };
        }

        // ── 글 ──

        internal static string Name(RunDemoRoute.Node node) => node switch
        {
            RunDemoRoute.Node.ThiefDefense => "도적 침입",
            RunDemoRoute.Node.Merchant => "상인",
            RunDemoRoute.Node.StoryEvent => "글 사건",
            RunDemoRoute.Node.SapperDefense => "공병 침입",
            RunDemoRoute.Node.OutpostRaid => "진지 원정",
            _ => "정예 침입",
        };

        private static string Kind(RunDemoRoute.Node node) => node switch
        {
            RunDemoRoute.Node.Merchant or RunDemoRoute.Node.StoryEvent => "들르는 칸 · 전투 없음",
            RunDemoRoute.Node.OutpostRaid => "공격 칸 · 원정",
            _ => "방어 칸 · 침입",
        };

        private string Body(RunDemoRoute.Node node)
        {
            _text.Clear();
            switch (node)
            {
                case RunDemoRoute.Node.ThiefDefense:
                    AppendParty(1).Append("\n도적이 금고의 금화를 노립니다.\n지면 런이 끝납니다.");
                    break;
                // 상인 좌판·교환 사건으로 바뀐 내용(2026-11-11 사용자 결정).
                case RunDemoRoute.Node.Merchant:
                    _text.Append("한 번 들를 때 ").Append(_demo.Economy.MerchantPurchaseLimit).Append("개까지 삽니다.\n")
                        .Append("· 심장 수리 · 함정 강화 · 쓰러진 부하 복귀 (각 금화 ").Append(RunDemoCampaign.MerchantPrice).Append(")\n")
                        .Append("· 마력 용량 +1 · 새 부하 · 유물 둘");
                    if (_demo.Carryover != null) _text.Append("\n지금 금화 ").Append(_demo.Carryover.Gold);
                    break;
                case RunDemoRoute.Node.StoryEvent:
                    _text.Append("굴 속 거래: 교환 네 가지 중 두 가지가 나옵니다.\n심장·금화·부하 체력·방 하나를 내고 부하 강화나 유물을 얻습니다.\n그냥 떠나도 됩니다.");
                    break;
                case RunDemoRoute.Node.SapperDefense:
                    AppendParty(2).Append("\n공병이 방 가운데에 폭약을 놓고 터뜨립니다. 때리거나 함정으로 끊으세요.\n지면 런이 끝납니다.");
                    break;
                case RunDemoRoute.Node.OutpostRaid:
                    _text.Append("부하를 최대 3명 보내 진지 대장을 처치합니다.\n지키는 적 · ").Append(_demo.RaidDefenders)
                        .Append("\n언제든 후퇴할 수 있습니다. 이기면 마지막 침입의 마력 용량이 늘어납니다.");
                    break;
                default:
                    AppendParty(3).Append("\n마지막 침입입니다. 마력 용량 ").Append(_demo.DefenseSettings(3).StartingManaCapacity)
                        .Append("\n지면 런이 끝납니다.");
                    break;
            }
            return _text.ToString();
        }

        private StringBuilder AppendParty(int number)
        {
            RunIntruderEntry[] party = _demo.DefenseParty(number);
            _text.Append("오는 적 · ");
            for (int i = 0; i < party.Length; i++)
            {
                if (i > 0) _text.Append(" → ");
                _text.Append(party[i].Combatant.DisplayName);
            }
            int waves = RunDemoCampaign.DefenseWaves(number);
            if (waves > 1) _text.Append(" · ").Append(waves).Append("무리(").Append(RunDemoCampaign.WaveGapSeconds.ToString("0")).Append("초 간격)");
            return _text;
        }

        // 위 줄: 심장·금화·부하, 가진 유물(2026-11-11 목업 "구역 지도 + 가진 유물", 칩 대신 글).
        private string Resources()
        {
            RunBattleCarryover state = _demo.Carryover;
            if (state == null) return string.Empty;
            int alive = 0;
            for (int i = 0; i < state.MinionHealth.Length; i++)
                if (state.MinionHealth[i] > 0) alive++;
            _text.Clear().Append("심장 ").Append(state.HeartHealth).Append('/').Append(_encounter.Settings.HeartHealth)
                .Append(" · 금화 ").Append(state.Gold).Append(" · 부하 ").Append(alive).Append('/').Append(state.MinionHealth.Length)
                .Append(" · 유물 ");
            int start = _text.Length;
            foreach (RunDemoRelic relic in new[] { RunDemoRelic.QuickTrap, RunDemoRelic.ThickBones, RunDemoRelic.ManaCrystal, RunDemoRelic.HeavyLock })
            {
                if (!_demo.Has(relic)) continue;
                if (_text.Length > start) _text.Append(", ");
                _text.Append(HudPalette.Paint(RunMerchantPresenter.RelicName(relic), HudPalette.Accent));
            }
            if (_text.Length == start) _text.Append("없음");
            return _text.ToString();
        }
    }
}
