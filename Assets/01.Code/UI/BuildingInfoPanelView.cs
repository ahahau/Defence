using System;
using _01.Code.Buildings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class BuildingInfoPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text dangerText;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private Button installButton;

        public void SetInstallHandler(Action handler)
        {
            if (installButton == null)
                return;

            installButton.onClick.RemoveAllListeners();
            if (handler != null)
                installButton.onClick.AddListener(() => handler.Invoke());
        }

        public void SetInstallInteractable(bool interactable)
        {
            if (installButton != null)
                installButton.interactable = interactable;
        }

        public void Show(BuildingDataSO buildingData)
        {
            SetVisible(true);

            if (buildingData == null)
            {
                ShowEmpty();
                return;
            }

            SetInstallButtonVisible(true);
            SetText(nameText, string.IsNullOrWhiteSpace(buildingData.DisplayName)
                ? buildingData.name
                : buildingData.DisplayName);
            SetText(costText, buildingData.Cost > 0 ? $"비용: {buildingData.Cost} Gold" : "비용: 무료");
            SetText(dangerText, $"위험도: {buildingData.BaseDanger}");
            SetText(gradeText, $"등급: {(int)buildingData.Grade}");
            ApplyIcon(buildingData);
        }

        public void ShowEmpty()
        {
            SetVisible(true);
            SetText(nameText, string.Empty);
            SetText(costText, string.Empty);
            SetText(dangerText, string.Empty);
            SetText(gradeText, string.Empty);
            SetInstallButtonVisible(true);
            SetInstallInteractable(false);
            ApplyIcon(null);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            var target = root != null ? root : gameObject;
            target.SetActive(visible);
        }

        private void ApplyIcon(BuildingDataSO buildingData)
        {
            if (iconImage == null)
                return;

            var sprite = ResolvePreviewSprite(buildingData);
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        private void SetInstallButtonVisible(bool visible)
        {
            if (installButton != null)
                installButton.gameObject.SetActive(visible);
        }

        private static Sprite ResolvePreviewSprite(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return null;

            var prefabSprite = buildingData.Prefab != null
                ? buildingData.Prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite
                : null;

            return prefabSprite != null ? prefabSprite : buildingData.BoardSprite;
        }

        private void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
