using UnityEngine;

namespace _01.Code.Test
{
    public class TestLine : MonoBehaviour
    {
        public int size = 30;         
        public float cellSize = 1f;   
        public Material lineMat; 

        void Start()
        {
            DrawGrid();
        }

        void DrawGrid()
        {
            float totalSize = size * cellSize;
            float halfCell = cellSize / 2f;

            for (int i = -size; i <= size; i++)
            {
                float offset = i * cellSize - halfCell;

                Vector3 startV = new Vector3(offset, -totalSize - halfCell, 0f);
                Vector3 endV = new Vector3(offset, totalSize - halfCell, 0f);
                CreateLine(startV, endV);
                Vector3 startH = new Vector3(-totalSize - halfCell, offset, 0f);
                Vector3 endH = new Vector3(totalSize - halfCell, offset, 0f);
                CreateLine(startH, endH);
            }
        }

        void CreateLine(Vector3 start, Vector3 end)
        {
            GameObject lineObj = new GameObject("GridLine");
            lineObj.transform.parent = this.transform;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = lineMat;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = 0.03f;
            lr.endWidth = 0.03f;
            lr.useWorldSpace = true;
            lr.sortingOrder = 1000;
        }
    }
}