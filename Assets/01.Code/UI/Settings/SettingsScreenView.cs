using Code.Audio;
using Code.Manager;
using Code.Persistence;
using Code.UI.Popup;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 게임 화면 설정 창(uGUI, A안, 화면 가운데). 효과음·음악 음량, 전체 화면, 다시 시작, 타이틀로.
    /// 닫을 창이 없을 때 ESC(<see cref="UiEscapeRouter"/>)나 오른쪽 위 톱니 버튼으로 연다.
    /// 열려 있는 동안 게임 시간을 멈춘다 — 영업 중에 켜 둔 사이 손님을 놓치지 않게.
    /// 타이틀 씬의 옛 설정 창(<see cref="SettingsPanelView"/>)과 따로 돈다.
    /// </summary>
    public sealed class SettingsScreenView : MonoBehaviour, IEscapeClosable
    {
        [SerializeField] private GameSpeedController speedController;
        [SerializeField] private GameObject root;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text sfxValue;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private UiFrameButton fullscreenButton;
        [SerializeField] private UiFrameButton restartButton;
        [SerializeField] private UiFrameButton titleButton;
        [SerializeField] private UiFrameButton closeButton;

        public bool IsOpen => root != null && root.activeSelf;

        public void Open()
        {
            if (IsOpen)
                return;

            root.SetActive(true);
            sfxSlider.SetValueWithoutNotify(Mathf.Round(GameSfxPlayer.Volume * 100f));
            musicSlider.SetValueWithoutNotify(Mathf.Round(GameMusicPlayer.Volume * 100f));
            RenderValues();
            speedController?.Suspend(this);
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            root.SetActive(false);
            speedController?.Release(this);
            GameSfxPlayer.Play(GameSfxCue.UiClose);
        }

        private void Awake()
        {
            if (root != null)
                root.SetActive(false);
        }

        private void OnEnable()
        {
            sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            fullscreenButton.Button.onClick.AddListener(ToggleFullscreen);
            restartButton.Button.onClick.AddListener(ConfirmRestart);
            titleButton.Button.onClick.AddListener(GoToTitle);
            closeButton.Button.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
            musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            fullscreenButton.Button.onClick.RemoveListener(ToggleFullscreen);
            restartButton.Button.onClick.RemoveListener(ConfirmRestart);
            titleButton.Button.onClick.RemoveListener(GoToTitle);
            closeButton.Button.onClick.RemoveListener(Close);
            // 열린 채로 꺼지면(씬 이동) 멈춤을 남기지 않는다.
            speedController?.Release(this);
        }

        // 슬라이더는 끄는 동안 값이 수십 번 바뀐다. 소리를 매번 들려주면 크기를 가늠할 수 없어 값만 바꾼다.
        private void HandleSfxChanged(float value)
        {
            GameSfxPlayer.Volume = value / 100f;
            RenderValues();
        }

        private void HandleMusicChanged(float value)
        {
            GameMusicPlayer.Volume = value / 100f;
            RenderValues();
        }

        private void ToggleFullscreen()
        {
            // 전체 화면 전환은 다음 프레임에 반영된다. 글자는 바뀔 값으로 미리 쓴다.
            var next = !Screen.fullScreen;
            Screen.fullScreen = next;
            fullscreenButton.SetLabel(FullscreenLabel(next));
        }

        private void RenderValues()
        {
            sfxValue.text = Mathf.RoundToInt(sfxSlider.value) + "%";
            musicValue.text = Mathf.RoundToInt(musicSlider.value) + "%";
            fullscreenButton.SetLabel(FullscreenLabel(Screen.fullScreen));
        }

        private static string FullscreenLabel(bool on) => on ? "전체 화면: 켬" : "전체 화면: 끔";

        private void ConfirmRestart()
        {
            PopupController.Show(PopupRequest.Confirm(
                "이번 런을 다시 시작할까요?",
                "현재 저장과 진행 중인 운영 기록이 삭제됩니다.",
                RestartRun,
                confirmLabel: "삭제 후 재시작",
                destructive: true));
        }

        private void RestartRun()
        {
            RunSaveSystem.DeleteSave();
            LeaveTo(TitleMenuActions.GameSceneName);
        }

        private void GoToTitle() => LeaveTo(TitleMenuActions.TitleSceneName);

        /// <summary>창을 닫고 배속을 되돌린 뒤 씬을 바꾼다. 멈춘 채로 나가면 다음 판이 멈춘 채로 시작한다.</summary>
        private void LeaveTo(string sceneName)
        {
            Close();
            speedController?.ResetToNormal();
            SceneManager.LoadScene(sceneName);
        }
    }
}
