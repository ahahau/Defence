using UnityEngine;

namespace _01.Code.Test
{
    public class TestLine : MonoBehaviour
    {
        public int size = 30;           // 셀 개수: -30 ~ +30
        public float cellSize = 1f;     // 셀 한 칸 크기
        public Material lineMaterial;   // 라인 머티리얼

        void Start()
        {
            DrawGridWithOffset();
        }

        void DrawGridWithOffset()
        {
            float totalSize = size * cellSize;
            float halfCell = cellSize / 2f;

            for (int i = -size; i <= size; i++)
            {
                float offset = i * cellSize - halfCell;

                // 수직선 (Y 방향)
                Vector3 startV = new Vector3(offset, -totalSize - halfCell, 0f);
                Vector3 endV = new Vector3(offset, totalSize - halfCell, 0f);
                CreateLine(startV, endV);

                // 수평선 (X 방향)
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
            lr.material = lineMaterial;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.useWorldSpace = true;
            lr.sortingOrder = 1000;
        }
    }
}