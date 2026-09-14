using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Manager
{
    public class GameOverManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField, Tooltip("부채 한도를 넘겨 파산했을 때도 게임오버로 잇기 위해 필요하다.")]
        private GameEventChannelSO costEventChannel;
        [SerializeField] private bool pauseOnGameOver = true;

        public bool IsGameOver { get; private set; }

        private void Awake()
        {
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
            TriggerGameOver(null, $"청산일에 {evt.Owed}G를 내야 했지만 {evt.Gold}G뿐이라 파산했습니다");
        }

        private void TriggerGameOver(MainUnit mainUnit, string reason)
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            WaveManager.Current?.StopForRunEnd();
            gameStateEventChannel.RaiseEvent(new GameOverEvent(mainUnit));

            // 여태 패배 화면이 없어 게임이 멈춘 채로 남았다. 승리와 같은 패널에 결과를 띄운다.
            var presenter = WaveManager.Current != null ? WaveManager.Current.BossPresenter : null;
            if (presenter != null)
                presenter.ShowDefeatPanel(reason);
            else if (pauseOnGameOver)
            {
                // 결과 화면 없이 멈추는 경로다. 푸는 쪽이 없으므로 씬을 새로 띄워야 풀린다.
                if (GameSpeedController.Current != null)
                    GameSpeedController.Current.Suspend(this);
                else
                    Time.timeScale = 0f;
            }
        }
    }
}
