using Code.Buildings;
using UnityEngine;

namespace Code.UI
{
    /// <summary>설치 항목을 UI에 표시할 문구로 바꾸는 재사용 가능한 순수 포맷터.</summary>
    public class InstallCardTextFormatter
    {
        public string FormatPercent(float value) => $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";

        public string FormatTrapDamage(Trap trap) => trap.BonusDamage <= 0
            ? trap.Damage.ToString()
            : $"{trap.Damage}+{trap.BonusDamage}";

        public string FormatTrapStatus(Trap trap)
        {
            if (trap.StatusEffect == null || trap.InjuryChance <= 0f)
                return "상태이상 없음";
            var name = string.IsNullOrWhiteSpace(trap.StatusEffect.DisplayName)
                ? trap.StatusEffect.name
                : trap.StatusEffect.DisplayName;
            return $"{name}: {FormatPercent(trap.InjuryChance)}";
        }

        /// <summary>발동 방식 한 줄. 범위·지속은 아군도 맞는다는 것을 반드시 적는다.</summary>
        public string FormatTrapMode(Trap trap) => trap.TriggerMode switch
        {
            TrapTriggerMode.Area => $"범위(주변 {trap.AreaRadiusCells:0.#}칸, 아군 포함)",
            TrapTriggerMode.Persistent => $"지속({trap.PersistentSeconds:0.#}초, 아군 포함)",
            TrapTriggerMode.Projectile => "투사체(같은 줄 첫 모험가)",
            _ => "밟기(한 명)"
        };

        /// <summary>건설비 표기. 건설 할인 중이면 원래 값과 함께 적는다.</summary>
        public string FormatCost(BuildingDataSO buildingData, int discountedCost) =>
            buildingData == null || buildingData.Cost <= 0 ? "무료"
            : discountedCost < buildingData.Cost ? $"{buildingData.Cost}→{discountedCost}G"
            : $"{buildingData.Cost}G";

        /// <summary>설치 창 상세의 요약 한 줄: 값·운영비·위험·놓는 자리.</summary>
        public string BuildSummary(BuildingDataSO buildingData, int discountedCost)
        {
            if (buildingData == null)
                return string.Empty;

            var text = $"건설 {FormatCost(buildingData, discountedCost)} · 운영비 {buildingData.DailyUpkeep}G/일 · 위험 {buildingData.BaseDanger}";
            return text + " · " + FormatPlacement(buildingData);
        }

        /// <summary>설치 창 상세의 효과(두세 줄). 함정은 피해·발동, 회복실은 회복량, 수익 시설은 인접 보너스.</summary>
        public string BuildEffect(BuildingDataSO buildingData)
        {
            if (buildingData == null || buildingData.Prefab == null)
                return string.Empty;

            if (buildingData.Prefab is Trap trap)
                return $"피해 {FormatTrapDamage(trap)} · 발동 {FormatPercent(trap.TriggerChance)} · {FormatTrapStatus(trap)}\n"
                       + $"{FormatTrapMode(trap)} · 재사용 {trap.CooldownSeconds:0.#}초 · 내구도 {trap.MaxDurability}";

            if (buildingData.Prefab is RecoveryFacility recovery)
            {
                var text = $"웨이브마다 피로 -{Mathf.RoundToInt(recovery.FatigueRecoveryPerWave)}";
                if (recovery.HealthRecoveryRatioPerWave > 0f) text += $" · HP +{FormatPercent(recovery.HealthRecoveryRatioPerWave)}";
                if (recovery.ImproveInjury) text += " · 부상 완화";
                return text;
            }

            var effect = buildingData.DwellSeconds > 0f
                ? $"모험가가 {buildingData.DwellSeconds:0.#}초 머물며 돈을 씁니다 · 이 방에는 유닛을 못 세웁니다"
                : $"등급 무게 {buildingData.GradeWeight}";
            if (!string.IsNullOrWhiteSpace(buildingData.Description))
                effect = buildingData.Description;
            if (buildingData.DailyUpkeep > 0)
                effect += "\n이웃한 수익 시설이 있으면 수입 최대 +40%";
            return buildingData.Prefab.IsDestructible
                ? effect + $"\n내구도 {buildingData.Prefab.MaxDurability}"
                : effect;
        }

        /// <summary>카드 아래 한 줄 설명.</summary>
        public string BuildCardNote(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return string.Empty;

            if (buildingData.Prefab is Trap trap)
                return $"피해 {FormatTrapDamage(trap)} · {trap.Footprint.x}×{trap.Footprint.y}칸";

            return buildingData.DailyUpkeep > 0
                ? $"운영비 {buildingData.DailyUpkeep}G/일"
                : FormatPlacement(buildingData);
        }

        private static string FormatPlacement(BuildingDataSO buildingData)
        {
            if (buildingData.InstallOnEdge)
                return "방 사이 설치";

            if (!BuildingPlacement.UsesGridCell(buildingData))
                return "중앙 · 방당 1개";

            var footprint = buildingData.Prefab != null ? buildingData.Prefab.Footprint : Vector2Int.one;
            return $"{footprint.x}×{footprint.y}칸";
        }
    }
}
