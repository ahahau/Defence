using _01.Code.Enemies;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>던전을 습격하는 모험가 파티 구성. 멤버를 등장 순서대로 정의한다(보통 탱커→딜러→힐러).
    /// WaveManager가 한 웨이브에 이 구성을 따라(순서 반복) 스폰해 역할이 섞인 그룹이 함께 쳐들어온다.</summary>
    [CreateAssetMenu(menuName = "SO/Wave/Adventurer Party", fileName = "AdventurerParty")]
    public class AdventurerPartySO : ScriptableObject
    {
        [field: SerializeField] public string PartyName { get; private set; } = "Adventurer Party";

        [field: SerializeField, Tooltip("등장 순서대로. 보통 전열(탱커) → 근접/원거리 딜러 → 힐러.")]
        public EnemyDataSO[] Members { get; private set; }
    }
}
