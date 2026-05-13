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
            SetButtonVisual(pauseButton, ref pauseButtonBackground, Mathf.Approximately(_currentSpeed, 0f));
            SetButtonVisual(normalButton, ref normalButtonBackground, Mathf.Approximately(_currentSpeed, 1f));
            SetButtonVisual(fastButton, ref fastButtonBackground, Mathf.Approximately(_currentSpeed, 2f));
        }

        private void SetButtonVisual(Button button, ref Image background, bool selected)
        {
            if (button == null)
                return;

            if (background == null)
                background = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (background == null)
                return;

            var baseColor = selected ? selectedColor : normalColor;
            background.color = baseColor;
            button.targetGraphic = background;

            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
            colors.selectedColor = selectedColor;
            colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.04f;
            button.colors = colors;
        }
    }
}
