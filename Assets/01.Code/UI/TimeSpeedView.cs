using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class TimeSpeedView : MonoBehaviour
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button normalButton;
        [SerializeField] private Button fastButton;
        [SerializeField] private Image pauseButtonBackground;
        [SerializeField] private Image normalButtonBackground;
        [SerializeField] private Image fastButtonBackground;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.2f, 0.95f);
        [SerializeField] private Color selectedColor = new Color(0.25f, 0.45f, 0.27f, 0.95f);
        [SerializeField] private float defaultSpeed = 1f;

        private float _currentSpeed = 1f;

        private void Awake()
        {
            _currentSpeed = Mathf.Clamp(defaultSpeed, 0f, 2f);
            ApplySpeed(_currentSpeed);
        }

        private void OnEnable()
        {
            if (pauseButton != null)
                pauseButton.onClick.AddListener(SetPauseSpeed);
            if (normalButton != null)
                normalButton.onClick.AddListener(SetNormalSpeed);
            if (fastButton != null)
                fastButton.onClick.AddListener(SetFastSpeed);

            RefreshVisuals();
        }

        private void OnDisable()
        {
            if (pauseButton != null)
                pauseButton.onClick.RemoveListener(SetPauseSpeed);
            if (normalButton != null)
                normalButton.onClick.RemoveListener(SetNormalSpeed);
            if (fastButton != null)
                fastButton.onClick.RemoveListener(SetFastSpeed);
        }

        public void SetTimeSpeed(float speed)
        {
            ApplySpeed(Mathf.Clamp(speed, 0f, 2f));
        }

        private void SetPauseSpeed() => ApplySpeed(0f);

        private void SetNormalSpeed() => ApplySpeed(1f);

        private void SetFastSpeed() => ApplySpeed(2f);

        private void ApplySpeed(float speed)
        {
            _currentSpeed = speed;
            Time.timeScale = speed;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            SetButtonColor(pauseButtonBackground, Mathf.Approximately(_currentSpeed, 0f));
            SetButtonColor(normalButtonBackground, Mathf.Approximately(_currentSpeed, 1f));
            SetButtonColor(fastButtonBackground, Mathf.Approximately(_currentSpeed, 2f));
        }

        private void SetButtonColor(Image image, bool selected)
        {
            if (image == null)
                return;

            image.color = selected ? selectedColor : normalColor;
        }
    }
}
