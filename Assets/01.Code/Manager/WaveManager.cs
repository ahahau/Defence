using System.Collections;
using System.Collections.Generic;
using _01.Code.BT;
using _01.Code.Buildings;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Enemies;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.Progression;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Manager
{
    [RequireComponent(typeof(BossWavePresenter))]
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Current { get; private set; }

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
        [Header("Village Conquest")]
        [SerializeField, Range(0f, 1f),
         Tooltip("마을을 모두 장악했을 때 줄어드는 침입자 비율. 0.4면 습격이 40%까지 줄어든다.")]
        private float maxWaveReductionFromConquest = 0.4f;

        [Header("Enemy Level Scaling")]
        [SerializeField, Min(0), Tooltip("일차마다 침입자에게 더해지는 최대 체력.")]
        private int enemyHealthPerLevel = 1;

        [SerializeField, Min(0),
         Tooltip("일차마다 더해지는 공격력. 0을 권장한다 — 일차 수만큼 누적되므로 1만 넣어도 " +
                 "18일차 침입자의 공격력이 4에서 18로 뛰어 수비대가 두세 대에 쓰러진다. " +
                 "후반을 조이려면 일차별 보스 항목의 배율을 쓰는 편이 정밀하다.")]
        private int enemyAttackPerLevel;
        [Header("Boss Wave")]
        [SerializeField, Min(1f), Tooltip("보스 승격 체력 배율(일차 스케일링 이후 적용).")]
        private float bossHealthMultiplier = 6f;
        [SerializeField, Min(1f), Tooltip("보스 승격 공격력 배율.")]
        private float bossAttackMultiplier = 2f;
        [SerializeField, Min(1f), Tooltip("보스 거대화 배율.")]
        private float bossVisualScale = 1.6f;

        private Node _portalNode;
        public bool HasPortal => _portalNode != null;
        public bool IsWaveRunning => _isWaveRunning;
        public bool IsBossWave => _isBossWave;
        public int TotalEnemyCount => Mathf.Max(0, _waveEnemyCount);
        public int KillCount => Mathf.Max(0, _waveKillCount);
        /// <summary>직전 웨이브 전과. 정산 보고서가 읽어 간다.</summary>
        public int WaveDamageDealt => Mathf.Max(0, _waveDamageDealt);
        public int WaveDamageTaken => Mathf.Max(0, _waveDamageTaken);
        public int WaveCriticalHits => Mathf.Max(0, _waveCriticalHitCount);

        /// <summary>이번 웨이브에서 함정이 낸 피해. 유닛이 낸 몫과 나눠 보여야 함정 투자를 판단할 수 있다.</summary>
        public int WaveTrapDamage => Mathf.Max(0, _waveTrapDamage);

        public void RecordTrapDamage(int damage)
        {
            if (_isWaveRunning && damage > 0)
                _waveTrapDamage += damage;
        }
        public int ActiveEnemyCount => _activeEnemies.Count;
        public int PendingSpawnCount => Mathf.Max(0, _remainingSpawns);
        public int RemainingThreatCount => ActiveEnemyCount + PendingSpawnCount;
        public int FinalDay => waveConfig != null ? waveConfig.FinalDay : 0;

        public int GetPreviewEnemyCount(int day)
        {
            var baseEnemyCount = GetBasePreviewEnemyCount(day);
            // 예고도 장악 보정을 거친 수를 보여야 실제로 오는 수와 어긋나지 않는다.
            return GetConquestAdjustedEnemyCount(baseEnemyCount);
        }

        /// <summary>마을 장악 보정을 적용하기 전의 원래 습격 인원.</summary>
        public int GetBasePreviewEnemyCount(int day)
        {
            var entry = waveConfig != null ? waveConfig.GetWaveForDay(day) : null;
            return entry != null ? Mathf.Max(0, entry.enemyCount) : 0;
        }

        public bool IsBossDay(int day) => waveConfig != null && waveConfig.IsBossDay(day);

        /// <summary>결과 화면을 띄우는 연출 담당. 게임오버 쪽에서도 같은 패널을 쓴다.</summary>
        public BossWavePresenter BossPresenter => bossPresenter;

        /// <summary>침입자가 들어오는 구역. 어디까지 걸어올 수 있는지 재는 기준점이다.</summary>
        public Node PortalNode => _portalNode;
        private int _currentDay;
        private int _remainingSpawns;
        private int _currentClearGoldReward;
        private bool _isWaveRunning;
        private Coroutine _waveCoroutine;
        private Coroutine _groupSpawnCoroutine;
        private float _currentGroupInterval;
        private readonly List<Enemy> _activeEnemies = new();
        private readonly List<EnemyDataSO> _partyQueue = new();
        private int _partyIndex;
        private bool _isDestroying;
        [SerializeField] private BossWavePresenter bossPresenter;
        private Enemy _bossEnemy;
        /// <summary>이 날 전용 보스 정의. 없으면 공용 보스 설정으로 떨어진다.</summary>
        private WaveConfigSO.BossEntry _currentBoss;
        private bool _isBossWave;
        private bool _isFinalWave;
        private bool _bossSpawned;
        private bool _isGameCleared;
        private int _waveEnemyCount;
        private int _waveKillCount;
        private int _waveDamageDealt;
        private int _waveDamageTaken;
        private int _waveCriticalHitCount;
        private int _waveTrapDamage;
        private bool _unitConditionWearPending;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(WaveManager)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
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
            ClearEnemyTrackers();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;

            _isDestroying = true;
            StopRunningWave();
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

        /// <summary>
        /// 장악한 마을은 사람을 덜 보낸다. 마릿수를 줄여야 실제로 방어가 편해진다 —
        /// 파티 등장만 막으면 구성만 바뀌고 쳐들어오는 수는 그대로다.
        /// </summary>
        public int GetConquestAdjustedEnemyCount(int baseEnemyCount)
        {
            var normalized = Mathf.Max(0, baseEnemyCount);
            if (normalized <= 0 || maxWaveReductionFromConquest <= 0f)
                return normalized;

            var conquest = VillageConquestSystem.Current;
            if (conquest == null)
                return normalized;

            var reduction = conquest.AverageConquestRatio * maxWaveReductionFromConquest;
            // 다 장악해도 습격이 아예 사라지지는 않는다. 최소 한 명은 온다.
            return Mathf.Max(1, Mathf.RoundToInt(normalized * (1f - reduction)));
        }

        private IEnumerator RunWave(WaveConfigSO.WaveEntry entry)
        {
            var adjustedEnemyCount = GetConquestAdjustedEnemyCount(entry.enemyCount);
            _remainingSpawns = adjustedEnemyCount;
            ResetWaveResults(adjustedEnemyCount);
            _currentClearGoldReward = CoreCohesionSystem.ScaleGoldReward(entry.clearGoldReward);
            _isWaveRunning = true;
            _unitConditionWearPending = true;
            _activeEnemies.Clear();
            _isBossWave = waveConfig != null && waveConfig.IsBossDay(_currentDay);
            _isFinalWave = waveConfig != null && waveConfig.IsFinalDay(_currentDay);
            // 이 날의 보스가 누구인지 웨이브가 도는 내내 같은 값을 봐야 호위·배율·배너가 어긋나지 않는다.
            _currentBoss = waveConfig != null ? waveConfig.GetBossForDay(_currentDay) : null;
            _bossEnemy = null;
            _bossSpawned = false;
            SetupPartyForWave();

            waveEventChannel.RaiseEvent(new WaveStartedEvent(_currentDay, adjustedEnemyCount));

            if (_isBossWave)
            {
                waveEventChannel.RaiseEvent(new BossWaveStartedEvent(_currentDay, _isFinalWave));
                EnsureBossPresenter().ShowBossBanner(
                    _currentDay, _isFinalWave, _currentBoss?.title, _currentBoss?.subtitle);
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

                // 포탈 노드에서 전투가 붙어 있으면 스폰을 미룬다.
                // 그대로 밀어 넣으면 스폰 지점에 적이 겹겹이 쌓여 싸움이 보이지 않는다.
                if (IsPortalNodeInCombat())
                    continue;

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

        /// <summary>포탈 노드에서 아군과 적이 맞붙어 있는 상태인가.</summary>
        private bool IsPortalNodeInCombat()
        {
            if (_portalNode == null)
                return false;

            var battlefield = _portalNode.GetComponent<NodeBattlefield>();
            return battlefield != null && battlefield.PlayerCount > 0 && battlefield.EnemyCount > 0;
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

                // 그룹을 쏟는 도중에 전투가 붙으면 남은 인원은 다음 기회로 미룬다.
                if (IsPortalNodeInCombat())
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
                enemy.PromoteToBoss(
                    _currentBoss != null ? _currentBoss.healthMultiplier : bossHealthMultiplier,
                    _currentBoss != null ? _currentBoss.attackMultiplier : bossAttackMultiplier,
                    _currentBoss != null ? _currentBoss.visualScale : bossVisualScale);
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

            // 보스 웨이브 첫 그룹은 그 날 보스 파티의 호위(첫 멤버=보스는 SpawnEnemy가 따로 처리) 구성으로.
            var bossParty = waveConfig != null ? waveConfig.GetBossPartyForDay(_currentDay) : null;
            if (_isBossWave && !_bossSpawned && bossParty != null
                && bossParty.Members != null && bossParty.Members.Length > 1)
            {
                for (var i = 1; i < bossParty.Members.Length; i++)
                {
                    if (bossParty.Members[i] != null)
                        _partyQueue.Add(bossParty.Members[i]);
                }

                if (_partyQueue.Count > 0)
                    return;
            }

            if (parties == null || parties.Length == 0)
                return;

            var validParties = new List<AdventurerPartySO>();
            foreach (var party in parties)
            {
                if (party == null || party.Members == null || party.Members.Length == 0)
                    continue;

                // 장악한 마을에서 오는 파티는 그만큼 발길을 끊는다. 완전히 장악하면 더 이상 오지 않는다.
                var conquest = VillageConquestSystem.Current;
                if (conquest != null && Random.value < conquest.GetSuppression(party))
                    continue;

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

        /// <summary>보스 데이터 — 그 날 보스 파티의 첫 멤버, 없으면 풀에서 최대 체력 적(승격은 SpawnEnemy가).</summary>
        private EnemyDataSO ResolveBossData()
        {
            var bossParty = waveConfig != null ? waveConfig.GetBossPartyForDay(_currentDay) : null;
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

            // 잡은 만큼만 받는다. 아래 지급처와 정산 표시가 같은 값을 봐야 하므로 여기서 한 번만 깎는다.
            _currentClearGoldReward = ResolveClearGoldReward();

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

            // 보상 선택 없이 정산으로만 마무리한다.
            // 여기서 발행한 수입은 CostManager가 바로 반영하지 않고 정산 장부에만 쌓인다.
            if (_currentClearGoldReward > 0)
                costEventChannel?.RaiseEvent(new GoldEarnedEvent(_currentClearGoldReward, GoldChangeSource.WaveReward));

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

        /// <summary>
        /// 클리어 보상 중 실제로 지급할 몫. 도망친 침입자는 전리품을 남기지 않는다.
        /// 전액을 주면 쫓아내는 쪽이 잡는 쪽보다 싸게 먹혀서, 겁을 주거나 길을 막아
        /// 아무도 죽이지 않고 매일 만액을 걷는 편이 최적해가 된다.
        /// </summary>
        private int ResolveClearGoldReward()
        {
            if (_currentClearGoldReward <= 0)
                return 0;

            var total = Mathf.Max(0, _waveEnemyCount);
            if (total <= 0)
                return _currentClearGoldReward;

            var killed = Mathf.Clamp(_waveKillCount, 0, total);
            return Mathf.RoundToInt(_currentClearGoldReward * (killed / (float)total));
        }

        private void RaiseWaveEnded()
        {
            if (_isDestroying || waveEventChannel == null)
                return;

            ApplyUnitConditionWear();
            // 웨이브 집계는 다음 웨이브에서 초기화되므로, 판 전체 전과는 여기서 넘겨 둔다.
            RunSummarySystem.Current?.RecordWave(_waveEnemyCount, _waveKillCount, _waveDamageDealt, _waveDamageTaken, _waveCriticalHitCount);
            waveEventChannel.RaiseEvent(new WaveEndedEvent(_currentDay, _currentClearGoldReward));
        }

        private void ApplyUnitConditionWear()
        {
            if (!_unitConditionWearPending)
                return;

            _unitConditionWearPending = false;
            var processed = new HashSet<Unit>();
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                foreach (var placement in node.UnitPlacements)
                {
                    var unit = placement?.Instance;
                    if (unit == null || unit is MainUnit || !processed.Add(unit))
                        continue;

                    unit.CompleteWaveCondition();
                }
            }

            foreach (var node in Node.ActiveNodes)
            {
                if (node?.AssignedBuilding is RecoveryFacility recoveryFacility)
                    recoveryFacility.ApplyRecovery(node);
            }
        }

        private void ResetWaveResults(int enemyCount)
        {
            _waveEnemyCount = Mathf.Max(0, enemyCount);
            _waveKillCount = 0;
            _waveDamageDealt = 0;
            _waveDamageTaken = 0;
            _waveCriticalHitCount = 0;
            _waveTrapDamage = 0;
        }

        private void HandleWaveEnemyDeathStarted(Enemy enemy)
        {
            if (enemy != null)
                enemy.DeathStarted -= HandleWaveEnemyDeathStarted;

            if (!_isWaveRunning)
                return;

            _waveKillCount++;
            // 잘 막아낼수록 권능이 붙어 더 개입할 수 있다.
            DungeonPowerSystem.Current?.RewardKill();
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
