using Code.Core;
using Code.Events;
using Code.Units;
using UnityEngine;

namespace Code.Manager
{
    public class GameOverManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField, Tooltip("부채 한도를 넘겨 파산했을 때도 게임오버로 잇기 위해 필요하다.")]
        private GameEventChannelSO costEventChannel;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private BossWavePresenter bossWavePresenter;
        [SerializeField] private GameSpeedController gameSpeedController;
        [SerializeField] private bool pauseOnGameOver = true;
        [SerializeField, Tooltip(GameScope.FieldTooltip)] private GameScope scope;

        public bool IsGameOver { get; private set; }

        private void Awake()
        {
            ResolveDependencies();
            IsGameOver = false;
            // 이전 판이 멈춘 채로 씬을 떠났을 수 있다. 컨트롤러보다 먼저 깨어날 수 있어 직접 푼다.
            Time.timeScale = GameSpeedController.NormalSpeed;
        } 

        private void OnEnable()
        {
            gameStateEventChannel.AddListener<MainUnitDefeatedEvent>(HandleMainUnitDefeated);
            if (costEventChannel != null)
                costEventChannel.AddListener<BankruptcyEvent>(HandleBankruptcy);
        }

        private void OnDisable()
        {
            gameStateEventChannel.RemoveListener<MainUnitDefeatedEvent>(HandleMainUnitDefeated);
            if (costEventChannel != null)
                costEventChannel.RemoveListener<BankruptcyEvent>(HandleBankruptcy);
        }

        private void HandleMainUnitDefeated(MainUnitDefeatedEvent evt)
        {
            TriggerGameOver(evt.MainUnit, "던전의 주인이 쓰러졌습니다");
        }

        private void HandleBankruptcy(BankruptcyEvent evt)
        {
            TriggerGameOver(null, string.IsNullOrEmpty(evt.Reason)
                ? $"청산일에 {evt.Owed}G를 내야 했지만 {evt.Gold}G뿐이라 파산했습니다"
                : evt.Reason);
        }

        /// <summary>보스 패배 조건처럼 유닛·금화가 아닌 이유로 판이 끝날 때.</summary>
        public void TriggerRunDefeat(string reason) => TriggerGameOver(null, reason);

        private void TriggerGameOver(MainUnit mainUnit, string reason)
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            ResolveDependencies();
            // 끝없는 판에서 패배가 곧 엔딩이다. 결과 화면이 읽기 전에 메타 진행을 정산한다.
            var dungeonGrade = GameScope.Resolve(scope, s => s.DungeonGrade, this, nameof(scope));
            Code.Progression.MetaProfileStore.RecordRunEnd(
                DayManager.Current != null ? DayManager.Current.CurrentDay : 0,
                dungeonGrade != null ? dungeonGrade.Grade : 0);
            waveManager?.StopForRunEnd();
            gameStateEventChannel.RaiseEvent(new GameOverEvent(mainUnit));

            // 여태 패배 화면이 없어 게임이 멈춘 채로 남았다. 승리와 같은 패널에 결과를 띄운다.
            if (bossWavePresenter != null)
                bossWavePresenter.ShowDefeatPanel(reason);
            else if (pauseOnGameOver)
            {
                // 결과 화면 없이 멈추는 경로다. 푸는 쪽이 없으므로 씬을 새로 띄워야 풀린다.
                if (gameSpeedController != null)
                    gameSpeedController.Suspend(this);
                else
                    Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// 씬에서 연결이 빠져 있어도 패배 화면이 뜨게 한다. 연결이 비면 시간만 멈추고 화면이 없어
        /// 파산·메인 유닛 사망이 그냥 멈춘 게임처럼 보였다. 패배 화면은 웨이브 매니저가 들고 있는 것을 쓴다.
        /// </summary>
        private void ResolveDependencies()
        {
            if (waveManager == null)
                waveManager = WaveManager.Current;
            if (bossWavePresenter == null && waveManager != null)
                bossWavePresenter = waveManager.BossPresenter;
            if (bossWavePresenter == null && IsGameOver)
                Debug.LogWarning($"{nameof(GameOverManager)}: 패배 화면({nameof(BossWavePresenter)})이 연결되지 않아 시간만 멈춥니다.", this);
            if (gameSpeedController == null)
                gameSpeedController = GameSpeedController.Current;
        }
    }
}
