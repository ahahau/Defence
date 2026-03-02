using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Enemies;
using _01.Code.WaveSystem;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private WaveDataSO database;
    [SerializeField] private EnemySpawner spawner;

    [Header("Options")]
    [SerializeField] private bool autoStart = true;

    public event Action<int> OnWaveStarted;   
    public event Action<int> OnWaveCleared;   
    public event Action OnAllWavesCleared;

    private readonly HashSet<Enemy> _alive = new();
    private Coroutine _routine;
    private int _currentWaveIndex = -1;
    private bool _isRunning;

    private void Start()
    {
        if (autoStart) StartWaves();
    }

    public void StartWaves()
    {
        if (_isRunning) return;
        if (database == null || database.waves == null || database.waves.Count == 0)
        {
            Debug.LogWarning("WaveManager: database is empty");
            return;
        }
        if (spawner == null)
        {
            Debug.LogWarning("WaveManager: spawner is null");
            return;
        }

        _isRunning = true;
        _routine = StartCoroutine(RunWaves());
    }

    public void StopWaves()
    {
        _isRunning = false;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
    }

    private IEnumerator RunWaves()
    {
        for (int i = 0; i < database.waves.Count; i++)
        {
            _currentWaveIndex = i;
            var wave = database.waves[i];

            if (wave.preDelay > 0) yield return new WaitForSeconds(wave.preDelay);

            OnWaveStarted?.Invoke(i + 1);

            // spawn groups
            foreach (var g in wave.groups)
            {
                if (g == null || g.enemyPrefab == null) continue;

                if (g.startDelay > 0) yield return new WaitForSeconds(g.startDelay);

                for (int c = 0; c < g.count; c++)
                {
                    var enemy = spawner.EnemySpawn(g.startDelay);
                    if (g.interval > 0) yield return new WaitForSeconds(g.interval);
                }
            }
            yield return new WaitUntil(() => _alive.Count == 0);

            OnWaveCleared?.Invoke(i + 1);

            if (wave.postDelay > 0) yield return new WaitForSeconds(wave.postDelay);
        }

        _isRunning = false;
        OnAllWavesCleared?.Invoke();
    }

}