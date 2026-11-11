using Code.Enemies;
using Code.Entities;
using Code.Skills;
using Code.Units;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Run
{
    /// <summary>
    /// 던전 FTL 실시간 전투의 부하·적 공용 데이터. 이름은 지금 부하·적 데이터에서 가져오고,
    /// 전투 값은 여기만 둔다(예전 데이터는 함정 본편이 그대로 쓰므로 건드리지 않는다).
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Run/Combatant", fileName = "RunCombatant")]
    public class RunCombatantSO : ScriptableObject
    {
        [field: SerializeField, Tooltip("이름·그림을 가져올 부하 데이터. 적이면 비운다.")]
        public UnitDataSO SourceUnit { get; private set; }

        [field: SerializeField, Tooltip("이름·그림을 가져올 적 데이터. 부하면 비운다.")]
        public EnemyDataSO SourceEnemy { get; private set; }

        [field: SerializeField] public RunSide Side { get; private set; }
        [field: SerializeField] public RunAffinity Affinity { get; private set; }
        [field: SerializeField, Min(1)] public int MaxHealth { get; private set; } = 10;
        [field: SerializeField, Min(0)] public int Attack { get; private set; } = 1;

        [field: SerializeField, Min(1), Tooltip("공격 간격(초) = 전투 설정의 AttackIntervalFactor ÷ 속도.")]
        public int Speed { get; private set; } = 1;

        [field: SerializeField, Min(0.1f), Tooltip("걷는 속도(씬 단위/초).")]
        public float MoveSpeed { get; private set; } = 1.2f;

        [field: SerializeField, Tooltip("켜면 원거리 공격 거리, 끄면 근접 공격 거리로 친다.")]
        public bool IsRanged { get; private set; }

        [field: SerializeField, Min(0f), Tooltip("부하가 1초에 방 내구를 되살리는 양.")]
        [field: FormerlySerializedAs("<Repair>k__BackingField")]
        public float RepairPerSecond { get; private set; }

        [field: SerializeField, Tooltip("침입자의 목표.")]
        public RunGoal Goal { get; private set; }

        [field: SerializeField, Tooltip("Goal이 Room일 때 노리는 방 종류. 마력이 없어도 간다.")]
        public RunRoomKind GoalRoomKind { get; private set; }

        [field: SerializeField] public RunIntruderRole IntruderRole { get; private set; }

        [field: SerializeField, Tooltip("켜면 FavoredRoom에 있을 때 그 방 효과가 커진다.")]
        public bool HasFavoredRoom { get; private set; }

        [field: SerializeField] public RunRoomKind FavoredRoom { get; private set; }

        [field: SerializeField] public SkillDataSO Skill { get; private set; }

        private string _demoDisplayName;

        internal RunCombatantSO CreateDemoVariant(string displayName, EnemyDataSO appearance = null, float healthBonus = 0.5f)
        {
            RunCombatantSO variant = Instantiate(this);
            variant.hideFlags = HideFlags.DontSave;
            variant.name = displayName;
            variant._demoDisplayName = displayName;
            if (appearance != null) variant.SourceEnemy = appearance;
            variant.MaxHealth += Mathf.CeilToInt(MaxHealth * healthBonus);
            return variant;
        }

        /// <summary>시연용 변형의 이동 속도를 배율만큼 바꾼다. 에셋이 아닌 <see cref="CreateDemoVariant"/> 사본에만 쓴다.</summary>
        internal void ScaleDemoMoveSpeed(float ratio)
        {
            if (_demoDisplayName == null) throw new System.InvalidOperationException("시연용 변형에만 쓸 수 있습니다.");
            MoveSpeed *= ratio;
        }

        /// <summary>시연용 변형의 최대 체력을 배율만큼 바꾼다. 에셋이 아닌 <see cref="CreateDemoVariant"/> 사본에만 쓴다.</summary>
        internal void ScaleDemoHealth(float ratio)
        {
            if (_demoDisplayName == null) throw new System.InvalidOperationException("시연용 변형에만 쓸 수 있습니다.");
            MaxHealth = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * ratio));
        }

        /// <summary>
        /// 그림 묶음. 적은 데이터에, 부하는 프리팹의 EntityRender에 들어 있다.
        /// 전투를 차릴 때 한 번 부른다(프리팹 계층을 훑는다).
        /// </summary>
        public CharacterAnimationSetSO ResolveAnimationSet()
        {
            if (SourceEnemy != null)
                return SourceEnemy.AnimationSet;
            if (SourceUnit != null && SourceUnit.Prefab != null)
            {
                EntityRender render = SourceUnit.Prefab.GetComponentInChildren<EntityRender>(true);
                return render != null ? render.AnimationSet : null;
            }
            return null;
        }

        public string DisplayName
        {
            get
            {
                if (_demoDisplayName != null)
                    return _demoDisplayName;
                if (SourceUnit != null)
                    return SourceUnit.Name;
                if (SourceEnemy != null)
                    return SourceEnemy.Name;
                return name;
            }
        }
    }
}
