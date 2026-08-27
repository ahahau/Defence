using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
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
                    TmpTextLayoutUtility.KeepHorizontal(text, true);
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

        private void Refresh()
        {
            ClearEntries();

            foreach (var artifact in artifactInventory.ObtainedArtifacts)
            {
                if (artifact == null)
                    continue;

                var entry = Instantiate(entryPrefab, contentRoot);
                entry.Initialize(artifact, this);
                entries.Add(entry);
            }
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
            _visibilityGroup.alpha = unlocked ? 1f : 0f;
            _visibilityGroup.interactable = unlocked;
            _visibilityGroup.blocksRaycasts = unlocked;
            if (!unlocked)
                HideTooltip();
        }
    }
}
