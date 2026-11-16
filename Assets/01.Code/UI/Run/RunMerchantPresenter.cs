using System;
using Code.Entities;
using Code.Run;

namespace Code.UI.Run
{
    /// <summary>
    /// 상인 좌판의 문구와 살 수 있는지를 정해 <see cref="RunMerchantView"/>에 넘기고, 클릭을 캠페인 구매로 바꾼다.
    /// 산 뒤 저장·화면 반영은 세션 컨트롤러가 <see cref="Bought"/>에서 한다.
    /// </summary>
    internal sealed class RunMerchantPresenter
    {
        private static readonly RunDemoCampaign.MerchantItem[] Rows =
        {
            RunDemoCampaign.MerchantItem.HeartRepair,
            RunDemoCampaign.MerchantItem.TrapUpgrade,
            RunDemoCampaign.MerchantItem.Revive,
            RunDemoCampaign.MerchantItem.Capacity,
            RunDemoCampaign.MerchantItem.NewMinion,
            RunDemoCampaign.MerchantItem.Relic0,
            RunDemoCampaign.MerchantItem.Relic1,
        };

        private readonly RunMerchantView _view;
        private RunDemoCampaign _demo;
        private RunEncounterSO _encounter;
        private int _pick = -1;
        private int _revived = -1;

        internal RunMerchantPresenter(RunMerchantView view)
        {
            _view = view;
            _view.RowClicked += HandleRow;
            _view.PickCardClicked += HandlePickCard;
            _view.PickBackClicked += HandlePickBack;
            _view.PickConfirmClicked += HandlePickConfirm;
            _view.LeaveClicked += HandleLeave;
        }

        /// <summary>항목을 하나 샀다. 세션 컨트롤러가 이어지는 상태를 전투 화면에 반영하고 저장한다.</summary>
        internal event Action Bought;

        /// <summary>떠나기를 눌렀다. 세션 컨트롤러가 칸을 끝낸다.</summary>
        internal event Action Left;

        internal bool IsOpen => _view.IsOpen;

        internal void Show(RunDemoCampaign demo, RunEncounterSO encounter)
        {
            _demo = demo;
            _encounter = encounter;
            _demo.EnsureBranchOffers();
            _pick = -1;
            _revived = -1;
            _view.ShowPick(false);
            Refresh();
            _view.Open();
        }

        internal void Hide() => _view.Hide();

        internal void Unbind()
        {
            _view.RowClicked -= HandleRow;
            _view.PickCardClicked -= HandlePickCard;
            _view.PickBackClicked -= HandlePickBack;
            _view.PickConfirmClicked -= HandlePickConfirm;
            _view.LeaveClicked -= HandleLeave;
        }

        private void Refresh()
        {
            RunBattleCarryover state = _demo.Carryover;
            int limit = _demo.Economy.MerchantPurchaseLimit;
            _view.SetHeader($"가진 금화 {state.Gold}",
                limit > 0 ? $"한 번 들를 때 {limit}개까지 · 산 것 {_demo.MerchantBoughtCount}/{limit}" : "항목마다 한 번 살 수 있습니다");
            for (int i = 0; i < Rows.Length && i < _view.RowCount; i++) RefreshRow(i, Rows[i]);
            _view.SetWarning(_demo.MerchantLimitReached ? "이번에는 다 샀습니다. 떠나기를 누르세요" : null);
            if (_view.IsPicking) RefreshPick();
        }

