using Code.Audio;
using Code.Persistence;
using Code.Progression;
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
        private Label _metaRecord;
        private Label _metaSlots;
        private VisualElement _metaModifierList;

        private const string MetaModifierClass = "meta-modifier";
        private const string MetaEquippedClass = "is-equipped";

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _metaRecord = root.Q<Label>("meta-record");
            _metaSlots = root.Q<Label>("meta-slots");
            _metaModifierList = root.Q<VisualElement>("meta-modifier-list");
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
            RefreshMeta();
        }

        /// <summary>
        /// 최고 기록과 시작 보정 칸. 보정은 다음 "새 게임"에만 걸린다(이어하기는 시작할 때 고른 것을 그대로 쓴다).
        /// </summary>
        /// <summary>다음 콘텐츠(유닛·함정·시설·사건)가 열리는 진행도.</summary>
        private static string DescribeNextContentUnlock(int progress)
        {
            var next = MetaUnlockRules.NextTier(progress);
            return next > 0 ? $"새 콘텐츠 {next}에 열림" : "콘텐츠 모두 열림";
        }

        private void RefreshMeta()
        {
            var profile = MetaProfileStore.Profile;
            var slots = MetaProgressRules.SlotCount(profile.progress);
            if (_metaRecord != null)
                _metaRecord.text = profile.runsPlayed > 0
                    ? $"최고 {profile.bestDay}일 · 최고 등급 {profile.bestGrade} · 보스 {profile.totalBossesDefeated}명\n진행도 {profile.progress} · {profile.runsPlayed}판 · {DescribeNextContentUnlock(profile.progress)}"
                    : "아직 끝낸 판이 없습니다\n버틴 날과 잡은 보스가 진행도로 쌓입니다";
            if (_metaSlots != null)
                _metaSlots.text = $"새 게임 시작 보정 {profile.equipped.Count}/{slots}칸";

            if (_metaModifierList == null)
                return;

            _metaModifierList.Clear();
            foreach (var modifier in MetaProgressRules.AllModifiers)
            {
                var unlocked = MetaProgressRules.IsUnlocked(modifier, profile.progress);
                var equipped = MetaProfileStore.IsEquipped(modifier);
                var target = modifier;
                var button = new Button(() => ToggleModifier(target))
                {
                    text = unlocked
                        ? $"{(equipped ? "[장착] " : string.Empty)}{MetaProgressRules.Label(modifier)} · {MetaProgressRules.Describe(modifier)}"
                        : $"{MetaProgressRules.Label(modifier)} · 진행도 {MetaProgressRules.UnlockAt(modifier)}에 열림"
                };
                button.AddToClassList(MetaModifierClass);
                button.EnableInClassList(MetaEquippedClass, equipped);
                button.SetEnabled(unlocked && (equipped || profile.equipped.Count < slots));
                _metaModifierList.Add(button);
            }
        }

        private void ToggleModifier(MetaModifier modifier)
        {
            var changed = MetaProfileStore.ToggleEquip(modifier);
            GameSfxPlayer.Play(changed ? GameSfxCue.UiConfirm : GameSfxCue.UiFail);
            RefreshMeta();
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
