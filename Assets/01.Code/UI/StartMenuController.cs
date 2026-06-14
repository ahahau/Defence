using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class StartMenuController : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "SampleScene";
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private SoundSettingsController soundSettingsController;
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeSettingsButton;
        [SerializeField] private Button resetSoundButton;

        private void Awake()
        {
            SetSettingsVisible(false);
        }

        private void OnEnable()
        {
            AddButtonListener(startButton, StartGame);
            AddButtonListener(settingsButton, ShowSettings);
            AddButtonListener(quitButton, QuitGame);
            AddButtonListener(closeSettingsButton, HideSettings);
            AddButtonListener(resetSoundButton, ResetSoundSettings);
        }

        private void OnDisable()
        {
            RemoveButtonListener(startButton, StartGame);
            RemoveButtonListener(settingsButton, ShowSettings);
            RemoveButtonListener(quitButton, QuitGame);
            RemoveButtonListener(closeSettingsButton, HideSettings);
            RemoveButtonListener(resetSoundButton, ResetSoundSettings);
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            SetSettingsVisible(settingsPanel == null || !settingsPanel.activeSelf);
        }

        public void StartGame()
        {
            if (!string.IsNullOrWhiteSpace(gameSceneName))
                SceneManager.LoadScene(gameSceneName);
        }

        public void ShowSettings()
        {
            SetSettingsVisible(true);
        }

        public void HideSettings()
        {
            SetSettingsVisible(false);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private void ResetSoundSettings()
        {
            soundSettingsController?.ResetDefaults();
        }

        private void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }

        private void SetSettingsVisible(bool visible)
        {
            if (settingsPanel == null)
                return;

            settingsPanel.SetActive(visible);
            if (visible)
                soundSettingsController?.Load();
        }
    }
}
