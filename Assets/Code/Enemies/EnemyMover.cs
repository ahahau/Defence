using Code.MapCreateSystem;
using Code.Buildings;
using Code.Manager;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Code.BT;
using UnityEngine;

namespace Code.Enemies
{
    public class EnemyMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;

        /// <summary>피로로 느려지는 배율. 1이면 평소 속도다. 시설에 머문 모험가가 낮춘다.</summary>
        public float FatigueSpeedMultiplier { get; set; } = 1f;
        [SerializeField, Min(0.01f)] private float minMoveDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float maxMoveDuration = 0.75f;
        [SerializeField, Min(0f)] private float visualHopHeight = 0.12f;
        [SerializeField, Min(0f)] private float visualSquashAmount = 0.08f;
        [SerializeField, Min(0f)] private float visualLeanAngle = 7f;
        [SerializeField] private Transform visual;

        // 일반 방은 파티 단위로만 점유한다. 같은 파티의 멤버 수는 따로 세야 마지막 멤버가
        // 떠날 때까지 뒤 파티를 막을 수 있다.
        private static readonly Dictionary<string, Dictionary<int, int>> _occupiedNodeCounts = new();
        private static int _nextPartyOccupancyId;
        private static readonly List<EnemyMover> _activeEnemies = new();
        public static IReadOnlyList<EnemyMover> ActiveEnemies => _activeEnemies;

        private readonly HashSet<string> _visitedNodes = new();
        private Node _currentNode;
        private Tween _moveTween;
        private Vector3 _visualStartLocalPosition;
        private Vector3 _visualStartLocalScale;
        private Vector3 _visualStartLocalEulerAngles;
        private bool _isTurning;
        private BattleAgent _battleAgent;
        private int _partyOccupancyId;
        private int _blockedNodeWaitTurns = 2;
        private int _blockedNodeWaitCount;

        public Func<Node, bool> NodeArrived { get; set; }
        /// <summary>
        /// 다음에 향할 곳을 정한다. null을 돌려주면 목적지 없이 배회한다.
        ///
        /// 비워 두면 예전처럼 금고를 향한다 — 목적을 모르는 이동체(보스·테스트)는 침입자로 본다.
        /// </summary>
        public Func<Node> RouteGoalResolver { get; set; }
        /// <summary>라인(엣지)에 설치된 건물을 지나갈 때 호출 — 이동 구간 중간(라인 위 건물 위치)에서 발동.</summary>
        public Action<global::Code.Buildings.Building> EdgeBuildingPassed { get; set; }
        /// <summary>파티(그룹) 스폰 시 멤버끼리 겹치지 않도록 노드 기준 위치에 더하는 대형 오프셋.</summary>
        public Vector3 FormationOffset { get; set; }
        public Vector3? InitialSpawnPosition { get; set; }
        public Node CurrentNode => _currentNode;
        public bool IsMoving => _isTurning;
        public NodeBattlefield CurrentBattlefield => _battleAgent != null ? _battleAgent.Battlefield : null;

        /// <summary>새 스폰 그룹에 붙일, 실행 중 유일한 파티 점유 식별자를 만든다.</summary>
        public static int CreatePartyOccupancyId()
        {
            return ++_nextPartyOccupancyId;
        }

        /// <summary>같은 그룹의 멤버가 일반 노드를 함께 점유하도록 식별자를 설정한다.</summary>
        public void ConfigurePartyOccupancy(int partyOccupancyId)
        {
            _partyOccupancyId = partyOccupancyId > 0
                ? partyOccupancyId
                : CreatePartyOccupancyId();
        }

        /// <summary>막힌 일반 노드 앞에서 우회 탐색 전까지 기다릴 이동 턴 수를 설정한다.</summary>
        public void ConfigureBlockedNodeWaitTurns(int turns)
        {
            _blockedNodeWaitTurns = Mathf.Max(1, turns);
        }

        public void Initialize(Node startNode)
        {
            CacheVisualPose();
            _battleAgent ??= GetComponent<BattleAgent>();

            if (_currentNode?.Data != null)
                VacateNode(_currentNode, PartyOccupancyId);

            _currentNode = startNode;
            _visitedNodes.Clear();
            _blockedNodeWaitCount = 0;

            if (_currentNode == null)
                return;

            _visitedNodes.Add(_currentNode.Data.Id);
            OccupyNode(_currentNode, PartyOccupancyId);
            transform.position = InitialSpawnPosition ?? GetEnemyPosition(_currentNode);
            InitialSpawnPosition = null;
            TryEnterBattlefield(_currentNode);
        }

