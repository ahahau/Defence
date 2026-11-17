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

        [SerializeField, Tooltip(GameScope.FieldTooltip)] private GameScope scope;
        private DungeonGradeManager _dungeonGrade;

        [SerializeField, Range(0f, 1f), Tooltip("공사를 취소할 때 돌려받는 비율(실제로 낸 금화 기준).")]
        private float constructionCancelRefundRate = 0.5f;

        /// <summary>그래프 방이 이 수 이하일 때 뻗는 후보는 중형으로 고정한다(입구·첫 확장).</summary>
        private const int OpeningMediumRoomCount = 3;

        /// <summary>자리마다 정해 둔 후보 크기. 다시 그려도 같은 크기를 보여 준다. 판 저장에는 넣지 않는다.</summary>
        private readonly Dictionary<Vector2Int, RoomSize> _candidateSizes = new();

        /// <summary>자리마다 정해 둔 후보 특수 효과. 크기와 같이 한 번 뽑으면 유지한다.</summary>
        private readonly Dictionary<Vector2Int, RoomFeature> _candidateFeatures = new();

        /// <summary>자리마다 정해 둔 후보 방 프리팹. 열기 전에 보여 준 모양과 문이 열고 나서 바뀌면 안 된다.</summary>
        private readonly Dictionary<Vector2Int, RoomShape> _candidateLayouts = new();

        private bool _missingCatalogReported;

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

            // 새 판의 시드. 이어하기면 곧 저장 복원이 시드를 되돌린다.
            RunRandom.BeginNewRun();
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
            _candidateSizes.Clear();
            _candidateFeatures.Clear();
            _candidateLayouts.Clear();
            nodeManager.ClearAll();
            edgeManager.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();
            ClearUnitsRoot();

            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            var entranceView = nodeManager.CreateNode(entrance);
            entranceView.ApplyLayout(LayoutCatalog != null && LayoutCatalog.Entrance != null ? LayoutCatalog.Entrance.ToShape() : null,
                nodeManager.GridSpacing);
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

            // 대형 방은 던전 등급이 모자라면 열 수 없다. 후보 위에도 "등급 N 필요"로 이미 보인다.
            if (!RoomSizeRules.MeetsGrade(lockedNode.Size, CurrentDungeonGrade))
            {
                PopupController.Show(PopupRequest.Notice("등급 부족",
                    $"{RoomSizeRules.Label(lockedNode.Size)} 방은 던전 등급 {RoomSizeRules.RequiredGrade(lockedNode.Size)} 이상에서 열 수 있습니다.\n" +
                    $"지금 등급: {CurrentDungeonGrade}"));
                return;
            }

            ShowBuildConfirmPopup(lockedNode);
        }

        private void RequestBuildCost(Node lockedNode)
        {
            if (lockedNode == null)
                return;

            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            costEventChannel.RaiseEvent(new BuildCostRequestedEvent(lockedNode, BaseBuildCost(lockedNode.Size, lockedNode.Feature)));
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            var lockedNode = evt.Node;
            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            var buildPosition = lockedNode.GridPosition;
            var buildParent = lockedNode.FromNode;
            var type = ResolveBuildType(lockedNode);
            var size = lockedNode.Size;
            var feature = lockedNode.Feature;
            var layout = lockedNode.Layout;

            HideLockedNodeView(lockedNode);
            nodeManager.ClearLockedNodeAt(buildPosition);

            var node = graph.AddNode(type, buildPosition);
            var nodeView = nodeManager.CreateNode(node);
            nodeView.ApplyLayout(layout, nodeManager.GridSpacing);
            nodeView.SetRoomSize(size);
            nodeView.SetRoomFeature(feature);
            _candidateSizes.Remove(buildPosition);
            _candidateFeatures.Remove(buildPosition);
            _candidateLayouts.Remove(buildPosition);

            lastBuiltNodeView = nodeView;
            lastBuiltFrame = Time.frameCount;
            RegisterUnlockedNode(nodeView);
            ConnectAdjacentNodes(node, buildParent);
            RefreshDoorsAround(node);

            // 방은 바로 생기지만 공사가 끝나야 쓸 수 있다. 방이 생겼다는 알림(튜토리얼·영업 시작 조건)은 완공 때 낸다.
            var constructionSeconds = ConstructionSecondsFor(size, feature);
            if (constructionSeconds > 0f)
            {
                nodeView.ConstructionCompleted += HandleConstructionCompleted;
                nodeView.BeginConstruction(constructionSeconds, evt.GoldAmount);
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

        /// <summary>
        /// 취소 환불. 크기마다 비율이 다르고(소 70%·중 50%·대 30%), 중형은 씬에 둔 기본 비율을 쓴다.
        /// 특수 효과 방은 20%p 덜 돌려준다.
        /// </summary>
        private int ResolveConstructionRefund(Node nodeView)
        {
            var rate = nodeView.Size == RoomSize.Medium
                ? constructionCancelRefundRate
                : RoomSizeRules.CancelRefundRate(nodeView.Size);
            rate = RoomFeatureRules.CancelRefundRate(rate, nodeView.Feature);
            return Mathf.FloorToInt(nodeView.ConstructionPaidGold * rate);
        }

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

            // 되돌린 자리는 같은 크기의 후보로 다시 보인다. 취소로 크기를 다시 뽑을 수 없게 한다.
            _candidateSizes[nodeView.GridPosition] = nodeView.Size;
            _candidateFeatures[nodeView.GridPosition] = nodeView.Feature;
            if (nodeView.Layout != null)
                _candidateLayouts[nodeView.GridPosition] = nodeView.Layout;

            // 이웃 방의 복도 반쪽도 같이 거둔다. 연결이 끊긴 뒤에는 누가 이웃이었는지 모르므로 먼저 적어 둔다.
            var formerNeighbors = new List<DungeonNode>();
            foreach (var neighborId in nodeView.Data.ConnectedNodeIds)
            {
                var neighbor = graph.GetNode(neighborId);
                if (neighbor != null)
                    formerNeighbors.Add(neighbor);
            }

            edgeManager.RemoveEdgesFor(nodeView.Data.Id);
            graph.RemoveNode(nodeView.Data);
            foreach (var neighbor in formerNeighbors)
                RefreshDoors(neighbor);
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

        /// <summary>두 방이 서로 마주 보는 문을 가졌는가. 맞닿은 방, 또는 입구와 그 왼쪽 위·아래 방만 이을 수 있다.</summary>
        private bool CanConnectNodes(DungeonNode a, DungeonNode b)
        {
            if (a == null || b == null)
                return false;

            var sideA = SideToward(a, b);
            var sideB = SideToward(b, a);
            return sideA != RoomDoors.None && sideB != RoomDoors.None
                   && ViewHasDoor(a, sideA) && ViewHasDoor(b, sideB);
        }

        private static RoomDoors SideToward(DungeonNode self, DungeonNode other) =>
            RoomDoorRules.SideToward(
                self.GridPosition, self.Type == DungeonNodeType.Entrance,
                other.GridPosition, other.Type == DungeonNodeType.Entrance);

        private static bool ViewHasDoor(DungeonNode node, RoomDoors side)
        {
            var view = Node.FindByDataId(node.Id);
            return view != null && view.HasDoor(side);
        }

        /// <summary>이 방에서 복도가 이어진 문을 그래프 연결로 다시 정해 그린다.</summary>
        private void RefreshDoors(DungeonNode node)
        {
            var view = node != null ? Node.FindByDataId(node.Id) : null;
            if (view == null)
                return;

            var connected = RoomDoors.None;
            foreach (var otherId in node.ConnectedNodeIds)
            {
                var other = graph.GetNode(otherId);
                if (other != null)
                    connected |= SideToward(node, other);
            }

            view.SetConnectedDoors(connected);
        }

        private void RefreshDoorsAround(DungeonNode node)
        {
            RefreshDoors(node);
            foreach (var otherId in node.ConnectedNodeIds)
                RefreshDoors(graph.GetNode(otherId));
        }

        private RoomLayoutCatalog LayoutCatalog
        {
            get
            {
                var catalog = nodeManager != null ? nodeManager.LayoutCatalog : null;
                if (catalog == null && !_missingCatalogReported)
                {
                    _missingCatalogReported = true;
                    Debug.LogError($"{nameof(DungeonNodeManager)}에 방 프리팹 목록(RoomLayoutCatalog)이 연결되지 않았습니다. 방이 그려지지 않습니다.", this);
                }

                return catalog;
            }
        }

        /// <summary>
        /// 이 자리 후보의 방 모양. 한 번 정하면 유지하되, 크기가 다르거나 부모 쪽 문이 없으면(부모가 바뀐 경우) 다시 정한다.
        /// </summary>
        private RoomShape ResolveCandidateLayout(Vector2Int position, RoomSize size, RoomDoors requiredDoor)
        {
            if (_candidateLayouts.TryGetValue(position, out var layout)
                && layout != null && layout.Size == size && layout.HasDoor(requiredDoor))
                return layout;

            layout = RollCandidateShape(position, size, requiredDoor);
            _candidateLayouts[position] = layout;
            return layout;
        }

        /// <summary>
        /// 후보 모양을 판 시드로 정한다. 가끔(<see cref="RoomLayoutRules.SpecialRoomChance"/>) 손으로 만든 특별 방 프리팹이고,
        /// 나머지는 그 자리에서 문과 모양을 새로 깎는다. 부모 쪽 문을 시드에 섞어, 부모가 바뀌면 다른 모양이 나온다.
        /// </summary>
        private RoomShape RollCandidateShape(Vector2Int position, RoomSize size, RoomDoors requiredDoor)
        {
            var salt = (int)requiredDoor;
            var catalog = LayoutCatalog;
            if (catalog != null
                && RunRandom.Value(RunRandomStream.RoomLayout, position.x, position.y * 16 + salt) < RoomLayoutRules.SpecialRoomChance)
            {
                var special = catalog.Pick(size, requiredDoor,
                    RunRandom.Value(RunRandomStream.RoomLayout, position.y, position.x * 16 + salt));
                if (special != null && special.HasDoor(requiredDoor))
                    return special.ToShape();
            }

            var doors = RoomShapeGenerator.RollDoors(requiredDoor,
                RunRandom.Hash(RunRandomStream.RoomLayout, position.x * 31 + salt, position.y));
            return RoomShapeGenerator.Generate(size, doors, false,
                RunRandom.Hash(RunRandomStream.RoomLayout, position.x, position.y * 31 + salt));
        }

        /// <summary>
        /// 저장된 방 모양. 줄이 있으면 그대로 되살린다(생성한 방은 다시 깎으면 생성 규칙이 바뀌었을 때 달라진다).
        /// 줄이 없으면 프리팹 이름으로, 그것도 없으면 네 변 모두 문이 있는 방을 새로 깎아 저장된 연결을 살린다.
        /// </summary>
        private RoomShape ResolveSavedShape(string layoutId, List<string> rows, int doors, DungeonNodeType type, RoomSize size,
            Vector2Int position)
        {
            var catalog = LayoutCatalog;
            var prefab = catalog != null ? catalog.Find(layoutId) : null;
            var entrance = type == DungeonNodeType.Entrance;
            if (rows != null && rows.Count > 0)
                return new RoomShape(layoutId, size, entrance, (RoomDoors)doors, rows.ToArray(), prefab);

            if (prefab != null)
                return prefab.ToShape();

            if (!string.IsNullOrEmpty(layoutId))
                Debug.LogWarning($"저장된 방 모양 '{layoutId}'을(를) 찾지 못해 새로 깎습니다.", this);

            if (entrance && catalog != null && catalog.Entrance != null)
                return catalog.Entrance.ToShape();

            return RoomShapeGenerator.Generate(size, RoomDoors.Up | RoomDoors.Right | RoomDoors.Down | RoomDoors.Left, entrance,
                RunRandom.Hash(RunRandomStream.RoomLayout, position.x, position.y));
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
                // 공사 중인 방에서는 다음 확장을 뻗지 않는다. 취소되면 붙일 곳이 사라진다.
                var view = Node.FindByDataId(node.Id);
                if (view == null || node.FreePorts <= 0 || IsUnderConstruction(node))
                    continue;

                // 후보는 이 방의 문으로만 뻗는다. 입구 바깥문(오른쪽)은 침입자가 들어오는 곳이라 뻗지 않는다.
                var fromEntrance = node.Type == DungeonNodeType.Entrance;
                foreach (var side in RoomDoorRules.Sides)
                {
                    if (!view.HasDoor(side))
                        continue;

                    var offset = RoomDoorRules.CandidateOffset(fromEntrance, side);
                    if (offset == Vector2Int.zero)
                        continue;

                    var position = node.GridPosition + offset;
                    if (graph.IsOccupied(position) || usedLockedNodePositions.Contains(position))
                        continue;

                    if (!IsAllowedBuildCandidatePosition(position, mainGridPosition))
                        continue;

                    var size = ResolveCandidateSize(position);
                    var layout = ResolveCandidateLayout(position, size, RoomDoorRules.RequiredDoor(fromEntrance, side));
                    usedLockedNodePositions.Add(position);

                    var lockedNode = nodeManager.CreateLockedNode(node, position, offset, layout);
                    var feature = ResolveCandidateFeature(position);
                    lockedNode.SetRoomSize(size);
                    lockedNode.SetRoomFeature(feature);
                    lockedNode.SetBuildCandidateLabel(BuildCandidateLabel(lockedNode));
                    RegisterLockedNode(lockedNode);
                }
            }
        }

        // 표시 비용은 실제 청구액(건설 할인 반영)과 같아야 한다. 금화가 모자라면 팝업이 확장 버튼을 잠근다.
        private void ShowBuildConfirmPopup(Node lockedNode)
        {
            var costManager = CostManager.Current;
            var size = lockedNode.Size;
            var feature = lockedNode.Feature;
            int cost = DiscountedBuildCost(size, feature);
            int gold = costManager != null ? costManager.CurrentGold : 0;
            var seconds = ConstructionSecondsFor(size, feature);

            var description = RoomSizeRules.Describe(size, seconds);
            if (feature != RoomFeature.None)
            {
                var refund = RoomFeatureRules.CancelRefundRate(
                    size == RoomSize.Medium ? constructionCancelRefundRate : RoomSizeRules.CancelRefundRate(size), feature);
                description += $"\n특수 효과 {RoomFeatureRules.Label(feature)}: {RoomFeatureRules.Describe(feature)}" +
                               $" (취소 환불 {Mathf.RoundToInt(refund * 100f)}%)";
            }

            description += $"\n{RoomFeatureRules.ConnectionLabel(PredictConnections(lockedNode))} · {RoomDoorRules.Label(lockedNode.Doors)}";

            PopupController.Show(PopupRequest
                .Cost("방 확장", description, cost, gold, () => RequestBuildCost(lockedNode), confirmLabel: "확장")
                .WithTag(ExpandRoomPopupTag));
        }

        /// <summary>
        /// 잠긴 후보 위의 안내: 크기(·특수 효과) / 비용·공사 시간(또는 필요 등급) / 연결 방향.
        /// </summary>
        private string BuildCandidateLabel(Node lockedNode)
        {
            var size = lockedNode.Size;
            var feature = lockedNode.Feature;
            var label = RoomSizeRules.CandidateLabel(
                size, DiscountedBuildCost(size, feature), ConstructionSecondsFor(size, feature), CurrentDungeonGrade);
            if (feature != RoomFeature.None)
            {
                var lineBreak = label.IndexOf('\n');
                label = lineBreak < 0
                    ? $"{label} · {RoomFeatureRules.Label(feature)}"
                    : $"{label.Substring(0, lineBreak)} · {RoomFeatureRules.Label(feature)}{label.Substring(lineBreak)}";
            }

            return $"{label}\n{RoomFeatureRules.ConnectionLabel(PredictConnections(lockedNode))}";
        }

        /// <summary>그 후보를 열면 이어질 방향. 실제 연결(<see cref="ConnectAdjacentNodes"/>)과 같은 규칙을 쓴다.</summary>
        private List<Vector2Int> PredictConnections(Node lockedNode)
        {
            var position = lockedNode.GridPosition;
            var neighborPorts = new Dictionary<Vector2Int, int>();
            var parent = lockedNode.FromNode;
            var parentDirection = parent != null ? parent.GridPosition - position : Vector2Int.zero;
            // 후보는 부모 방의 문으로 뻗었고 부모 쪽 문을 가진 모양으로 골랐으니 부모와는 늘 이어진다.
            if (parent != null)
                neighborPorts[parentDirection] = parent.FreePorts;

            foreach (var direction in directions)
            {
                if (!graph.TryGetNodeAt(position + direction, out var adjacent) || adjacent == parent)
                    continue;

                var side = RoomDoorRules.FromDirection(direction);
                var adjacentSide = RoomDoorRules.SideToward(
                    adjacent.GridPosition, adjacent.Type == DungeonNodeType.Entrance, position, false);
                if (lockedNode.HasDoor(side) && ViewHasDoor(adjacent, adjacentSide))
                    neighborPorts[direction] = adjacent.FreePorts;
            }

            return RoomFeatureRules.PredictConnections(
                parentDirection, neighborPorts, graph.GetMaxConnections(ResolveBuildType(lockedNode)));
        }

        /// <summary>
        /// 이 자리 후보의 특수 효과. 한 번 정하면 유지하고, 처음 몇 방은 효과 없이 둔다(시작 흐름을 흔들지 않게).
        /// </summary>
        private RoomFeature ResolveCandidateFeature(Vector2Int position)
        {
            if (_candidateFeatures.TryGetValue(position, out var feature))
                return feature;

            feature = graph.Nodes.Count <= OpeningMediumRoomCount
                ? RoomFeature.None
                : RoomFeatureRules.Roll(
                    RunRandom.Value(RunRandomStream.RoomFeatureChance, position.x, position.y),
                    RunRandom.Value(RunRandomStream.RoomFeatureKind, position.x, position.y));
            _candidateFeatures[position] = feature;
            return feature;
        }

        /// <summary>
        /// 이 자리 후보의 크기. 한 번 정하면 다시 그려도 같다(열기 전에 공개한 정보가 바뀌면 안 된다).
        /// 처음 몇 방(입구 옆 첫 확장들)은 튜토리얼과 시작 흐름을 막지 않게 중형으로 둔다.
        /// </summary>
        private RoomSize ResolveCandidateSize(Vector2Int position)
        {
            if (_candidateSizes.TryGetValue(position, out var size))
                return size;

            size = graph.Nodes.Count <= OpeningMediumRoomCount
                ? RoomSize.Medium
                : RoomSizeRules.Roll(RunRandom.Value(RunRandomStream.RoomSize, position.x, position.y), CurrentDungeonGrade);
            _candidateSizes[position] = size;
            return size;
        }

        private int BaseBuildCost(RoomSize size, RoomFeature feature) =>
            RoomFeatureRules.BuildCost(RoomSizeRules.BuildCost(buildGoldCost, size), feature);

        private float ConstructionSecondsFor(RoomSize size, RoomFeature feature) =>
            RoomFeatureRules.ConstructionSeconds(RoomSizeRules.ConstructionSeconds(size, roomConstructionSeconds), feature);

        private int DiscountedBuildCost(RoomSize size, RoomFeature feature)
        {
            var costManager = CostManager.Current;
            var baseCost = BaseBuildCost(size, feature);
            return costManager != null ? costManager.GetDiscountedBuildCost(baseCost) : baseCost;
        }

        private int CurrentDungeonGrade
        {
            get
            {
                if (_dungeonGrade == null)
                    _dungeonGrade = GameScope.Resolve(scope, s => s.DungeonGrade, this, nameof(scope));
                return _dungeonGrade != null ? _dungeonGrade.Grade : 0;
            }
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

        private void HandleMouseInput()
        {
            hasPendingMouseInput = true;
        }

        /// <summary>클릭 지점이 노드 콜라이더 중심부의 좁힌 영역 안인지. 콜라이더 자체(전투 트리거 공유)는
        /// 그대로 두고 모양이 있는 방은 바닥 칸, 옛 노드는 nodeClickAreaScale 비율로 판정한다.</summary>
        private bool IsWithinClickArea(Collider2D clickedCollider, Vector2 worldPosition)
        {
            if (clickedCollider == null)
                return false;
            if ((unlockedNodeByCollider.TryGetValue(clickedCollider, out Node node)
                || lockedNodeByCollider.TryGetValue(clickedCollider, out node)) && node.Layout != null)
                return node.ContainsFloorPoint(worldPosition);
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

            if (IsPointerOverUi())
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

            var clickedCollider = ResolveNodeClickCollider(worldPosition);

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
                if (hit != null && unlockedNodeByCollider.ContainsKey(hit) && IsWithinClickArea(hit, worldPosition))
                    return hit;
            }

            foreach (var hit in hits)
            {
                if (hit != null && lockedNodeByCollider.ContainsKey(hit) && IsWithinClickArea(hit, worldPosition))
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
            // 버튼만 막으면 패널 바탕·창 뒤 덮개를 눌렀을 때 그 뒤의 방이 눌렸다. 화면 UI면 무엇이든 막고,
            // 방 위 글자처럼 지도에 붙은 월드 공간 UI만 지도 클릭으로 둔다.
            foreach (var hit in uiRaycastResults)
            {
                if (hit.gameObject == null)
                    continue;

                var canvas = hit.gameObject.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.WorldSpace)
                    return true;
            }

            return false;
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
                    roomSize = (int)node.Size,
                    roomFeature = (int)node.Feature,
                    layoutId = node.Layout != null ? node.Layout.Id : null,
                    layoutRows = node.Layout != null ? new List<string>(node.Layout.CopyLines()) : null,
                    layoutDoors = node.Layout != null ? (int)node.Layout.Doors : 0,
                    rumorTraps = node.Rumor.Traps,
                    rumorDowns = node.Rumor.Downs,
                    rumorRetreats = node.Rumor.Retreats,
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
                        // 여러 칸 함정은 덮은 칸마다 나온다. 기준 칸(왼쪽 아래)에서 한 번만 저장한다.
                        if (grid.TryGetAnchor(building, out var anchorColumn, out var anchorRow)
                            && (anchorColumn != column || anchorRow != row))
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

        /// <summary>뽑아 둔 확장 후보의 크기·특수 효과. 판 저장에 넣는다.</summary>
        public List<SavedCandidate> CaptureCandidateRolls()
        {
            var list = new List<SavedCandidate>();
            foreach (var pair in _candidateSizes)
            {
                _candidateFeatures.TryGetValue(pair.Key, out var feature);
                _candidateLayouts.TryGetValue(pair.Key, out var layout);
                list.Add(new SavedCandidate
                {
                    x = pair.Key.x, y = pair.Key.y, roomSize = (int)pair.Value, roomFeature = (int)feature,
                    layoutId = layout != null ? layout.Id : null,
                    layoutRows = layout != null ? new List<string>(layout.CopyLines()) : null,
                    layoutDoors = layout != null ? (int)layout.Doors : 0
                });
            }

            return list;
        }

        /// <summary>저장해 둔 후보를 되돌리고 후보를 다시 그린다. 없으면(옛 저장) 자리마다 시드로 뽑는다.</summary>
        public void RestoreCandidateRolls(List<SavedCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            foreach (var saved in candidates)
            {
                var position = new Vector2Int(saved.x, saved.y);
                if (graph != null && graph.IsOccupied(position))
                    continue;

                _candidateSizes[position] = System.Enum.IsDefined(typeof(RoomSize), saved.roomSize)
                    ? (RoomSize)saved.roomSize
                    : RoomSize.Medium;
                _candidateFeatures[position] = System.Enum.IsDefined(typeof(RoomFeature), saved.roomFeature)
                    ? (RoomFeature)saved.roomFeature
                    : RoomFeature.None;
                // 옛 저장처럼 모양이 없으면 자리마다 다시 깎는다.
                if (saved.layoutRows != null && saved.layoutRows.Count > 0)
                    _candidateLayouts[position] = ResolveSavedShape(saved.layoutId, saved.layoutRows, saved.layoutDoors,
                        DungeonNodeType.Corridor, _candidateSizes[position], position);
            }

            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        public bool RestoreRunSave(RunSaveData save)
        {
            if (!ValidateSaveLayout(save))
                return false;

            graph = new DungeonGraph();
            _candidateSizes.Clear();
            _candidateFeatures.Clear();
            _candidateLayouts.Clear();
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
                var size = System.Enum.IsDefined(typeof(RoomSize), saved.roomSize)
                    ? (RoomSize)saved.roomSize
                    : RoomSize.Medium;
                // 칸 수·막힌 칸이 모양을 따르므로 건물·유닛보다 먼저 입힌다.
                view.ApplyLayout(ResolveSavedShape(saved.layoutId, saved.layoutRows, saved.layoutDoors, saved.type, size, position),
                    nodeManager.GridSpacing);
                view.RestoreDanger(saved.danger);
                view.RestoreRumor(new RoomRumor(saved.rumorTraps, saved.rumorDowns, saved.rumorRetreats));
                view.SetRoomSize(size);
                view.SetRoomFeature(System.Enum.IsDefined(typeof(RoomFeature), saved.roomFeature)
                    ? (RoomFeature)saved.roomFeature
                    : RoomFeature.None);
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

            foreach (var node in graph.Nodes)
                RefreshDoors(node);

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
                closed = building.IsClosed,
                reopenOnDay = building.ReopenOnDay,
                repairRemaining = building.RepairRemaining,
                repairCount = building.RepairCount
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
                ? RestoreOnCell(node, saved, data)
                : BuildingPlacement.RestoreCentral(node, data);
            RestoreBuildingState(building, saved);
        }

        /// <summary>
        /// 저장된 칸에 다시 세운다. 칸 하나짜리였던 함정이 여러 칸이 되어 그 자리에 안 들어가면
        /// 가장 가까운 빈자리로 옮긴다 — 옛 저장에서 함정이 조용히 사라지면 낸 금화가 날아간다.
        /// </summary>
        private static Building RestoreOnCell(Node node, SavedBuilding saved, BuildingDataSO data)
        {
            var building = BuildingPlacement.RestoreOnCell(node, saved.column, saved.row, data);
            var grid = node != null ? node.TrapGrid : null;
            if (building != null || grid == null || data == null || data.Prefab == null)
                return building;

            var footprint = data.Prefab.Footprint;
            var origin = grid.IsValidCell(saved.column, saved.row)
                ? grid.CellWorldPosition(saved.column, saved.row)
                : node.transform.position;
            return grid.TryGetNearestFreeCell(origin, footprint, out var column, out var row)
                ? BuildingPlacement.RestoreOnCell(node, column, row, data)
                : null;
        }

        private static void RestoreBuildingState(Building building, SavedBuilding saved)
        {
            if (building == null || saved == null)
                return;
            building.RestoreDurability(saved.durability);
            building.RestoreClosure(saved.closed, saved.reopenOnDay);
            building.RestoreWear(saved.wear);
            building.RestoreRepair(saved.repairRemaining, saved.repairCount);
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
                instance.BindPolicies(GameScope.Resolve(scope, s => s.MoralePolicy, this, nameof(scope)));
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
