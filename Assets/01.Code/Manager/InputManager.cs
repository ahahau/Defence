using _01.Code.Cameras;
using _01.Code.Core;
using _01.Code.Unit;
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
        private Unit.Unit _draggedUnit;
        private Collider2D _pointerDownCollider;
        private bool _isPointerDown;
        private bool _isDraggingBuilding;
        private bool _queuedLeftPointerPressed;
        private bool _queuedLeftPointerReleased;
        private bool _queuedRightPointerPressed;
        private Vector2 _pointerDownScreenPosition;
        private Vector2 _pointerDownWorldPosition;

        public void Initialize()
        {
            InputData.LeftPointerPressed += QueueLeftPointerPressed;
            InputData.LeftPointerReleased += QueueLeftPointerReleased;
            InputData.RightPointerPressed += QueueRightPointerPressed;
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

            if (_isDraggingBuilding && _draggedUnit != null && InputData.IsLeftPointerPressed)
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
            if (!hitGameObject.CompareTag("Building"))
            {
                return;
            }
        }

        private void ClickGround(Vector2 worldPosition)
        {
            Vector2Int gridPos = GameManager.Instance.GridManager.WorldToCell(worldPosition);
            CurrentMouseCellPosition = gridPos;
            GameManager.Instance.UiManager?.TryRequestBuild(GameManager.Instance.GridManager.CellToWorld(gridPos));
        }

        private void HandleRightPointerPressed()
        {
            if (IsPointerOverUi())
            {
                return;
            }

            GameManager.Instance.UiManager?.CancelSelection();
            ResetPointerState();
        }

        private void HandleLeftPointerPressed()
        {
            Vector2 worldPosition = InputData.GetWorldPosition2D();
            if (IsPointerOverUi())
            {
                ResetPointerState();
                return;
            }

            _isPointerDown = true;
            _isDraggingBuilding = false;
            _pointerDownScreenPosition = InputData.MousePosition;
            _pointerDownWorldPosition = worldPosition;
            _pointerDownCollider = GetHitCollider(worldPosition);
            _draggedUnit = _pointerDownCollider != null && !_pointerDownCollider.CompareTag("EnemySpawner")
                ? _pointerDownCollider.GetComponentInParent<Unit.Unit>()
                : null;
        }

        private void HandleLeftPointerReleased()
        {
            if (!_isPointerDown)
            {
                return;
            }

            Vector3 targetWorldPosition = GameManager.Instance.GridManager.CellToWorld(CurrentMouseCellPosition);

            if (_isDraggingBuilding && _draggedUnit != null)
            {
                GameManager.Instance.BuildManager.TryMove(_draggedUnit, targetWorldPosition);
                ResetPointerState();
                return;
            }

            if (_pointerDownCollider == null)
            {
                ClickGround(_pointerDownWorldPosition);
            }
            else if (!_pointerDownCollider.CompareTag("EnemySpawner"))
            {
                ClickObject(_pointerDownCollider.gameObject);
            }

            ResetPointerState();
        }

        private void StartDragging()
        {
            if (_draggedUnit == null)
            {
                return;
            }

            _isDraggingBuilding = true;
        }

        private void DragBuilding(Vector2Int gridPos)
        {
            CurrentMouseCellPosition = gridPos;
            _draggedUnit.PreviewPosition(gridPos);
        }

        private bool ShouldStartDragging(Vector2Int hoveredCell)
        {
            if (_draggedUnit == null)
            {
                return false;
            }

            if (hoveredCell != _draggedUnit.GridPosition)
            {
                return true;
            }

            Vector2 pointerDelta = InputData.MousePosition - _pointerDownScreenPosition;
            return pointerDelta.sqrMagnitude >= dragStartThresholdPixels * dragStartThresholdPixels;
        }

        private bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void QueueLeftPointerPressed() => _queuedLeftPointerPressed = true;

        private void QueueLeftPointerReleased() => _queuedLeftPointerReleased = true;

        private void QueueRightPointerPressed() => _queuedRightPointerPressed = true;

        private void ResetPointerState()
        {
            _isPointerDown = false;
            _isDraggingBuilding = false;
            _draggedUnit = null;
            _pointerDownCollider = null;
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
