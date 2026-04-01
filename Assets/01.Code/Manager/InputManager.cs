using _01.Code.Cameras;
using _01.Code.Entities;
using _01.Code.Core;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _01.Code.Manager
{
    public class InputManager : MonoBehaviour
    {
        [field: SerializeField] public InputDataSO InputData { get; private set; }
        [field: SerializeField] public Vector2Int CurrentMouseCellPosition { get; private set; }

        [SerializeField] private LayerMask whatIsClickable;
        [SerializeField] private float dragStartThresholdPixels = 8f;
        private Units.Unit _draggedUnit;
        private PlaceableEntity _selectedBuilding;
        private Collider2D _pointerDownCollider;
        private bool _isPointerDown;
        private bool _isDraggingUnit;
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
            HandleDeleteSaveHotkey();
            HandleSpawnUnitHotkey();

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

            if (_isPointerDown && !_isDraggingUnit && ShouldStartDragging(hoveredCell))
            {
                StartDragging();
            }

            if (_isDraggingUnit && _draggedUnit != null && InputData.IsLeftPointerPressed)
            {
                DragUnit(hoveredCell);
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
            if (TryGetClickedUnit(hitGameObject, out Units.Unit unit))
            {
                ClickUnit(unit);
                return;
            }

            if (TryGetClickedBuilding(hitGameObject, out PlaceableEntity building))
            {
                ClickBuilding(building);
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
            _isDraggingUnit = false;
            _pointerDownScreenPosition = InputData.MousePosition;
            _pointerDownWorldPosition = worldPosition;
            _pointerDownCollider = GetHitCollider(worldPosition);
            _draggedUnit = ResolveDraggedUnit(_pointerDownCollider);
            _selectedBuilding = ResolveSelectedBuilding(_pointerDownCollider);
        }

        private void HandleLeftPointerReleased()
        {
            if (!_isPointerDown)
            {
                return;
            }

            Vector3 targetWorldPosition = GameManager.Instance.GridManager.CellToWorld(CurrentMouseCellPosition);

            if (_isDraggingUnit && _draggedUnit != null)
            {
                bool moved = GameManager.Instance.BuildManager.TryMove(_draggedUnit, targetWorldPosition);
                if (!moved)
                {
                    _draggedUnit.CommitPosition(_draggedUnit.GridPosition);
                }

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
            if (_draggedUnit == null || !CanModifyPlacements())
            {
                return;
            }

            _isDraggingUnit = true;
        }

        private void DragUnit(Vector2Int gridPos)
        {
            CurrentMouseCellPosition = gridPos;
            _draggedUnit.PreviewPosition(gridPos);
        }

        private bool ShouldStartDragging(Vector2Int hoveredCell)
        {
            if (_draggedUnit == null || !CanModifyPlacements())
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
            _isDraggingUnit = false;
            _draggedUnit = null;
            _selectedBuilding = null;
            _pointerDownCollider = null;
        }

        private void HandleDeleteSaveHotkey()
        {
            if (Keyboard.current == null || !Keyboard.current.mKey.wasPressedThisFrame)
            {
                return;
            }

            if (GameManager.Instance?.SaveManager == null)
            {
                return;
            }

            GameManager.Instance.SaveManager.StartNewGame();
        }

        private void HandleSpawnUnitHotkey()
        {
            if (Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame)
            {
                return;
            }

            UIManager uiManager = GameManager.Instance?.UiManager;
            if (uiManager == null || uiManager.AvailableUnits == null || uiManager.AvailableUnits.Count == 0)
            {
                return;
            }

            UnitDataSO unitData = uiManager.AvailableUnits[0];
            if (unitData == null)
            {
                return;
            }

            UnitPanelUI unitPanelUi = FindFirstObjectByType<UnitPanelUI>();
            if (unitPanelUi == null)
            {
                return;
            }

            unitPanelUi.TryAddCard(unitData);
        }

        private bool CanModifyPlacements()
        {
            // return GameManager.Instance?.TimeManager == null || GameManager.Instance.TimeManager.IsDay;
            return true;
        }

        private Collider2D GetHitCollider(Vector2 worldPosition)
        {
            if (whatIsClickable.value == 0)
            {
                return Physics2D.OverlapPoint(worldPosition);
            }

            return Physics2D.OverlapPoint(worldPosition, whatIsClickable);
        }

        private Units.Unit ResolveDraggedUnit(Collider2D hitCollider)
        {
            if (hitCollider == null || hitCollider.CompareTag("EnemySpawner"))
            {
                return null;
            }

            return hitCollider.GetComponentInParent<Units.Unit>();
        }

        private PlaceableEntity ResolveSelectedBuilding(Collider2D hitCollider)
        {
            if (hitCollider == null || hitCollider.CompareTag("EnemySpawner"))
            {
                return null;
            }

            PlaceableEntity placeable = hitCollider.GetComponentInParent<PlaceableEntity>();
            return placeable is Units.Unit ? null : placeable;
        }

        private bool TryGetClickedUnit(GameObject hitGameObject, out Units.Unit unit)
        {
            unit = hitGameObject.GetComponentInParent<Units.Unit>();
            return unit != null;
        }

        private bool TryGetClickedBuilding(GameObject hitGameObject, out PlaceableEntity building)
        {
            building = hitGameObject.GetComponentInParent<PlaceableEntity>();
            return building != null && building is not Units.Unit;
        }

        private void ClickUnit(Units.Unit _)
        {
        }

        private void ClickBuilding(PlaceableEntity building)
        {
            _selectedBuilding = building;
        }
    }
}
