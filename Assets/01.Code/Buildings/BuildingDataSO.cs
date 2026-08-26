using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(menuName = "SO/Building/Data", fileName = "BuildingData", order = 0)]
    public class BuildingDataSO : EntityDataSO
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public int Cost { get; private set; }
        [field: SerializeField] public Building Prefab { get; private set; }
        [field: SerializeField] public bool Unique { get; private set; }
        [field: SerializeField, Tooltip("켜면 노드가 아니라 노드 사이 라인(엣지)에 설치된다. 적이 라인을 지나갈 때 발동 — 상점/여관은 통과 효과, 함정은 피해. 노드 칸은 수비대와 자리를 다투므로 함정에 자기 자리를 주는 용도이기도 하다.")]
        public bool InstallOnEdge { get; private set; }
        [field: SerializeField] public bool Locked { get; private set; } = true;
        [field: SerializeField, Min(0)] public int BaseDanger { get; private set; } = 1;
        [field: SerializeField] public InstallCategory Category { get; private set; } = InstallCategory.Building;
    }
}
