using Code.Enemies;
using Code.StatusEffects;
using UnityEngine;

namespace Code.UI.ViewModels
{
    /// <summary>
    /// 모험가 한 명의 상태를 창 값으로 옮긴다: 체력·공격·목적·예산·파티 자금·경계·탐욕·철수 확률,
    /// 시설 대기 만족도와 상태이상. 문구는 지운 Toolkit 모험가 상태 창과 같다.
    /// </summary>
    public sealed class EnemyStatusPresenter
    {
        public EnemyStatusViewModel Build(Enemy target)
        {
            if (target == null)
                return default;

            var health = target.Health;
            var combatant = target.Combatant;
            var model = new EnemyStatusViewModel
            {
                IsValid = true,
                Title = $"{target.DisplayName} Lv {target.Level}",
                State = ResolveState(target),
                Hp = health != null ? $"HP {health.CurrentHealth}/{health.MaxHealth}" : "HP -",
                Attack = combatant != null
                    ? $"ATK {combatant.AttackDamage}  SPD {combatant.AttackInterval:0.##}s"
                    : "ATK -  SPD -",
                // 예산은 이 모험가가 쓸 마음이 있는 금액, 파티 자금은 실제 지갑이다. 지갑은 단계로만 보여 준다.
                Purpose = $"목적 {target.VisitPurposeLabel}  ·  예산 {target.RemainingBudget}/{target.InitialBudget}G"
                          + $"  ·  파티 자금 {PartyFundLedger.TierLabel(target.PartyFundTier)}",
                Party = ResolveParty(target),
                Mood = $"경계 {target.Fear}  ·  탐욕 {target.Greed}  ·  철수 {Mathf.RoundToInt(target.RetreatChance * 100f)}%",
                Satisfaction = ResolveSatisfaction(target),
            };

            FillEffects(target, ref model);
            return model;
        }

        private static string ResolveState(Enemy target)
        {
            if (target.Health == null || !target.Health.IsAlive)
                return "처치됨";

            var trait = string.IsNullOrWhiteSpace(target.TraitLabel) ? string.Empty : $"{target.TraitLabel} · ";
            return $"{trait}{target.VisitPurposeLabel} · {target.InstinctState}";
        }

        /// <summary>함께 다니는 파티. 남은 인원·끌고 다니는 관·리더 여부.</summary>
        private static string ResolveParty(Enemy target)
        {
            var partyId = target.PartyId;
            var alive = PartyFundLedger.Active.AliveMembersOf(partyId);
            var coffins = PartyCoffin.CountFor(partyId);
            var role = target.IsBoss ? "보스" : target.IsPartyLeader ? "리더" : "파티원";
            var line = $"파티 {Mathf.Max(1, alive)}명  ·  {role}";
            return coffins > 0 ? $"{line}  ·  관 {coffins}" : line;
        }

        /// <summary>
        /// 시설 줄에서의 만족도와 다음에 일어날 일. 줄에 서 있지 않으면 지금 이용 중이거나
        /// 마지막으로 이용한 시설의 만족도를 보여 준다.
        /// </summary>
        private static string ResolveSatisfaction(Enemy target)
        {
            var waiting = target.WaitingFacility;
            if (waiting != null)
            {
                var wait = target.FacilityWaitSeconds;
                var head = $"만족도 {Percent(target.QueueSatisfaction)}  ·  {FacilityName(waiting)} 대기 {Mathf.FloorToInt(wait)}초";
                if (wait < target.AlternativeSearchSeconds)
                    return $"{head}  ·  {Mathf.CeilToInt(target.AlternativeSearchSeconds - wait)}초 뒤 딴 곳 찾음";

                // 대체 시설을 찾는 중인데 아직 줄에 있다면 갈 곳이 없다는 뜻이다.
                return target.EscapeWaitSeconds < float.MaxValue
                    ? $"{head}  ·  {Mathf.CeilToInt(Mathf.Max(0f, target.EscapeWaitSeconds - wait))}초 뒤 떠남"
                    : $"{head}  ·  계속 기다림";
            }

            var dwelling = target.DwellFacility;
            if (dwelling != null)
                return $"만족도 {Percent(target.LastVisitSatisfaction)}  ·  {FacilityName(dwelling)} 이용 중";

            return target.LastVisitSatisfaction >= 0f
                ? $"마지막 이용 만족도 {Percent(target.LastVisitSatisfaction)}"
                : "시설 대기 없음";
        }

        private static void FillEffects(Enemy target, ref EnemyStatusViewModel model)
        {
            var controller = target.StatusController != null
                ? target.StatusController
                : target.GetComponent<EnemyStatusController>();
            var active = controller != null ? controller.GetActiveEffects() : null;
            var count = active != null ? active.Count : 0;
            model.EffectCount = Mathf.Min(count, EnemyStatusViewModel.MaxEffects);

            for (var i = 0; i < model.EffectCount; i++)
            {
                var effect = active[i].Effect;
                // 넘치는 상태이상은 마지막 줄에 개수로 줄인다.
                var text = i == EnemyStatusViewModel.MaxEffects - 1 && count > EnemyStatusViewModel.MaxEffects
                    ? $"외 {count - (EnemyStatusViewModel.MaxEffects - 1)}개"
                    : effect != null ? $"{effect.DisplayName}  {active[i].RemainingNodeVisits}칸" : string.Empty;
                switch (i)
                {
                    case 0: model.Effect0 = text; break;
                    case 1: model.Effect1 = text; break;
                    case 2: model.Effect2 = text; break;
                    default: model.Effect3 = text; break;
                }
            }
        }

        private static string Percent(float ratio) => $"{Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f)}%";

        private static string FacilityName(Code.Buildings.Building building) =>
            building.Data != null && !string.IsNullOrWhiteSpace(building.Data.DisplayName)
                ? building.Data.DisplayName
                : building.name;
    }
}
