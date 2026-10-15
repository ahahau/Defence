using Code.Artifacts;
using Code.Core;
using Code.Events;
using Code.Manager;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 씬에 배선된 유물 인벤토리·채널을 듣고 UI Toolkit 유물 줄(<c>ArtifactToolkitView</c>)로 넘긴다.
    /// 줄이 열리는 날(<see cref="CoreLoopFeatureUnlocks.IsArtifactUnlocked"/>)도 여기서 판정한다. 화면은 Toolkit이 그린다.
    /// </summary>
    public class ArtifactPanelView : MonoBehaviour
    {
        [SerializeField] private ArtifactInventorySO artifactInventory;
        [SerializeField] private GameEventChannelSO artifactEventChannel;

        private int _lastVisibilityDay = int.MinValue;

        private void Awake() => RefreshFeatureVisibility();

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
        }

        private void HandleInventoryChanged(ArtifactInventoryChangedEvent evt)
        {
            if (evt.Inventory == artifactInventory)
                Refresh();
        }

        private void Refresh()
        {
            Code.UI.Toolkit.ArtifactToolkitView.Ensure()?.SetArtifacts(artifactInventory.ObtainedArtifacts);
        }

        private void RefreshFeatureVisibility()
        {
            _lastVisibilityDay = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            Code.UI.Toolkit.ArtifactToolkitView.Ensure()?.SetUnlocked(CoreLoopFeatureUnlocks.IsArtifactUnlocked(_lastVisibilityDay));
        }
    }
}
