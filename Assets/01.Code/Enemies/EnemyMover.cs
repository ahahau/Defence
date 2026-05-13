using _01.Code.MapCreateSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Scripting;

namespace _01.Code.Enemies
{
    public class EnemyMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField, Min(0.01f)] private float minMoveDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float maxMoveDuration = 0.75f;
        [SerializeField, Min(0f)] private float visualHopHeight = 0.12f;
        [SerializeField, Min(0f)] private float visualSquashAmount = 0.08f;
        [SerializeField] private Transform visual;

        private static readonly HashSet<string> _occupiedNodes = new();
        private static readonly List<EnemyMover> _activeEnemies = new();
        public static IReadOnlyList<EnemyMover> ActiveEnemies => _activeEnemies;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOccupied()
        {
            _occupiedNodes.Clear();
            _activeEnemies.Clear();
        }

        private readonly HashSet<string> _visitedNodes = new();
        private Node _currentNode;
        private Tween _moveTween;
        private Vector3 _visualStartLocalPosition;
        private Vector3 _visualStartLocalScale;
        private bool _isTurning;

        public Func<Node, bool> NodeArrived { get; set; }
        public Node CurrentNode => _currentNode;

        public void Initialize(Node startNode)
        {
            CacheVisualPose();
            _currentNode = startNode;
            _visitedNodes.Clear();

            if (_currentNode == null)
                return;

            _visitedNodes.Add(_currentNode.Data.Id);
            _occupiedNodes.Add(_currentNode.Data.Id);
            transform.position = GetEnemyPosition(_currentNode);
        }

        public void TakeTurn()
        {
            if (_isTurning || _currentNode == null)
                return;

            StartCoroutine(DoTurn());
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

            _occupiedNodes.Remove(_currentNode.Data.Id);
            _occupiedNodes.Add(nextNode.Data.Id);

            _currentNode = nextNode;
            _visitedNodes.Add(_currentNode.Data.Id);

            yield return SmoothMove();

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
            sequence.Join(transform.DOMove(targetPos, duration).SetEase(Ease.InOutSine));

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

                sequence.Join(visual.DOScale(squashScale, duration * 0.18f)
                    .SetEase(Ease.OutSine)
                    .SetLoops(2, LoopType.Yoyo));
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
                if (!Node.TryGetByDataId(id, out var node))
                    continue;

                if (_occupiedNodes.Contains(id))
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

        private static Vector3 GetEnemyPosition(Node node)
        {
            return node.EnemyPosition != null
                ? node.EnemyPosition.position
                : node.transform.position;
        }

        private void CacheVisualPose()
        {
            if (visual == null)
                return;

            _visualStartLocalPosition = visual.localPosition;
            _visualStartLocalScale = visual.localScale;
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
        }

        private void OnDestroy()
        {
            _activeEnemies.Remove(this);

            if (_currentNode?.Data != null)
                _occupiedNodes.Remove(_currentNode.Data.Id);

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