        public void TakeTurn()
        {
            if (_isTurning || _currentNode == null)
                return;

            StartCoroutine(DoTurn());
        }

        public void StopMoving()
        {
            StopAllCoroutines();
            _moveTween?.Kill();
            _moveTween = null;
            _isTurning = false;
            _battleAgent?.EndTraversal();
            ResetVisualPose();
        }

        /// <summary>
        /// 방문을 마친 모험가를 현재 방에서 입구 방까지 되짚어 보낸 뒤 실제 출입문으로 이동시킨다.
        /// 돌아가는 동안에는 시설·함정·전투를 다시 처리하지 않는다.
        /// </summary>
        public void ReturnToExit(Node exitNode, Vector3 exitPosition, Action onComplete)
        {
            StopMoving();
            StartCoroutine(ReturnToExitRoutine(exitNode, exitPosition, onComplete));
        }

        private IEnumerator ReturnToExitRoutine(Node exitNode, Vector3 exitPosition, Action onComplete)
        {
            _isTurning = true;
            CurrentBattlefield?.Leave(_battleAgent);

            while (_currentNode != null && exitNode != null && _currentNode != exitNode)
            {
                var path = NodePathfinder.FindPath(_currentNode, exitNode, node => node.IsPassBlocked);
                if (path == null || path.Count < 2)
                    break;

                var nextNode = path[1];
                if (_currentNode.Data != null)
                    VacateNode(_currentNode, PartyOccupancyId);
                if (nextNode.Data != null)
                    OccupyNode(nextNode, PartyOccupancyId);
                _currentNode = nextNode;
                yield return SmoothMove();
            }

            if (_currentNode?.Data != null)
                VacateNode(_currentNode, PartyOccupancyId);
            _currentNode = null;

            var distance = Vector3.Distance(transform.position, exitPosition);
            var duration = Mathf.Clamp(distance / Mathf.Max(moveSpeed, 0.1f), minMoveDuration, maxMoveDuration);
            _moveTween = transform.DOMove(exitPosition, duration)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject);
            yield return _moveTween.WaitForCompletion();
            _moveTween = null;
            _isTurning = false;
            onComplete?.Invoke();
        }

        private IEnumerator DoTurn()
        {
            _isTurning = true;

            // 다음 방으로 넘어가기 전에 지금 방을 한 번 어슬렁거린다. 곧장 옆으로 직진하면
            // 침입자가 아니라 레일 위를 도는 말처럼 보인다.
            yield return WanderInsideNode();

            var nextNode = SelectNextNode();
            if (nextNode == null)
            {
                _isTurning = false;
                yield break;
            }

            var previousBattlefield = CurrentBattlefield;
            var nextBattlefield = nextNode.GetComponent<NodeBattlefield>();
            previousBattlefield?.Leave(_battleAgent);
            if (nextBattlefield != null && _battleAgent != null && !nextBattlefield.TryEnter(_battleAgent))
            {
                previousBattlefield?.TryEnter(_battleAgent);
                _isTurning = false;
                yield break;
            }

            _battleAgent?.BeginTraversal();

            var previousNodeId = _currentNode.Data.Id;
            VacateNode(_currentNode, PartyOccupancyId);
            OccupyNode(nextNode, PartyOccupancyId);

            _currentNode = nextNode;
            _visitedNodes.Add(_currentNode.Data.Id);

            // 이 이동 구간의 라인(엣지)에 건물이 있으면 이동 중간에 통과 효과를 발동한다.
            var edge = EdgeLine.FindBetween(previousNodeId, _currentNode.Data.Id);
            var edgeBuilding = edge != null ? edge.InstalledBuilding : null;

            yield return SmoothMove(edgeBuilding);

            _battleAgent?.EndTraversal();
            _isTurning = false;
            NodeArrived?.Invoke(_currentNode);
        }

        [Header("Node Wander")]
        [SerializeField, Range(0f, 1f), Tooltip("다음 방으로 가기 전 방 안을 둘러볼 확률.")]
        private float wanderChance = 0.55f;

        [SerializeField, Min(0f), Tooltip("둘러보는 거리(월드 단위). 아레나 반지름 안으로 잘린다.")]
        private float wanderDistance = 1.1f;

        [SerializeField, Min(0.05f), Tooltip("둘러보는 데 걸리는 시간(초).")]
        private float wanderDuration = 0.45f;

        [SerializeField, Min(0),
         Tooltip("최단 경로보다 이만큼까지 돌아가는 길도 후보로 삼는다. 0이면 늘 최단 경로만 탄다.")]
        private int detourTolerance = 2;

        /// <summary>
        /// 방 안에서 한 걸음 어슬렁거린다.
        ///
        /// 턴 간격 안에서 끝나야 한다 — 이동보다 오래 끌면 침입 속도가 느려져 밸런스가 바뀐다.
        /// 그래서 짧고, 늘 하지도 않는다.
        ///
        /// 싸움이 붙은 방에서는 건너뛴다. 전투 중에 배회하면 전투 연출과 겹쳐 어지럽고,
        /// 어차피 그 자리에 붙들려 있어야 맞다.
        /// </summary>
        private IEnumerator WanderInsideNode()
        {
            if (_currentNode == null || wanderChance <= 0f || UnityEngine.Random.value > wanderChance)
                yield break;

            var battlefield = CurrentBattlefield;
            if (battlefield != null && battlefield.PlayerCount > 0)
                yield break;

            var anchor = GetEnemyPosition(_currentNode);
            var radius = battlefield != null && battlefield.ArenaRadius > 0f
                ? battlefield.ArenaRadius * 0.6f
                : wanderDistance;

            var offset = UnityEngine.Random.insideUnitCircle.normalized * Mathf.Min(wanderDistance, radius);
            var target = anchor + new Vector3(offset.x, offset.y, 0f);

            FaceMoveDirection(target - transform.position);

            _moveTween?.Kill();
            _moveTween = transform.DOMove(target, wanderDuration)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject);

            yield return _moveTween.WaitForCompletion();
            _moveTween = null;
        }

        private IEnumerator SmoothMove(global::Code.Buildings.Building edgeBuilding = null)
        {
            var targetPos = GetEnemyPosition(_currentNode);
            var distance = Vector3.Distance(transform.position, targetPos);
            var effectiveSpeed = moveSpeed * Mathf.Clamp(FatigueSpeedMultiplier, 0.1f, 1f);
            var duration = Mathf.Clamp(distance / Mathf.Max(effectiveSpeed, 0.1f), minMoveDuration, maxMoveDuration);
            var direction = targetPos - transform.position;
            FaceMoveDirection(direction);

            _moveTween?.Kill();
            ResetVisualPose();

            // 이동 중에 죽는 적이 흔하다. 시퀀스를 오브젝트에 묶지 않으면 파괴된 뒤에도
            // 트윈이 계속 돌며 사라진 Transform 의 position 을 건드려 예외가 난다.
            var sequence = DOTween.Sequence().SetLink(gameObject);
            sequence.Join(transform.DOMove(targetPos, duration).SetEase(Ease.InOutQuad));

            // 라인 위 건물(중점) 통과 시점(구간의 절반)에 효과 발동.
            if (edgeBuilding != null)
            {
                var passedBuilding = edgeBuilding;
                sequence.InsertCallback(duration * 0.5f, () => EdgeBuildingPassed?.Invoke(passedBuilding));
            }

            if (visual != null && visualHopHeight > 0f)
            {
                sequence.Join(visual.DOLocalMoveY(_visualStartLocalPosition.y + visualHopHeight, duration * 0.5f)
                    .SetEase(Ease.OutSine)
                    .SetLoops(2, LoopType.Yoyo));
            }

            if (visual != null && visualSquashAmount > 0f)
            {
                var squashScale = new Vector3(
                    _visualStartLocalScale.x * (1f + visualSquashAmount),
                    _visualStartLocalScale.y * (1f - visualSquashAmount),
                    _visualStartLocalScale.z);
                var stretchScale = new Vector3(
                    _visualStartLocalScale.x * (1f - visualSquashAmount * 0.45f),
                    _visualStartLocalScale.y * (1f + visualSquashAmount * 0.45f),
                    _visualStartLocalScale.z);

                sequence.Insert(0f, visual.DOScale(squashScale, duration * 0.2f).SetEase(Ease.OutSine));
                sequence.Insert(duration * 0.2f, visual.DOScale(stretchScale, duration * 0.2f).SetEase(Ease.InOutSine));
                sequence.Insert(duration * 0.42f, visual.DOScale(_visualStartLocalScale, duration * 0.28f).SetEase(Ease.OutBack));
            }

            if (visual != null && visualLeanAngle > 0f)
            {
                var leanDirection = Mathf.Abs(direction.x) > 0.001f
                    ? -Mathf.Sign(direction.x)
                    : Mathf.Sign(direction.y);
                var leanRotation = new Vector3(0f, 0f, visualLeanAngle * leanDirection);

                sequence.Join(visual.DOLocalRotate(leanRotation, duration * 0.25f).SetEase(Ease.OutSine));
                sequence.Insert(duration * 0.55f, visual.DOLocalRotate(Vector3.zero, duration * 0.35f).SetEase(Ease.OutQuad));
            }

            sequence.OnKill(ResetVisualPose);
            sequence.OnComplete(ResetVisualPose);
            _moveTween = sequence
                .SetLink(gameObject);

            yield return _moveTween.WaitForCompletion();
        }

        private Node SelectNextNode()
        {
            if (_currentNode?.Data == null)
                return null;

            // 1순위: A*로 목적지까지의 최단 경로를 따라간다(벽 회피).
            // 목적지는 저마다 다르다 — 보물을 노린 자는 금고로, 볼일이 있는 모험가는 그 시설로.
            var pathStep = SelectNextNodeByPathfinding(out var waitForPath);
            if (pathStep != null)
            {
                _blockedNodeWaitCount = 0;
                return pathStep;
            }

            // 경로는 있는데 다음 칸이 붐비면 먼저 줄을 선다. 성격·예산별 한도를 넘기면
            // 아래의 랜덤 배회 폴백으로 넘어가 다른 열린 길을 찾는다.
            if (waitForPath)
            {
                _blockedNodeWaitCount++;
                if (_blockedNodeWaitCount <= _blockedNodeWaitTurns)
                    return null;

                _blockedNodeWaitCount = 0;
            }
            else
            {
                _blockedNodeWaitCount = 0;
            }

            // 폴백: 목적지가 없거나(볼일을 마친 모험가) 경로가 완전히 막힌 경우 랜덤 배회(벽 노드는 제외).
            var unvisitedFree = new List<Node>();
            var visitedFree = new List<Node>();

            foreach (var id in _currentNode.Data.ConnectedNodeIds)
            {
                var node = ResolveNodeByDataId(id);
                if (node == null || node.IsPassBlocked)
                    continue;

                if (IsNodeOccupied(node, PartyOccupancyId))
                    continue;

                var battlefield = node.GetComponent<NodeBattlefield>();
                if (battlefield != null && _battleAgent != null && !battlefield.CanEnter(_battleAgent.Team))
                    continue;

                if (!_visitedNodes.Contains(id))
                    unvisitedFree.Add(node);
                else
                    visitedFree.Add(node);
            }

            if (unvisitedFree.Count > 0)
                return unvisitedFree[UnityEngine.Random.Range(0, unvisitedFree.Count)];

            if (visitedFree.Count > 0)
                return visitedFree[UnityEngine.Random.Range(0, visitedFree.Count)];

            return null;
        }

        /// <summary>A*: <see cref="RouteGoalResolver"/>가 정한 목적지로 가는 최단 경로의 다음 노드.
        /// 경로 자체가 없으면 null + shouldWait=false(랜덤 배회 폴백),
        /// 경로는 있는데 다음 칸이 점유/정원 초과면 null + shouldWait=true(이번 턴 대기).</summary>
        private Node SelectNextNodeByPathfinding(out bool shouldWait)
        {
            shouldWait = false;

            var goal = RouteGoalResolver != null
                ? RouteGoalResolver()
                : IntrusionThreat.FindPriorityTarget(transform.position, out _);
            if (goal == null || goal == _currentNode)
                return null;

            var path = NodePathfinder.FindPath(_currentNode, goal, node => node.IsPassBlocked);
            if (path == null || path.Count < 2)
                return null;

            var candidates = CollectRouteCandidates(goal, path.Count);
            if (candidates.Count == 0)
            {
                // 길은 있는데 갈 수 있는 다음 칸이 없다 → 딴 길로 새지 않고 이번 턴은 줄을 선다.
                shouldWait = true;
                return null;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// 갈 만한 다음 칸을 모은다. 최단만 고집하지 않고 조금 돌아가는 길까지 넣는다.
        ///
        /// 예전에는 A*가 내놓은 경로의 두 번째 칸을 그대로 썼다. 최단 경로는 하나로 정해지므로
        /// 모든 침입자가 같은 줄을 따라 지도 한가운데로만 내려왔다. 비슷한 길이의 길을 모아 두고
        /// 그중 하나를 고르면, 목적지는 그대로면서 지나는 길이 판마다 달라진다.
        ///
        /// <see cref="detourTolerance"/>만큼만 봐준다. 이걸 크게 열면 헤매는 것이 아니라
        /// 길을 잃은 것처럼 보이고, 금고에 닿는 시간이 들쭉날쭉해져 밸런스가 흔들린다.
        /// </summary>
        private List<Node> CollectRouteCandidates(Node goal, int bestPathLength)
        {
            _routeCandidates.Clear();
            if (_currentNode?.Data == null)
                return _routeCandidates;

            foreach (var id in _currentNode.Data.ConnectedNodeIds)
            {
                var node = ResolveNodeByDataId(id);
                if (node == null || node.IsPassBlocked || IsNodeOccupied(node, PartyOccupancyId))
                    continue;

                var battlefield = node.GetComponent<NodeBattlefield>();
                if (battlefield != null && _battleAgent != null && !battlefield.CanEnter(_battleAgent.Team))
                    continue;

                if (node == goal)
                {
                    _routeCandidates.Add(node);
                    continue;
                }

                var rest = NodePathfinder.FindPath(node, goal, n => n.IsPassBlocked);
                if (rest == null || rest.Count < 1)
                    continue;

                if (1 + rest.Count > bestPathLength + detourTolerance)
                    continue;

                _routeCandidates.Add(node);
            }

            return _routeCandidates;
        }

        private readonly List<Node> _routeCandidates = new();

        private Node ResolveNodeByDataId(string dataId)
        {
            foreach (var node in Node.ActiveNodes)
            {
                if (node != null && node.Data != null && node.Data.Id == dataId)
                    return node;
            }

            return null;
        }

        private int PartyOccupancyId
        {
            get
            {
                if (_partyOccupancyId <= 0)
                    _partyOccupancyId = CreatePartyOccupancyId();

                return _partyOccupancyId;
            }
        }

        private static bool IsNodeOccupied(Node node, int partyOccupancyId)
        {
            if (node == null || node.HasInstalledBuilding || node.Data == null)
                return false;

            return IsNodeOccupied(node.Data.Id, partyOccupancyId);
        }

        private static bool IsNodeOccupied(string nodeId, int partyOccupancyId)
        {
            return !string.IsNullOrEmpty(nodeId)
                   && _occupiedNodeCounts.TryGetValue(nodeId, out var parties)
                   && parties.Count > 0
                   && !parties.ContainsKey(partyOccupancyId);
        }

        private static void OccupyNode(string nodeId, int partyOccupancyId)
        {
            if (string.IsNullOrEmpty(nodeId) || partyOccupancyId <= 0)
                return;

            if (!_occupiedNodeCounts.TryGetValue(nodeId, out var parties))
            {
                parties = new Dictionary<int, int>();
                _occupiedNodeCounts.Add(nodeId, parties);
            }

            parties.TryGetValue(partyOccupancyId, out var memberCount);
            parties[partyOccupancyId] = memberCount + 1;
        }

        private static void OccupyNode(Node node, int partyOccupancyId)
        {
            if (node == null || node.HasInstalledBuilding || node.Data == null)
                return;

            OccupyNode(node.Data.Id, partyOccupancyId);
        }

        private static void VacateNode(string nodeId, int partyOccupancyId)
        {
            if (string.IsNullOrEmpty(nodeId)
                || partyOccupancyId <= 0
                || !_occupiedNodeCounts.TryGetValue(nodeId, out var parties)
                || !parties.TryGetValue(partyOccupancyId, out var memberCount))
                return;

            if (memberCount <= 1)
                parties.Remove(partyOccupancyId);
            else
                parties[partyOccupancyId] = memberCount - 1;

            if (parties.Count == 0)
                _occupiedNodeCounts.Remove(nodeId);
        }

        private static void VacateNode(Node node, int partyOccupancyId)
        {
            if (node == null || node.HasInstalledBuilding || node.Data == null)
                return;

            VacateNode(node.Data.Id, partyOccupancyId);
        }

        private Vector3 GetEnemyPosition(Node node)
        {
            var basePosition = node.EnemyPosition != null
                ? node.EnemyPosition.position
                : node.transform.position;
            return basePosition + FormationOffset + ResolveNodeScatter(node);
        }

        /// <summary>
        /// 이 침입자가 이 방에서 설 자리. 방마다 다르게, 그러나 같은 방 안에서는 늘 같게.
        ///
        /// <see cref="FormationOffset"/>은 스폰할 때 한 번 정해지고 죽을 때까지 바뀌지 않는다.
        /// 그래서 모든 침입자가 방마다 똑같은 상대 위치에 서고, 결과적으로 지도 한가운데를
        /// 일렬로 지나가는 것처럼 보였다. 방이 바뀔 때마다 자리를 새로 뽑아 흩어 놓는다.
        ///
        /// 매번 새로 뽑으면 안 된다 — 이동 목표를 정할 때와 도착한 뒤에 다른 자리가 나오면
        /// 침입자가 제자리에서 떠는 것처럼 보인다. 그래서 방이 바뀔 때만 다시 뽑아 들고 있는다.
        /// </summary>
        private Vector3 ResolveNodeScatter(Node node)
        {
            var nodeId = node != null && node.Data != null ? node.Data.Id : null;
            if (string.IsNullOrEmpty(nodeId))
                return Vector3.zero;

            if (nodeId == _scatterNodeId)
                return _nodeScatter;

            var battlefield = node.GetComponent<NodeBattlefield>();
            var radius = battlefield != null && battlefield.ArenaRadius > 0f
                ? battlefield.ArenaRadius * 0.55f
                : wanderDistance;

            var offset = UnityEngine.Random.insideUnitCircle * radius;
            _nodeScatter = new Vector3(offset.x, offset.y, 0f);
            _scatterNodeId = nodeId;
            return _nodeScatter;
        }

        private string _scatterNodeId;
        private Vector3 _nodeScatter;

        private void TryEnterBattlefield(Node node)
        {
            if (node == null || _battleAgent == null)
                return;

            node.GetComponent<NodeBattlefield>()?.TryEnter(_battleAgent);
        }

        private void CacheVisualPose()
        {
            if (visual == null)
                return;

            _visualStartLocalPosition = visual.localPosition;
            _visualStartLocalScale = visual.localScale;
            _visualStartLocalEulerAngles = visual.localEulerAngles;
        }

        private void FaceMoveDirection(Vector3 direction)
        {
            if (visual == null || Mathf.Abs(direction.x) <= 0.001f)
                return;

            var scale = _visualStartLocalScale;
            scale.x = Mathf.Abs(scale.x) * (direction.x < 0f ? -1f : 1f);
            visual.localScale = scale;
            _visualStartLocalScale = scale;
        }

        private void ResetVisualPose()
        {
            if (visual == null)
                return;

            visual.localPosition = _visualStartLocalPosition;
            visual.localScale = _visualStartLocalScale;
            visual.localEulerAngles = _visualStartLocalEulerAngles;
        }

        private void OnDestroy()
        {
            _activeEnemies.Remove(this);

            if (_currentNode?.Data != null)
                VacateNode(_currentNode, PartyOccupancyId);

            CurrentBattlefield?.Leave(_battleAgent);

            _moveTween?.Kill();
        }

        private void OnEnable()
        {
            if (!_activeEnemies.Contains(this))
                _activeEnemies.Add(this);
        }

        private void OnDisable()
        {
            _activeEnemies.Remove(this);
        }
    }
}
