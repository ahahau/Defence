using Code.Audio;
using Code.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>UI Toolkit 제목 문서의 버튼과 음량 상태를 연결한다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TitleToolkitView : MonoBehaviour
    {
        private Button _newRunButton;
        private Button _continueButton;
        private Button _settingsButton;
        private Button _quitButton;
        private Button _closeSettingsButton;
        private VisualElement _settingsPanel;
        private Slider _sfxSlider;
        private Slider _musicSlider;
        private Label _sfxValue;
        private Label _musicValue;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _newRunButton = root.Q<Button>("new-run-button");
            _continueButton = root.Q<Button>("continue-button");
            _settingsButton = root.Q<Button>("settings-button");
            _quitButton = root.Q<Button>("quit-button");
            _closeSettingsButton = root.Q<Button>("close-settings-button");
            _settingsPanel = root.Q<VisualElement>("settings-panel");
            _sfxSlider = root.Q<Slider>("sfx-slider");
            _musicSlider = root.Q<Slider>("music-slider");
            _sfxValue = root.Q<Label>("sfx-value");
            _musicValue = root.Q<Label>("music-value");

            if (_newRunButton != null)
                _newRunButton.clicked += StartNewRun;
            if (_continueButton != null)
                _continueButton.clicked += ContinueRun;
            if (_settingsButton != null)
                _settingsButton.clicked += ShowSettings;
            if (_quitButton != null)
                _quitButton.clicked += QuitGame;
            if (_closeSettingsButton != null)
                _closeSettingsButton.clicked += HideSettings;
            if (_sfxSlider != null)
                _sfxSlider.RegisterValueChangedCallback(HandleSfxChanged);
            if (_musicSlider != null)
                _musicSlider.RegisterValueChangedCallback(HandleMusicChanged);

            Refresh();
            SetSettingsVisible(false);
        }

        private void OnDisable()
        {
            if (_newRunButton != null)
                _newRunButton.clicked -= StartNewRun;
            if (_continueButton != null)
                _continueButton.clicked -= ContinueRun;
            if (_settingsButton != null)
                _settingsButton.clicked -= ShowSettings;
            if (_quitButton != null)
                _quitButton.clicked -= QuitGame;
            if (_closeSettingsButton != null)
                _closeSettingsButton.clicked -= HideSettings;
            if (_sfxSlider != null)
                _sfxSlider.UnregisterValueChangedCallback(HandleSfxChanged);
            if (_musicSlider != null)
                _musicSlider.UnregisterValueChangedCallback(HandleMusicChanged);
        }

        private void Refresh()
        {
            if (_continueButton != null)
                _continueButton.SetEnabled(RunSaveSystem.HasSave);

            if (_sfxSlider != null)
                _sfxSlider.SetValueWithoutNotify(GameSfxPlayer.Volume);
            if (_musicSlider != null)
                _musicSlider.SetValueWithoutNotify(GameMusicPlayer.Volume);
            RefreshVolumeLabels();
        }

        private void StartNewRun()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            RunSaveSystem.DeleteSave();
            SceneManager.LoadScene(TitleMenuActions.GameSceneName);
        }

        private void ContinueRun()
        {
            if (!RunSaveSystem.HasSave)
                return;

            GameSfxPlayer.Play(GameSfxCue.UiClick);
            SceneManager.LoadScene(TitleMenuActions.GameSceneName);
        }

        private void ShowSettings()
        {
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
            Refresh();
            SetSettingsVisible(true);
        }

        private void HideSettings()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClose);
            SetSettingsVisible(false);
        }

        private void QuitGame()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleSfxChanged(ChangeEvent<float> evt)
        {
            GameSfxPlayer.Volume = evt.newValue;
            RefreshVolumeLabels();
        }

        private void HandleMusicChanged(ChangeEvent<float> evt)
        {
            GameMusicPlayer.Volume = evt.newValue;
            RefreshVolumeLabels();
        }

        private void RefreshVolumeLabels()
        {
            if (_sfxValue != null)
                _sfxValue.text = ToPercent(GameSfxPlayer.Volume);
            if (_musicValue != null)
                _musicValue.text = ToPercent(GameMusicPlayer.Volume);
        }

        private void SetSettingsVisible(bool visible)
        {
            if (_settingsPanel != null)
                _settingsPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string ToPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
