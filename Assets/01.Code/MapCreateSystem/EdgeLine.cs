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

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }
}
