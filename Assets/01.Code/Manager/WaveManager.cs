using System;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

// Rule: Wave flow should be controlled only by WaveManager.
public class WaveManager : MonoBehaviour, _01.Code.Manager.IManageable
{
    [SerializeField] private GameEventChannelSO waveEventChannel;

    public event Action OnWaveStarted;
    public event Action OnWaveCleared;

    private bool _isRunning;

    public void Initialize()
    {
        if (waveEventChannel != null)
        {
            waveEventChannel.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
            waveEventChannel.AddListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }
    }

    private void OnDestroy()
    {
        if (waveEventChannel != null)
        {
            waveEventChannel.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }
    }

    public void StartWaves()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        Debug.Log("Wave started");
        OnWaveStarted?.Invoke();
        waveEventChannel?.RaiseEvent(WaveEvents.WaveStartedEvent);
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
}
