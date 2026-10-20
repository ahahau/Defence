using Code.Artifacts;
using Code.Core;
using Code.Events;
using Code.Manager;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 씬에 배선된 유물 인벤토리·채널을 듣고 유물 줄에 보일 값을 정한다.
    /// 줄이 열리는 날(<see cref="CoreLoopFeatureUnlocks.IsArtifactUnlocked"/>)도 여기서 판정한다.
    /// 그리는 화면은 지금 없다(uGUI 전환 대기) — 화면은 <see cref="Changed"/>를 듣고 <see cref="Artifacts"/>·<see cref="IsUnlocked"/>를 읽는다.
    /// </summary>
    public class ArtifactPanelView : MonoBehaviour
    {
        [SerializeField] private ArtifactInventorySO artifactInventory;
        [SerializeField] private GameEventChannelSO artifactEventChannel;

        private int _lastVisibilityDay = int.MinValue;

        public System.Collections.Generic.IReadOnlyList<ArtifactDataSO> Artifacts => artifactInventory.ObtainedArtifacts;
        public bool IsUnlocked { get; private set; }

        /// <summary>유물 목록이나 해금 여부가 바뀌었다.</summary>
        public event System.Action Changed;

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
            Changed?.Invoke();
        }

        private void RefreshFeatureVisibility()
        {
            _lastVisibilityDay = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            IsUnlocked = CoreLoopFeatureUnlocks.IsArtifactUnlocked(_lastVisibilityDay);
            Changed?.Invoke();
        }
    }
}
