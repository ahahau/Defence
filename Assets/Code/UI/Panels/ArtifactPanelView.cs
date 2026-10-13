using System.Collections.Generic;
using Code.Artifacts;
using Code.Core;
using Code.Events;
using Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class ArtifactPanelView : MonoBehaviour
    {
        [SerializeField] private ArtifactInventorySO artifactInventory;
        [SerializeField] private GameEventChannelSO artifactEventChannel;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private ArtifactEntryView entryPrefab;
        [SerializeField] private GameObject tooltipRoot;
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private TMP_Text tooltipNameText;
        [SerializeField] private TMP_Text tooltipDescriptionText;
        [SerializeField] private Vector2 tooltipOffset = new(18f, 18f);

        private readonly List<ArtifactEntryView> entries = new();
        private CanvasGroup _visibilityGroup;
        private int _lastVisibilityDay = int.MinValue;

        private void Awake()
        {
            _visibilityGroup = GetComponent<CanvasGroup>();
            if (_visibilityGroup == null)
                _visibilityGroup = gameObject.AddComponent<CanvasGroup>();

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text == "아티팩트" || text.text == "Artifacts")
                    text.text = text.text.Replace('\n', ' ');
            }

            RefreshFeatureVisibility();
        }

        private void Update()
        {
            var day = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            if (day != _lastVisibilityDay)
                RefreshFeatureVisibility();
        }

        private void OnEnable()
        {
            artifactEventChannel.AddListener<ArtifactInventoryChangedEvent>(HandleInventoryChanged);
            Refresh();
        }

        private void OnDisable()
        {
            artifactEventChannel.RemoveListener<ArtifactInventoryChangedEvent>(HandleInventoryChanged);
            HideTooltip();
            ClearEntries();
        }

        private void HandleInventoryChanged(ArtifactInventoryChangedEvent evt)
        {
            if (evt.Inventory == artifactInventory)
                Refresh();
        }

        // 화면은 UI Toolkit(ArtifactToolkitView)이 그린다. 이 컴포넌트는 씬에 배선된 인벤토리·채널을 듣고 넘겨주기만 한다.
        // 옛 uGUI 줄은 씬을 정리할 때 함께 지운다.
        private void Refresh()
        {
            ClearEntries();
            Code.UI.Toolkit.ArtifactToolkitView.Ensure()?.SetArtifacts(artifactInventory.ObtainedArtifacts);
        }

        public void ShowTooltip(ArtifactDataSO artifact, Vector2 screenPosition)
        {
            tooltipNameText.text = artifact.DisplayName;
            tooltipDescriptionText.text = artifact.Description;
            tooltipRect.position = screenPosition + tooltipOffset;
            tooltipRoot.SetActive(true);
        }

        public void HideTooltip()
        {
            tooltipRoot.SetActive(false);
        }

        private void ClearEntries()
        {
            foreach (var entry in entries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }

            entries.Clear();
        }

        private void RefreshFeatureVisibility()
        {
            _lastVisibilityDay = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            var unlocked = CoreLoopFeatureUnlocks.IsArtifactUnlocked(_lastVisibilityDay);
            // 옛 uGUI 줄은 늘 숨기고, 보이는 것은 Toolkit 줄이다.
            _visibilityGroup.alpha = 0f;
            _visibilityGroup.interactable = false;
            _visibilityGroup.blocksRaycasts = false;
            HideTooltip();
            Code.UI.Toolkit.ArtifactToolkitView.Ensure()?.SetUnlocked(unlocked);
        }
    }
}
