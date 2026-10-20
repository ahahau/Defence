using Code.Audio;
using Code.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 타이틀 버튼이 부르는 곳.
    ///
    /// 화면은 씬에 세워 두고(에디터에서 보고 고칠 수 있게) 동작만 여기에 둔다.
    /// 버튼의 onClick 은 씬에 배선돼 있어서, 무엇이 걸려 있는지 인스펙터에서 보인다.
    /// </summary>
    public class TitleMenuActions : MonoBehaviour
    {
        /// <summary>타이틀이 올라가는 씬. 인게임 설정 창이 "타이틀로 나가기"에 쓴다.</summary>
        public const string TitleSceneName = "Start";

        /// <summary>시작하면 넘어갈 씬.</summary>
        public const string GameSceneName = "SampleScene";

        [SerializeField, Tooltip("저장이 없으면 잠글 버튼.")]
        private Button continueButton;

        [SerializeField, Tooltip("잠겼을 때 흐리게 만들 글자.")]
        private TMP_Text continueLabel;

        private static readonly Color LabelOn = new(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color LabelOff = new(0.46f, 0.42f, 0.37f, 1f);

        private void Start()
        {
            RefreshContinue();
        }

        /// <summary>
        /// 이어할 판이 있는지 실행할 때 다시 본다. 씬에 박아 둔 값은 만들 때의 상태일 뿐이라
        /// 저장을 지우고 돌아오면 어긋난다. 감추지 않고 잠그는 건 버튼 자리가 흔들리지 않게 하려는 것이다.
        /// </summary>
        private void RefreshContinue()
        {
            var hasSave = RunSaveSystem.HasSave;

            if (continueButton != null)
                continueButton.interactable = hasSave;

            if (continueLabel != null)
                continueLabel.color = hasSave ? LabelOn : LabelOff;
        }

        /// <summary>새 판. 남아 있던 저장을 지우고 들어간다.</summary>
        public void StartNewRun()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            RunSaveSystem.DeleteSave();
            SceneManager.LoadScene(GameSceneName);
        }

        /// <summary>저장을 그대로 두고 들어간다. 복원은 게임 씬 쪽이 맡는다.</summary>
        public void ContinueRun()
        {
            if (!RunSaveSystem.HasSave)
                return;

            GameSfxPlayer.Play(GameSfxCue.UiClick);
            SceneManager.LoadScene(GameSceneName);
        }

        public void OpenSettings()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            SettingsPanelView.Open();
        }

        public void QuitGame()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
