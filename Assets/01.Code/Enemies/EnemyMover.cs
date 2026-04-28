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

        private static readonly HashSet<string> _occupiedNodes = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOccupied() => _occupiedNodes.Clear();

        private readonly HashSet<string> _visitedNodes = new();
        private Node _currentNode;
        private Tween _moveTween;
        private bool _isTurning;

        public Func<Node, bool> NodeArrived { get; set; }
        public Node CurrentNode => _currentNode;

        public void Initialize(Node startNode)
        {
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
            var duration = distance / Mathf.Max(moveSpeed, 0.1f);

            _moveTween?.Kill();
            _moveTween = transform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear)
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

        private void OnDestroy()
        {
            if (_currentNode?.Data != null)
                _occupiedNodes.Remove(_currentNode.Data.Id);

            _moveTween?.Kill();
        }
    }
}
