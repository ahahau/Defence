using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(menuName = "SO/Building/Data", fileName = "BuildingData", order = 0)]
    public class BuildingDataSO : EntityDataSO
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public int Cost { get; private set; }
        [field: SerializeField, Min(0), Tooltip("매일 정산에서 지불하는 시설 운영비. 0이면 운영비와 인접 수익 보너스가 없는 시설이다.")]
        public int DailyUpkeep { get; private set; }
        // 예전 이름은 Fame이었다. 직렬화 이름이 바뀌면 지어 둔 건물 스무 개의 값이 조용히
        // 0이 되고, 그러면 등급이 통째로 주저앉는데 에러는 하나도 나지 않는다.
        [field: SerializeField, Min(0),
                UnityEngine.Serialization.FormerlySerializedAs("<Fame>k__BackingField"),
                Tooltip("이 건물이 던전 등급에 더하는 무게. 클수록 더 많고 센 모험가를 부른다. 0이면 밖에서 보이지 않는 건물이다.")]
        public int GradeWeight { get; private set; }
        [field: SerializeField, Min(0f), Tooltip("모험가가 이 시설에 머무는 시간(초). 머무는 동안 돈을 쓰고 지친다. 0이면 그냥 지나간다.")]
        public float DwellSeconds { get; private set; }
        [field: SerializeField] public Building Prefab { get; private set; }
        [field: SerializeField] public bool Unique { get; private set; }
        [field: SerializeField, Tooltip("중요 시설을 방 중앙 슬롯에만 설치한다.")]
        public bool CentralOnly { get; private set; }
        [field: SerializeField, Tooltip("켜면 노드가 아니라 노드 사이 라인(엣지)에 설치된다. 적이 라인을 지나갈 때 발동 — 상점/여관은 통과 효과, 함정은 피해. 노드 칸은 유닛과 자리를 다투므로 함정에 자기 자리를 주는 용도이기도 하다.")]
        public bool InstallOnEdge { get; private set; }
        [field: SerializeField] public bool Locked { get; private set; } = true;
        [field: SerializeField, Min(0)] public int BaseDanger { get; private set; } = 1;
        [field: SerializeField] public InstallCategory Category { get; private set; } = InstallCategory.Building;
    }
}
