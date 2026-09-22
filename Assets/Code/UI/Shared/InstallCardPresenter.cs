using Code.Buildings;
using Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 설치 카드에 무엇을 보여줄지 결정하는 순수 표시 로직.
    /// 패널 상태를 건드리지 않으므로 노드 패널에서 떼어내 따로 모았다.
    /// </summary>
    public class InstallCardPresenter
    {
        private readonly InstallCardTextFormatter _textFormatter = new();

        public string GetCategoryTitle(InstallCategory category)
        {
            return _textFormatter.GetCategoryTitle(category);
        }

        public string GetCategoryCardText(InstallCategory category)
        {
            return _textFormatter.GetCategoryCardText(category);
        }

        /// <summary>건물 카드에 적을 이름·비용·성능 요약. 건설 할인 중이면 원래 가격과 함께 보여준다.</summary>
        public string BuildCardText(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return string.Empty;
            var discountedCost = CostManager.Current != null
                ? CostManager.Current.GetDiscountedBuildCost(buildingData.Cost)
                : buildingData.Cost;
            return _textFormatter.BuildCardText(buildingData, discountedCost);
        }

        public string FormatTrapDamage(Trap trap)
        {
            if (trap.BonusDamage <= 0)
                return trap.Damage.ToString();

            return $"{trap.Damage}+{trap.BonusDamage}";
        }

        public string FormatTrapStatus(Trap trap)
        {
            if (trap.StatusEffect == null || trap.InjuryChance <= 0f)
                return "상태이상 없음";

            var displayName = string.IsNullOrWhiteSpace(trap.StatusEffect.DisplayName)
                ? trap.StatusEffect.name
                : trap.StatusEffect.DisplayName;
            return $"{displayName}: {FormatPercent(trap.InjuryChance)}";
        }

        public string FormatPercent(float value)
        {
            return _textFormatter.FormatPercent(value);
        }

        /// <summary>카드에 쓸 그림. 프리팹 스프라이트를 먼저 쓰고 없으면 보드용 스프라이트로 넘어간다.</summary>
        public Sprite ResolvePreviewSprite(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return null;

            var prefabSprite = buildingData.Prefab != null
                ? buildingData.Prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite
                : null;

            return prefabSprite != null ? prefabSprite : buildingData.BoardSprite;
        }

        public void SetButtonLabel(Button button, BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return;

            SetButtonText(button, BuildCardText(buildingData));
        }

        public void SetButtonText(Button button, string value)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null)
                return;

            text.text = value;
        }

        public string GetButtonLabel(Button button)
        {
            if (button == null)
                return string.Empty;

            var text = button.GetComponentInChildren<TMP_Text>();
            return text != null ? text.text : string.Empty;
        }

        public void ApplyCardSprite(Button button, Sprite sprite)
        {
            if (button == null)
                return;

            var image = ResolveCardIconImage(button);
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        /// <summary>카드의 아이콘 이미지. "Icon"이라는 이름의 자식을 먼저 찾고, 없으면 배경이 아닌 첫 이미지를 쓴다.</summary>
        public Image ResolveCardIconImage(Button button)
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
