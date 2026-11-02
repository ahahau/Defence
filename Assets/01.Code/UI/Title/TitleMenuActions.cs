using Code.Audio;
using Code.Persistence;
using Code.Stages;
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
        public const string StageSceneName = "StageDefense";

        [SerializeField, Tooltip("저장이 없으면 잠글 버튼.")]
        private Button continueButton;

        [SerializeField, Tooltip("잠겼을 때 흐리게 만들 글자.")]
        private TMP_Text continueLabel;

        // 카탈로그를 직접 참조하면 타이틀이 스테이지·적·부하 데이터와 프리팹을 모두 올린다. ID 문자열만 둔다.
        [SerializeField, Tooltip("마지막 스테이지 ID. 생성기 StageDefenseBuilder.AddTitleClearRecord가 카탈로그에서 써 넣는다(스테이지 목록이 바뀌면 다시 돌린다).")]
        private string finalStageId;

        [SerializeField, Tooltip("모든 스테이지를 지켜냈으면 보이는 기록. 아니면 비워 자리만 둔다.")]
        private TMP_Text clearRecord;

        private static readonly Color LabelOn = new(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color LabelOff = new(0.46f, 0.42f, 0.37f, 1f);

        private void Start()
        {
            RefreshContinue();
            RefreshClearRecord();
        }

        /// <summary>
        /// 마지막 스테이지를 깬 프로필이면 "모든 스테이지 방어 완료"를 보인다(2026-11-02 사용자 결정).
        /// 따로 기록하지 않고 저장된 클리어 목록을 읽기만 한다.
        /// </summary>
        private void RefreshClearRecord()
        {
            if (clearRecord == null)
                return;
            // 읽기만 한다. 손상된 프로필은 기록 없음으로 보고 오류 로그는 스테이지 씬이 남긴다.
            bool allCleared = StageProfile.IsClearedOnDisk(Application.persistentDataPath, finalStageId);
            clearRecord.text = allCleared ? "모든 스테이지 방어 완료" : string.Empty;
        }

        /// <summary>
        /// 이어할 판이 있는지 실행할 때 다시 본다. 씬에 박아 둔 값은 만들 때의 상태일 뿐이라
        /// 저장을 지우고 돌아오면 어긋난다. 감추지 않고 잠그는 건 버튼 자리가 흔들리지 않게 하려는 것이다.
        /// </summary>
        private void RefreshContinue()
        {
            bool stageSave = StageCheckpointStore.HasSave(Application.persistentDataPath);
            bool hasSave = stageSave || RunSaveSystem.HasSave;

            if (continueButton != null)
                continueButton.interactable = hasSave;

            if (continueLabel != null)
            {
                continueLabel.color = hasSave ? LabelOn : LabelOff;
                continueLabel.text = stageSave ? "스테이지 이어하기" : hasSave ? "이전 던전 이어하기" : "이어하기";
            }
        }

        /// <summary>새 스테이지 판. 이전 준비 저장은 보관하고 영구 강화와 구 런은 유지한다.</summary>
        public void StartNewRun()
        {
            // 새 스테이지는 구 런 파일을 지우거나 변환하지 않는다. 기존 이어하기 경로는 보존한다.
            if (Application.CanStreamedLevelBeLoaded(StageSceneName))
            {
                // 이전 준비 저장을 보관하지 못하면 덮어쓰지 않게 멈춘다. 타이틀에는 팝업 화면이 없어 실패음으로 알린다.
                if (!StageCheckpointStore.EndRun(Application.persistentDataPath))
                {
                    GameSfxPlayer.Play(GameSfxCue.UiFail);
                    return;
                }
                GameSfxPlayer.Play(GameSfxCue.UiClick);
                SceneManager.LoadScene(StageSceneName);
                return;
            }
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            RunSaveSystem.DeleteSave();
            SceneManager.LoadScene(GameSceneName);
        }

        /// <summary>저장을 그대로 두고 들어간다. 복원은 게임 씬 쪽이 맡는다.</summary>
        public void ContinueRun()
        {
            bool stageSave = StageCheckpointStore.HasSave(Application.persistentDataPath);
            if (!stageSave && !RunSaveSystem.HasSave)
                return;

            GameSfxPlayer.Play(GameSfxCue.UiClick);
            SceneManager.LoadScene(stageSave ? StageSceneName : GameSceneName);
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
