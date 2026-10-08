using System.Collections.Generic;
using Code.Artifacts;
using Code.Buildings;
using Code.Core;
using Code.Enemies;
using Code.Events;
using Code.Tutorial;
using Code.UI;
using Code.UI.Popup;
using Code.Units;
using Code.Persistence;
using Code.Manager;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Code.MapCreateSystem
{
    public class DungeonGraphController : MonoBehaviour
    {
        /// <summary>방 확장 비용 팝업의 이름. 튜토리얼이 이 팝업의 확장 버튼을 강조할 때 쓴다.</summary>
        public const string ExpandRoomPopupTag = "build.expand-room";

        public static DungeonGraphController Current { get; private set; }

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

        [SerializeField, Min(0f), Tooltip("방 하나를 확장하는 공사 시간(게임 시간 초). 공사 중인 방은 막힌 길이다.")]
        private float roomConstructionSeconds = 4f;

        [SerializeField, Range(0f, 1f), Tooltip("공사를 취소할 때 돌려받는 비율(실제로 낸 금화 기준).")]
        private float constructionCancelRefundRate = 0.5f;

        [SerializeField]
        private GameEventChannelSO artifactEventChannel;

        [Header("Events")]
        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField] private GameEventChannelSO nodeEventChannel;

        [Header("Input")]
        [SerializeField]
        private InputDataSO inputDataSO;

        [SerializeField]
        private Camera inputCamera;

        [SerializeField]
        private LayerMask nodeClickMask = Physics2D.DefaultRaycastLayers;

        [SerializeField, Range(0.1f, 1f), Tooltip("노드 클릭 유효 범위(콜라이더 대비 비율). 작을수록 노드 중심 가까이서만 클릭이 먹는다. 콜라이더/전투 트리거는 안 건드림.")]
        private float nodeClickAreaScale = 0.55f;

        [SerializeField, Min(0.1f),
         Tooltip("우클릭으로 부하 정보를 열 수 있는 반경(월드 단위). " +
                 "부하 콜라이더 지름이 0.8이므로 그보다 조금 넉넉하게 둔다.")]
        private float unitInspectRadius = 1.2f;

        [Header("UI Blocking")]
        [SerializeField]
        private RectTransform nodePanelBlockRect;

        private DungeonGraph graph;
        private readonly Dictionary<Collider2D, Node> lockedNodeByCollider = new();
        private readonly Dictionary<Collider2D, Node> unlockedNodeByCollider = new();
        private readonly List<DungeonNode> buildParentCandidates = new();
        private readonly List<RaycastResult> uiRaycastResults = new();
        private Node lastBuiltNodeView;
        private int lastBuiltFrame = -1;
        private bool hasPendingMouseInput;
        private bool hasPendingRightMouseInput;

        public bool HasLockedNodesVisible { get; private set; }
        public IEnumerable<Node> LockedCandidateNodes => lockedNodeByCollider.Values;

        private void OnEnable()
        {
            Current = this;

            if (!Application.isPlaying)
                return;

            costEventChannel.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            inputDataSO?.SetWorldCamera(inputCamera);
            inputDataSO.OnMouseInputEvent += HandleMouseInput;
        }

        

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

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

            TutorialInputGate.Clear();
            RebuildInitialGraph();
            ShowLockedNodes();
        }

        private IEnumerator Start()
        {
            if (!Application.isPlaying)
                yield break;

            // 모든 매니저의 Awake/Start가 기본 UI를 만든 다음 체크포인트 값으로 한 번 갱신한다.
            yield return null;
            RunSaveSystem.TryRestoreCurrentRun();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                RunSaveSystem.SaveCurrentRun();
        }

        private void OnApplicationQuit()
        {
            RunSaveSystem.SaveCurrentRun();
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

            if (nodeManager == null || edgeManager == null)
            {
                graph = new DungeonGraph();
                Debug.LogError("DungeonGraphController requires preconfigured node and edge managers before play starts.", this);
                return;
            }

            BuildInitialGraph();
            HasLockedNodesVisible = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 플레이 시작 때와 같은 첫 던전 배치를 에디터에서 씬에 펼친다. 플레이 없이 씬 구성을
        /// 확인하는 용도이고, 오프닝 방 검사 테스트(AssetWiringTests)가 이걸로 첫 방을 만든다.
        /// </summary>
        public void EditorBakeInitialScenePreview()
        {
            if (Application.isPlaying || nodeManager == null || edgeManager == null || unitsRoot == null)
                return;

            BuildInitialGraph();
            HasLockedNodesVisible = true;
            RefreshLockedNodes();
        }
#endif

        private void BuildInitialGraph()
        {
            graph = new DungeonGraph();
            nodeManager.ClearAll();
            edgeManager.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();
            ClearUnitsRoot();

            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            var entranceView = nodeManager.CreateNode(entrance);
            RegisterUnlockedNode(entranceView);
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

            ShowBuildConfirmPopup(lockedNode);
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

            // 방은 바로 생기지만 공사가 끝나야 쓸 수 있다. 방이 생겼다는 알림(튜토리얼·영업 시작 조건)은 완공 때 낸다.
            if (roomConstructionSeconds > 0f)
            {
                nodeView.ConstructionCompleted += HandleConstructionCompleted;
                nodeView.BeginConstruction(roomConstructionSeconds, evt.GoldAmount);
            }
            else
            {
                nodeEventChannel?.RaiseEvent(new NodeBuiltEvent(nodeView));
            }

            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        private void HandleConstructionCompleted(Node nodeView)
        {
            nodeView.ConstructionCompleted -= HandleConstructionCompleted;
            nodeEventChannel?.RaiseEvent(new NodeBuiltEvent(nodeView));

            // 공사 중인 방에서는 다음 확장을 뻗지 않았으므로, 완공되면 후보를 다시 깐다.
            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        private void ShowCancelConstructionPopup(Node nodeView)
        {
            var refund = ResolveConstructionRefund(nodeView);
            PopupController.Show(PopupRequest.Confirm(
                "공사 취소",
                $"공사를 멈추고 이 방을 되돌립니다.\n낸 비용 {nodeView.ConstructionPaidGold:N0}G 중 {refund:N0}G를 돌려받습니다.",
                () => CancelConstruction(nodeView),
                confirmLabel: "공사 취소",
                cancelLabel: "계속 짓기",
                destructive: true));
        }

        private int ResolveConstructionRefund(Node nodeView) =>
            Mathf.FloorToInt(nodeView.ConstructionPaidGold * constructionCancelRefundRate);

        /// <summary>
        /// 공사 중인 방을 없애고 잠긴 칸으로 되돌린다. 공사 중인 방에는 유닛·건물을 둘 수 없으므로
        /// 정리할 것은 그래프·라인·노드 표시뿐이다.
        /// </summary>
        private void CancelConstruction(Node nodeView)
        {
            if (nodeView == null || !nodeView.IsUnderConstruction || nodeView.Data == null)
                return;

            var refund = ResolveConstructionRefund(nodeView);
            nodeView.ConstructionCompleted -= HandleConstructionCompleted;

            // 장부에는 "건설 취소 환불"로 남는다. 노드가 사라지기 전에 알린다.
            if (refund > 0)
                costEventChannel?.RaiseEvent(new BuildCostRefundedEvent(nodeView, refund, 0f));

            edgeManager.RemoveEdgesFor(nodeView.Data.Id);
            graph.RemoveNode(nodeView.Data);
            unlockedNodeByCollider.Remove(nodeView.ClickCollider);
            nodeView.gameObject.SetActive(false);
            Destroy(nodeView.gameObject);

            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        private static bool IsUnderConstruction(DungeonNode node)
        {
            var view = node != null ? Node.FindByDataId(node.Id) : null;
            return view != null && view.IsUnderConstruction;
        }

        private void HandleBuildCostRejected(BuildCostRejectedEvent evt)
        {
            if (evt.Node == null || graph.IsOccupied(evt.Node.GridPosition))
                return;

            // 팝업을 띄울 때는 금화가 충분했지만 확인을 누르기 전에 줄어든 경우다.
            PopupController.Show(PopupRequest.Notice("골드 부족",
                $"확장 비용이 부족합니다.\n보유: {evt.CurrentGold:N0}G / 필요: {evt.GoldAmount:N0}G"));
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

            edgeManager.CreateEdge(fromNode.GridPosition, toNode.GridPosition, fromNode.Id, toNode.Id);
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

        // 표시 비용은 실제 청구액(건설 할인 반영)과 같아야 한다. 금화가 모자라면 팝업이 확장 버튼을 잠근다.
        private void ShowBuildConfirmPopup(Node lockedNode)
        {
            var costManager = CostManager.Current;
            int cost = costManager != null ? costManager.GetDiscountedBuildCost(buildGoldCost) : buildGoldCost;
            int gold = costManager != null ? costManager.CurrentGold : 0;

            PopupController.Show(PopupRequest
                .Cost("방 확장", "이 위치를 확장합니다.", cost, gold, () => RequestBuildCost(lockedNode), confirmLabel: "확장")
                .WithTag(ExpandRoomPopupTag));
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

            // 공사 중인 방에서는 다음 확장을 뻗지 않는다. 취소되면 붙일 곳이 사라진다.
            if (preferredParent != null && preferredParent.FreePorts > 0 && !IsUnderConstruction(preferredParent))
                parentNode = preferredParent;

            foreach (var direction in directions)
            {
                var adjacentPosition = position + direction;
                if (!graph.TryGetNodeAt(adjacentPosition, out var adjacentNode))
                    continue;

                if (adjacentNode.FreePorts <= 0 || IsUnderConstruction(adjacentNode))
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

        /// <summary>클릭 지점이 노드 콜라이더 중심부의 좁힌 영역 안인지. 콜라이더 자체(전투 트리거 공유)는
        /// 그대로 두고 클릭 판정만 nodeClickAreaScale 비율로 줄인다. 노드는 회전이 없어 AABB로 충분.</summary>
        private bool IsWithinClickArea(Collider2D clickedCollider, Vector2 worldPosition)
        {
            if (clickedCollider == null)
                return false;
            if (nodeClickAreaScale >= 1f)
                return true;

            var bounds = clickedCollider.bounds;
            var half = (Vector2)bounds.extents * nodeClickAreaScale;
            var offset = worldPosition - (Vector2)bounds.center;
            return Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y;
        }

        private void ProcessMouseInput()
        {
            if (PopupController.IsOpen)
                return;

            if (IsPointerOverUi() || IsPointerOverNodePanel())
                return;

            Vector2 worldPosition = inputDataSO.SceneToWorldPoint();
            if (TryRaiseUnitManagementAt(worldPosition))
                return;

            var clickedCollider = ResolveNodeClickCollider(worldPosition);

            if (clickedCollider == null)
                return;

            // 콜라이더(=전투필드 트리거 공유)는 크게 두고, 클릭 유효 범위만 중심부로 좁힌다.
            if (!lockedNodeByCollider.TryGetValue(clickedCollider, out var lockedNode))
            {
                if (!unlockedNodeByCollider.TryGetValue(clickedCollider, out var unlockedNode))
                    return;

                if (!TutorialInputGate.AllowsUnlockedNode(unlockedNode))
                    return;

                // 공사 중인 방은 관리할 것이 없다. 누르면 취소할지만 묻는다.
                if (unlockedNode.IsUnderConstruction)
                {
                    if (IsWithinClickArea(clickedCollider, worldPosition))
                        ShowCancelConstructionPopup(unlockedNode);
                    return;
                }

                var unitGrid = unlockedNode.TrapGrid;
                if (unitGrid != null
                    && unitGrid.IsFocusedGridVisible
                    && unitGrid.TrySelectCell(worldPosition, out var column, out var row))
                {
                    nodeEventChannel.RaiseEvent(new NodeGridCellSelectedEvent(unlockedNode, column, row));
                    return;
                }

                if (!IsWithinClickArea(clickedCollider, worldPosition))
                    return;

                nodeEventChannel.RaiseEvent(new UnlockedNodeClickedEvent(unlockedNode));
                nodeEventChannel.RaiseEvent(new NodeCameraFocusStartedEvent(unlockedNode));
                return;
            }

            if (!TutorialInputGate.AllowsLockedNode(lockedNode))
                return;

            if (!IsWithinClickArea(clickedCollider, worldPosition))
                return;

            TryBuildAt(lockedNode);
        }

        private bool TryRaiseUnitManagementAt(Vector2 worldPosition)
        {
            var colliders = Physics2D.OverlapPointAll(worldPosition);
            if (colliders == null || colliders.Length == 0)
                return false;

            Unit closestUnit = null;
            var closestDistance = float.PositiveInfinity;
            foreach (var hit in colliders)
            {
                if (hit == null)
                    continue;

                var unitTarget = hit.GetComponentInParent<UnitClickTarget>();
                if (unitTarget == null || unitTarget.Target == null || unitTarget.Target is MainUnit)
                    continue;

                var distance = Vector2.SqrMagnitude((Vector2)hit.bounds.center - worldPosition);
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                closestUnit = unitTarget.Target;
            }

            if (closestUnit == null)
                return false;

            nodeEventChannel.RaiseEvent(new UnitManagementRequestedEvent(ResolveNodeForUnit(closestUnit), closestUnit));
            return true;
        }

        private void ProcessRightMouseInput()
        {
            if (PopupController.IsOpen)
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

            // 노드 콜라이더는 방 하나(15x15)를 통째로 덮는데 카메라는 그보다 좁게 비춘다.
            // 그래서 콜라이더에만 기대면 빈 바닥을 눌러도 부하 정보가 떠서, 화면 아무 데나
            // 우클릭한 것처럼 보인다. 부하 근처를 눌렀을 때만 연다.
            // 부하 콜라이더를 정확히 맞힌 경우는 위의 TryRaiseEntityStatusAt이 이미 처리했고,
            // 여기 반경은 살짝 빗나간 클릭을 받아주는 여유분이다.
            var assignedUnit = unlockedNode.AssignedUnitInstance;
            if (assignedUnit == null)
                return;

            var offset = (Vector2)assignedUnit.transform.position - worldPosition;
            if (offset.sqrMagnitude > unitInspectRadius * unitInspectRadius)
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

            nodeEventChannel.RaiseEvent(new UnitStatusRequestedEvent(ResolveNodeForUnit(closestUnit), closestUnit, screenPosition));
            return true;
        }

        private Node ResolveNodeForUnit(Unit unit)
        {
            return Node.TryFindUnit(unit, out var node, out _) ? node : null;
        }

        private Collider2D ResolveNodeClickCollider(Vector2 worldPosition)
        {
            var hits = Physics2D.OverlapPointAll(worldPosition, nodeClickMask);
            if (hits == null)
                return null;

            foreach (var hit in hits)
            {
                if (hit != null && unlockedNodeByCollider.ContainsKey(hit))
                    return hit;
            }

            foreach (var hit in hits)
            {
                if (hit != null && lockedNodeByCollider.ContainsKey(hit))
                    return hit;
            }

            return null;
        }

        private bool IsPointerOverUi()
        {
            if (EventSystem.current == null || Mouse.current == null)
                return false;

            uiRaycastResults.Clear();
            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };

            EventSystem.current.RaycastAll(pointerData, uiRaycastResults);
            foreach (var hit in uiRaycastResults)
            {
                if (hit.gameObject == null)
                    continue;

                if (hit.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;

                if (hit.gameObject.GetComponentInParent<ScrollRect>() != null)
                    return true;
            }

            return false;
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

        public RunSaveData CaptureRunSave()
        {
            var save = new RunSaveData();
            foreach (var node in Node.ActiveNodes)
            {
                if (node?.Data == null)
                    continue;

                var savedNode = new SavedNode
                {
                    type = node.Data.Type,
                    x = node.GridPosition.x,
                    y = node.GridPosition.y,
                    danger = node.DangerLevel,
                    constructionRemaining = node.ConstructionRemaining,
                    constructionPaidGold = node.ConstructionPaidGold,
                    centralBuilding = CaptureBuilding(node.AssignedBuilding)
                };

                var grid = node.TrapGrid;
                if (grid != null)
                {
                    for (var row = 0; row < grid.Rows; row++)
                    for (var column = 0; column < grid.Columns; column++)
                    {
                        var building = grid.BuildingAt(column, row);
                        if (building == null)
                            continue;
                        var savedBuilding = CaptureBuilding(building);
                        if (savedBuilding == null)
                            continue;
                        savedBuilding.column = column;
                        savedBuilding.row = row;
                        savedNode.cellBuildings.Add(savedBuilding);
                    }
                }

                foreach (var placement in node.UnitPlacements)
                {
                    if (placement?.Instance == null || placement.Instance is MainUnit)
                        continue;
                    savedNode.units.Add(new SavedUnit
                    {
                        assetKey = HiredUnitRoster.AssetKey(placement.Data),
                        column = placement.Column,
                        row = placement.Row,
                        condition = placement.Instance.CaptureConditionState()
                    });
                }

                save.nodes.Add(savedNode);
            }

            foreach (var edge in EdgeLine.ActiveEdges)
            {
                var from = Node.FindByDataId(edge?.FromId);
                var to = Node.FindByDataId(edge?.ToId);
                if (from == null || to == null)
                    continue;
                save.edges.Add(new SavedEdge
                {
                    fromX = from.GridPosition.x,
                    fromY = from.GridPosition.y,
                    toX = to.GridPosition.x,
                    toY = to.GridPosition.y,
                    building = CaptureBuilding(edge.InstalledBuilding)
                });
            }

            return save;
        }

        public bool RestoreRunSave(RunSaveData save)
        {
            if (!ValidateSaveLayout(save))
                return false;

            graph = new DungeonGraph();
            nodeManager.ClearAll();
            edgeManager.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();
            ClearUnitsRoot();

            var views = new Dictionary<Vector2Int, Node>();
            foreach (var saved in save.nodes)
            {
                var position = new Vector2Int(saved.x, saved.y);
                var data = graph.AddNode(saved.type, position);
                var view = nodeManager.CreateNode(data);
                view.RestoreDanger(saved.danger);
                RegisterUnlockedNode(view);
                if (saved.constructionRemaining > 0f)
                {
                    view.ConstructionCompleted += HandleConstructionCompleted;
                    view.BeginConstruction(saved.constructionRemaining, saved.constructionPaidGold);
                }
                views.Add(position, view);
            }

            foreach (var saved in save.edges)
            {
                var fromPosition = new Vector2Int(saved.fromX, saved.fromY);
                var toPosition = new Vector2Int(saved.toX, saved.toY);
                if (!views.TryGetValue(fromPosition, out var from) || !views.TryGetValue(toPosition, out var to))
                    continue;
                if (graph.Connect(from.Data, to.Data))
                    edgeManager.CreateEdge(fromPosition, toPosition, from.Data.Id, to.Data.Id);
            }

            var roster = HiredUnitRoster.Current;
            foreach (var saved in save.nodes)
            {
                var view = views[new Vector2Int(saved.x, saved.y)];
                RestoreBuilding(view, saved.centralBuilding, roster, false);
                foreach (var building in saved.cellBuildings)
                    RestoreBuilding(view, building, roster, true);
                RestoreUnits(view, saved.units, roster);
            }

            foreach (var saved in save.edges)
            {
                if (saved.building == null)
                    continue;
                if (!views.TryGetValue(new Vector2Int(saved.fromX, saved.fromY), out var from)
                    || !views.TryGetValue(new Vector2Int(saved.toX, saved.toY), out var to))
                    continue;
                var edge = EdgeLine.FindBetween(from.Data.Id, to.Data.Id);
                var data = roster?.ResolveBuilding(saved.building.assetKey);
                RestoreBuildingState(BuildingPlacement.InstallOnEdge(edge, data), saved.building);
            }

            HasLockedNodesVisible = true;
            RefreshLockedNodes();
            return true;
        }

        private static SavedBuilding CaptureBuilding(Building building)
        {
            if (building == null || building is Portal || building.Data == null)
                return null;
            return new SavedBuilding
            {
                assetKey = HiredUnitRoster.AssetKey(building.Data),
                durability = building.CurrentDurability,
                storedGold = building is Treasury treasury ? treasury.StoredGold : 0,
                wear = building.Wear,
                closed = building.IsClosed,
                reopenOnDay = building.ReopenOnDay
            };
        }

        private void RestoreBuilding(Node node, SavedBuilding saved, HiredUnitRoster roster, bool cell)
        {
            if (saved == null)
                return;
            var data = roster?.ResolveBuilding(saved.assetKey);
            // 이전 저장의 포탈은 문 입구로 대체되었으므로 중앙 슬롯을 비워 둔다.
            if (data != null && data.Prefab is Portal)
                return;
            var building = cell
                ? BuildingPlacement.InstallOnCell(node, saved.column, saved.row, data)
                : BuildingPlacement.InstallCentral(node, data);
            RestoreBuildingState(building, saved);
        }

        private static void RestoreBuildingState(Building building, SavedBuilding saved)
        {
            if (building == null || saved == null)
                return;
            building.RestoreDurability(saved.durability);
            building.RestoreClosure(saved.closed, saved.reopenOnDay);
            building.RestoreWear(saved.wear);
            if (building is Treasury treasury)
                treasury.RestoreStoredGold(saved.storedGold);
        }

        private void RestoreUnits(Node node, List<SavedUnit> units, HiredUnitRoster roster)
        {
            if (units == null)
                return;
            foreach (var saved in units)
            {
                // 이전 저장 파일의 플레이어 캐릭터는 새 규칙에서 복원하지 않는다.
                if (saved.isMainUnit)
                    continue;

                var data = roster?.ResolveUnit(saved.assetKey);
                if (data?.Prefab == null)
                    continue;
                var instance = Instantiate(data.Prefab, node.TrapGrid.CellWorldPosition(saved.column, saved.row), Quaternion.identity);
                instance.transform.SetParent(unitsRoot, true);
                instance.Initialize(data);
                instance.ApplyConditionState(saved.condition);
                node.TryAssignUnitToCell(data, instance, saved.column, saved.row);
                artifactEventChannel?.RaiseEvent(new UnitArtifactApplyRequestedEvent(instance));
            }
        }

        private static bool ValidateSaveLayout(RunSaveData save)
        {
            if (save?.nodes == null || save.nodes.Count == 0)
                return false;
            var positions = new HashSet<Vector2Int>();
            var entrances = 0;
            foreach (var node in save.nodes)
            {
                if (!positions.Add(new Vector2Int(node.x, node.y)))
                    return false;
                if (node.type == DungeonNodeType.Entrance)
                    entrances++;
            }
            return entrances == 1;
        }

    }
}
