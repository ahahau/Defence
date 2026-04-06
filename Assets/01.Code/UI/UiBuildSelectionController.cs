using System;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.UI
{
    public class UiBuildSelectionController
    {
        private readonly GameEventChannelSO _buildEventChannel;
        private readonly Func<bool> _canUseDayActions;
        private readonly Func<UnitDataSO, bool> _canSelectBuilding;
        private readonly Action _notifyStateChanged;

        private PlaceableEntity _placementPreview;
        private Vector2Int _hoveredCellPosition;

        public UiBuildSelectionController(
            GameEventChannelSO buildEventChannel,
            Func<bool> canUseDayActions,
            Func<UnitDataSO, bool> canSelectBuilding,
            Action notifyStateChanged)
        {
            _buildEventChannel = buildEventChannel;
            _canUseDayActions = canUseDayActions;
            _canSelectBuilding = canSelectBuilding;
            _notifyStateChanged = notifyStateChanged;
        }

        public UnitDataSO SelectedUnit { get; private set; }

        public Vector3 CurrentBuildPosition { get; private set; }

        public event Action<UnitDataSO> OnBuildingSelected;
        public event Action<UnitDataSO, Vector3> OnBuildRequested;

        public void Tick()
        {
            if (_placementPreview == null || SelectedUnit == null)
            {
                return;
            }

            _placementPreview.PreviewPosition(_hoveredCellPosition);
        }

        public void SetHoveredCell(Vector2Int cellPosition)
        {
            _hoveredCellPosition = cellPosition;
        }

        public void SelectBuilding(UnitDataSO unitData)
        {
            if (!_canUseDayActions() || !_canSelectBuilding(unitData))
            {
                return;
            }

            if (SelectedUnit == unitData)
            {
                CancelSelection();
                return;
            }

            SelectedUnit = unitData;
            EnsurePlacementPreview();
            OnBuildingSelected?.Invoke(unitData);
            _notifyStateChanged?.Invoke();
        }

        public void CancelSelection()
        {
            SelectedUnit = null;
            ClearPlacementPreview();
            _notifyStateChanged?.Invoke();
        }

        public bool TryRequestBuild(Vector3 worldPosition)
        {
            if (!_canUseDayActions() || SelectedUnit == null)
            {
                return false;
            }

            CurrentBuildPosition = worldPosition;
            OnBuildRequested?.Invoke(SelectedUnit, worldPosition);
            _buildEventChannel.RaiseEvent(BuildEvents.BuildRequestedEvent.Initializer(SelectedUnit, worldPosition));
            return true;
        }

        public void HandleBuildCompleted()
        {
            ClearPlacementPreview();
            SelectedUnit = null;
            _notifyStateChanged?.Invoke();
        }

        public void Dispose()
        {
            ClearPlacementPreview();
        }

        private void EnsurePlacementPreview()
        {
            ClearPlacementPreview();

            if (SelectedUnit?.Prefab == null)
            {
                return;
            }

            _placementPreview = UnityEngine.Object.Instantiate(SelectedUnit.Prefab);
            _placementPreview.name = $"{SelectedUnit.Name}_Preview";

            foreach (MonoBehaviour behaviour in _placementPreview.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Collider2D collider2D in _placementPreview.GetComponentsInChildren<Collider2D>(true))
            {
                collider2D.enabled = false;
            }

            foreach (Rigidbody2D body in _placementPreview.GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.simulated = false;
            }

            foreach (SpriteRenderer spriteRenderer in _placementPreview.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color color = spriteRenderer.color;
                color.a *= 0.45f;
                spriteRenderer.color = color;
                spriteRenderer.sortingOrder += 1000;
            }
        }

        private void ClearPlacementPreview()
        {
            if (_placementPreview == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_placementPreview.gameObject);
            _placementPreview = null;
        }
    }
}
