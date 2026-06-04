using _01.Code.Combat;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class PlayerStatusHudView : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Slider experienceSlider;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text levelText;

        private MainUnit target;
        private Health targetHealth;
        private UnitLevel targetLevel;

#if UNITY_EDITOR
        public void EditorConfigure(Slider configuredHealthSlider, TMP_Text configuredHealthText, Slider configuredExperienceSlider, TMP_Text configuredExperienceText, TMP_Text configuredLevelText)
        {
            healthSlider = configuredHealthSlider;
            healthText = configuredHealthText;
            experienceSlider = configuredExperienceSlider;
            experienceText = configuredExperienceText;
            levelText = configuredLevelText;
        }
#endif

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetTarget(MainUnit mainUnit)
        {
            Unsubscribe();

            target = mainUnit;
            targetHealth = target != null ? target.Health : null;
            targetLevel = target != null ? target.Level : null;

            if (targetHealth != null)
                targetHealth.Changed += HandleHealthChanged;

            if (targetLevel != null)
                targetLevel.Changed += HandleLevelChanged;

            RefreshHealth();
            RefreshLevel();
            gameObject.SetActive(target != null);
        }

        private void Unsubscribe()
        {
            if (targetHealth != null)
                targetHealth.Changed -= HandleHealthChanged;

            if (targetLevel != null)
                targetLevel.Changed -= HandleLevelChanged;

            target = null;
            targetHealth = null;
            targetLevel = null;
        }

        private void HandleHealthChanged(float _)
        {
            RefreshHealth();
        }

        private void HandleLevelChanged(UnitLevel _)
        {
            RefreshLevel();
        }

        private void RefreshHealth()
        {
            var ratio = targetHealth != null ? targetHealth.CurrentRatio : 0f;
            if (healthSlider != null)
                healthSlider.SetValueWithoutNotify(ratio);

            if (healthText != null)
                healthText.text = targetHealth != null
                    ? $"{targetHealth.CurrentHealth}/{targetHealth.MaxHealth}"
                    : "-";
        }

        private void RefreshLevel()
        {
            var ratio = targetLevel != null ? targetLevel.ExperienceRatio : 0f;
            if (experienceSlider != null)
                experienceSlider.SetValueWithoutNotify(ratio);

            if (experienceText != null)
                experienceText.text = targetLevel != null
                    ? $"{targetLevel.Experience}/{targetLevel.ExperienceToNextLevel}"
                    : "-";

            if (levelText != null)
                levelText.text = targetLevel != null ? $"Lv {targetLevel.Level}" : "Lv -";
        }
    }
}
