using _01.Code.MapCreateSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyMover : MonoBehaviour
    {
        private readonly HashSet<string> visitedNodeIds = new();
        private float turnInterval = 5f;
        private Node currentNode;
        private Coroutine moveRoutine;

        public Func<Node, bool> NodeArrived { get; set; }
        public Node CurrentNode => currentNode;
        public bool IsMoving => moveRoutine != null;

        public void Initialize(Node startNode, float interval)
        {
            StopMove();

            currentNode = startNode;
            turnInterval = interval;
            visitedNodeIds.Clear();

            if (currentNode == null)
                return;

            visitedNodeIds.Add(currentNode.Data.Id);
            MoveToCurrentNode();
        }

        public void StartMove()
        {
            if (moveRoutine != null || currentNode == null || !isActiveAndEnabled)
                return;

            moveRoutine = StartCoroutine(MoveByTurn());
        }

        public void StopMove()
        {
            if (moveRoutine != null)
                StopCoroutine(moveRoutine);

            moveRoutine = null;
        }

        private IEnumerator MoveByTurn()
        {
            while (currentNode != null)
            {
                yield return new WaitForSeconds(turnInterval);

                var nextNode = SelectNextUnvisitedNode();
                if (nextNode == null)
                    continue;

                currentNode = nextNode;
                visitedNodeIds.Add(currentNode.Data.Id);
                MoveToCurrentNode();

                if (NodeArrived != null && !NodeArrived.Invoke(currentNode))
                {
                    moveRoutine = null;
                    yield break;
                }
            }

            moveRoutine = null;
        }

        private void MoveToCurrentNode()
        {
            transform.position = currentNode.EnemyPosition.position;
        }

        private Node SelectNextUnvisitedNode()
        {
            if (currentNode == null || currentNode.Data == null)
                return null;

            var candidates = new List<Node>();
            foreach (var connectedNodeId in currentNode.Data.ConnectedNodeIds)
            {
                if (visitedNodeIds.Contains(connectedNodeId))
                    continue;

                if (Node.TryGetByDataId(connectedNodeId, out var connectedNode))
                    candidates.Add(connectedNode);
            }

            if (candidates.Count == 0)
                return null;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }
}
