using _01.Code.Buildings;
using _01.Code.Cameras;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.Manager
{
    public class InputManager : MonoBehaviour
    {
        [field: SerializeField] public InputDataSO InputData { get; private set; }
        [field: SerializeField] public Vector2Int CurrentMouseCellPosition { get; private set; }

        [SerializeField] private LayerMask whatIsClickable;
        [SerializeField] private float dragStartThresholdPixels = 8f;
        [SerializeField] private GameEventChannelSO uiEventChannel;

        private Building _draggedBuilding;
        private bool _isPointerDown;
        private bool _isDraggingBuilding;
        private bool _queuedLeftPointerPressed;
        private bool _queuedLeftPointerReleased;
        private bool _queuedRightPointerPressed;
        private Vector2 _pointerDownScreenPosition;

        public void Initialize()
        {
            InputData.LeftPointerPressed += QueueLeftPointerPressed;
            InputData.LeftPointerReleased += QueueLeftPointerReleased;
            InputData.RightPointerPressed += QueueRightPointerPressed;
            GameManager.Instance.LogManager?.System("InputManager initialized.");
        }

        private void OnDestroy()
        {
            InputData.LeftPointerPressed -= QueueLeftPointerPressed;
            InputData.LeftPointerReleased -= QueueLeftPointerReleased;
            InputData.RightPointerPressed -= QueueRightPointerPressed;
        }

        private void Update()
        {
            Vector2 worldPosition = InputData.GetWorldPosition2D();
            Vector2Int hoveredCell = GameManager.Instance.GridManager.WorldToCell(worldPosition);
            CurrentMouseCellPosition = hoveredCell;

            if (_queuedRightPointerPressed)
            {
                _queuedRightPointerPressed = false;
                HandleRightPointerPressed();
            }

            if (_queuedLeftPointerPressed)
            {
                _queuedLeftPointerPressed = false;
                HandleLeftPointerPressed();
            }

            if (_isPointerDown && !_isDraggingBuilding && ShouldStartDragging(hoveredCell))
            {
                StartDragging();
            }

            if (_isDraggingBuilding && _draggedBuilding != null && InputData.IsLeftPointerPressed)
            {
                DragBuilding(hoveredCell);
            }

            if (_queuedLeftPointerReleased)
            {
                _queuedLeftPointerReleased = false;
                HandleLeftPointerReleased();
            }
        }

        public void OnClick(Vector2 worldPosition)
        {
            Collider2D hit = GetHitCollider(worldPosition);

            if (hit == null)
            {
                ClickGround(worldPosition);
                return;
            }

            ClickObject(hit.gameObject);
        }

        private void ClickObject(GameObject hitGameObject)
        {
            uiEventChannel.RaiseEvent(UIEvents.HideBuildPanelRequested);

            if (!hitGameObject.CompareTag("Building"))
            {
                GameManager.Instance.LogManager?.UI($"Clicked object `{hitGameObject.name}` and hid build panel.");
                return;
            }

            Building building = hitGameObject.GetComponentInParent<Building>();
            if (building != null)
            {
                GameManager.Instance.LogManager?.Building($"Selected existing building `{building.name}`.");
            }
        }

        private void ClickGround(Vector2 worldPosition)
        {
            Vector2Int gridPos = GameManager.Instance.GridManager.WorldToCell(worldPosition);
            CurrentMouseCellPosition = gridPos;
            GameManager.Instance.LogManager?.UI($"Requested build panel at cell {gridPos}.");
            uiEventChannel.RaiseEvent(UIEvents.ShowBuildPanelRequested.Initializer(worldPosition));
        }

        private void HandleRightPointerPressed()
        {
            if (IsPointerOverUi())
            {
                return;
            }

            OnClick(InputData.GetWorldPosition2D());
        }

        private void HandleLeftPointerPressed()
        {
            Vector2 worldPosition = InputData.GetWorldPosition2D();
            if (IsPointerOverUi())
            {
                ResetPointerState();
                return;
            }

            Collider2D hit = GetHitCollider(worldPosition);
            if (hit == null || hit.CompareTag("EnemySpawner"))
            {
                return;
            }

            _draggedBuilding = hit.GetComponentInParent<Building>();
            if (_draggedBuilding != null)
            {
                _isPointerDown = true;
                _isDraggingBuilding = false;
                _pointerDownScreenPosition = InputData.MousePosition;
                StartDragging();
            }
        }

        private void HandleLeftPointerReleased()
        {
            if (!_isPointerDown)
            {
                return;
            }

            Vector3 targetWorldPosition = GameManager.Instance.GridManager.CellToWorld(CurrentMouseCellPosition);

            if (_isDraggingBuilding && _draggedBuilding != null)
            {
                GameManager.Instance.LogManager?.Building($"Requested move for `{_draggedBuilding.name}`.");
                GameManager.Instance.BuildManager.TryMove(_draggedBuilding, targetWorldPosition);
            }

            ResetPointerState();
        }

        private void StartDragging()
        {
            if (_draggedBuilding == null)
            {
                return;
            }

            _isDraggingBuilding = true;
            GameManager.Instance.LogManager?.Building($"Started dragging `{_draggedBuilding.name}` from {_draggedBuilding.GridPosition}.");
            uiEventChannel.RaiseEvent(UIEvents.HideBuildPanelRequested);
        }

        private void DragBuilding(Vector2Int gridPos)
        {
            CurrentMouseCellPosition = gridPos;
            _draggedBuilding.PreviewPosition(gridPos);
        }

        private bool ShouldStartDragging(Vector2Int hoveredCell)
        {
            if (_draggedBuilding == null)
            {
                return false;
            }

            if (hoveredCell != _draggedBuilding.GridPosition)
            {
                return true;
            }

            Vector2 pointerDelta = InputData.MousePosition - _pointerDownScreenPosition;
            return pointerDelta.sqrMagnitude >= dragStartThresholdPixels * dragStartThresholdPixels;
        }

        private bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void QueueLeftPointerPressed()
        {
            _queuedLeftPointerPressed = true;
        }

        private void QueueLeftPointerReleased()
        {
            _queuedLeftPointerReleased = true;
        }

        private void QueueRightPointerPressed()
        {
            _queuedRightPointerPressed = true;
        }

        private void ResetPointerState()
        {
            _isPointerDown = false;
            _isDraggingBuilding = false;
            _draggedBuilding = null;
        }

        private Collider2D GetHitCollider(Vector2 worldPosition)
        {
            if (whatIsClickable.value == 0)
            {
                return Physics2D.OverlapPoint(worldPosition);
            }

            return Physics2D.OverlapPoint(worldPosition, whatIsClickable);
        }
    }
}
