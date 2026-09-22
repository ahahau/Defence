using Code.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 음량과 게임 안내를 여는 설정 창.
    ///
    /// 프리팹으로 제작한 오버레이와 창을 실행 시 연결한다.
    ///
    /// 화면 구성은 SettingsHost와 SettingsWindow 프리팹에 있고, 여기서는 동작만 연결한다.
    /// </summary>
    public class SettingsPanelView : MonoBehaviour
    {
        private static SettingsPanelView current;
        public static bool IsOpen => current != null && current.window != null && current.window.activeInHierarchy;

        private GameObject window;
        private GameObject backdrop;
        private GameObject confirmWindow;
        private GameObject gameGuideWindow;
        private GameObject restartButton;
        private GameObject titleButton;
        private GameObject closeButton;
        private Slider slider;
        private TMP_Text valueLabel;
        private Slider musicSlider;
        private TMP_Text musicValueLabel;

        private void Start()
        {
            current = this;
            var refs = ResolveWindow(transform);
            if (refs == null || !refs.IsComplete)
            {
                Debug.LogError("SettingsPanelView requires a SettingsWindowRefs child assigned in the scene hierarchy.", this);
                enabled = false;
                return;
            }
            Bind(refs);

            window.SetActive(false);
            gameGuideWindow.SetActive(false);
            if (backdrop != null)
                backdrop.SetActive(false);
            FitToScene();
        }

        private void OnEnable()
        {
            // 이 창은 씬을 넘어가도 살아남는다. 씬이 바뀌면 구성을 다시 맞춰야
            // 판을 떠나는 두 버튼이 제자리를 찾는다 — 타이틀에서 만들어진 창이
            // 그대로 판 위로 따라오기 때문이다.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (window == null)
                return;

            Toggle(false);
            SetConfirmVisible(false);
            FitToScene();
        }

        /// <summary>타이틀 메뉴처럼 바깥에서 설정을 열 때 쓴다.</summary>
        public static void Open()
        {
            if (current != null && current.window != null)
                current.Toggle(true);
        }

        private void Update()
        {
            if (!EscapePressedThisFrame())
                return;

            if (gameGuideWindow != null && gameGuideWindow.activeSelf)
            {
                SetGameGuideVisible(false);
                return;
            }

            // 확인 창이 떠 있으면 그것부터 닫는다. 한 번에 둘을 닫으면 취소한 줄 모르고 지나간다.
            if (confirmWindow != null && confirmWindow.activeSelf)
            {
                SetConfirmVisible(false);
                return;
            }

            // ESC 는 "지금 보고 있는 것"을 닫는 열쇠다. 설정이 떠 있으면 설정을 닫는다.
            if (window != null && window.activeSelf)
            {
                Toggle(false);
                return;
            }

            // 다른 창이 떠 있는데 설정을 새로 열면, 닫으려고 누른 키가 창을 하나 더 얹는다.
            // 그 경우에는 떠 있는 창을 대신 닫아 준다.
            if (ExclusiveWindow.CloseTopmost())
            {
                GameSfxPlayer.Play(GameSfxCue.UiClose);
                return;
            }

            Toggle(true);
        }

        /// <summary>
        /// 이번 프레임에 ESC 를 눌렀는가.
        ///
        /// 이 프로젝트는 입력 처리가 Input System 패키지로 넘어가 있어서(activeInputHandler 1)
        /// 옛 UnityEngine.Input 을 부르면 그 자리에서 예외가 난다. 두 방식 다 켜 둔 프로젝트도
        /// 있으므로 컴파일 기호로 갈라 둔다.
        /// </summary>
        private static bool EscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private SettingsWindowRefs ResolveWindow(Transform parent) =>
            parent.GetComponentInChildren<SettingsWindowRefs>(true);

        /// <summary>프리팹 참조에 동작을 연결한다.</summary>
        private void Bind(SettingsWindowRefs refs)
        {
            backdrop = refs.backdrop;
            window = refs.window;
            confirmWindow = refs.confirmWindow;
            gameGuideWindow = refs.gameGuideWindow;
            restartButton = refs.restartButton.gameObject;
            titleButton = refs.titleButton.gameObject;
            closeButton = refs.closeButton.gameObject;

            slider = refs.sfxSlider;
            valueLabel = refs.sfxValueLabel;
            musicSlider = refs.musicSlider;
            musicValueLabel = refs.musicValueLabel;

            slider.SetValueWithoutNotify(GameSfxPlayer.Volume);
            valueLabel.text = Percent(slider.value);
            slider.onValueChanged.AddListener(OnSfxVolumeChanged);

            musicSlider.SetValueWithoutNotify(GameMusicPlayer.Volume);
            musicValueLabel.text = Percent(musicSlider.value);
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            refs.restartButton.onClick.AddListener(() => SetConfirmVisible(true));
            refs.titleButton.onClick.AddListener(GoToTitle);
            refs.closeButton.onClick.AddListener(() => Toggle(false));
            refs.confirmCancelButton.onClick.AddListener(() => SetConfirmVisible(false));
            refs.confirmAcceptButton.onClick.AddListener(RestartRun);
            refs.guideButton.onClick.AddListener(() => SetGameGuideVisible(true));
            refs.guideCloseButton.onClick.AddListener(() => SetGameGuideVisible(false));
        }

        private void SetGameGuideVisible(bool visible)
        {
            if (gameGuideWindow == null)
                return;
            if (gameGuideWindow.activeSelf == visible)
                return;

            gameGuideWindow.SetActive(visible);
            if (visible)
                gameGuideWindow.transform.SetAsLastSibling();
            GameSfxPlayer.Play(visible ? GameSfxCue.UiOpen : GameSfxCue.UiClose);
        }

        /// <summary>
        /// 지금 씬에 맞게 창을 고쳐 놓는다. 타이틀에서는 판을 떠나는 두 버튼이 뜻이 없으므로
        /// 감추고 창도 그만큼 줄인다. 열 때마다 보므로 씬을 오간 뒤에도 어긋나지 않는다.
        /// </summary>
        private void FitToScene()
        {
            var inGame = IsInGame;

            if (restartButton != null)
                restartButton.SetActive(inGame);
            if (titleButton != null)
                titleButton.SetActive(inGame);

            if (window != null && window.transform is RectTransform rect)
                rect.sizeDelta = new Vector2(440f, inGame ? 386f : 274f);

        }

        /// <summary>판 위인가. 타이틀 씬에서는 나가기·다시하기가 뜻이 없다.</summary>
        private static bool IsInGame =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != TitleMenuActions.TitleSceneName;

        private void SetConfirmVisible(bool visible)
        {
            if (confirmWindow == null)
                return;

            confirmWindow.SetActive(visible);
            GameSfxPlayer.Play(visible ? GameSfxCue.UiOpen : GameSfxCue.UiClose);
        }

        /// <summary>저장을 지우고 판을 처음부터 다시 올린다.</summary>
        private void RestartRun()
        {
            // 되돌릴 수 없는 동작이라 둘러보는 클릭과 다른 소리를 낸다.
            GameSfxPlayer.Play(GameSfxCue.UiConfirm);
            Code.Persistence.RunSaveSystem.DeleteSave();
            LeaveTo(TitleMenuActions.GameSceneName);
        }

        private void GoToTitle()
        {
            LeaveTo(TitleMenuActions.TitleSceneName);
        }

        /// <summary>
        /// 창을 정리하고 씬을 바꾼다. 시간 배속을 되돌리는 게 핵심이다 —
        /// 정산이나 모달이 timeScale 을 0 으로 잡아 둔 채 나가면 다음 판이 멈춘 채로 시작한다.
        /// </summary>
        private void LeaveTo(string sceneName)
        {
            SetConfirmVisible(false);
            Toggle(false);
            Manager.GameSpeedController.Current?.ResetToNormal();
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        private void OnSfxVolumeChanged(float value)
        {
            // 정한 크기를 바로 들려주려 했는데, 슬라이더는 끄는 동안 값이 수십 번 바뀐다.
            // 한 번 만질 때마다 딸깍 소리가 연달아 터져서 오히려 크기를 가늠할 수 없었다.
            GameSfxPlayer.Volume = value;
            valueLabel.text = Percent(value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            // 음악은 계속 흐르고 있으므로 따로 들려줄 필요가 없다.
            GameMusicPlayer.Volume = value;
            musicValueLabel.text = Percent(value);
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private void Toggle(bool open)
        {
            if (open)
                FitToScene();
            else
                SetGameGuideVisible(false);

            if (backdrop != null)
                backdrop.SetActive(open);
            window.SetActive(open);
            if (open)
            {
                slider.SetValueWithoutNotify(GameSfxPlayer.Volume);
                valueLabel.text = Percent(slider.value);
                musicSlider.SetValueWithoutNotify(GameMusicPlayer.Volume);
                musicValueLabel.text = Percent(musicSlider.value);
            }

            GameSfxPlayer.Play(open ? GameSfxCue.UiOpen : GameSfxCue.UiClose);
        }


    }
}
