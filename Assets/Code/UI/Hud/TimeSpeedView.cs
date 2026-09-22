using UnityEngine;
using UnityEngine.UI;
using Code.Manager;

namespace Code.UI
{
    public class TimeSpeedView : MonoBehaviour
    {
        [SerializeField, Tooltip("시간 상태의 유일한 소유자. 씬에서 직접 연결한다.")]
        private GameSpeedController gameSpeedController;
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
        private bool _interactionLocked;

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

        /// <summary>
        /// 컨트롤러가 정한 속도를 따라간다.
        ///
        /// 여기 버튼만 속도를 바꾸는 것이 아니다 — 스페이스와 1·2 키도 바꾸고, 모달이 닫히면
        /// 되돌아온다. 자기 복사본만 보고 있으면 키로 멈춘 뒤에도 버튼은 1배속에 켜져 있다.
        /// 멈춤은 이제 배치할 수 있는 상태이기도 해서, 표시가 어긋나면 손댈 수 있는지를
        /// 화면이 거짓으로 알려 주게 된다.
        /// </summary>
        private void Update()
        {
            if (gameSpeedController == null)
                return;

            var setting = gameSpeedController.Setting;
            if (Mathf.Approximately(setting, _currentSpeed))
                return;

            _currentSpeed = setting;
            RefreshVisuals();
        }

        public void SetTimeSpeed(float speed)
        {
            ApplySpeed(Mathf.Clamp(speed, 0f, 2f));
        }

        public void SetInteractionLocked(bool locked)
        {
            _interactionLocked = locked;
            if (pauseButton != null)
                pauseButton.interactable = !locked;
            if (normalButton != null)
                normalButton.interactable = !locked;
            if (fastButton != null)
                fastButton.interactable = !locked;
            RefreshVisuals();
        }

        private void SetPauseSpeed() => ApplySpeed(0f);

        private void SetNormalSpeed() => ApplySpeed(1f);

        private void SetFastSpeed() => ApplySpeed(2f);

        private void ApplySpeed(float speed)
        {
            if (_interactionLocked)
                return;

            _currentSpeed = speed;

            // 실제로 시간을 건드리는 것은 컨트롤러 하나다. 여기서 직접 쓰면 모달이 닫힐 때
            // 컨트롤러가 되돌리는 값과 어긋난다.
            gameSpeedController?.SetSetting(speed);

            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            RenderSelection(pauseButton, pauseButtonBackground, Mathf.Approximately(_currentSpeed, 0f));
            RenderSelection(normalButton, normalButtonBackground, Mathf.Approximately(_currentSpeed, 1f));
            RenderSelection(fastButton, fastButtonBackground, Mathf.Approximately(_currentSpeed, 2f));
        }

        private void RenderSelection(Button button, Image background, bool selected)
        {
            if (button == null || background == null)
                return;

            var baseColor = selected ? selectedColor : normalColor;
            background.color = baseColor;
        }
    }
}
