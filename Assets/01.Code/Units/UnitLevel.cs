using System;
using UnityEngine;

namespace _01.Code.Units
{
    public class UnitLevel : MonoBehaviour
    {
        [SerializeField] private UnitLevelView levelView;
        [SerializeField] private int level = 1;
        [SerializeField] private int experience;
        [SerializeField] private int baseExperienceToNextLevel = 3;
        [SerializeField] private int experienceGrowthPerLevel = 2;
        [SerializeField] private int attackDamageBonusPerLevel = 1;

        [SerializeField, Min(0),
         Tooltip("레벨당 늘어나는 최대 체력. 침입자는 웨이브 레벨마다 체력과 공격력을 함께 받으므로 " +
                 "이게 0이면 부하는 더 세게 때리기만 할 뿐 후반으로 갈수록 종잇장이 된다.")]
        private int maxHealthBonusPerLevel = 3;

        [SerializeField] private Unit unit;

        public event Action<UnitLevel> Changed;
        public int Level => level;
        public int Experience => experience;
        public int ExperienceToNextLevel => baseExperienceToNextLevel + (level - 1) * experienceGrowthPerLevel;
        public float ExperienceRatio => ExperienceToNextLevel > 0 ? (float)experience / ExperienceToNextLevel : 0f;

        private void Awake()
        {
            RefreshView();
        }

        public void AddKillExperience(int amount)
        {
            if (amount <= 0)
                return;

            experience += amount;

            while (experience >= ExperienceToNextLevel)
            {
                experience -= ExperienceToNextLevel;
                level++;
                ApplyLevelBonuses(1);
            }

            RefreshView();
        }

        /// <summary>
        /// 회수했다 다시 내보낸 부하의 레벨을 되살린다.
        /// 회수는 유닛 오브젝트를 파괴하므로 이 경로가 없으면 다친 부하를 낫게 할 때마다 레벨이 0으로 돌아간다.
        /// </summary>
        public void Restore(int restoredLevel, int restoredExperience)
        {
            var normalizedLevel = Mathf.Max(1, restoredLevel);
            level = normalizedLevel;
            experience = Mathf.Clamp(restoredExperience, 0, Mathf.Max(0, ExperienceToNextLevel - 1));

            // 보너스는 레벨업마다 한 칸씩 더해져 온 값이라, 복원할 때도 오른 횟수만큼 한 번에 되돌린다.
            ApplyLevelBonuses(normalizedLevel - 1);
            RefreshView();
        }

        private void ApplyLevelBonuses(int levelsGained)
        {
            if (levelsGained <= 0 || unit == null)
                return;

            unit.Combatant?.AddAttackDamage(attackDamageBonusPerLevel * levelsGained);
            unit.Health?.AddMaxHealth(maxHealthBonusPerLevel * levelsGained, true);
        }

        private void RefreshView()
        {
            levelView.SetLevel(level);
            levelView.SetExperienceRatio(ExperienceRatio);
            Changed?.Invoke(this);
        }
    }
}
