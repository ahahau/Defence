using Code.Manager;
using Code.Persistence;

namespace Code.Progression
{
    /// <summary>
    /// 게임 씬이 열릴 때 시작 보정을 건다.
    ///
    /// 판 저장이 없으면 새 판이다 — 장착한 보정을 이번 판 보정으로 확정하고 전부 건다.
    /// 판 저장이 있으면 이어하기다 — 금화·민심처럼 한 번 받은 보정은 이미 저장에 들어 있으므로
    /// 판 내내 유지되는 보정(부활 마력)만 다시 건다. 조립 루트의 시작 단계에서 지원자 생성 전에 적용한다.
    /// </summary>
    public static class MetaRunStarter
    {
        /// <summary>조립 루트가 전달한 매니저에 새 판 또는 이어하기 보정을 적용한다.</summary>
        public static void ApplyForRun(CostManager cost, MoralePolicyManager morale, UnitRevivalSystem revival, bool hasSave)
        {
            if (!hasSave)
            {
                foreach (var value in MetaProfileStore.BeginNewRun())
                    Apply((MetaModifier)value, true, cost, morale, revival);
                return;
            }

            var run = MetaProfileStore.Profile.currentRun;
            if (run == null || !run.active || run.modifiers == null)
                return;

            foreach (var value in run.modifiers)
            {
                var modifier = (MetaModifier)value;
                if (MetaProgressRules.IsPersistent(modifier))
                    Apply(modifier, false, cost, morale, revival);
            }
        }

        private static void Apply(MetaModifier modifier, bool newRun, CostManager cost, MoralePolicyManager morale, UnitRevivalSystem revival)
        {
            switch (modifier)
            {
                case MetaModifier.EmergencyFund:
                    if (newRun)
                        cost?.AddGold(MetaProgressRules.EmergencyFundGold);
                    break;
                case MetaModifier.SteadyMorale:
                    if (newRun)
                        morale?.ApplyBonusMorale(MetaProgressRules.SteadyMoraleBonus, "시작 보정");
                    break;
                case MetaModifier.ManaReserve:
                    revival?.AddMaxManaBonus(MetaProgressRules.ManaReserveBonus);
                    break;
            }
        }
    }
}
