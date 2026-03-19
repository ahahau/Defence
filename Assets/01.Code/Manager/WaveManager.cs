using System;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

// Rule: Wave flow should be controlled only by WaveManager.
namespace _01.Code.Manager
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;

        public event Action OnWaveStarted;
        public event Action OnWaveCleared;

        private bool _isRunning;

        /// <summary>
        /// 이 함수는 웨이브 요청과 웨이브 클리어 이벤트를 구독합니다
        /// </summary>
        public void Initialize()
        {
            waveEventChannel.AddListener<WaveStartRequestedEvent>(HandleWaveStartRequestedEvent);
            waveEventChannel.AddListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }

        private void OnDestroy()
        {
            waveEventChannel.RemoveListener<WaveStartRequestedEvent>(HandleWaveStartRequestedEvent);
            waveEventChannel.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }

        public void StartWaves()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            OnWaveStarted?.Invoke();
            waveEventChannel.RaiseEvent(WaveEvents.WaveStartedEvent);
        }

        private void HandleWaveClearedEvent(WaveClearedEvent _)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            OnWaveCleared?.Invoke();
        }

        /// <summary>
        /// 이 함수는 외부의 웨이브 시작 요청을 실제 시작 함수로 연결합니다
        /// </summary>
        private void HandleWaveStartRequestedEvent(WaveStartRequestedEvent _)
        {
            StartWaves();
        }
    }
}
