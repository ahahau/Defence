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
            Time.timeScale = 1f;
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
            TriggerGameOver(null, $"부채 {evt.CurrentDebt}G가 한도 {evt.DebtLimit}G를 넘겨 파산했습니다");
        }

        private void TriggerGameOver(MainUnit mainUnit, string reason)
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            gameStateEventChannel.RaiseEvent(new GameOverEvent(mainUnit));

            // 여태 패배 화면이 없어 게임이 멈춘 채로 남았다. 승리와 같은 패널에 결과를 띄운다.
            var presenter = WaveManager.Current != null ? WaveManager.Current.BossPresenter : null;
            if (presenter != null)
                presenter.ShowDefeatPanel(reason);
            else if (pauseOnGameOver)
                Time.timeScale = 0f;
        }
    }
}