        private void RefreshRow(int row, RunDemoCampaign.MerchantItem item)
        {
            RunBattleCarryover state = _demo.Carryover;
            bool bought = _demo.HasBoughtAtMerchant(item);
            bool can = _demo.CanBuyAtMerchant(item);
            string name;
            string detail;
            // 산 것과 조건이 안 맞는 것(가득 찬 심장 등)은 흐린다. 금화만 모자라면 흐리지 않고 버튼만 잠근다.
            bool unavailable;
            switch (item)
            {
                case RunDemoCampaign.MerchantItem.HeartRepair:
                    int maxHeart = _encounter.Settings.HeartHealth;
                    name = "심장 수리";
                    detail = $"심장 +10 (지금 {state.HeartHealth}/{maxHeart})";
                    unavailable = state.HeartHealth >= maxHeart;
                    break;
                case RunDemoCampaign.MerchantItem.TrapUpgrade:
                    name = "함정 강화";
                    detail = _demo.TrapBonus != 0 && !bought ? "이번 런에 이미 강화했습니다" : "이번 런 함정 기본 피해 +2";
                    unavailable = _demo.TrapBonus != 0 && !bought;
                    break;
                case RunDemoCampaign.MerchantItem.Revive:
                    int lost = _demo.FirstLostMinion;
                    name = "쓰러진 부하";
                    // 산 뒤에는 다음 대상 대신 이번에 복귀시킨 부하를 보인다.
                    detail = bought ? (_revived >= 0 ? $"{_demo.Minion(_revived).DisplayName} 복귀함" : "이번에 한 명 복귀시켰습니다")
                        : lost >= 0 ? $"{_demo.Minion(lost).DisplayName} 복귀 · 체력 가득" : "쓰러진 부하가 없습니다";
                    unavailable = lost < 0 && !bought;
                    break;
                case RunDemoCampaign.MerchantItem.Capacity:
                    int capacity = 3 + _demo.ExtraCapacity;
                    int left = RunDemoCampaign.MaxExtraCapacity - _demo.ExtraCapacity;
                    name = "마력 용량 +1";
                    // 산 뒤에는 다음 단계가 아니라 이번에 늘린 값을 보인다(다음 단계를 보이면 안 산 것처럼 읽혔다).
                    detail = bought ? $"이번에 {capacity - 1} → {capacity}로 늘렸습니다"
                        : left > 0 ? $"{capacity} → {capacity + 1} · {left}번 더 늘릴 수 있음" : "더 늘릴 수 없습니다";
                    unavailable = left <= 0 && !bought;
                    break;
                case RunDemoCampaign.MerchantItem.NewMinion:
                    name = "새 부하";
                    detail = _demo.ExtraMinionKind >= 0 && !bought ? "이번 런에 이미 들였습니다" : "4종 중 하나를 5번째로 들입니다 · 체력 가득";
                    unavailable = _demo.ExtraMinionKind >= 0 && !bought;
                    break;
                default:
                    RunDemoRelic relic = _demo.OfferedRelic(item == RunDemoCampaign.MerchantItem.Relic0 ? 0 : 1);
                    name = relic == RunDemoRelic.None ? "유물" : "유물 · " + RelicName(relic);
                    detail = relic == RunDemoRelic.None ? "내놓은 유물이 없습니다" : RelicEffect(relic);
                    unavailable = relic == RunDemoRelic.None;
                    break;
            }
            string label = bought ? "산 것" :item == RunDemoCampaign.MerchantItem.NewMinion ? "고르기" : "사기";
            // 이번 방문에서 살 수 있는 수를 다 채웠으면 남은 줄도 흐린다(버튼만 잠그면 살 수 있어 보였다).
            _view.SetRow(row, name, detail, $"금화 {_demo.PriceOf(item)}", label, can, bought || unavailable || _demo.MerchantLimitReached);
        }

        private void HandleRow(int row)
        {
            if (_demo == null || row < 0 || row >= Rows.Length) return;
            RunDemoCampaign.MerchantItem item = Rows[row];
            if (!_demo.CanBuyAtMerchant(item)) return;
            int lost = _demo.FirstLostMinion;
            if (item == RunDemoCampaign.MerchantItem.NewMinion)
            {
                _pick = -1;
                _view.ShowPick(true);
                RefreshPick();
                return;
            }
            if (!_demo.TryBuyAtMerchant(item)) return;
            if (item == RunDemoCampaign.MerchantItem.Revive) _revived = lost;
            Bought?.Invoke();
            Refresh();
        }

        // ── 새 부하 고르기 ──

