using System.Collections.Generic;
using _01.Code.Buildings;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    /// <summary>노드 사이 연결 라인. 비주얼(LineRenderer)에 더해 건물 설치 슬롯 하나를 가진다 —
    /// 상점/여관 같은 통과형 건물을 라인 위(중점)에 설치하면 적이 이 라인을 지나갈 때 효과가 발동한다.</summary>
    public class EdgeLine : MonoBehaviour
    {
        [SerializeField]
        private LineRenderer lineRenderer;

        private static readonly List<EdgeLine> _active = new();
        /// <summary>씬의 모든 활성 엣지(설치 미리보기·통과 판정용).</summary>
        public static IReadOnlyList<EdgeLine> ActiveEdges => _active;

        public string FromId { get; private set; }
        public string ToId { get; private set; }
        public Vector3 StartPoint { get; private set; }
        public Vector3 EndPoint { get; private set; }
        public Vector3 Midpoint => (StartPoint + EndPoint) * 0.5f;
        public Building InstalledBuilding { get; private set; }
        public bool HasBuilding => InstalledBuilding != null;

        public void Initialize(string objectName, Vector3 start, Vector3 end)
        {
            Initialize(objectName, start, end, null, null);
        }

        public void Initialize(string objectName, Vector3 start, Vector3 end, string fromId, string toId)
        {
            name = objectName;
            StartPoint = start;
            EndPoint = end;
            FromId = fromId;
            ToId = toId;

            if (lineRenderer == null)
            {
                Debug.LogError($"{nameof(EdgeLine)} requires a line renderer.", this);
                return;
            }

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        private void OnEnable()
        {
            if (!_active.Contains(this))
                _active.Add(this);
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        /// <summary>두 노드를 잇는 엣지를 찾는다(방향 무관). 없으면 null.</summary>
        public static EdgeLine FindBetween(string nodeIdA, string nodeIdB)
        {
            if (string.IsNullOrEmpty(nodeIdA) || string.IsNullOrEmpty(nodeIdB))
                return null;

            foreach (var edge in _active)
            {
                if (edge == null || string.IsNullOrEmpty(edge.FromId))
                    continue;

                if ((edge.FromId == nodeIdA && edge.ToId == nodeIdB)
                    || (edge.FromId == nodeIdB && edge.ToId == nodeIdA))
                    return edge;
            }

            return null;
        }

        /// <summary>라인 중점에 건물을 설치한다(슬롯이 비어 있을 때만). 성공 시 인스턴스 반환.</summary>
        public Building TryInstall(Building buildingPrefab)
        {
            if (buildingPrefab == null || HasBuilding)
                return null;

            InstalledBuilding = Instantiate(buildingPrefab, Midpoint, Quaternion.identity);
            InstalledBuilding.transform.SetParent(transform, true);
            return InstalledBuilding;
        }

        /// <summary>점과 이 라인 사이의 최단 거리(설치 하이라이트 판정용).</summary>
        public float DistanceTo(Vector2 point)
        {
            Vector2 a = StartPoint;
            Vector2 b = EndPoint;
            var ab = b - a;
            if (ab.sqrMagnitude < 0.0001f)
                return Vector2.Distance(a, point);

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(a + ab * t, point);
        }
    }
}
