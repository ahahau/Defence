using _01.Code.Cameras;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _01.Code.Manager
{
    public class InputManager : MonoBehaviour, IManageable, IAfterManageable
    {
        [field: SerializeField] public InputDataSO InputData { get; private set; }
        [field: SerializeField] public Vector2Int CurrentMouseCellPosition { get; private set; }

        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private LayerMask whatIsClickable;
        [SerializeField] private float dragStartThresholdPixels = 8f;
        private GridManager _gridManager;
        private Unit _draggedUnit;
        private PlaceableEntity _selectedBuilding;
        private global::System.Collections.Generic.List<UnitDataSO> _availableUnits = new();
        private Collider2D _pointerDownCollider;
        private bool _isPointerDown;
        private bool _isDraggingUnit;
        private bool _queuedLeftPointerPressed;
        private bool _queuedLeftPointerReleased;
        private bool _queuedRightPointerPressed;
        private Vector2 _pointerDownScreenPosition;
        private Vector2 _pointerDownWorldPosition;
        private Camera _worldCamera;

        public void Initialize(IManagerContainer managerContainer)
        {
            _gridManager = managerContainer.GetManager<GridManager>();
            ResolveChannels();
            InputData.LeftPointerPressed += QueueLeftPointerPressed;
            InputData.LeftPointerReleased += QueueLeftPointerReleased;
            InputData.RightPointerPressed += QueueRightPointerPressed;
        }

        public void AfterInitialize(IManagerContainer managerContainer)
        {
            RefreshUnitCatalog();
            ResolveWorldCamera();
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
            if (_gridManager == null)
            {
                return;
            }

            Vector2Int hoveredCell = _gridManager.WorldToCell(worldPosition);
            CurrentMouseCellPosition = hoveredCell;
            uiEventChannel?.RaiseEvent(UIEvents.UiHoverCellChangedEvent.Initializer(hoveredCell));

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
            if (_gridManager == null)
            {
                return;
            }

            Vector2Int gridPos = _gridManager.WorldToCell(worldPosition);
            CurrentMouseCellPosition = gridPos;
            Vector3 buildWorldPosition = _gridManager.CellToWorld(gridPos);
            uiEventChannel?.RaiseEvent(UIEvents.UiBuildAtWorldPositionRequestedEvent.Initializer(buildWorldPosition));
        }

        private void HandleRightPointerPressed()
        {
            if (IsPointerOverUi())
            {
                return;
            }

            uiEventChannel?.RaiseEvent(UIEvents.UiCancelSelectionRequestedEvent);
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

            if (_gridManager == null)
            {
                ResetPointerState();
                return;
            }

            Vector3 targetWorldPosition = _gridManager.CellToWorld(CurrentMouseCellPosition);

            if (_isDraggingUnit && _draggedUnit != null)
            {
                BuildMoveRequestedEvent moveRequest =
                    BuildEvents.BuildMoveRequestedEvent.Initializer(_draggedUnit, targetWorldPosition, 0);
                buildEventChannel?.RaiseEvent(moveRequest);
                bool moved = moveRequest.Succeeded;
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

            uiEventChannel?.RaiseEvent(SaveEvents.SaveStartNewGameRequestedEvent);
        }

        private void HandleSpawnUnitHotkey()
        {
            if (Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame)
            {
                return;
            }

            RefreshUnitCatalog();
            if (_availableUnits == null || _availableUnits.Count == 0)
            {
                return;
            }

            UnitDataSO unitData = _availableUnits[0];
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
            return true;
        }

        private void ResolveWorldCamera()
        {
            _worldCamera = Camera.main;
            if (_worldCamera == null)
            {
                _worldCamera = FindFirstObjectByType<Camera>();
            }
        }

        private Collider2D GetHitCollider(Vector2 worldPosition)
        {
            ResolveWorldCamera();
            if (_worldCamera == null)
            {
                return null;
            }

            Ray ray = _worldCamera.ScreenPointToRay(InputData.MousePosition);
            RaycastHit2D hit = whatIsClickable.value == 0
                ? Physics2D.GetRayIntersection(ray, Mathf.Infinity)
                : Physics2D.GetRayIntersection(ray, Mathf.Infinity, whatIsClickable);

            if (hit.collider != null)
            {
                return hit.collider;
            }

            return null;
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

        private void RefreshUnitCatalog()
        {
            UiUnitCatalogQueryEvent query = UIEvents.UiUnitCatalogQueryEvent.Initializer();
            uiEventChannel?.RaiseEvent(query);
            _availableUnits = query.Units ?? new global::System.Collections.Generic.List<UnitDataSO>();
        }

        private void ResolveChannels()
        {
            UIManager uiManager = FindFirstObjectByType<UIManager>();
            if (uiEventChannel == null)
            {
                uiEventChannel = uiManager != null ? uiManager.UiEventChannel : null;
            }

            if (buildEventChannel == null)
            {
                buildEventChannel = uiManager != null ? uiManager.BuildEventChannel : null;
            }
        }
    }
}