        private void RefreshPick()
        {
            RunPlacement[] kinds = _encounter.Minions;
            _view.SetPickHeading($"5번째 부하로 들어옵니다 · 체력 가득 · 자리는 다음 준비 화면에서 정합니다");
            int bones = _demo.Has(RunDemoRelic.ThickBones) ? RunDemoCampaign.ThickBonesHealth : 0;
            for (int i = 0; i < _view.PickCount; i++)
            {
                if (i >= kinds.Length || kinds[i].Combatant == null)
                {
                    _view.SetPickCard(i, string.Empty, string.Empty, null, false);
                    continue;
                }
                RunCombatantSO data = kinds[i].Combatant;
                string skill = data.Skill != null ? data.Skill.DisplayName : "없음";
                // 카드 아래 빈 곳을 그림과 이미 있는 부하 정보로 채운다(2026-11-12 사용자 A안 승인).
                string reach = data.IsRanged ? "원거리" : "근접";
                string affinity = HudPalette.Paint(AffinityName(data.Affinity), RunVisualPalette.Affinity(data.Affinity));
                string favored = data.HasFavoredRoom ? RunBattleText.RoomName(data.FavoredRoom) : "없음";
                _view.SetPickCard(i, data.DisplayName,
                    $"체력 {data.MaxHealth + bones} · 공격 {data.Attack}\n{reach} · {affinity}\n큰 기술 · {skill}\n맞는 방 · {favored}\n{CountLine(data.DisplayName)}",
                    Portrait(data), i == _pick);
            }
            bool chosen = _pick >= 0 && _pick < kinds.Length;
            int price = _demo.PriceOf(RunDemoCampaign.MerchantItem.NewMinion);
            _view.SetPickConfirm(chosen ? $"{kinds[_pick].Combatant.DisplayName} 고용 · 금화 {price}" : "부하를 고르세요",
                chosen && _demo.CanBuyAtMerchant(RunDemoCampaign.MerchantItem.NewMinion));
        }

        private static string AffinityName(RunAffinity affinity) => affinity switch
        {
            RunAffinity.Magic => "마법",
            RunAffinity.Curse => "저주",
            _ => "물리",
        };

        // 전투 화면 부하 카드와 같은 그림(서 있는 첫 프레임)을 쓴다.
        private static UnityEngine.Sprite Portrait(RunCombatantSO data)
        {
            CharacterAnimationSetSO set = data.ResolveAnimationSet();
            UnityEngine.Sprite[] frames = set != null ? set.GetFrames(CharacterClip.Idle) : null;
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        // 같은 이름의 부하가 지금 몇 명 살아 있는지(쓰러진 부하는 따로 센다).
        private string CountLine(string displayName)
        {
            int alive = 0, lost = 0;
            for (int i = 0; i < _demo.MinionCount; i++)
            {
                if (_demo.Minion(i).DisplayName != displayName) continue;
                if (_demo.IsMinionAlive(i)) alive++;
                else lost++;
            }
            return lost > 0 ? $"지금 {alive}명 · 쓰러짐 {lost}명" : $"지금 {alive}명";
        }

        private void HandlePickCard(int index)
        {
            if (_demo == null || index < 0 || index >= _encounter.Minions.Length || _encounter.Minions[index].Combatant == null) return;
            _pick = index;
            RefreshPick();
        }

        private void HandlePickBack()
        {
            _pick = -1;
            _view.ShowPick(false);
            Refresh();
        }

        private void HandlePickConfirm()
        {
            if (_demo == null || _pick < 0 || !_demo.TryBuyAtMerchant(RunDemoCampaign.MerchantItem.NewMinion, _pick)) return;
            _pick = -1;
            _view.ShowPick(false);
            Bought?.Invoke();
            Refresh();
        }

        private void HandleLeave()
        {
            if (_demo == null) return;
            _view.Hide();
            Left?.Invoke();
        }

        // ── 유물 글(2026-11-11 사용자 결정 목록) ──

        internal static string RelicName(RunDemoRelic relic) => relic switch
        {
            RunDemoRelic.QuickTrap => "빠른 태엽",
            RunDemoRelic.ThickBones => "두꺼운 뼈",
            RunDemoRelic.ManaCrystal => "마력 결정",
            RunDemoRelic.HeavyLock => "무거운 자물쇠",
            _ => string.Empty,
        };

        internal static string RelicEffect(RunDemoRelic relic) => relic switch
        {
            RunDemoRelic.QuickTrap => "함정 충전 4초 → 3초",
            RunDemoRelic.ThickBones => "모든 부하 최대 체력 +4",
            RunDemoRelic.ManaCrystal => "마력 용량 +1",
            RunDemoRelic.HeavyLock => "도적이 한 번에 드는 금화 절반",
            _ => string.Empty,
        };
    }
}
