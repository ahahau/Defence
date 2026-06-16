using _01.Code.MapCreateSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using _01.Code.BT;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField, Min(0.01f)] private float minMoveDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float maxMoveDuration = 0.75f;
        [SerializeField, Min(0f)] private float visualHopHeight = 0.12f;
        [SerializeField, Min(0f)] private float visualSquashAmount = 0.08f;
        [SerializeField, Min(0f)] private float visualLeanAngle = 7f;
        [SerializeField] private Transform visual;

        private static readonly Dictionary<string, int> _occupiedNodeCounts = new();
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

        public Func<Node, bool> NodeArrived { get; set; }
        public Node CurrentNode => _currentNode;
        public bool IsMoving => _isTurning;
        public NodeBattlefield CurrentBattlefield => _battleAgent != null ? _battleAgent.Battlefield : null;

        public void Initialize(Node startNode)
        {
            CacheVisualPose();
            _battleAgent ??= GetComponent<BattleAgent>();

            if (_currentNode?.Data != null)
                VacateNode(_currentNode.Data.Id);

            _currentNode = startNode;
            _visitedNodes.Clear();

            if (_currentNode == null)
                return;

            _visitedNodes.Add(_currentNode.Data.Id);
            OccupyNode(_currentNode.Data.Id);
            transform.position = GetEnemyPosition(_currentNode);
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

        private IEnumerator DoTurn()
        {
            _isTurning = true;

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

            VacateNode(_currentNode.Data.Id);
            OccupyNode(nextNode.Data.Id);

            _currentNode = nextNode;
            _visitedNodes.Add(_currentNode.Data.Id);

            yield return SmoothMove();

            _battleAgent?.EndTraversal();
            _isTurning = false;
            NodeArrived?.Invoke(_currentNode);
        }

        private IEnumerator SmoothMove()
        {
            var targetPos = GetEnemyPosition(_currentNode);
            var distance = Vector3.Distance(transform.position, targetPos);
            var duration = Mathf.Clamp(distance / Mathf.Max(moveSpeed, 0.1f), minMoveDuration, maxMoveDuration);
            var direction = targetPos - transform.position;
            FaceMoveDirection(direction);

            _moveTween?.Kill();
            ResetVisualPose();

            var sequence = DOTween.Sequence();
            sequence.Join(transform.DOMove(targetPos, duration).SetEase(Ease.InOutQuad));

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

            var unvisitedFree = new List<Node>();
            var visitedFree = new List<Node>();

            foreach (var id in _currentNode.Data.ConnectedNodeIds)
            {
                var node = ResolveNodeByDataId(id);
                if (node == null)
                    continue;

                if (IsNodeOccupied(id))
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

        private Node ResolveNodeByDataId(string dataId)
        {
            foreach (var node in Node.ActiveNodes)
            {
                if (node != null && node.Data != null && node.Data.Id == dataId)
                    return node;
            }

            return null;
        }

        private static bool IsNodeOccupied(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId)
                   && _occupiedNodeCounts.TryGetValue(nodeId, out var count)
                   && count > 0;
        }

        private static void OccupyNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;

            _occupiedNodeCounts.TryGetValue(nodeId, out var count);
            _occupiedNodeCounts[nodeId] = count + 1;
        }

        private static void VacateNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)
                || !_occupiedNodeCounts.TryGetValue(nodeId, out var count))
                return;

            if (count <= 1)
                _occupiedNodeCounts.Remove(nodeId);
            else
                _occupiedNodeCounts[nodeId] = count - 1;
        }

        private Vector3 GetEnemyPosition(Node node)
        {
            return node.EnemyPosition != null
                ? node.EnemyPosition.position
                : node.transform.position;
        }

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
                VacateNode(_currentNode.Data.Id);

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
