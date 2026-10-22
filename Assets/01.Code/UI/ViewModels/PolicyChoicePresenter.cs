using System.Collections.Generic;
using System.Text;
using Code.Manager;
using UnityEngine;

namespace Code.UI.ViewModels
{
    /// <summary>
    /// 운영 방침 창의 값을 만든다. 판단은 <see cref="MoralePolicyManager"/>(제시·공포)와 <see cref="PolicyChoiceRules"/>(금화 잠금)가 하고,
    /// 여기서는 그 결과를 글자·색으로 옮긴다. 효과 줄은 0인 항목을 빼고 금화 → 민심 → 매일 민심 → 부하 공격 → 부하 방어 순이다.
    /// </summary>
    public static class PolicyChoicePresenter
    {
        public const int MaxEffectLines = 5;
        public const int LowMorale = 40;
        public const int CriticalMorale = 20;

        public static PolicyChoiceViewModel Build(
            IReadOnlyList<PolicyDataSO> choices,
            int day,
            int gold,
            int morale,
            IReadOnlyList<KeyValuePair<PolicyDataSO, int>> active,
            System.Func<PolicyDataSO, IReadOnlyList<(PolicyCombo combo, PolicyDataSO partner, bool partnerActive)>> combosFor)
        {
            var model = new PolicyChoiceViewModel
            {
                Title = $"운영 방침 · {day}일차",
                Subtitle = "하나를 골라 공포합니다 · 고를 때까지 시간이 멈춥니다",
                MoraleLabel = $"{Mathf.Clamp(morale, 0, 100)} / 100",
                MoraleRatio = Mathf.Clamp01(morale / 100f),
                MoraleTone = morale < CriticalMorale ? 2 : morale < LowMorale ? 1 : 0,
                ActivePolicies = DescribeActive(active)
            };

            var affordable = new List<bool>();
            if (choices != null)
            {
                foreach (var policy in choices)
                {
                    if (policy == null)
                        continue;

                    var canAfford = PolicyChoiceRules.IsAffordable(policy.GoldDeltaOnSelect, gold);
                    affordable.Add(canAfford);
                    model.Cards.Add(BuildCard(policy, canAfford, combosFor?.Invoke(policy)));
                }
            }

            model.ShowDecline = PolicyChoiceRules.ShouldOfferDecline(affordable);
            model.Footer = model.ShowDecline
                ? "금화가 모자라 어느 방침도 공포할 수 없습니다"
                : "방침은 기간이 끝날 때까지 이어집니다";
            return model;
        }

        public static PolicyCardViewModel BuildCard(PolicyDataSO policy, bool affordable,
            IReadOnlyList<(PolicyCombo combo, PolicyDataSO partner, bool partnerActive)> combos)
        {
            var labels = new StringBuilder();
            var values = new StringBuilder();
            var lines = 0;
            void Line(string label, string value, bool good)
            {
                if (lines >= MaxEffectLines)
                    return;
                if (lines > 0)
                {
                    labels.Append('\n');
                    values.Append('\n');
                }

                labels.Append(label);
                values.Append(HudPalette.Paint(value, good ? HudPalette.Gain : HudPalette.Loss));
                lines++;
            }

            if (policy.GoldDeltaOnSelect != 0)
                Line("금화", Signed(policy.GoldDeltaOnSelect), policy.GoldDeltaOnSelect > 0);
            if (policy.MoraleDeltaOnSelect != 0)
                Line("민심", Signed(policy.MoraleDeltaOnSelect), policy.MoraleDeltaOnSelect > 0);
            if (policy.DailyMoraleDelta != 0)
                Line("매일 민심", Signed(policy.DailyMoraleDelta), policy.DailyMoraleDelta > 0);
            var attack = Mathf.RoundToInt((policy.UnitDamageMultiplier - 1f) * 100f);
            if (attack != 0)
                Line("부하 공격", Signed(attack) + "%", attack > 0);
            if (policy.UnitDefenseBonus != 0)
                Line("부하 방어", Signed(policy.UnitDefenseBonus), policy.UnitDefenseBonus > 0);

            var card = new PolicyCardViewModel
            {
                Name = policy.DisplayName,
                Duration = HasLastingEffect(policy) ? $"{policy.DurationDays}일간" : "즉시",
                Description = policy.Description,
                EffectLabels = labels.ToString(),
                EffectValues = values.ToString(),
                Locked = !affordable,
                ButtonLabel = affordable ? "이 방침으로" : "이 방침으로 · 금화 부족"
            };

            FillCombo(ref card, policy, combos);
            return card;
        }

        /// <summary>
        /// 짝이 시행 중인 조합이 있으면 그 하나를 금색으로("조합 성립"), 없으면 묶이는 짝 목록("함께 쓰면 → 조합")을 적는다.
        /// 조합이 없는 방침은 비운다(칸 자리는 남는다).
        /// </summary>
        private static void FillCombo(ref PolicyCardViewModel card, PolicyDataSO policy,
            IReadOnlyList<(PolicyCombo combo, PolicyDataSO partner, bool partnerActive)> combos)
        {
            card.ComboTitle = string.Empty;
            card.ComboBody = string.Empty;
            if (combos == null || combos.Count == 0)
                return;

            foreach (var (combo, partner, partnerActive) in combos)
            {
                if (!partnerActive)
                    continue;

                card.ComboComplete = true;
                card.ComboTitle = $"조합 성립 · {combo.DisplayName}";
                var effect = MoralePolicyManager.DescribeComboEffect(combo);
                card.ComboBody = $"{MoralePolicyManager.WithParticle(partner.DisplayName)} 함께"
                                 + (string.IsNullOrEmpty(effect) ? string.Empty : $" · {effect}");
                return;
            }

            card.ComboTitle = "함께 쓰면";
            var body = new StringBuilder();
            foreach (var (combo, partner, _) in combos)
            {
                if (body.Length > 0)
                    body.Append('\n');
                body.Append($"{partner.DisplayName} → {combo.DisplayName}");
            }

            card.ComboBody = body.ToString();
        }

        private static string DescribeActive(IReadOnlyList<KeyValuePair<PolicyDataSO, int>> active)
        {
            if (active == null || active.Count == 0)
                return "시행 중인 방침 없음";

            var text = new StringBuilder("시행 중 ");
            for (var i = 0; i < active.Count; i++)
            {
                if (i > 0)
                    text.Append(" · ");
                text.Append($"{active[i].Key.DisplayName} {active[i].Value}일");
            }

            return text.ToString();
        }

        /// <summary>공포 뒤에도 남는 효과(매일 민심·전투)가 있는가. 없으면 즉시 끝나는 방침이다.</summary>
        private static bool HasLastingEffect(PolicyDataSO policy) =>
            policy.DurationDays > 0 && (policy.DailyMoraleDelta != 0 || policy.HasCombatEffect);

        private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
