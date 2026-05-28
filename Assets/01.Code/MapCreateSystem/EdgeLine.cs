using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class EdgeLine : MonoBehaviour
    {
        [SerializeField]
        private LineRenderer lineRenderer;

        public void Initialize(string objectName, Vector3 start, Vector3 end)
        {
            name = objectName;

            if (lineRenderer == null)
            {
                Debug.LogError($"{nameof(EdgeLine)} requires a line renderer.", this);
                return;
            }

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }
}
