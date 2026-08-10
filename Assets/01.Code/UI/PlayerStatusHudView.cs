using _01.Code.Combat;
using _01.Code.Units;
using DG.Tweening;
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
        private Tween _healthTween;
        private Tween _experienceTween;
        private float _lastHealthRatio = -1f;
        private int _lastLevel = -1;
        private Color _baseHealthTextColor = Color.white;
        private Color _baseHealthFillColor = Color.white;
        private Image _healthFillImage;
        private Vector3 _healthTextBaseScale = Vector3.one;
        private Vector3 _levelTextBaseScale = Vector3.one;

        private void Awake()
        {
            NestHudStyle.ApplyPanel(gameObject, true);
            NestHudStyle.ApplyPlayerStatusLayout(gameObject);
            if (healthText != null)
            {
                _baseHealthTextColor = healthText.color;
                _healthTextBaseScale = healthText.transform.localScale;
            }
            if (levelText != null)
                _levelTextBaseScale = levelText.transform.localScale;
            if (healthSlider != null && healthSlider.fillRect != null)
            {
                _healthFillImage = healthSlider.fillRect.GetComponent<Image>();
                if (_healthFillImage != null)
                    _baseHealthFillColor = _healthFillImage.color;
            }
        }

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
            _healthTween?.Kill();
            _experienceTween?.Kill();
            healthText?.DOKill();
            levelText?.transform.DOKill();
            if (healthText != null)
                healthText.transform.localScale = _healthTextBaseScale;
            if (levelText != null)
                levelText.transform.localScale = _levelTextBaseScale;
            Unsubscribe();
        }

        public void SetTarget(MainUnit mainUnit)
        {
            Unsubscribe();

            target = mainUnit;
            targetHealth = target != null ? target.Health : null;
            targetLevel = target != null ? target.Level : null;
            _lastHealthRatio = -1f;
            _lastLevel = -1;

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
            {
                _healthTween?.Kill();
                if (_lastHealthRatio < 0f)
                    healthSlider.SetValueWithoutNotify(ratio);
                else
                    _healthTween = DOTween.To(
                            () => healthSlider.value,
                            value => healthSlider.SetValueWithoutNotify(value),
                            ratio,
                            0.22f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true)
                        .SetLink(healthSlider.gameObject);
            }

            if (healthText != null)
            {
                healthText.text = targetHealth != null
                    ? $"{targetHealth.CurrentHealth}/{targetHealth.MaxHealth}"
                    : "-";
                var lowHealth = targetHealth != null && ratio <= 0.3f;
                var targetColor = lowHealth ? new Color(1f, 0.35f, 0.3f, 1f) : _baseHealthTextColor;
                healthText.DOKill();
                if (_lastHealthRatio >= 0f && ratio < _lastHealthRatio)
                {
                    healthText.color = new Color(1f, 0.22f, 0.18f, 1f);
                    healthText.DOColor(targetColor, 0.48f).SetUpdate(true).SetLink(healthText.gameObject);
                    healthText.transform.DOKill();
                    healthText.transform.localScale = _healthTextBaseScale;
                    healthText.transform.DOPunchScale(Vector3.one * 0.13f, 0.28f, 7, 0.7f)
                        .SetUpdate(true).SetLink(healthText.gameObject);
                }
                else
                {
                    healthText.color = targetColor;
                }
            }

            if (_healthFillImage != null)
                _healthFillImage.color = ratio <= 0.3f
                    ? new Color(0.92f, 0.16f, 0.13f, 1f)
                    : _baseHealthFillColor;

            _lastHealthRatio = ratio;
        }

        private void RefreshLevel()
        {
            var ratio = targetLevel != null ? targetLevel.ExperienceRatio : 0f;
            if (experienceSlider != null)
            {
                _experienceTween?.Kill();
                if (_lastLevel < 0)
                    experienceSlider.SetValueWithoutNotify(ratio);
                else
                    _experienceTween = DOTween.To(
                            () => experienceSlider.value,
                            value => experienceSlider.SetValueWithoutNotify(value),
                            ratio,
                            0.28f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true)
                        .SetLink(experienceSlider.gameObject);
            }

            if (experienceText != null)
                experienceText.text = targetLevel != null
                    ? $"{targetLevel.Experience}/{targetLevel.ExperienceToNextLevel}"
                    : "-";

            if (levelText != null)
            {
                levelText.text = targetLevel != null ? $"Lv {targetLevel.Level}" : "Lv -";
                if (_lastLevel > 0 && targetLevel != null && targetLevel.Level > _lastLevel)
                {
                    levelText.transform.DOKill();
                    levelText.transform.localScale = _levelTextBaseScale;
                    levelText.transform.DOPunchScale(Vector3.one * 0.22f, 0.46f, 8, 0.75f)
                        .SetUpdate(true).SetLink(levelText.gameObject);
                }
            }

            _lastLevel = targetLevel != null ? targetLevel.Level : -1;
        }
    }
}
