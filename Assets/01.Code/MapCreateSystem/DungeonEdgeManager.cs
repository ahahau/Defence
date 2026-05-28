using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class DungeonEdgeManager : MonoBehaviour
    {
        [SerializeField]
        private EdgeLine edgeLinePrefab;

        [SerializeField]
        private float gridSpacing = 2.4f;

        [SerializeField]
        private float nodeSize = 1f;

        
        public void ClearAll()
        {
            ClearRootChildren(transform);
        }
 
        public void CreateEdge(Vector2Int from, Vector2Int to)
        {
            if (edgeLinePrefab == null)
            {
                Debug.LogError("DungeonEdgeManager requires an edge line prefab.", this);
                return;
            }

            var edgeLine = Instantiate(edgeLinePrefab);
            edgeLine.transform.SetParent(transform);

            var start = ToWorld(from);
            var end = ToWorld(to);
            var direction = (end - start).normalized;
            var nodeRadius = nodeSize * 0.5f;

            edgeLine.Initialize($"Edge_{from}_{to}", start + direction * nodeRadius, end - direction * nodeRadius);
        }
        private Vector3 ToWorld(Vector2Int gridPosition)
        {
            return new Vector3(gridPosition.x * gridSpacing, gridPosition.y * gridSpacing, 0f);
        }

        private void ClearRootChildren(Transform root)
        {
            if (root == null)
                return;

            for (var i = root.childCount - 1; i >= 0; i--)
                HideAndDestroy(root.GetChild(i).gameObject);
        }

        private void HideAndDestroy(GameObject target)
        {
            if (target == null)
                return;

            target.SetActive(false);
            DestroyTarget(target);
        }

        private void DestroyTarget(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
