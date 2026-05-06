using System.Collections;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private WaveConfigSO waveConfig;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField, Min(0)] private int treasuryGoldLoss = 10;

        private Node _portalNode;
        public bool HasPortal => _portalNode != null;
        private int _currentDay;
        private int _aliveEnemies;
        private Coroutine _waveCoroutine;
        private readonly List<Enemy> _activeEnemies = new();

        private void OnEnable()
        {
            dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            nodeEventChannel.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
        }

        private void OnDisable()
        {
            dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
            nodeEventChannel.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel.RemoveListener<PortalRemovedEvent>(HandlePortalRemoved);
        }

        private void HandlePortalInstalled(PortalInstalledEvent evt)
        {
            _portalNode = evt.Node;
        }

        private void HandlePortalRemoved(PortalRemovedEvent evt)
        {
            _portalNode = null;
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            _currentDay = evt.Day;

            if (_portalNode == null || waveConfig == null)
                return;

            var entry = waveConfig.GetWaveForDay(evt.Day);
            if (entry == null)
                return;

            if (_waveCoroutine != null)
                StopCoroutine(_waveCoroutine);

            _waveCoroutine = StartCoroutine(RunWave(entry));
        }

        private IEnumerator RunWave(WaveConfigSO.WaveEntry entry)
        {
            _aliveEnemies = entry.enemyCount;
            _activeEnemies.Clear();

            waveEventChannel.RaiseEvent(new WaveStartedEvent(_currentDay, entry.enemyCount));

            SpawnNextEnemy();

            int remaining = entry.enemyCount - 1;

            while (_aliveEnemies > 0)
            {
                yield return new WaitForSeconds(entry.enemyTurnInterval);

                if (_aliveEnemies <= 0)
                    break;

                // 모든 적 동시에 한 칸 이동
                foreach (var enemy in _activeEnemies)
                {
                    if (enemy != null)
                        enemy.TakeTurn();
                }

                // 이전 적이 포탈에서 이동한 후 다음 적 스폰
                if (remaining > 0)
                {
                    SpawnNextEnemy();
                    remaining--;
                }
            }

            _waveCoroutine = null;
            waveEventChannel.RaiseEvent(new WaveEndedEvent(_currentDay, entry.clearGoldReward));
        }

        private void SpawnNextEnemy()
        {
            if (_portalNode == null)
                return;

            var spawnPos = _portalNode.EnemyPosition != null
                ? _portalNode.EnemyPosition.position
                : _portalNode.transform.position;

            Enemy enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

            var captured = enemy;
            var tracker = enemy.GetComponent<WaveEnemyTracker>();
            tracker.OnEnemyDied = () =>
            {
                _activeEnemies.Remove(captured);
                _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);
            };

            enemy.Initialize(_portalNode, costEventChannel, treasuryGoldLoss);
            if (enemy != null)
                _activeEnemies.Add(enemy);
        }
    }
}
