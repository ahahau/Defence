using System.Collections;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.UI;
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
        [Header("Enemy Level Scaling")]
        [SerializeField, Min(0)] private int enemyHealthPerLevel = 2;
        [SerializeField, Min(0)] private int enemyAttackPerLevel = 1;
        [Header("Reward")]
        [SerializeField] private WaveRewardPanelView rewardPanelPrefab;
        [SerializeField] private Transform rewardPanelParent;

        private Node _portalNode;
        public bool HasPortal => _portalNode != null;
        private int _currentDay;
        private int _remainingSpawns;
        private int _currentClearGoldReward;
        private bool _isWaveRunning;
        private Coroutine _waveCoroutine;
        private WaveRewardPanelView _rewardPanel;
        private readonly List<Enemy> _activeEnemies = new();
        private readonly List<Enemy> _turnEnemies = new();
        private bool _isWaitingForRewardPanel;

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

        public bool CanStartWave(int day)
        {
            return _portalNode != null && waveConfig != null && waveConfig.GetWaveForDay(day) != null;
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            _currentDay = evt.Day;

            if (_portalNode == null || waveConfig == null)
            {
                _currentClearGoldReward = 0;
                RaiseWaveEnded();
                return;
            }

            var entry = waveConfig.GetWaveForDay(evt.Day);
            if (entry == null)
            {
                _currentClearGoldReward = 0;
                RaiseWaveEnded();
                return;
            }

            if (_waveCoroutine != null)
                StopCoroutine(_waveCoroutine);

            _waveCoroutine = StartCoroutine(RunWave(entry));
        }

        private IEnumerator RunWave(WaveConfigSO.WaveEntry entry)
        {
            _remainingSpawns = Mathf.Max(0, entry.enemyCount);
            _currentClearGoldReward = entry.clearGoldReward;
            _isWaveRunning = true;
            _isWaitingForRewardPanel = false;
            _activeEnemies.Clear();

            waveEventChannel.RaiseEvent(new WaveStartedEvent(_currentDay, entry.enemyCount));

            SpawnNextEnemyIfNeeded(false);
            var spawnInterval = Mathf.Max(0.5f, entry.spawnInterval);
            var turnInterval = Mathf.Max(0.1f, entry.enemyTurnInterval);
            var spawnTimer = 0f;
            var turnTimer = 0f;

            while (_isWaveRunning)
            {
                yield return null;

                if (!_isWaveRunning)
                    break;

                var deltaTime = Time.deltaTime;
                spawnTimer += deltaTime;
                turnTimer += deltaTime;

                if (spawnTimer >= spawnInterval)
                {
                    spawnTimer = 0f;
                    SpawnNextEnemyIfNeeded(false);
                }

                if (turnTimer < turnInterval)
                {
                    CompleteWaveIfCleared(false);
                    continue;
                }

                turnTimer = 0f;
                RemoveMissingEnemies();

                _turnEnemies.Clear();
                _turnEnemies.AddRange(_activeEnemies);
                foreach (var enemy in _turnEnemies)
                {
                    if (enemy != null && _activeEnemies.Contains(enemy))
                        enemy.TakeTurn();
                }
                _turnEnemies.Clear();

                CompleteWaveIfCleared(false);
            }

            CompleteWave(false);
        }

        private void SpawnNextEnemyIfNeeded(bool stopRunningCoroutine)
        {
            if (_portalNode == null || _remainingSpawns <= 0)
            {
                CompleteWaveIfCleared(stopRunningCoroutine);
                return;
            }

            var spawnPos = _portalNode.EnemyPosition != null
                ? _portalNode.EnemyPosition.position
                : _portalNode.transform.position;

            Enemy enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            enemy.ApplyWaveLevel(_currentDay, enemyHealthPerLevel, enemyAttackPerLevel);
            _remainingSpawns--;
            
            var captured = enemy;
            var tracker = enemy.GetComponent<WaveEnemyTracker>();
            if (tracker == null)
                tracker = enemy.gameObject.AddComponent<WaveEnemyTracker>();
            
            tracker.OnEnemyDied = () =>
            {
                HandleEnemyRemoved(captured);
            };
            
            enemy.Initialize(_portalNode, costEventChannel, treasuryGoldLoss, nodeEventChannel);
            if (enemy != null)
                _activeEnemies.Add(enemy);

            CompleteWaveIfCleared(stopRunningCoroutine);
        }

        private void HandleEnemyRemoved(Enemy enemy)
        {
            if (!_isWaveRunning)
                return;

            _activeEnemies.Remove(enemy);
            CompleteWaveIfCleared(false);
        }

        private void RemoveMissingEnemies()
        {
            for (var i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                if (_activeEnemies[i] != null)
                    continue;

                _activeEnemies.RemoveAt(i);
            }
        }

        private void CompleteWaveIfCleared(bool stopRunningCoroutine)
        {
            RemoveMissingEnemies();

            if (_remainingSpawns <= 0 && _activeEnemies.Count <= 0)
                CompleteWave(stopRunningCoroutine);
        }

        private void CompleteWave(bool stopRunningCoroutine)
        {
            if (!_isWaveRunning)
                return;
            
            _isWaveRunning = false;
            _remainingSpawns = 0;
            _activeEnemies.Clear();
            _turnEnemies.Clear();
            
            if (stopRunningCoroutine && _waveCoroutine != null)
            {
                StopCoroutine(_waveCoroutine);
                _waveCoroutine = null;
            }
            else
            {
                _waveCoroutine = null;
            }

            var rewardPanel = EnsureRewardPanel();
            if (rewardPanel != null)
            {
                _isWaitingForRewardPanel = true;
                rewardPanel.Closed -= HandleRewardPanelClosed;
                rewardPanel.Closed += HandleRewardPanelClosed;
                rewardPanel.ShowGoldReward(_currentClearGoldReward, _currentDay > 0 && _currentDay % 3 == 0);
                rewardPanel.transform.SetAsLastSibling();

                if (rewardPanel.IsShowingReward)
                    return;
            
                rewardPanel.Closed -= HandleRewardPanelClosed;
                _isWaitingForRewardPanel = false;
            }

            if (_currentClearGoldReward > 0)
                costEventChannel?.RaiseEvent(new GoldEarnedEvent(_currentClearGoldReward));

            RaiseWaveEnded();
        }

        private void HandleRewardPanelClosed()
        {
            _rewardPanel.Closed -= HandleRewardPanelClosed;
            
            if (!_isWaitingForRewardPanel)
                return;

            _isWaitingForRewardPanel = false;
            RaiseWaveEnded();
        }

        private void RaiseWaveEnded()
        {
            waveEventChannel.RaiseEvent(new WaveEndedEvent(_currentDay, _currentClearGoldReward));
        }

        private WaveRewardPanelView EnsureRewardPanel()
        {
            if (_rewardPanel != null)
                return _rewardPanel;
            
            var parent = ResolveRewardPanelParent();
            _rewardPanel = FindExistingRewardPanel(parent);
            if (_rewardPanel != null)
            {
                _rewardPanel.Initialize(costEventChannel);
                _rewardPanel.gameObject.SetActive(false);
                return _rewardPanel;
            }

            if (rewardPanelPrefab == null)
            {
                Debug.LogError($"{nameof(WaveManager)} requires a reward panel prefab assigned.", this);
                return null;
            }

            _rewardPanel = Instantiate(rewardPanelPrefab, parent);
            _rewardPanel.name = rewardPanelPrefab.name;
            _rewardPanel.Initialize(costEventChannel);
            _rewardPanel.gameObject.SetActive(false);
            return _rewardPanel;
        }

        private WaveRewardPanelView FindExistingRewardPanel(Transform parent)
        {
            if (parent == null)
                return null;

            var panels = parent.GetComponentsInChildren<WaveRewardPanelView>(true);
            return panels.Length > 0 ? panels[0] : null;
        }

        private Transform ResolveRewardPanelParent()
        {
            if (rewardPanelParent != null)
                return rewardPanelParent;

            Debug.LogError($"{nameof(WaveManager)} requires a reward panel parent assigned.", this);
            return transform;
        }
    }
}
