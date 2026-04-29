using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class GameOverManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
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
        }

        private void OnDisable()
        {
            gameStateEventChannel.RemoveListener<MainUnitDefeatedEvent>(HandleMainUnitDefeated);
        }

        private void HandleMainUnitDefeated(MainUnitDefeatedEvent evt)
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            gameStateEventChannel.RaiseEvent(new GameOverEvent(evt.MainUnit));

            if (pauseOnGameOver)
                Time.timeScale = 0f;
        }
    }
}
