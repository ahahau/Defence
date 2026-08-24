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
            TriggerGameOver(evt.MainUnit);
        }

        private void HandleBankruptcy(BankruptcyEvent evt)
        {
            Debug.Log($"부채 {evt.CurrentDebt}G가 한도 {evt.DebtLimit}G를 넘겨 파산했습니다.", this);
            TriggerGameOver(null);
        }

        private void TriggerGameOver(MainUnit mainUnit)
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            gameStateEventChannel.RaiseEvent(new GameOverEvent(mainUnit));

            if (pauseOnGameOver)
                Time.timeScale = 0f;
        }
    }
}
