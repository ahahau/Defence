using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class EdgeLine : MonoBehaviour
    {
        [field: SerializeField]
        public LineRenderer LineRenderer { get; private set; }

        public void Initialize(string objectName, Vector3 start, Vector3 end)
        {
            name = objectName;

            LineRenderer.positionCount = 2;
            LineRenderer.SetPosition(0, start);
            LineRenderer.SetPosition(1, end);
        }
    }
}
