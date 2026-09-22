using Code.BT;
using Code.Entities;
using UnityEngine;

namespace Code.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Defence/Enemy")]
    public class EnemyDataSO : EntityDataSO
    {
        [field: SerializeField]
        public string Name { get; private set; } = "Enemy";

        [field: SerializeField, Tooltip("전투 역할. 전열(Tank/Melee)/후열(Ranged)/지원(Support).")]
        public BattleRole Role { get; private set; } = BattleRole.Melee;

        [field: SerializeField, Tooltip("시설 이용과 공포 반응을 바꾸는 개인 특성.")]
        public AdventurerTrait Trait { get; private set; }

        [field: SerializeField, Tooltip("이 적 종류의 전용 프리팹. 비어 있으면 WaveManager의 기본 프리팹 사용.")]
        public Enemy Prefab { get; private set; }

        [field: SerializeField, Min(0)]
        public int Fear { get; private set; }

        [field: SerializeField, Min(0)]
        public int Greed { get; private set; }

        [field: SerializeField, Min(1)]
        public int MaxHealth { get; private set; } = 10;

        [field: SerializeField, Min(1)]
        public int AttackDamage { get; private set; } = 1;

        [field: SerializeField, Min(0.05f)]
        public float AttackInterval { get; private set; } = 1f;

        [field: SerializeField]
        public Sprite IdleSprite { get; private set; }

        [field: SerializeField]
        public Sprite AttackSprite { get; private set; }

        [field: SerializeField]
        public Sprite DefeatedSprite { get; private set; }

        [field: SerializeField, Tooltip("이 적이 때릴 때 나는 소리. 열두 종이 프리팹 넷을 나눠 쓰기 때문에" +
            " 프리팹에만 두면 관광객도 검을 휘두르는 소리를 낸다.")]
        public global::Code.Audio.GameSfxCue AttackSfx { get; private set; } = global::Code.Audio.GameSfxCue.Attack;

        [field: SerializeField, Tooltip("밟는 대신 함정을 뜯어낸다. 공병이 이걸로 산다 —" +
            " 함정을 깔아 둔 길이 그냥 뚫리므로 도착하기 전에 잡아야 한다.")]
        public bool DisarmsTraps { get; private set; }
    }
}
