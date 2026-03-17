using _01.Code.Buildings;
using _01.Code.Cameras;
using _01.Code.Core;
using _01.Code.Events;
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
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;

        private Building _draggedBuilding;
        private bool _isPointerDown;
        private bool _isDraggingBuilding;
        private Vector2 _pointerDownScreenPosition;
        
        public void Initialize()
        {
            GameManager.Instance.LogManager?.System("InputManager initialized.");
        }


        private void Update()
        {
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 worldPosition = GetMouseWorldPosition();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                BeginPointerInteraction(worldPosition);
            }

            if (_isPointerDown && !_isDraggingBuilding && ShouldStartDragging())
            {
                StartDragging();
            }

            if (_isDraggingBuilding && _draggedBuilding != null && Mouse.current.leftButton.isPressed)
            {
                DragBuilding(worldPosition);
            }

            if (_isPointerDown && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndPointerInteraction(worldPosition);
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
            
            // 지금은 하는일 없음
            ClickObject(hit.gameObject);
        }

        /// <summary>
        /// 이 함수는 오브젝트 클릭시 빌드 패널을 닫고 오브젝트 분기를 처리합니다
        /// </summary>
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
                return;
            }
        }

        /// <summary>
        /// 이 함수는 빈 땅 클릭을 빌드 패널 열기 요청으로 바꿔줍니다
        /// </summary>
        private void ClickGround(Vector2 worldPosition)
        {
            Vector2Int gridPos = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            CurrentMouseCellPosition = gridPos;
            GameManager.Instance.LogManager?.UI($"Requested build panel at cell {gridPos}.");
            uiEventChannel.RaiseEvent(UIEvents.ShowBuildPanelRequested.Initializer(worldPosition));
        }
        
        private void BeginPointerInteraction(Vector2 worldPosition)
        {
            if (IsPointerOverUi())
            {
                ResetPointerState();
                return;
            }

            _isPointerDown = true;
            _isDraggingBuilding = false;
            _pointerDownScreenPosition = Mouse.current.position.ReadValue();

            Collider2D hit = GetHitCollider(worldPosition);
            if (hit == null)
            {
                return;
            }

            if (hit.CompareTag("EnemySpawner"))
            {
                return;
            }

            Building building = hit.GetComponentInParent<Building>();
            if (building == null)
            {
                return;
            }

            _draggedBuilding = building;
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

        private void DragBuilding(Vector2 worldPosition)
        {
            Vector2Int gridPos = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            Vector2Int snappedWorldPosition = GameManager.Instance.GridManager.Tilemap.CellToWorld(gridPos);
            CurrentMouseCellPosition = gridPos;
            _draggedBuilding.PreviewPosition(snappedWorldPosition);
        }

        private void EndPointerInteraction(Vector2 worldPosition)
        {
            if (_isDraggingBuilding && _draggedBuilding != null)
            {
                ReleaseDrag(worldPosition);
                ResetPointerState();
                return;
            }

            if (!IsPointerOverUi())
            {
                OnClick(worldPosition);
            }

            ResetPointerState();
        }

        private void ReleaseDrag(Vector2 worldPosition)
        {
            GameManager.Instance.LogManager?.Building($"Requested move for `{_draggedBuilding.name}`.");
            buildEventChannel.RaiseEvent(BuildEvents.MoveBuildingRequested.Initializer(_draggedBuilding, worldPosition));
        }

        private bool ShouldStartDragging()
        {
            if (_draggedBuilding == null || Mouse.current == null)
            {
                return false;
            }

            Vector2 pointerDelta = Mouse.current.position.ReadValue() - _pointerDownScreenPosition;
            return pointerDelta.sqrMagnitude >= dragStartThresholdPixels * dragStartThresholdPixels;
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void ResetPointerState()
        {
            _isPointerDown = false;
            _isDraggingBuilding = false;
            _draggedBuilding = null;
        }

        private Vector2 GetMouseWorldPosition()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                return Vector2.zero;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 screenPoint = new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, Mathf.Abs(mainCam.transform.position.z));
            Vector3 worldPos = mainCam.ScreenToWorldPoint(screenPoint);
            return new Vector2(worldPos.x, worldPos.y);
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
