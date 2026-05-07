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
                unit.Combatant.AddAttackDamage(attackDamageBonusPerLevel);
            }

            RefreshView();
        }

        private void RefreshView()
        {
            levelView.SetLevel(level);
            levelView.SetExperienceRatio(ExperienceRatio);
            Changed?.Invoke(this);
        }
    }
}
