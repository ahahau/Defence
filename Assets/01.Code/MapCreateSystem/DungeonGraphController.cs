using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using _01.Code.Tutorial;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

        private readonly Vector2Int[] initialBuildCandidateOffsets =
        {
            new(-1, 1),
            Vector2Int.left,
            new(-1, -1)
        };

        private readonly Vector2Int[] nodeBuildCandidateOffsets =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left
        };

        [Header("Scene Managers")]
        [SerializeField]
        private DungeonNodeManager nodeManager;

        [SerializeField]
        private DungeonEdgeManager edgeManager;

        [SerializeField]
        private Transform unitsRoot;

        
        [Header("Build")]
        [SerializeField]
        private DungeonNodeType selectedType = DungeonNodeType.Corridor;

        [SerializeField]
        private int buildGoldCost = 10;

        [Header("Main Unit")]
        [SerializeField]
        private MainUnit mainUnitPrefab;

        [SerializeField]
        private PlayerStatusHudView playerStatusHud;

#if UNITY_EDITOR
        public void EditorSetPlayerStatusHud(PlayerStatusHudView hud)
        {
            playerStatusHud = hud;
        }
#endif

        [SerializeField]
        private GameEventChannelSO gameStateEventChannel;

        [SerializeField]
        private GameEventChannelSO artifactEventChannel;

        [Header("Events")]
        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField] private GameEventChannelSO nodeEventChannel;

        [Header("Build Warning")]
        [SerializeField]
        private BuildConfirmPanelView buildConfirmPanel;

        [Header("Input")]
        [SerializeField]
        private InputDataSO inputDataSO;

        [SerializeField]
        private LayerMask nodeClickMask = Physics2D.DefaultRaycastLayers;

        [Header("UI Blocking")]
        [SerializeField]
        private RectTransform nodePanelBlockRect;

        private DungeonGraph graph;
        private readonly Dictionary<Collider2D, Node> lockedNodeByCollider = new();
        private readonly Dictionary<Collider2D, Node> unlockedNodeByCollider = new();
        private readonly List<DungeonNode> buildParentCandidates = new();
        private Node lastBuiltNodeView;
        private Vector2 lastBuildClickScreenPosition;
        private int lastBuiltFrame = -1;
        private bool hasPendingMouseInput;
        private bool hasPendingRightMouseInput;

        public bool HasLockedNodesVisible { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            costEventChannel.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            inputDataSO.OnMouseInputEvent += HandleMouseInput;
        }

        

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            costEventChannel.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel.RemoveListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            inputDataSO.OnMouseInputEvent -= HandleMouseInput;
            nodeManager?.ClearAll();
            edgeManager?.ClearAll();
            ClearUnitsRoot();
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            RebuildInitialGraph();
            ShowLockedNodes();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            if (!hasPendingMouseInput)
            {
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                    hasPendingRightMouseInput = true;

                if (!hasPendingRightMouseInput)
                    return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                hasPendingRightMouseInput = true;

            if (hasPendingRightMouseInput)
            {
                hasPendingRightMouseInput = false;
                ProcessRightMouseInput();
                hasPendingMouseInput = false;
                return;
            }

            if (hasPendingMouseInput)
            {
                hasPendingMouseInput = false;
                ProcessMouseInput();
            }
        }

        [ContextMenu("Rebuild Initial Graph")]
        public void RebuildInitialGraph()
        {
            if (!Application.isPlaying)
                return;

            graph = new DungeonGraph();
            if (nodeManager == null || edgeManager == null)
            {
                Debug.LogError("DungeonGraphController requires preconfigured node and edge managers before play starts.", this);
                return;
            }

            nodeManager.ClearAll();
            edgeManager.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();
            ClearUnitsRoot();

            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            var entranceView = nodeManager.CreateNode(entrance);
            RegisterUnlockedNode(entranceView);
            CreateMainUnit(entranceView);

            HasLockedNodesVisible = false;
        }

        private void CreateMainUnit(Node entranceNode)
        {
            if (!Application.isPlaying)
                return;

            var spawnPosition = entranceNode.UnitPosition.position;
            if (unitsRoot == null)
            {
                Debug.LogError("DungeonGraphController requires a preconfigured Units root before play starts.", this);
                return;
            }

            MainUnit mainUnit = Instantiate(mainUnitPrefab, spawnPosition, Quaternion.identity);
            mainUnit.transform.SetParent(unitsRoot, true);
            mainUnit.transform.position = spawnPosition;
            mainUnit.transform.localScale = Vector3.one;
            mainUnit.name = "Player";
            mainUnit.Initialize(null);
            mainUnit.InitializeMainUnit(gameStateEventChannel);

            entranceNode.AssignUnit(null, mainUnit);
            playerStatusHud?.SetTarget(mainUnit);
            artifactEventChannel.RaiseEvent(new UnitArtifactApplyRequestedEvent(mainUnit));
        }

        private void ClearUnitsRoot()
        {
            if (unitsRoot == null)
                return;

            if (Application.isPlaying)
            {
                ClearChildren(unitsRoot);
            }
            else
            {
                ClearChildrenImmediate(unitsRoot);
            }
        }

        private void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private void ClearChildrenImmediate(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
        }

        [ContextMenu("Show Locked Nodes")]
        public void ShowLockedNodes()
        {
            if (!Application.isPlaying || graph == null || nodeManager == null)
                return;

            HasLockedNodesVisible = true;
            RefreshLockedNodes();
        }
        
        [ContextMenu("Hide Locked Nodes")]
        public void HideLockedNodes()
        {
            HasLockedNodesVisible = false;
            lockedNodeByCollider.Clear();
            nodeManager?.ClearLockedNodes();
        }

        public void TryBuildAt(Node lockedNode)
        {
            if (lockedNode == null)
                return;

            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            ShowBuildConfirmPanel(lockedNode, inputDataSO.ReadScreenMousePosition());
        }

        private void RequestBuildCost(Node lockedNode)
        {
            if (lockedNode == null)
                return;

            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            costEventChannel.RaiseEvent(new BuildCostRequestedEvent(lockedNode, buildGoldCost));
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            var lockedNode = evt.Node;
            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            var buildPosition = lockedNode.GridPosition;
            var buildParent = lockedNode.FromNode;
            var type = ResolveBuildType(lockedNode);

            HideLockedNodeView(lockedNode);
            nodeManager.ClearLockedNodeAt(buildPosition);

            var node = graph.AddNode(type, buildPosition);
            var nodeView = nodeManager.CreateNode(node);

            lastBuiltNodeView = nodeView;
            lastBuiltFrame = Time.frameCount;
            RegisterUnlockedNode(nodeView);
            ConnectAdjacentNodes(node, buildParent);
            nodeEventChannel?.RaiseEvent(new NodeBuiltEvent(nodeView));

            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        private void HandleBuildCostRejected(BuildCostRejectedEvent evt)
        {
            if (evt.Node == null || graph.IsOccupied(evt.Node.GridPosition))
                return;

            if (buildConfirmPanel == null)
                return;

            buildConfirmPanel.ShowNotEnoughGoldAt(evt.GoldAmount, evt.CurrentGold, lastBuildClickScreenPosition);
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

            foreach (var direction in directions)
            {
                var adjacentPosition = node.GridPosition + direction;
                if (!graph.TryGetNodeAt(adjacentPosition, out var adjacentNode))
                    continue;

                if (adjacentNode == preferredFirstNode)
                    continue;

                TryConnectNodes(adjacentNode, node);
            }
        }

        private void TryConnectNodes(DungeonNode fromNode, DungeonNode toNode)
        {
            if (!CanConnectNodes(fromNode, toNode))
                return;

            if (!graph.Connect(fromNode, toNode))
                return;

            edgeManager.CreateEdge(fromNode.GridPosition, toNode.GridPosition);
        }

        private bool CanConnectNodes(DungeonNode a, DungeonNode b)
        {
            return AreOrthogonallyAdjacent(a, b) || IsInitialEntranceCandidateConnection(a, b);
        }

        private bool AreOrthogonallyAdjacent(DungeonNode a, DungeonNode b)
        {
            if (a == null || b == null)
                return false;

            var delta = a.GridPosition - b.GridPosition;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
        }

        private bool IsInitialEntranceCandidateConnection(DungeonNode a, DungeonNode b)
        {
            if (a == null || b == null)
                return false;

            if (a.Type == DungeonNodeType.Entrance)
                return IsInitialEntranceCandidatePosition(a.GridPosition, b.GridPosition);

            if (b.Type == DungeonNodeType.Entrance)
                return IsInitialEntranceCandidatePosition(b.GridPosition, a.GridPosition);

            return false;
        }

        private bool IsInitialEntranceCandidatePosition(Vector2Int entrancePosition, Vector2Int candidatePosition)
        {
            var offset = candidatePosition - entrancePosition;
            foreach (var candidateOffset in initialBuildCandidateOffsets)
            {
                if (offset == candidateOffset)
                    return true;
            }

            return false;
        }

        private void HideLockedNodeView(Node lockedNode)
        {
            if (lockedNode == null)
                return;

            lockedNodeByCollider.Remove(lockedNode.ClickCollider);
            lockedNode.gameObject.SetActive(false);
            Destroy(lockedNode.gameObject);
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
            nodeManager.ClearLockedNodes();

            var usedLockedNodePositions = new HashSet<Vector2Int>();
            var mainGridPosition = ResolveMainGridPosition();
            foreach (var node in graph.Nodes)
            {
                var candidateOffsets = ResolveBuildCandidateOffsets(node);
                foreach (var offset in candidateOffsets)
                {
                    var position = node.GridPosition + offset;
                    if (graph.IsOccupied(position) || !usedLockedNodePositions.Add(position))
                        continue;

                    if (!IsAllowedBuildCandidatePosition(position, mainGridPosition))
                        continue;

                    if (!TryResolveBuildParent(position, node, out var parentNode))
                        continue;

                    var lockedNode = nodeManager.CreateLockedNode(parentNode, position, position - parentNode.GridPosition);
                    lockedNode.SetBuildCost(buildGoldCost);
                    RegisterLockedNode(lockedNode);
                }
            }
        }

        private void ShowBuildConfirmPanel(Node lockedNode, Vector2 screenPosition)
        {
            lastBuildClickScreenPosition = screenPosition;
            if (buildConfirmPanel != null)
            {
                buildConfirmPanel.ShowAt(buildGoldCost, screenPosition, () => RequestBuildCost(lockedNode));
                return;
            }

            Debug.LogError("DungeonGraphController needs an existing BuildConfirmPanelView assigned in the inspector.", this);
        }

        private Vector2Int[] ResolveBuildCandidateOffsets(DungeonNode node)
        {
            return node.Type == DungeonNodeType.Entrance
                ? initialBuildCandidateOffsets
                : nodeBuildCandidateOffsets;
        }

        private Vector2Int ResolveMainGridPosition()
        {
            foreach (var node in graph.Nodes)
            {
                if (node.Type == DungeonNodeType.Entrance)
                    return node.GridPosition;
            }

            return Vector2Int.zero;
        }

        private bool IsAllowedBuildCandidatePosition(Vector2Int position, Vector2Int mainGridPosition)
        {
            if (position.x >= mainGridPosition.x)
                return false;

            return true;
        }

        private bool TryResolveBuildParent(Vector2Int position, DungeonNode preferredParent, out DungeonNode parentNode)
        {
            parentNode = null;

            if (preferredParent != null && preferredParent.FreePorts > 0)
                parentNode = preferredParent;

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
            if (buildConfirmPanel != null && buildConfirmPanel.IsOpen)
                return;

            if (IsPointerOverUi() || IsPointerOverNodePanel())
                return;

            Vector2 worldPosition = inputDataSO.SceneToWorldPoint();
            var clickedCollider = Physics2D.OverlapPoint(worldPosition, nodeClickMask);

            if (clickedCollider == null)
                return;

            if (!lockedNodeByCollider.TryGetValue(clickedCollider, out var lockedNode))
            {
                if (!unlockedNodeByCollider.TryGetValue(clickedCollider, out var unlockedNode))
                    return;

                if (!TutorialInputGate.AllowsUnlockedNode(unlockedNode))
                    return;

                nodeEventChannel.RaiseEvent(new NodeCameraFocusStartedEvent(unlockedNode));
                nodeEventChannel.RaiseEvent(new UnlockedNodeClickedEvent(unlockedNode));
                return;
            }

            if (!TutorialInputGate.AllowsLockedNode(lockedNode))
                return;

            TryBuildAt(lockedNode);
        }

        private void ProcessRightMouseInput()
        {
            if (buildConfirmPanel != null && buildConfirmPanel.IsOpen)
                return;

            if (IsPointerOverUi())
                return;

            Vector2 worldPosition = inputDataSO.SceneToWorldPoint();
            var screenPosition = inputDataSO.ReadScreenMousePosition();
            if (TryRaiseEntityStatusAt(worldPosition, screenPosition))
                return;

            var clickedCollider = Physics2D.OverlapPoint(worldPosition, nodeClickMask);

            if (clickedCollider == null)
                return;

            if (!unlockedNodeByCollider.TryGetValue(clickedCollider, out var unlockedNode))
                return;

            if (!unlockedNode.HasAssignedUnit)
                return;

            nodeEventChannel.RaiseEvent(new UnitStatusRequestedEvent(unlockedNode, screenPosition));
        }

        private bool TryRaiseEntityStatusAt(Vector2 worldPosition, Vector2 screenPosition)
        {
            var colliders = Physics2D.OverlapPointAll(worldPosition);
            if (colliders == null || colliders.Length == 0)
                return false;

            Unit closestUnit = null;
            Enemy closestEnemy = null;
            var closestUnitDistance = float.PositiveInfinity;
            var closestEnemyDistance = float.PositiveInfinity;

            foreach (var hit in colliders)
            {
                if (hit == null)
                    continue;

                var unitTarget = hit.GetComponentInParent<UnitClickTarget>();
                if (unitTarget != null && unitTarget.Target != null)
                {
                    var distance = Vector2.SqrMagnitude((Vector2)hit.bounds.center - worldPosition);
                    if (distance < closestUnitDistance)
                    {
                        closestUnitDistance = distance;
                        closestUnit = unitTarget.Target;
                    }
                }

                var enemyTarget = hit.GetComponentInParent<EnemyClickTarget>();
                if (enemyTarget != null && enemyTarget.Target != null)
                {
                    var distance = Vector2.SqrMagnitude((Vector2)hit.bounds.center - worldPosition);
                    if (distance < closestEnemyDistance)
                    {
                        closestEnemyDistance = distance;
                        closestEnemy = enemyTarget.Target;
                    }
                }
            }

            if (closestUnit == null && closestEnemy == null)
                return false;

            if (closestEnemy != null && (closestUnit == null || closestEnemyDistance <= closestUnitDistance))
            {
                nodeEventChannel.RaiseEvent(new EnemyStatusRequestedEvent(closestEnemy, screenPosition));
                return true;
            }

            nodeEventChannel.RaiseEvent(new UnitStatusRequestedEvent(FindNodeForUnit(closestUnit), closestUnit, screenPosition));
            return true;
        }

        private Node FindNodeForUnit(Unit unit)
        {
            if (unit == null)
                return null;

            foreach (var node in Node.ActiveNodes)
            {
                if (node != null && node.AssignedUnitInstance == unit)
                    return node;
            }

            return null;
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

    }
}
