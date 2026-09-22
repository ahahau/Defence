using Code.Buildings;
using UnityEngine;

namespace Code.UI
{
    /// <summary>설치 항목을 UI에 표시할 문구로 바꾸는 재사용 가능한 순수 포맷터.</summary>
    public class InstallCardTextFormatter
    {
        public string GetCategoryTitle(InstallCategory category) => category switch
        {
            InstallCategory.Building => "빌딩 설치",
            InstallCategory.Unit => "유닛 배치",
            InstallCategory.Trap => "함정 설치",
            InstallCategory.Decoration => "장식품 설치",
            _ => "설치"
        };

        public string GetCategoryCardText(InstallCategory category) => category switch
        {
            InstallCategory.Building => "빌딩\n건물 목록 보기",
            InstallCategory.Unit => "유닛\n보유 유닛 배치",
            InstallCategory.Trap => "함정\n피해/상태이상 설치",
            InstallCategory.Decoration => "장식품\n꾸미기 설치",
            _ => "설치"
        };

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

        public string BuildCardText(BuildingDataSO buildingData, int discountedCost)
        {
            if (buildingData == null)
                return string.Empty;

            var name = string.IsNullOrWhiteSpace(buildingData.DisplayName) ? buildingData.name : buildingData.DisplayName;
            var cost = buildingData.Cost <= 0 ? "무료"
                : discountedCost < buildingData.Cost ? $"{buildingData.Cost} → {discountedCost}G" : $"{buildingData.Cost}G";
            var text = $"{name}\n건설  {cost}   ·   경계 +{buildingData.BaseDanger}\n등급 {(int)buildingData.Grade}";
            text += buildingData.InstallOnEdge ? "\n방 사이 설치"
                : BuildingPlacement.UsesGridCell(buildingData) ? "\n개별 칸 설치" : "\n중앙 전용 · 방당 1개";
            if (buildingData.DailyUpkeep > 0) text += $"\n운영비 {buildingData.DailyUpkeep}G/일 · 인접 수익 최대 +40%";
            if (buildingData.Prefab == null) return text;
            if (buildingData.Prefab is Trap trap)
                text += $"\n피해: {FormatTrapDamage(trap)}\n발동: {FormatPercent(trap.TriggerChance)} / {FormatTrapStatus(trap)}";
            if (buildingData.Prefab is RecoveryFacility recovery)
            {
                text += $"\n회복: 피로 -{Mathf.RoundToInt(recovery.FatigueRecoveryPerWave)}";
                if (recovery.HealthRecoveryRatioPerWave > 0f) text += $" / HP +{FormatPercent(recovery.HealthRecoveryRatioPerWave)}";
                if (recovery.ImproveInjury) text += " / 부상 완화";
            }
            return buildingData.Prefab.IsDestructible ? text + $"\n내구도: {buildingData.Prefab.MaxDurability}" : text;
        }
    }
}
