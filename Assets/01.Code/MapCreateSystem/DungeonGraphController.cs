using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.MapCreateSystem
{
    public class DungeonGraphController : MonoBehaviour
    {
        private readonly Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        [Header("Layout")]
        [SerializeField]
        private float gridSpacing = 2.4f;

        [SerializeField]
        private float nodeSize = 1f;

        [Header("Prefabs")]
        [SerializeField]
        private Node nodePrefab;

        [SerializeField]
        private EdgeLine edgeLinePrefab;

        [Header("Build")]
        [SerializeField]
        private DungeonNodeType selectedType = DungeonNodeType.Corridor;

        [SerializeField]
        private int buildGoldCost = 10;

        [Header("Events")]
        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField] private GameEventChannelSO nodeEventChannel;

        [Header("Random Links")]
        [SerializeField]
        private bool useRandomAdjacentLinks = true;

        [SerializeField, Range(0f, 1f)]
        private float adjacentLinkChance = 0.35f;

        [SerializeField]
        private int maxRandomAdjacentLinks = 1;
        
        [Header("Input")]
        [SerializeField]
        private InputDataSO inputDataSO;

        [SerializeField]
        private Camera inputCamera;

        [SerializeField]
        private LayerMask nodeClickMask = Physics2D.DefaultRaycastLayers;

        [Header("UI Blocking")]
        [SerializeField]
        private RectTransform nodePanelBlockRect;

        [Header("Camera Focus")]
        [SerializeField]
        private float focusedOrthographicSize = 3f;

        [SerializeField]
        private float cameraFocusDuration = 0.25f;

        [SerializeField]
        private Ease cameraFocusEase = Ease.OutCubic;

        [SerializeField]
        private Vector2 cameraFocusOffset;

        private DungeonGraph graph;
        private DungeonGraphView view;
        private readonly Dictionary<Collider2D, Node> lockedNodeByCollider = new();
        private readonly Dictionary<Collider2D, Node> unlockedNodeByCollider = new();
        private readonly List<DungeonNode> buildParentCandidates = new();
        private readonly List<DungeonNode> randomAdjacentCandidates = new();
        private Node lastBuiltNodeView;
        private int lastBuiltFrame = -1;
        private Sequence cameraFocusSequence;
        private bool hasPendingMouseInput;
        private Node focusedNode;
        private bool isCameraFocused;
        private bool hasDefaultCameraState;
        private Vector3 defaultCameraPosition;
        private float defaultOrthographicSize;

        public bool HasLockedNodesVisible { get; private set; }

        private void OnEnable()
        {
            costEventChannel.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            nodeEventChannel.AddListener<UnlockedNodeClickedEvent>(HandleUnlockedNodeClicked);
            inputDataSO.OnMouseInputEvent += HandleMouseInput;
        }

        

        private void OnDisable()
        {
            costEventChannel.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            nodeEventChannel.RemoveListener<UnlockedNodeClickedEvent>(HandleUnlockedNodeClicked);
            inputDataSO.OnMouseInputEvent -= HandleMouseInput;
            cameraFocusSequence?.Kill();
            cameraFocusSequence = null;
        }

        private void Awake()
        {
            RebuildInitialGraph();

            if (Application.isPlaying)
                ShowLockedNodes();
        }

        private void Update()
        {
            if (!hasPendingMouseInput)
                return;

            hasPendingMouseInput = false;
            ProcessMouseInput();
        }

        [ContextMenu("Rebuild Initial Graph")]
        public void RebuildInitialGraph()
        {
            graph = new DungeonGraph();
            view = new DungeonGraphView(transform, nodePrefab, edgeLinePrefab, gridSpacing, nodeSize);
            view.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();

            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            RegisterUnlockedNode(view.CreateNode(entrance));
            HasLockedNodesVisible = false;
        }

        [ContextMenu("Show Locked Nodes")]
        public void ShowLockedNodes()
        {
            HasLockedNodesVisible = true;
            RefreshLockedNodes();
        }
        
        [ContextMenu("Hide Locked Nodes")]
        public void HideLockedNodes()
        {
            HasLockedNodesVisible = false;
            lockedNodeByCollider.Clear();
            view.ClearLockedNodes();
        }

        public void TryBuildAt(Node lockedNode)
        {
            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            costEventChannel.RaiseEvent(new BuildCostRequestedEvent(lockedNode, buildGoldCost));
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            var lockedNode = evt.Node;
            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;
            var type = ResolveBuildType(lockedNode);
            var node = graph.AddNode(type, lockedNode.GridPosition);
            var nodeView = view.CreateNode(node);

            lastBuiltNodeView = nodeView;
            lastBuiltFrame = Time.frameCount;
            RegisterUnlockedNode(nodeView);
            ConnectAdjacentNodes(node, lockedNode.FromNode);

            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        public void SelectBuildType(DungeonNodeType type)
        {
            if (type != DungeonNodeType.Entrance)
                selectedType = type;
        }

        private DungeonNodeType ResolveBuildType(Node lockedNode)
        {
            if (lockedNode.FromNode.Type == DungeonNodeType.Entrance && graph.Nodes.Count == 1)
                return DungeonNodeType.Corridor;

            return selectedType;
        }

        private void ConnectAdjacentNodes(DungeonNode node, DungeonNode preferredFirstNode)
        {
            TryConnectNodes(preferredFirstNode, node);

            if (!useRandomAdjacentLinks || maxRandomAdjacentLinks <= 0)
                return;

            var randomLinkCount = 0;
            randomAdjacentCandidates.Clear();
            foreach (var direction in directions)
            {
                var adjacentPosition = node.GridPosition + direction;
                if (!graph.TryGetNodeAt(adjacentPosition, out var adjacentNode))
                    continue;

                if (adjacentNode == preferredFirstNode)
                    continue;

                if (randomLinkCount >= maxRandomAdjacentLinks)
                    return;

                if (!CanRandomConnect(node, adjacentNode))
                    continue;

                randomAdjacentCandidates.Add(adjacentNode);
            }

            while (randomAdjacentCandidates.Count > 0 && randomLinkCount < maxRandomAdjacentLinks)
            {
                var index = Random.Range(0, randomAdjacentCandidates.Count);
                var adjacentNode = randomAdjacentCandidates[index];
                randomAdjacentCandidates.RemoveAt(index);

                if (Random.value > adjacentLinkChance)
                    continue;

                TryConnectNodes(adjacentNode, node);
                randomLinkCount++;
            }
        }

        private bool CanRandomConnect(DungeonNode node, DungeonNode adjacentNode)
        {
            if (node.FreePorts <= 0 || adjacentNode.FreePorts <= 0)
                return false;

            if (node.Type == DungeonNodeType.Boss || node.Type == DungeonNodeType.Treasury)
                return false;

            if (adjacentNode.Type == DungeonNodeType.Boss || adjacentNode.Type == DungeonNodeType.Treasury)
                return false;

            return true;
        }

        private void TryConnectNodes(DungeonNode fromNode, DungeonNode toNode)
        {
            if (!graph.Connect(fromNode, toNode))
                return;

            view.CreateEdge(fromNode.GridPosition, toNode.GridPosition);
        }

        public void RegisterLockedNode(Node lockedNode)
        {
            lockedNodeByCollider.Add(lockedNode.ClickCollider, lockedNode);
        }

        public void RegisterUnlockedNode(Node unlockedNode)
        {
            unlockedNodeByCollider.Add(unlockedNode.ClickCollider, unlockedNode);
        }

        private void RefreshLockedNodes()
        {
            lockedNodeByCollider.Clear();
            view.ClearLockedNodes();

            var usedLockedNodePositions = new HashSet<Vector2Int>();
            foreach (var node in graph.Nodes)
            {
                foreach (var direction in directions)
                {
                    var position = node.GridPosition + direction;
                    if (graph.IsOccupied(position) || !usedLockedNodePositions.Add(position))
                        continue;

                    if (!TryResolveBuildParent(position, out var parentNode))
                        continue;

                    view.CreateLockedNode(this, parentNode, position, position - parentNode.GridPosition);
                }
            }
        }

        private bool TryResolveBuildParent(Vector2Int position, out DungeonNode parentNode)
        {
            parentNode = null;
            foreach (var direction in directions)
            {
                var adjacentPosition = position + direction;
                if (!graph.TryGetNodeAt(adjacentPosition, out var adjacentNode))
                    continue;

                if (adjacentNode.FreePorts <= 0)
                    continue;

                if (parentNode == null || adjacentNode.FreePorts > parentNode.FreePorts)
                    parentNode = adjacentNode;
            }

            if (parentNode == null)
                return false;

            return true;
        }

        private void HandleMouseInput()
        {
            hasPendingMouseInput = true;
        }

        private void ProcessMouseInput()
        {
            if (IsPointerOverUi() || IsPointerOverNodePanel())
                return;

            Vector2 worldPosition = inputDataSO.SceneToWorldPoint(inputCamera);
            var clickedCollider = Physics2D.OverlapPoint(worldPosition, nodeClickMask);

            if (clickedCollider == null)
                return;

            if (!lockedNodeByCollider.TryGetValue(clickedCollider, out var lockedNode))
            {
                if (!unlockedNodeByCollider.TryGetValue(clickedCollider, out var unlockedNode))
                    return;

                nodeEventChannel.RaiseEvent(new UnlockedNodeClickedEvent(unlockedNode));
                return;
            }

            if (isCameraFocused)
                return;

            TryBuildAt(lockedNode);
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool IsPointerOverNodePanel()
        {
            if (nodePanelBlockRect == null || !nodePanelBlockRect.gameObject.activeInHierarchy)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(
                nodePanelBlockRect,
                inputDataSO.ReadScreenMousePosition(),
                null);
        }

        private void HandleUnlockedNodeClicked(UnlockedNodeClickedEvent evt)
        {
            if (evt.Node == lastBuiltNodeView && Time.frameCount <= lastBuiltFrame + 1)
                return;

            if (isCameraFocused && focusedNode == evt.Node)
            {
                ReleaseCameraFocus(evt.Node);
                return;
            }

            FocusCameraOnNode(evt.Node);
        }

        private void FocusCameraOnNode(Node node)
        {
            if (node == null)
                return;

            var targetCamera = inputCamera != null ? inputCamera : Camera.main;
            if (targetCamera == null)
                return;

            if (!hasDefaultCameraState)
            {
                defaultCameraPosition = targetCamera.transform.position;
                defaultOrthographicSize = targetCamera.orthographicSize;
                hasDefaultCameraState = true;
            }

            nodeEventChannel.RaiseEvent(new NodeCameraFocusStartedEvent(node));

            var targetPosition = new Vector3(
                node.transform.position.x + cameraFocusOffset.x,
                node.transform.position.y + cameraFocusOffset.y,
                targetCamera.transform.position.z);

            cameraFocusSequence?.Kill();

            var duration = Mathf.Max(0.01f, cameraFocusDuration);
            cameraFocusSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetEase(cameraFocusEase)
                .Append(targetCamera.transform.DOMove(targetPosition, duration));

            if (targetCamera.orthographic)
                cameraFocusSequence.Join(targetCamera.DOOrthoSize(focusedOrthographicSize, duration));

            cameraFocusSequence.OnComplete(() =>
            {
                cameraFocusSequence = null;
                focusedNode = node;
                isCameraFocused = true;
                nodeEventChannel.RaiseEvent(new NodeCameraFocusCompletedEvent(node));
            });
        }

        private void ReleaseCameraFocus(Node node)
        {
            var targetCamera = inputCamera != null ? inputCamera : Camera.main;
            if (targetCamera == null || !hasDefaultCameraState)
                return;

            nodeEventChannel.RaiseEvent(new NodeCameraFocusStartedEvent(node));
            cameraFocusSequence?.Kill();

            var duration = Mathf.Max(0.01f, cameraFocusDuration);
            cameraFocusSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetEase(cameraFocusEase)
                .Append(targetCamera.transform.DOMove(defaultCameraPosition, duration));

            if (targetCamera.orthographic)
                cameraFocusSequence.Join(targetCamera.DOOrthoSize(defaultOrthographicSize, duration));

            cameraFocusSequence.OnComplete(() =>
            {
                cameraFocusSequence = null;
                focusedNode = null;
                isCameraFocused = false;
            });
        }
    }
}
