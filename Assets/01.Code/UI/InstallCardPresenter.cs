using _01.Code.Buildings;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 설치 카드에 무엇을 보여줄지 결정하는 순수 표시 로직.
    /// 패널 상태를 건드리지 않으므로 노드 패널에서 떼어내 따로 모았다.
    /// </summary>
    public static class InstallCardPresenter
    {
        public static string GetCategoryTitle(InstallCategory category)
        {
            return category switch
            {
                InstallCategory.Building => "빌딩 설치",
                InstallCategory.Unit => "유닛 배치",
                InstallCategory.Trap => "함정 설치",
                InstallCategory.Decoration => "장식품 설치",
                _ => "설치"
            };
        }

        public static string GetCategoryCardText(InstallCategory category)
        {
            return category switch
            {
                InstallCategory.Building => "빌딩\n건물 목록 보기",
                InstallCategory.Unit => "유닛\n보유 유닛 배치",
                InstallCategory.Trap => "함정\n피해/상태이상 설치",
                InstallCategory.Decoration => "장식품\n꾸미기 설치",
                _ => "설치"
            };
        }

        public static Color GetCategoryAccent(InstallCategory category)
        {
            return category switch
            {
                InstallCategory.Unit => new Color(0.34f, 0.72f, 0.92f, 1f),
                InstallCategory.Trap => new Color(0.9f, 0.28f, 0.14f, 1f),
                InstallCategory.Decoration => new Color(0.48f, 0.74f, 0.42f, 1f),
                _ => new Color(0.88f, 0.6f, 0.2f, 1f)
            };
        }

        /// <summary>건물 카드에 적을 이름·비용·성능 요약. 건설 할인 중이면 원래 가격과 함께 보여준다.</summary>
        public static string BuildCardText(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return string.Empty;

            var displayName = string.IsNullOrWhiteSpace(buildingData.DisplayName)
                ? buildingData.name
                : buildingData.DisplayName;

            var discountedCost = CostManager.Current != null
                ? CostManager.Current.GetDiscountedBuildCost(buildingData.Cost)
                : buildingData.Cost;
            var costText = buildingData.Cost <= 0
                ? "무료"
                : discountedCost < buildingData.Cost
                    ? $"{buildingData.Cost} → {discountedCost}G"
                    : $"{buildingData.Cost}G";
            var text = $"{displayName}\n건설  {costText}   ·   경계 +{buildingData.BaseDanger}\n등급 {(int)buildingData.Grade}";

            if (buildingData.Prefab == null)
                return text;

            if (buildingData.Prefab is Trap trap)
            {
                text += $"\n피해: {FormatTrapDamage(trap)}";
                text += $"\n발동: {FormatPercent(trap.TriggerChance)} / {FormatTrapStatus(trap)}";
            }

            if (buildingData.Prefab is RecoveryFacility recoveryFacility)
            {
                text += $"\n회복: 피로 -{Mathf.RoundToInt(recoveryFacility.FatigueRecoveryPerWave)}";
                if (recoveryFacility.HealthRecoveryRatioPerWave > 0f)
                    text += $" / HP +{FormatPercent(recoveryFacility.HealthRecoveryRatioPerWave)}";
                if (recoveryFacility.ImproveInjury)
                    text += " / 부상 완화";
            }

            if (buildingData.Prefab.IsDestructible)
                text += $"\n내구도: {buildingData.Prefab.MaxDurability}";

            return text;
        }

        public static string FormatTrapDamage(Trap trap)
        {
            if (trap.BonusDamage <= 0)
                return trap.Damage.ToString();

            return $"{trap.Damage}+{trap.BonusDamage}";
        }

        public static string FormatTrapStatus(Trap trap)
        {
            if (trap.StatusEffect == null || trap.InjuryChance <= 0f)
                return "상태이상 없음";

            var displayName = string.IsNullOrWhiteSpace(trap.StatusEffect.DisplayName)
                ? trap.StatusEffect.name
                : trap.StatusEffect.DisplayName;
            return $"{displayName}: {FormatPercent(trap.InjuryChance)}";
        }

        public static string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        /// <summary>카드에 쓸 그림. 프리팹 스프라이트를 먼저 쓰고 없으면 보드용 스프라이트로 넘어간다.</summary>
        public static Sprite ResolvePreviewSprite(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return null;

            var prefabSprite = buildingData.Prefab != null
                ? buildingData.Prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite
                : null;

            return prefabSprite != null ? prefabSprite : buildingData.BoardSprite;
        }

        public static void SetButtonLabel(Button button, BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return;

            SetButtonText(button, BuildCardText(buildingData));
        }

        public static void SetButtonText(Button button, string value)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null)
                return;

            TmpTextLayoutUtility.KeepHorizontal(text);
            text.text = value;
        }

        public static string GetButtonLabel(Button button)
        {
            if (button == null)
                return string.Empty;

            var text = button.GetComponentInChildren<TMP_Text>();
            return text != null ? text.text : string.Empty;
        }

        public static void ApplyCardSprite(Button button, Sprite sprite)
        {
            if (button == null)
                return;

            var image = ResolveCardIconImage(button);
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        /// <summary>카드의 아이콘 이미지. "Icon"이라는 이름의 자식을 먼저 찾고, 없으면 배경이 아닌 첫 이미지를 쓴다.</summary>
        public static Image ResolveCardIconImage(Button button)
        {
            if (button == null)
                return null;

            for (var i = 0; i < button.transform.childCount; i++)
            {
                var child = button.transform.GetChild(i);
                if (child.name == "Icon" && child.TryGetComponent<Image>(out var iconImage))
                    return iconImage;
            }

            foreach (var image in button.GetComponentsInChildren<Image>(true))
            {
                if (image != null && image != button.targetGraphic)
                    return image;
            }

            return null;
        }
    }
}
