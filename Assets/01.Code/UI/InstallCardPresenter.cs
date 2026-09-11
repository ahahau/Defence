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
            text += buildingData.InstallOnEdge ? "\n통로 설치"
                : BuildingPlacement.UsesGridCell(buildingData) ? "\n개별 칸 설치" : "\n중앙 전용 · 방당 1개";

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

        /// <summary>
        /// 카드 글을 상자 안에서 접히게 채운다.
        ///
        /// <see cref="SetButtonText"/>는 <see cref="TmpTextLayoutUtility.KeepHorizontal"/>로
        /// 줄바꿈을 막는다. 건물 카드처럼 짧은 이름표가 두 줄로 접히는 걸 막으려던 것인데,
        /// 유물처럼 설명이 붙는 카드에 쓰면 한 줄로 끝없이 뻗어 카드 밖으로 새어 나간다.
        /// 상인 화면에서 설명이 패널 반대편까지 넘어간 것이 그것이다.
        ///
        /// 길이를 미리 알 수 없으므로 글자 크기도 상자에 맞춰 줄어들게 둔다.
        /// </summary>
        public static void SetWrappedButtonText(Button button, string value)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null)
                return;

            text.rectTransform.localRotation = Quaternion.identity;
            text.rectTransform.localScale = Vector3.one;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.Top;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 22f;
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
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        /// <summary>
        /// 카드 속을 보기 좋은 크기로 맞춘다.
        ///
        /// 예전에는 아이콘과 글의 사각형을 직접 230x230, 위에서 265픽셀 같은 숫자로 박아 넣었다.
        /// 그런데 이 카드에는 세로 배치(VerticalLayoutGroup)가 붙어 있어서, 다음 배치 계산이
        /// 그 숫자를 전부 덮어쓴다. 두 주인이 같은 사각형을 서로 다르게 잡고 있었던 셈이고
        /// 결과는 그때그때 달랐다 — 카드가 "안 맞아" 보이던 이유다.
        ///
        /// 배치가 붙어 있으면 자리는 배치에 맡기고, 여기서는 배치가 손대지 않는 것만 정한다 —
        /// 비율 유지, 클릭 통과, 글자 자동 축소.
        /// </summary>
        public static void EnlargeCard(Button button)
        {
            var layoutDriven = button != null && button.GetComponent<LayoutGroup>() != null;

            var icon = ResolveCardIconImage(button);
            if (icon != null)
            {
                if (!layoutDriven)
                {
                    var rect = icon.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -20f);
                    rect.sizeDelta = new Vector2(230f, 230f);
                    rect.localScale = Vector3.one;
                }

                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;

            if (!layoutDriven)
            {
                var textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(20f, 18f);
                textRect.offsetMax = new Vector2(-20f, -265f);
                textRect.localScale = Vector3.one;
            }

            text.margin = Vector4.zero;
            text.enableAutoSizing = true;
            text.fontSizeMin = 17f;
            text.fontSizeMax = 24f;
            text.alignment = TextAlignmentOptions.Top;
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
