using System;
using System.Collections.Generic;
using Code.Run;
using Code.UI.Popup;

namespace Code.UI.Run
{
    /// <summary>
    /// 글 사건 "굴 속 거래"(2026-11-11 사용자 결정: 약초꾼 대신 교환, 글은 기능대로 짧게).
    /// 범용 팝업 두 단계: 교환 두 개 + 떠나기 → 대상(부하·방) 고르기 → 유물을 얻었으면 알림.
    /// 교환·떠나기 뒤 저장과 다음 칸은 세션 컨트롤러가 <see cref="Finished"/>에서 한다.
    /// </summary>
    internal sealed class RunExchangePresenter
    {
        private const string Title = "굴 속 거래";

        private readonly List<int> _targets = new(8);
        private RunDemoCampaign _demo;
        private RunEncounterSO _encounter;

        /// <summary>교환하거나 떠나서 칸이 끝났다.</summary>
        internal event Action Finished;

        /// <summary>교환이 성사됐다. 유물 알림이 뜨기 전에 화면(부하 체력·방)을 맞춘다.</summary>
        internal event Action Exchanged;

        internal void Show(RunDemoCampaign demo, RunEncounterSO encounter)
        {
            _demo = demo;
            _encounter = encounter;
            _demo.EnsureBranchOffers();
            ShowChoice();
        }

        private void ShowChoice()
        {
            RunDemoExchange? first = _demo.OfferedExchange(0);
            RunDemoExchange? second = _demo.OfferedExchange(1);
            var body = new System.Text.StringBuilder();
            var buttons = new List<PopupButtonSpec>(3) { new("떠나기", PopupButtonStyle.Secondary, Leave) };
            foreach (RunDemoExchange? offer in new[] { first, second })
            {
                if (offer is not RunDemoExchange exchange) continue;
                if (body.Length > 0) body.Append('\n');
                body.Append(Heading(exchange)).Append('\n').Append("  ").Append(Effect(exchange));
                buttons.Add(new PopupButtonSpec(ButtonLabel(exchange), PopupButtonStyle.Primary, () => ShowTargets(exchange, 0), HasTarget(exchange)));
            }
            if (body.Length == 0) body.Append("거래할 것이 없습니다.");
            PopupController.Show(PopupRequest.Choice(Title, body.ToString(), buttons, () => { })
                .WithLines(Resources(), NoTargetWarning(first, second))
                .WithAccent(HudPalette.Warn, "무엇을 내줄지 고릅니다"));
        }

        private void ShowTargets(RunDemoExchange exchange, int ordinal)
        {
            CollectTargets(exchange);
            if (_targets.Count == 0)
            {
                ShowChoice();
                return;
            }
            ordinal %= _targets.Count;
            int target = _targets[ordinal];
            PopupController.Show(PopupRequest.Choice($"{Title} · {ButtonLabel(exchange)}", $"{TargetLine(exchange, target)} ({ordinal + 1}/{_targets.Count})\n{Effect(exchange)}",
                    new[]
                    {
                        new PopupButtonSpec("뒤로", PopupButtonStyle.Secondary, ShowChoice),
                        new PopupButtonSpec(IsRoomExchange(exchange) ? "다른 방" : "다른 부하", PopupButtonStyle.Secondary,
                            () => ShowTargets(exchange, ordinal + 1), _targets.Count > 1),
                        new PopupButtonSpec(ConfirmLabel(exchange), PopupButtonStyle.Primary, () => Confirm(exchange, target)),
                    }, ShowChoice)
                .WithLines(Resources(), null)
                .WithAccent(HudPalette.Warn, null));
        }

        private void Confirm(RunDemoExchange exchange, int target)
        {
            RunDemoRelic before = _demo.Relics;
            if (!_demo.TryExchange(exchange, target))
            {
                ShowChoice();
                return;
            }
            Exchanged?.Invoke();
            RunDemoRelic gained = _demo.Relics & ~before;
            if (gained == RunDemoRelic.None)
            {
                Finished?.Invoke();
                return;
            }
            PopupController.Show(PopupRequest.Notice("유물을 얻었습니다",
                    $"{RunMerchantPresenter.RelicName(gained)}\n{RunMerchantPresenter.RelicEffect(gained)}", () => Finished?.Invoke())
                .WithAccent(HudPalette.Gain, "이번 런 동안 유지"));
        }

