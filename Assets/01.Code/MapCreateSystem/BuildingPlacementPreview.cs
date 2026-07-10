using System;
using _01.Code.Buildings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _01.Code.MapCreateSystem
{
    /// <summary>건물 배치 미리보기(배치 모드). 노드 그리드의 빈 칸을 표시하고,
    /// 마우스가 올라간 칸에 하이라이트 + 건물 고스트 + "여기에 설치" 안내를 띄운다.
    /// 좌클릭으로 확정(onConfirm), 우클릭/ESC로 취소(onCancel).</summary>
    public class BuildingPlacementPreview : MonoBehaviour
    {
        private static BuildingPlacementPreview _active;
        private static int _lastActiveFrame = -1;

        /// <summary>이번 프레임에 배치 모드가 활성이었는지. 확정/취소 클릭이 노드 클릭(재선택)으로
        /// 새어 들어가 패널을 닫아버리는 것을 막을 때 사용.</summary>
        public static bool WasActiveThisFrame => _active != null || Time.frameCount == _lastActiveFrame;

        private NodeTrapGrid _grid;
        private Action<int, int> _onConfirm;
        private Action _onCancel;
        private SpriteRenderer _hoverMarker;
        private SpriteRenderer _ghost;
        private TextMeshPro _hintText;
        private Camera _camera;
        private int _hoverColumn = -1;
        private int _hoverRow = -1;
        private bool _hoverIsFree;

        private static readonly Color GridLineColor = new(1f, 1f, 1f, 0.35f);
        private static readonly Color HoverFreeColor = new(0.35f, 1f, 0.45f, 0.45f);
        private static readonly Color HoverBlockedColor = new(1f, 0.25f, 0.2f, 0.45f);
        private const int MarkerSortingOrder = 40;
        private const float GridLineWidth = 0.035f;

        private static Sprite _squareSprite;
        private static Material _lineMaterial;

        /// <summary>배치 모드 시작. 이미 열려 있던 미리보기는 취소된다.</summary>
        public static void Begin(NodeTrapGrid grid, BuildingDataSO buildingData, Action<int, int> onConfirm, Action onCancel)
        {
            CancelActive();

            if (grid == null)
            {
                onCancel?.Invoke();
                return;
            }

            var go = new GameObject("BuildingPlacementPreview");
            _active = go.AddComponent<BuildingPlacementPreview>();
            _active.Initialize(grid, buildingData, onConfirm, onCancel);
        }

        /// <summary>활성 미리보기를 취소한다(패널 닫힘 등 외부 사유).</summary>
        public static void CancelActive()
        {
            if (_active != null)
                _active.Cancel();
        }

        private void Initialize(NodeTrapGrid grid, BuildingDataSO buildingData, Action<int, int> onConfirm, Action onCancel)
        {
            _grid = grid;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _camera = Camera.main;

            BuildGridLines();
            BuildHoverMarker();
            BuildGhost(buildingData);
            BuildHintText();
            SetHoverVisible(false);
        }

        private void Update()
        {
            _lastActiveFrame = Time.frameCount;

            if (_grid == null || _camera == null)
            {
                Cancel();
                return;
            }

            if (WasCancelPressed())
            {
                Cancel();
                return;
            }

            UpdateHover();

            if (Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && !IsPointerOverUi()
                && _hoverIsFree)
            {
                Confirm();
            }
        }

        private static bool WasCancelPressed()
        {
            var rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            var escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            return rightClick || escape;
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void UpdateHover()
        {
            if (Mouse.current == null)
                return;

            var screenPosition = Mouse.current.position.ReadValue();
            var worldPosition = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));
            worldPosition.z = 0f;

            if (!_grid.TryGetCell(worldPosition, out _hoverColumn, out _hoverRow) || IsPointerOverUi())
            {
                _hoverIsFree = false;
                SetHoverVisible(false);
                return;
            }

            _hoverIsFree = _grid.IsCellFree(_hoverColumn, _hoverRow);
            var cellPosition = _grid.CellWorldPosition(_hoverColumn, _hoverRow);

            _hoverMarker.transform.position = cellPosition;
            _hoverMarker.color = _hoverIsFree ? HoverFreeColor : HoverBlockedColor;
            _hoverMarker.enabled = true;

            if (_ghost != null)
            {
                _ghost.transform.position = cellPosition;
                _ghost.enabled = _hoverIsFree;
            }

            if (_hintText != null)
            {
                _hintText.transform.position = cellPosition + Vector3.up * (_grid.CellSize * 0.62f);
                _hintText.gameObject.SetActive(_hoverIsFree);
            }
        }

        private void Confirm()
        {
            var onConfirm = _onConfirm;
            var column = _hoverColumn;
            var row = _hoverRow;
            Close();
            onConfirm?.Invoke(column, row);
        }

        private void Cancel()
        {
            var onCancel = _onCancel;
            Close();
            onCancel?.Invoke();
        }

        private void Close()
        {
            _onConfirm = null;
            _onCancel = null;
            if (_active == this)
                _active = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_active == this)
                _active = null;
        }

        // ── 비주얼 구성 ─────────────────────────────────────────

        /// <summary>격자를 라인으로 그린다 — 셀 경계선(세로 columns+1개, 가로 rows+1개).</summary>
        private void BuildGridLines()
        {
            var half = _grid.CellSize * 0.5f;
            var min = _grid.CellWorldPosition(0, 0) - new Vector3(half, half, 0f);
            var max = _grid.CellWorldPosition(_grid.Columns - 1, _grid.Rows - 1) + new Vector3(half, half, 0f);

            for (var c = 0; c <= _grid.Columns; c++)
            {
                var x = min.x + c * _grid.CellSize;
                CreateLine(new Vector3(x, min.y, 0f), new Vector3(x, max.y, 0f));
            }

            for (var r = 0; r <= _grid.Rows; r++)
            {
                var y = min.y + r * _grid.CellSize;
                CreateLine(new Vector3(min.x, y, 0f), new Vector3(max.x, y, 0f));
            }
        }

        private void CreateLine(Vector3 start, Vector3 end)
        {
            var go = new GameObject("GridLine");
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = GridLineWidth;
            line.endWidth = GridLineWidth;
            line.material = GetLineMaterial();
            line.startColor = GridLineColor;
            line.endColor = GridLineColor;
            line.sortingOrder = MarkerSortingOrder;
        }

        private static Material GetLineMaterial()
        {
            if (_lineMaterial == null)
                _lineMaterial = new Material(Shader.Find("Sprites/Default"));

            return _lineMaterial;
        }

        private void BuildHoverMarker()
        {
            _hoverMarker = CreateSquare("HoverCell", HoverFreeColor, MarkerSortingOrder + 1);
        }

        private void BuildGhost(BuildingDataSO buildingData)
        {
            var sprite = ResolveBuildingSprite(buildingData);
            if (sprite == null)
                return;

            var go = new GameObject("Ghost");
            go.transform.SetParent(transform, false);
            _ghost = go.AddComponent<SpriteRenderer>();
            _ghost.sprite = sprite;
            _ghost.color = new Color(1f, 1f, 1f, 0.55f);
            _ghost.sortingOrder = MarkerSortingOrder + 2;

            // 프리팹 원본 스케일을 따라가 실제 설치 크기와 같게 보이게 한다.
            if (buildingData != null && buildingData.Prefab != null)
            {
                var prefabRenderer = buildingData.Prefab.GetComponentInChildren<SpriteRenderer>();
                if (prefabRenderer != null)
                    go.transform.localScale = prefabRenderer.transform.lossyScale;
            }
        }

        private static Sprite ResolveBuildingSprite(BuildingDataSO buildingData)
        {
            if (buildingData == null)
                return null;

            if (buildingData.Prefab != null)
            {
                var renderer = buildingData.Prefab.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null)
                    return renderer.sprite;
            }

            return buildingData.BoardSprite;
        }

        private void BuildHintText()
        {
            // 런타임 TextMesh는 한글 폰트가 없어 TMP(프로젝트 기본 한글 폰트)를 쓴다.
            var go = new GameObject("HintText");
            go.transform.SetParent(transform, false);
            _hintText = go.AddComponent<TextMeshPro>();
            _hintText.text = "여기에 설치";
            _hintText.fontSize = 2.6f;
            _hintText.alignment = TextAlignmentOptions.Center;
            _hintText.color = new Color(0.75f, 1f, 0.8f, 0.95f);

            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sortingOrder = MarkerSortingOrder + 3;
        }

        private SpriteRenderer CreateSquare(string squareName, Color color, int sortingOrder)
        {
            var go = new GameObject(squareName);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (_grid.CellSize * 0.9f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void SetHoverVisible(bool visible)
        {
            if (_hoverMarker != null) _hoverMarker.enabled = visible;
            if (_ghost != null) _ghost.enabled = visible;
            if (_hintText != null) _hintText.gameObject.SetActive(visible);
        }

        private static Sprite GetSquareSprite()
        {
            if (_squareSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _squareSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width);
            }

            return _squareSprite;
        }
    }
}
