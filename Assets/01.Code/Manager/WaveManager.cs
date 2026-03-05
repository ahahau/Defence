using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Enemies;
using _01.Code.Manager;
using _01.Code.WaveSystem;
using UnityEngine;
// ** 규칙 ** 웨이브에 관련한건 WaveManager에서만 관리한다. 이외의 다른 
public class WaveManager : MonoBehaviour, IManageable
{
    public event Action OnWaveStarted;   
    public event Action OnWaveCleared;   
    private bool _isRunning;

    public void Initialize()
    {
        
    }
    

    public void StartWaves()
    {
        if (_isRunning)
            return;
        _isRunning = true;
        RunWaves();
    }

    private void RunWaves()
    {
        OnWaveStarted?.Invoke();
        GameManager.Instance.EnemySpawnerManager.RunWaves();
    }


    public void WaveEnd()
    {
        if (!_isRunning) return;
        _isRunning = false;
        OnWaveCleared?.Invoke();
    }
}