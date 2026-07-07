using System.Collections;
using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Manager
{
    [RequireComponent(typeof(BossWavePresenter))]
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private WaveConfigSO waveConfig;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private Enemy[] enemyPrefabs;
        [SerializeField] private EnemyDataSO[] enemyDataPool;
        [Header("Adventurer Parties")]
        [SerializeField, Tooltip("설정하면 웨이브가 랜덤 파티 구성(순서대로)으로 스폰된다. 비어있으면 enemyDataPool 랜덤.")]
        private AdventurerPartySO[] parties;
        [Header("Party Group Spawn")]
        [SerializeField, Tooltip("파티를 한 마리씩이 아니라 그룹 단위로 몰려오게 스폰한다. 그룹 간 간격은 spawnInterval × 그룹 크기로 늘어나 전체 스폰량은 유지된다.")]
        private bool spawnAsGroup = true;
        [SerializeField, Min(1), Tooltip("파티가 없을 때 한 그룹으로 스폰할 마릿수")]
        private int fallbackGroupSize = 3;
        [SerializeField, Min(0f), Tooltip("그룹 내 멤버 간 스폰 간격(초). 우르르 들어오는 연출용")]
        private float memberSpawnDelay = 0.15f;
        [SerializeField, Min(0f), Tooltip("파티원이 서로 겹치지 않게 흩어지는 대형 반경")]
        private float formationSpread = 0.35f;
        [SerializeField, Min(0)] private int treasuryGoldLoss = 10;
        [Header("Enemy Level Scaling")]
        [SerializeField, Min(0)] private int enemyHealthPerLevel = 2;
        [SerializeField, Min(0)] private int enemyAttackPerLevel = 1;
        [Header("Boss Wave")]
        [SerializeField, Min(1f), Tooltip("보스 승격 체력 배율(일차 스케일링 이후 적용).")]
        private float bossHealthMultiplier = 6f;
        [SerializeField, Min(1f), Tooltip("보스 승격 공격력 배율.")]
        private float bossAttackMultiplier = 2f;
        [SerializeField, Min(1f), Tooltip("보스 거대화 배율.")]
        private float bossVisualScale = 1.6f;
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
        private Coroutine _groupSpawnCoroutine;
        private float _currentGroupInterval;
        private readonly List<Enemy> _activeEnemies = new();
        private readonly List<EnemyDataSO> _partyQueue = new();
        private int _partyIndex;
        private bool _isWaitingForRewardPanel;
        private bool _isDestroying;
        [SerializeField] private BossWavePresenter bossPresenter;
        private Enemy _bossEnemy;
        private bool _isBossWave;
        private bool _isFinalWave;
        private bool _bossSpawned;
        private bool _isGameCleared;
        private int _waveEnemyCount;
        private int _waveKillCount;
        private int _waveDamageDealt;
        private int _waveDamageTaken;
        private int _waveCriticalHitCount;

        private void Awake()
        {
            bossPresenter ??= GetComponent<BossWavePresenter>();
            if (bossPresenter == null)
            {
                Debug.LogError($"{nameof(WaveManager)} requires {nameof(BossWavePresenter)} on the same GameObject.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _isDestroying = false;
            dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            nodeEventChannel.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
            Health.AnyDamaged += HandleAnyDamage;
        }

        private void OnDisable()
        {
            dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
            nodeEventChannel.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
            nodeEventChannel.RemoveListener<PortalRemovedEvent>(HandlePortalRemoved);
            Health.AnyDamaged -= HandleAnyDamage;
            StopRunningWave();
            UnsubscribeRewardPanel();
            ClearEnemyTrackers();
        }

        private void OnDestroy()
        {
            _isDestroying = true;
            StopRunningWave();
            UnsubscribeRewardPanel();
            ClearEnemyTrackers();
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
            ResetWaveResults(entry.enemyCount);
            _currentClearGoldReward = entry.clearGoldReward;
            _isWaveRunning = true;
            _isWaitingForRewardPanel = false;
            _activeEnemies.Clear();
            _isBossWave = waveConfig != null && waveConfig.IsBossDay(_currentDay);
            _isFinalWave = waveConfig != null && waveConfig.IsFinalDay(_currentDay);
            _bossEnemy = null;
            _bossSpawned = false;
            SetupPartyForWave();

            waveEventChannel.RaiseEvent(new WaveStartedEvent(_currentDay, entry.enemyCount));

            if (_isBossWave)
            {
                waveEventChannel.RaiseEvent(new BossWaveStartedEvent(_currentDay, _isFinalWave));
                EnsureBossPresenter().ShowBossBanner(_currentDay, _isFinalWave);
            }

            var spawnInterval = Mathf.Max(0.5f, entry.spawnInterval);
            _currentGroupInterval = spawnInterval;

            if (spawnAsGroup)
                SpawnNextGroup(spawnInterval);
            else
                SpawnNextEnemyIfNeeded(false);

            var spawnTimer = 0f;

            while (_isWaveRunning)
            {
                yield return null;

                if (!_isWaveRunning)
                    break;

                spawnTimer += Time.deltaTime;

                if (spawnTimer >= (spawnAsGroup ? _currentGroupInterval : spawnInterval))
                {
                    spawnTimer = 0f;
                    if (spawnAsGroup)
                        SpawnNextGroup(spawnInterval);
                    else
                        SpawnNextEnemyIfNeeded(false);
                }

                RemoveMissingEnemies();
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

            SpawnEnemy(Vector3.zero);
            CompleteWaveIfCleared(stopRunningCoroutine);
        }

        /// <summary>파티 전체를 한 그룹으로 몰아서 스폰한다. 그룹 간 간격은 spawnInterval × 그룹 크기로 늘려 전체 스폰량을 유지한다.</summary>
        private void SpawnNextGroup(float spawnInterval)
        {
            if (_portalNode == null || _remainingSpawns <= 0)
            {
                CompleteWaveIfCleared(false);
                return;
            }

            // 그룹마다 파티를 새로 뽑아 매번 다른 조합이 몰려오게 한다
            SetupPartyForWave();
            var groupSize = _partyQueue.Count > 0 ? _partyQueue.Count : Mathf.Max(1, fallbackGroupSize);
            // 보스는 파티 큐(호위) 밖에서 첫 스폰을 차지하므로 그룹에 한 자리 더.
            if (_isBossWave && !_bossSpawned)
                groupSize += 1;
            groupSize = Mathf.Min(groupSize, _remainingSpawns);
            _currentGroupInterval = spawnInterval * groupSize;

            if (_groupSpawnCoroutine != null)
                StopCoroutine(_groupSpawnCoroutine);
            _groupSpawnCoroutine = StartCoroutine(SpawnGroupRoutine(groupSize));
        }

        private IEnumerator SpawnGroupRoutine(int groupSize)
        {
            for (var i = 0; i < groupSize; i++)
            {
                if (!_isWaveRunning || _portalNode == null)
                    break;

                if (!SpawnEnemy(FormationOffsetFor(i, groupSize)))
                    break;

                if (memberSpawnDelay > 0f && i < groupSize - 1)
                    yield return new WaitForSeconds(memberSpawnDelay);
            }

            _groupSpawnCoroutine = null;
            CompleteWaveIfCleared(false);
        }

        /// <summary>멤버를 포탈 주위에 원형으로 흩어 배치해 같은 노드에서도 겹쳐 보이지 않게 한다.</summary>
        private Vector3 FormationOffsetFor(int index, int groupSize)
        {
            if (groupSize <= 1 || formationSpread <= 0f)
                return Vector3.zero;

            var angle = 360f / groupSize * index * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * formationSpread;
        }

        private bool SpawnEnemy(Vector3 formationOffset)
        {
            if (_portalNode == null || _remainingSpawns <= 0)
                return false;

            var spawnPos = (_portalNode.EnemyPosition != null
                ? _portalNode.EnemyPosition.position
                : _portalNode.transform.position) + formationOffset;

            // 데이터를 먼저 뽑고, 그 데이터 전용 프리팹이 있으면 그것을 스폰(종류↔프리팹 짝 보장).
            // 없으면 기존 방식(공용 프리팹 풀)으로 폴백한다.
            // 보스 웨이브의 첫 스폰은 보스 — 전용 파티가 없어도 풀에서 가장 강한 적을 승격시킨다.
            var isBossSpawn = _isBossWave && !_bossSpawned;
            var enemyData = isBossSpawn ? ResolveBossData() : ResolveEnemyData();
            var prefab = enemyData != null && enemyData.Prefab != null
                ? enemyData.Prefab
                : ResolveEnemyPrefab();
            if (prefab == null)
            {
                Debug.LogError($"{nameof(WaveManager)} requires at least one enemy prefab assigned.", this);
                _remainingSpawns = 0;
                return false;
            }

            Enemy enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
            enemy.DeathStarted += HandleWaveEnemyDeathStarted;
            enemy.Removed += HandleEnemyRemoved;
            enemy.ConfigureData(enemyData);
            enemy.ApplyWaveLevel(_currentDay, enemyHealthPerLevel, enemyAttackPerLevel);
            _remainingSpawns--;

            if (isBossSpawn)
            {
                _bossSpawned = true;
                _bossEnemy = enemy;
                enemy.PromoteToBoss(bossHealthMultiplier, bossAttackMultiplier, bossVisualScale);
                enemy.DeathStarted += HandleBossDeathStarted;
            }

            // Initialize가 mover 위치를 노드 위치로 스냅하므로, 그 전에 대형 오프셋을 넣어야 한다
            if (enemy.Mover != null)
                enemy.Mover.FormationOffset = formationOffset;

            enemy.Initialize(_portalNode, costEventChannel, treasuryGoldLoss, nodeEventChannel);
            if (enemy != null)
                _activeEnemies.Add(enemy);

            return true;
        }

        private Enemy ResolveEnemyPrefab()
        {
            if (enemyPrefabs != null && enemyPrefabs.Length > 0)
            {
                var candidates = new List<Enemy>();
                foreach (var prefab in enemyPrefabs)
                {
                    if (prefab != null)
                        candidates.Add(prefab);
                }

                if (candidates.Count > 0)
                    return candidates[Random.Range(0, candidates.Count)];
            }

            return enemyPrefab;
        }

        /// <summary>랜덤 모험가 파티를 골라 등장 순서 큐를 채운다. 파티 없으면 큐 비움(풀 랜덤).
        /// 단체 스폰 모드에선 그룹마다, 아니면 웨이브 시작 시 한 번 호출된다.</summary>
        private void SetupPartyForWave()
        {
            _partyQueue.Clear();
            _partyIndex = 0;

            // 보스 웨이브 첫 그룹은 보스 파티의 호위(첫 멤버=보스는 SpawnEnemy가 따로 처리) 구성으로.
            if (_isBossWave && !_bossSpawned && waveConfig != null && waveConfig.BossParty != null
                && waveConfig.BossParty.Members != null && waveConfig.BossParty.Members.Length > 1)
            {
                for (var i = 1; i < waveConfig.BossParty.Members.Length; i++)
                {
                    if (waveConfig.BossParty.Members[i] != null)
                        _partyQueue.Add(waveConfig.BossParty.Members[i]);
                }

                if (_partyQueue.Count > 0)
                    return;
            }

            if (parties == null || parties.Length == 0)
                return;

            var validParties = new List<AdventurerPartySO>();
            foreach (var party in parties)
            {
                if (party != null && party.Members != null && party.Members.Length > 0)
                    validParties.Add(party);
            }

            if (validParties.Count == 0)
                return;

            var chosen = validParties[Random.Range(0, validParties.Count)];
            foreach (var member in chosen.Members)
            {
                if (member != null)
                    _partyQueue.Add(member);
            }
        }

        /// <summary>보스 데이터 — 설정된 보스 파티의 첫 멤버, 없으면 풀에서 최대 체력 적(승격은 SpawnEnemy가).</summary>
        private EnemyDataSO ResolveBossData()
        {
            var bossParty = waveConfig != null ? waveConfig.BossParty : null;
            if (bossParty != null && bossParty.Members != null && bossParty.Members.Length > 0
                && bossParty.Members[0] != null)
                return bossParty.Members[0];

            EnemyDataSO strongest = null;
            if (enemyDataPool != null)
            {
                foreach (var enemyData in enemyDataPool)
                {
                    if (enemyData == null)
                        continue;

                    if (strongest == null || enemyData.MaxHealth > strongest.MaxHealth)
                        strongest = enemyData;
                }
            }

            return strongest != null ? strongest : ResolveEnemyData();
        }

        private EnemyDataSO ResolveEnemyData()
        {
            // 파티가 설정된 웨이브: 구성 순서대로(부족하면 순환) 스폰해 역할 섞인 그룹이 함께 온다.
            if (_partyQueue.Count > 0)
            {
                var data = _partyQueue[_partyIndex % _partyQueue.Count];
                _partyIndex++;
                return data;
            }

            if (enemyDataPool == null || enemyDataPool.Length == 0)
                return null;

            var candidates = new List<EnemyDataSO>();
            foreach (var enemyData in enemyDataPool)
            {
                if (enemyData != null)
                    candidates.Add(enemyData);
            }

            return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        }

        private void HandleEnemyRemoved(Enemy enemy)
        {
            if (this == null || _isDestroying)
                return;

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
            if (_isDestroying)
                return;

            if (!_isWaveRunning)
                return;
            
            _isWaveRunning = false;
            _remainingSpawns = 0;
            _activeEnemies.Clear();
            
            if (stopRunningCoroutine && _waveCoroutine != null)
            {
                StopCoroutine(_waveCoroutine);
                _waveCoroutine = null;
            }
            else
            {
                _waveCoroutine = null;
            }

            // 최종 보스 웨이브 클리어 = 승리 — 보상 패널 대신 승리 패널(시네마틱이 끝난 뒤).
            if (_isFinalWave && !_isGameCleared)
            {
                _isGameCleared = true;

                if (_currentClearGoldReward > 0)
                    costEventChannel?.RaiseEvent(new GoldEarnedEvent(_currentClearGoldReward, GoldChangeSource.WaveReward));

                waveEventChannel?.RaiseEvent(new GameClearedEvent(_currentDay));
                StartCoroutine(ShowVictoryAfterCinematic());
                RaiseWaveEnded();
                return;
            }

            var rewardPanel = EnsureRewardPanel();
            if (rewardPanel != null)
            {
                _isWaitingForRewardPanel = true;
                rewardPanel.Closed -= HandleRewardPanelClosed;
                rewardPanel.Closed += HandleRewardPanelClosed;
                rewardPanel.ShowWaveResult(
                    _currentClearGoldReward,
                    _currentDay,
                    _waveEnemyCount,
                    _waveKillCount,
                    _waveDamageDealt,
                    _waveDamageTaken,
                    _waveCriticalHitCount,
                    _currentDay > 0 && _currentDay % 3 == 0);
                rewardPanel.transform.SetAsLastSibling();

                if (rewardPanel.IsShowingReward)
                    return;
            
                rewardPanel.Closed -= HandleRewardPanelClosed;
                _isWaitingForRewardPanel = false;
            }

            if (_currentClearGoldReward > 0)
                costEventChannel?.RaiseEvent(new GoldEarnedEvent(_currentClearGoldReward, GoldChangeSource.WaveReward));

            RaiseWaveEnded();
        }

        private void HandleRewardPanelClosed()
        {
            if (this == null || _isDestroying)
                return;

            UnsubscribeRewardPanel();
            
            if (!_isWaitingForRewardPanel)
                return;

            _isWaitingForRewardPanel = false;
            RaiseWaveEnded();
        }

        private BossWavePresenter EnsureBossPresenter()
        {
            return bossPresenter;
        }

        /// <summary>보스 사망 시작(디졸브 직전) — 슬로모 + 카메라 줌인으로 쓰러지는 모습을 보여준다.</summary>
        private void HandleBossDeathStarted(Enemy boss)
        {
            if (boss != null)
                boss.DeathStarted -= HandleBossDeathStarted;

            if (this == null || _isDestroying || boss == null)
                return;

            EnsureBossPresenter().PlayBossDeathCinematic(boss.transform.position);
        }

        /// <summary>승리 패널은 보스 처치 시네마틱이 끝난 뒤에 띄운다(줌인 도중 팝업 방지).</summary>
        private IEnumerator ShowVictoryAfterCinematic()
        {
            var presenter = EnsureBossPresenter();
            while (presenter.IsCinematicRunning)
                yield return null;

            presenter.ShowVictoryPanel(_currentDay);
        }

        private void RaiseWaveEnded()
        {
            if (_isDestroying || waveEventChannel == null)
                return;

            waveEventChannel.RaiseEvent(new WaveEndedEvent(_currentDay, _currentClearGoldReward));
        }

        private void ResetWaveResults(int enemyCount)
        {
            _waveEnemyCount = Mathf.Max(0, enemyCount);
            _waveKillCount = 0;
            _waveDamageDealt = 0;
            _waveDamageTaken = 0;
            _waveCriticalHitCount = 0;
        }

        private void HandleWaveEnemyDeathStarted(Enemy enemy)
        {
            if (enemy != null)
                enemy.DeathStarted -= HandleWaveEnemyDeathStarted;

            if (_isWaveRunning)
                _waveKillCount++;
        }

        private void HandleAnyDamage(Health damagedHealth, int damage, bool isCritical)
        {
            if (!_isWaveRunning || damagedHealth == null || damage <= 0)
                return;

            if (damagedHealth.GetComponentInParent<Enemy>() != null)
            {
                _waveDamageDealt += damage;
                if (isCritical)
                    _waveCriticalHitCount++;
                return;
            }

            if (damagedHealth.GetComponentInParent<Unit>() != null)
                _waveDamageTaken += damage;
        }

        private WaveRewardPanelView EnsureRewardPanel()
        {
            if (_rewardPanel != null)
                return _rewardPanel;
            
            var parent = ResolveRewardPanelParent();
            if (parent == null)
                return null;

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

            Debug.LogError($"{nameof(WaveManager)} requires an explicit reward panel parent.", this);
            return null;
        }

        private void StopRunningWave()
        {
            if (_waveCoroutine != null)
            {
                StopCoroutine(_waveCoroutine);
                _waveCoroutine = null;
            }

            if (_groupSpawnCoroutine != null)
            {
                StopCoroutine(_groupSpawnCoroutine);
                _groupSpawnCoroutine = null;
            }

            _isWaveRunning = false;
            _isWaitingForRewardPanel = false;
        }

        private void UnsubscribeRewardPanel()
        {
            if (_rewardPanel != null)
                _rewardPanel.Closed -= HandleRewardPanelClosed;
        }

        private void ClearEnemyTrackers()
        {
            if (_bossEnemy != null)
                _bossEnemy.DeathStarted -= HandleBossDeathStarted;

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null)
                    continue;

                enemy.DeathStarted -= HandleWaveEnemyDeathStarted;
                enemy.Removed -= HandleEnemyRemoved;
            }

            _activeEnemies.Clear();
        }
    }
}