        private void Leave()
        {
            if (_demo.ResolveBranch(-1)) Finished?.Invoke();
        }

        // ── 대상 ──

        private static bool IsRoomExchange(RunDemoExchange exchange) => exchange == RunDemoExchange.RoomForRelic;

        private void CollectTargets(RunDemoExchange exchange)
        {
            _targets.Clear();
            int count = IsRoomExchange(exchange) ? _demo.Carryover.RoomBroken.Length : _demo.MinionCount;
            for (int i = 0; i < count; i++)
                if (_demo.CanExchange(exchange, i)) _targets.Add(i);
        }

        private bool HasTarget(RunDemoExchange exchange)
        {
            CollectTargets(exchange);
            return _targets.Count > 0;
        }

        private string NoTargetWarning(RunDemoExchange? first, RunDemoExchange? second)
        {
            bool none = (first is not RunDemoExchange a || !HasTarget(a)) && (second is not RunDemoExchange b || !HasTarget(b));
            return none && (first.HasValue || second.HasValue) ? "지금은 할 수 있는 거래가 없습니다" : null;
        }

        private string TargetLine(RunDemoExchange exchange, int target)
        {
            if (IsRoomExchange(exchange))
            {
                string room = RunBattleText.RoomName(_encounter.GridLayout[target].Kind);
                return $"{room} · 내구 {UnityEngine.Mathf.CeilToInt(_demo.Carryover.RoomDurability[target])}/{_encounter.Settings.RoomDurability} → 고장";
            }
            RunCombatantSO minion = _demo.Minion(target);
            int health = _demo.Carryover.MinionHealth[target];
            int max = minion.MaxHealth;
            if (exchange == RunDemoExchange.HealthForRelic)
                return $"{minion.DisplayName} · 체력 {health}/{max} → {Math.Max(1, health - _demo.HealthForRelicLoss(target))}/{max}";
            int bonus = _demo.Economy.UpgradeHealth;
            return $"{minion.DisplayName} · 체력 {health}/{max} → {Math.Min(max + bonus, health + bonus)}/{max + bonus} · 공격 {minion.Attack} → {minion.Attack + _demo.Economy.UpgradeAttack}";
        }

        // ── 글 ──

        private static string Heading(RunDemoExchange exchange) => exchange switch
        {
            RunDemoExchange.HeartForUpgrade => "심장을 내고 부하 강화",
            RunDemoExchange.GoldForUpgrade => "금화를 내고 부하 강화",
            RunDemoExchange.HealthForRelic => "부하 체력을 내고 유물",
            _ => "방을 내고 유물",
        };

        private string Effect(RunDemoExchange exchange)
        {
            RunDemoEconomy values = _demo.Economy;
            string upgrade = $"고른 부하 최대 체력 +{values.UpgradeHealth}·공격 +{values.UpgradeAttack}";
            return exchange switch
            {
                RunDemoExchange.HeartForUpgrade => $"심장 -{values.HeartUpgradeCost} · {upgrade}",
                RunDemoExchange.GoldForUpgrade => $"금화 -{values.UpgradePrice} · {upgrade}",
                RunDemoExchange.HealthForRelic => $"고른 부하가 최대 체력의 {UnityEngine.Mathf.RoundToInt(values.HealthForRelicRatio * 100f)}%를 잃음 · 유물 하나",
                _ => "고른 방이 고장 난 채 다음 전투 시작 · 유물 하나",
            };
        }

        private static string ButtonLabel(RunDemoExchange exchange) => exchange switch
        {
            RunDemoExchange.HeartForUpgrade => "심장 → 강화",
            RunDemoExchange.GoldForUpgrade => "금화 → 강화",
            RunDemoExchange.HealthForRelic => "체력 → 유물",
            _ => "방 → 유물",
        };

        private static string ConfirmLabel(RunDemoExchange exchange) => exchange switch
        {
            RunDemoExchange.HeartForUpgrade or RunDemoExchange.GoldForUpgrade => "강화",
            RunDemoExchange.HealthForRelic => "바치기",
            _ => "내주기",
        };

        private string Resources() =>
            $"심장 {_demo.Carryover.HeartHealth}/{_encounter.Settings.HeartHealth} · 금화 {_demo.Carryover.Gold}";
    }
}
