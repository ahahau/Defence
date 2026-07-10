using System;
using _01.Code.Buildings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _01.Code.MapCreateSystem
{
    /// <summary>라인(엣지) 건물 배치 모드. 설치 가능한 라인의 중점에 슬롯 마커를 표시하고,
    /// 마우스에 가장 가까운 빈 라인을 하이라이트 + 건물 고스트 + "여기에 설치"로 보여준다.
    /// 좌클릭 확정(onConfirm), 우클릭/ESC 취소(onCancel).</summary>
    public class EdgePlacementPreview : MonoBehaviour
    {
        private static EdgePlacementPreview _active;
        private static int _lastActiveFrame = -1;

        /// <summary>이번 프레임에 라인 배치 모드가 활성이었는지(노드 클릭 누수 방지용).</summary>
        public static bool WasActiveThisFrame => _active != null || Time.frameCount == _lastActiveFrame;

        private Action<EdgeLine> _onConfirm;
        private Action _onCancel;
        private Camera _camera;
        private SpriteRenderer _hoverMarker;
        private SpriteRenderer _ghost;
        private TextMeshPro _hintText;
        private EdgeLine _hoverEdge;

        private static readonly Color SlotColor = new(1f, 1f, 1f, 0.28f);
        private static readonly Color HoverColor = new(0.35f, 1f, 0.45f, 0.5f);
        private const float HoverDistance = 0.8f;
        private const float MarkerSize = 0.55f;
        private const int MarkerSortingOrder = 40;

        private static Sprite _squareSprite;

        public static void Begin(BuildingDataSO buildingData, Action<EdgeLine> onConfirm, Action onCancel)
        {
            CancelActive();

            var go = new GameObject("EdgePlacementPreview");
            _active = go.AddComponent<EdgePlacementPreview>();
            _active.Initialize(buildingData, onConfirm, onCancel);
        }

        public static void CancelActive()
        {
            if (_active != null)
                _active.Cancel();
        }

        /// <summary>설치 가능한(빈) 라인이 하나라도 있는지.</summary>
        public static bool HasFreeEdge()
        {
            foreach (var edge in EdgeLine.ActiveEdges)
            {
                if (edge != null && !edge.HasBuilding)
                    return true;
            }

            return false;
        }

        private void Initialize(BuildingDataSO buildingData, Action<EdgeLine> onConfirm, Action onCancel)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _camera = Camera.main;

            BuildSlotMarkers();
            _hoverMarker = CreateSquare("HoverSlot", HoverColor, MarkerSortingOrder + 1);
            BuildGhost(buildingData);
            BuildHintText();
            SetHoverVisible(false);
        }

        private void Update()
        {
            _lastActiveFrame = Time.frameCount;

            if (_camera == null)
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
                && _hoverEdge != null)
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

            _hoverEdge = FindNearestFreeEdge(worldPosition);
            if (_hoverEdge == null || IsPointerOverUi())
            {
                _hoverEdge = null;
                SetHoverVisible(false);
                return;
            }

            var slotPosition = _hoverEdge.Midpoint;
            _hoverMarker.transform.position = slotPosition;
            _hoverMarker.enabled = true;

            if (_ghost != null)
            {
                _ghost.transform.position = slotPosition;
                _ghost.enabled = true;
            }

            if (_hintText != null)
            {
                _hintText.transform.position = slotPosition + Vector3.up * (MarkerSize * 0.9f);
                _hintText.gameObject.SetActive(true);
            }
        }

        private static EdgeLine FindNearestFreeEdge(Vector2 worldPosition)
        {
            EdgeLine best = null;
            var bestDistance = HoverDistance;

            foreach (var edge in EdgeLine.ActiveEdges)
            {
                if (edge == null || edge.HasBuilding)
                    continue;

                var distance = edge.DistanceTo(worldPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = edge;
                }
            }

            return best;
        }

        private void Confirm()
        {
            var onConfirm = _onConfirm;
            var edge = _hoverEdge;
            Close();
            onConfirm?.Invoke(edge);
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

        private void BuildSlotMarkers()
        {
            foreach (var edge in EdgeLine.ActiveEdges)
            {
                if (edge == null || edge.HasBuilding)
                    continue;

                var marker = CreateSquare("EdgeSlot", SlotColor, MarkerSortingOrder);
                marker.transform.position = edge.Midpoint;
                marker.transform.localScale = Vector3.one * (MarkerSize * 0.7f);
            }
        }

        private void BuildGhost(BuildingDataSO buildingData)
        {
            Sprite sprite = null;
            if (buildingData != null && buildingData.Prefab != null)
            {
                var renderer = buildingData.Prefab.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null)
                    sprite = renderer.sprite;
            }

            if (sprite == null && buildingData != null)
                sprite = buildingData.BoardSprite;
            if (sprite == null)
                return;

            var go = new GameObject("Ghost");
            go.transform.SetParent(transform, false);
            _ghost = go.AddComponent<SpriteRenderer>();
            _ghost.sprite = sprite;
            _ghost.color = new Color(1f, 1f, 1f, 0.55f);
            _ghost.sortingOrder = MarkerSortingOrder + 2;

            if (buildingData != null && buildingData.Prefab != null)
            {
                var prefabRenderer = buildingData.Prefab.GetComponentInChildren<SpriteRenderer>();
                if (prefabRenderer != null)
                    go.transform.localScale = prefabRenderer.transform.lossyScale;
            }
        }

        private void BuildHintText()
        {
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
            go.transform.localScale = Vector3.one * MarkerSize;

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
