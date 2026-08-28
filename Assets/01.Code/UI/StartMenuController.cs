using UnityEngine;
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
            if (settingsButton != null)
                settingsButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            AddButtonListener(startButton, StartGame);
            AddButtonListener(quitButton, QuitGame);
        }

        private void OnDisable()
        {
            RemoveButtonListener(startButton, StartGame);
            RemoveButtonListener(quitButton, QuitGame);
        }

        public void StartGame()
        {
            if (!string.IsNullOrWhiteSpace(gameSceneName))
                SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>별도 '새 게임' 버튼을 연결할 때 쓰는 진입점. 기본 시작 버튼은 저장이 있으면 이어한다.</summary>
        public void StartNewGame()
        {
            _01.Code.Persistence.RunSaveSystem.DeleteSave();
            StartGame();
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
        }
    }
}
