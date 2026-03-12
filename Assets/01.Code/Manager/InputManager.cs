using _01.Code.Buildings;
using _01.Code.Cameras;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Manager
{
    // TODO: Separate input routing from click target handling.
    public class InputManager : MonoBehaviour, IManageable
    {
        [field: SerializeField] public InputDataSO InputData { get; private set; }
        [field: SerializeField] public Vector2Int CurrentMouseCellPosition { get; private set; }

        [SerializeField] private LayerMask whatIsClickable;

        private Building _draggedBuilding;
        private Vector2Int _draggedBuildingStartPosition;
        
        public void Initialize()
        {
        }

        private void Awake()
        {
            InputData.OnMouseClicked += OnClick;
        }

        private void OnDestroy()
        {
            InputData.OnMouseClicked -= OnClick;
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
                TryBeginDrag(worldPosition);
            }

            if (_draggedBuilding != null && Mouse.current.leftButton.isPressed)
            {
                DragBuilding(worldPosition);
            }

            if (_draggedBuilding != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                ReleaseDrag(worldPosition);
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
            GameManager.Instance.UiManager.HideBuildingPanel();

            if (hitGameObject.CompareTag("EnemySpawner"))
            {
                return;
            }

            Building building = hitGameObject.GetComponentInParent<Building>();
            if (building != null)
            {
                return;
            }
        }

        private void ClickGround(Vector2 worldPosition)
        {
            Vector2Int gridPos = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            CurrentMouseCellPosition = gridPos;
            GameManager.Instance.UiManager.ShowBuildingPanel(worldPosition);
        }
        
        private void TryBeginDrag(Vector2 worldPosition)
        {
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
            _draggedBuildingStartPosition = building.GridPosition;
            GameManager.Instance.UiManager.HideBuildingPanel();
        }

        private void DragBuilding(Vector2 worldPosition)
        {
            Vector2Int gridPos = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            Vector2Int snappedWorldPosition = GameManager.Instance.GridManager.Tilemap.CellToWorld(gridPos);
            CurrentMouseCellPosition = gridPos;
            _draggedBuilding.PreviewPosition(snappedWorldPosition);
        }

        private void ReleaseDrag(Vector2 worldPosition)
        {
            Building building = _draggedBuilding;
            _draggedBuilding = null;

            if (!GameManager.Instance.BuildManager.TryMove(building, worldPosition))
            {
                building.CommitPosition(_draggedBuildingStartPosition);
            }
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
