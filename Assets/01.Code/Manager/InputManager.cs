using System;
using _01.Code.Buildings;
using _01.Code.Cameras;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Profiling;

namespace _01.Code.Manager
{
    //TODO : 여기 싹다 뜯어 고쳐야함
    public class InputManager : MonoBehaviour, IManageable
    {
        [field:SerializeField] public CameraInputSO Input { get; private set; }
        [field:SerializeField] public Vector2Int CurrentMouseCellPosition { get; private set; }

        [SerializeField] private GameObject buildingPenalPrefab;
        [SerializeField] private LayerMask whatIsClickable;
        
        public void Initialize()
        {
            
        }
        
        private void Awake()
        {
            Input.OnMouseClicked += OnClick;
        }

        private void OnDestroy()
        {
            Input.OnMouseClicked -= OnClick;
        }

        public void OnClick(Vector2 worldPosition)
        {
            Collider2D hit = Physics2D.OverlapPoint(worldPosition, whatIsClickable);

            if (hit == null)
            {
                ClickGround(worldPosition);
            }
            else
            {
                ClickObject(hit.gameObject);
            }
        }

        private void ClickObject(GameObject hitGameObject)
        {
            if (gameObject.CompareTag("EnemySpawner") || gameObject.CompareTag("ObjectSpawner"))
            {
                buildingPenalPrefab.SetActive(false);
                return;
            }
            else
            {
                Building building = hitGameObject.GetComponent<Building>();
                if (building != null)
                {
                    //building.OnClicked();
                }
            }
        }

        private void ClickGround(Vector2 worldPosition)
        {
            Vector2Int gridPos = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            CurrentMouseCellPosition = gridPos;
            gridPos = GameManager.Instance.GridManager.Tilemap.CellToWorld(gridPos);
            
            buildingPenalPrefab.SetActive(true);
            buildingPenalPrefab.transform.position = new Vector3(gridPos.x, gridPos.y, 0);
            GameManager.Instance.UiManager.ShowBuildingPanel();
        }

        
    }
}