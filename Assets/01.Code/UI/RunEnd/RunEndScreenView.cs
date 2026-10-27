using System.Collections.Generic;
using Code.Audio;
using Code.Core;
using Code.Manager;
using Code.Progression;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.UI
{
    /// <summary>
    /// 판 종료 결과 화면(uGUI, A안, 화면 가운데 카드). <see cref="BossWavePresenter.RunEndRequested"/>를 받으면 열린다.
    /// 왼쪽은 이번 판 전과, 오른쪽은 진행도 막대와 이번 판으로 새로 열린 것, 바닥은 "타이틀로"·"새 판 시작".
    /// 판이 끝난 화면이라 닫기 버튼·ESC가 없다. 시간은 <see cref="BossWavePresenter"/>가 이미 멈춰 두었고, 떠날 때 되돌린다.
    ///
    /// 예전에는 판이 끝나도 그리는 화면이 없어 게임이 멈춘 채로 남았다.
    /// </summary>
    public sealed class RunEndScreenView : MonoBehaviour
    {
        [SerializeField] private BossWavePresenter bossWave;
        [SerializeField] private RunSummarySystem runSummary;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private GameSpeedController speedController;
        [SerializeField, Tooltip("새로 열린 부하·시설을 고를 해금 목록.")]
        private DungeonUnlockCatalogSO unlockCatalog;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text headline;
        [SerializeField] private TMP_Text days;
        [SerializeField] private GameObject bestBadge;
        [SerializeField] private TMP_Text bestNote;
        [SerializeField] private TMP_Text statLabels;
        [SerializeField] private TMP_Text statValues;
        [SerializeField] private TMP_Text progressGain;
        [SerializeField] private TMP_Text progressRange;
        [SerializeField] private UnityEngine.UI.Image progressBefore;
        [SerializeField] private UnityEngine.UI.Image progressAfter;
        [SerializeField] private TMP_Text progressNote;
        [SerializeField] private RunEndUnlockSlot[] unlockSlots;
        [SerializeField] private TMP_Text unlockEmpty;
        [SerializeField] private UiFrameButton titleButton;
        [SerializeField] private UiFrameButton restartButton;
        [SerializeField] private UiFrameButton detailsButton;

        private readonly List<RunEndUnlockViewModel> _unlocks = new();
        private RunEndViewModel _model;
        private bool _detailsOpen;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (bossWave == null)
                bossWave = SceneReferenceFallback.Find<BossWavePresenter>(this, nameof(bossWave));
            if (speedController == null)
                speedController = GameSpeedController.Current;

            if (root != null)
                root.SetActive(false);
        }

        private void OnEnable()
        {
            detailsButton?.Button.onClick.AddListener(ToggleDetails);
            titleButton.Button.onClick.AddListener(GoToTitle);
            restartButton.Button.onClick.AddListener(StartNewRun);
            if (bossWave != null)
                bossWave.RunEndRequested += Show;
        }

        private void OnDisable()
        {
            detailsButton?.Button.onClick.RemoveListener(ToggleDetails);
            titleButton.Button.onClick.RemoveListener(GoToTitle);
            restartButton.Button.onClick.RemoveListener(StartNewRun);
            if (bossWave != null)
                bossWave.RunEndRequested -= Show;
        }

        private void Show(string runTitle, string runHeadline, bool victory)
        {
            var meta = MetaProfileStore.LastResult;
            _unlocks.Clear();
            if (meta.HasValue)
            {
                var before = meta.Value.ProgressBefore;
                var after = meta.Value.TotalProgress;
                RunEndPresenter.AddCatalogUnlocks(unlockCatalog != null ? unlockCatalog.Entries : null, before, after, _unlocks);
                RunEndPresenter.AddModifierUnlocks(before, after, _unlocks);
            }

            var stats = runSummary != null
                ? new RunEndStats(runSummary.WavesFought, runSummary.Invaders, runSummary.Kills, runSummary.DamageDealt,
                    runSummary.DamageTaken, runSummary.CriticalHits, runSummary.PeakDebt)
                : default;
            var model = RunEndPresenter.Build(runTitle, runHeadline, victory, dayManager != null ? dayManager.CurrentDay : 0,
                stats, meta, _unlocks);
            Render(model);

            root.SetActive(true);
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
        }

        private void Render(RunEndViewModel model)
        {
            _model = model;
            _detailsOpen = false;
            title.text = model.Title;
            title.color = model.Victory ? HudPalette.Accent : HudPalette.Danger;
            headline.text = model.Headline;
            days.text = model.Days;
            bestBadge.SetActive(model.NewBest);
            bestNote.text = model.BestNote;
            RefreshStats();

            progressGain.text = model.ProgressGain;
            progressRange.text = model.ProgressRange;
            progressBefore.fillAmount = model.BeforeRatio;
            progressAfter.fillAmount = model.AfterRatio;
            progressNote.text = model.ProgressNote;

            for (var i = 0; i < unlockSlots.Length; i++)
            {
                if (i < model.Unlocks.Count)
                    unlockSlots[i].Bind(model.Unlocks[i]);
                else
                    unlockSlots[i].Hide();
            }

            unlockEmpty.text = model.UnlockEmpty;
        }

        private void ToggleDetails()
        {
            _detailsOpen = !_detailsOpen;
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            RefreshStats();
        }

        private void RefreshStats()
        {
            if (_model == null)
                return;

            statLabels.text = _detailsOpen ? _model.StatLabels : _model.SummaryStatLabels;
            statValues.text = _detailsOpen ? _model.StatValues : _model.SummaryStatValues;
            detailsButton?.SetLabel(_detailsOpen ? "통계 접기" : "전체 통계");
            detailsButton?.SetSelected(_detailsOpen);
        }

        private void StartNewRun()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            // 판 저장은 판이 끝날 때 이미 지웠다. 게임 씬을 새로 열면 새 판이다(메타 보정은 MetaRunStarter가 건다).
            LeaveTo(TitleMenuActions.GameSceneName);
        }

        private void GoToTitle()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            LeaveTo(TitleMenuActions.TitleSceneName);
        }

        /// <summary>멈춘 채로 나가면 다음 판이 멈춘 채로 시작한다. 배속을 되돌린 뒤 씬을 바꾼다.</summary>
        private void LeaveTo(string sceneName)
        {
            root.SetActive(false);
            speedController?.ResetToNormal();
            SceneManager.LoadScene(sceneName);
        }
    }
}
