using System.Collections;
using System.Collections.Generic;
using Code.Audio;
using Code.BT;
using Code.Buildings;
using Code.Combat;
using Code.Core;
using Code.Enemies;
using Code.Events;
using Code.MapCreateSystem;
using Code.Progression;
using Code.UI;
using Code.Units;
using UnityEngine;

namespace Code.Manager
{
    [RequireComponent(typeof(BossWavePresenter))]
    public class WaveManager : MonoBehaviour
    {
        public const int ExploitationBonusGold = 20;

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
        [SerializeField, Min(0f), Tooltip("입구에서 전투가 붙어 스폰을 미룰 수 있는 최대 시간(초). 넘기면 전투 중이어도 내보낸다 — 안 죽는 선두가 웨이브를 영구히 막는 것을 막는다.")]
        private float maxSpawnHoldSeconds = 6f;
        [SerializeField, Min(0f), Tooltip("파티원이 서로 겹치지 않게 흩어지는 대형 반경")]
        private float formationSpread = 0.35f;
        [Header("Day / Night Pace")]
        [SerializeField, Range(0.1f, 0.9f), Tooltip("하루 방문객 중 낮에 들어오는 비율. 나머지는 밤에 들어온다.")]
        private float daytimeSpawnShare = 0.7f;
        [SerializeField, Min(1f), Tooltip("밤 스폰 간격 배율. 밤에는 적은 파티가 더 느슨하게 들어온다.")]
        private float nightSpawnIntervalMultiplier = 1.6f;
        [SerializeField, Min(0)] private int treasuryGoldLoss = 10;

        [Header("Enemy Level Scaling")]
        [SerializeField, Min(0), Tooltip("일차마다 침입자에게 더해지는 최대 체력.")]
        private int enemyHealthPerLevel = 1;

        [SerializeField, Min(0),
         Tooltip("일차마다 더해지는 공격력. 0을 권장한다 — 일차 수만큼 누적되므로 1만 넣어도 " +
                 "18일차 침입자의 공격력이 4에서 18로 뛰어 유닛이 두세 대에 쓰러진다. " +
                 "후반을 조이려면 일차별 보스 항목의 배율을 쓰는 편이 정밀하다.")]
        private int enemyAttackPerLevel;
        [Header("Boss Wave")]
        [SerializeField, Min(1f), Tooltip("보스 승격 체력 배율(일차 스케일링 이후 적용).")]
        private float bossHealthMultiplier = 6f;
        [SerializeField, Min(1f), Tooltip("보스 승격 공격력 배율.")]
        private float bossAttackMultiplier = 2f;
        [SerializeField, Min(1f), Tooltip("보스 거대화 배율.")]
        private float bossVisualScale = 1.6f;

        private Node _entryNode
        {
            get
            {
                foreach (var node in Node.ActiveNodes)
                    if (node != null && node.Data != null && node.Data.Type == DungeonNodeType.Entrance)
                        return node;
                return null;
            }
        }

        public Node EntryNode => _entryNode;
        public bool HasEntryDoor => EntryNode != null;
        public bool HasPortal => HasEntryDoor; // 비활성 튜토리얼과 기존 호출부의 호환 이름
        public bool IsWaveRunning => _isWaveRunning;
        public bool IsBossWave => _isBossWave;
        public int TotalEnemyCount => _stats.EnemyCount;
        public int KillCount => _stats.KillCount;
        /// <summary>직전 웨이브 전과. 정산 보고서가 읽어 간다.</summary>
        public int WaveDamageDealt => _stats.DamageDealt;
        public int WaveDamageTaken => _stats.DamageTaken;
        public int WaveCriticalHits => _stats.CriticalHitCount;

        /// <summary>이번 웨이브에서 함정이 낸 피해. 유닛이 낸 몫과 나눠 보여야 함정 투자를 판단할 수 있다.</summary>
        public int WaveTrapDamage => _stats.TrapDamage;
        public int WaveCowardTrapTriggers => _stats.CowardTrapTriggers;
        public int WaveCowardBonusFear => _stats.CowardBonusFear;
        public int WavePriestHealingPrevented => _stats.PriestHealingPrevented;
        public int WaveShopaholicBonusGold => _stats.ShopaholicBonusGold;

        public void RecordTrapDamage(int damage)
        {
            if (_isWaveRunning && damage > 0)
                _stats.RecordTrapDamage(damage);
        }

        public void RecordCowardTrapPressure(int bonusFear)
        {
            if (!_isWaveRunning || bonusFear <= 0)
                return;

            _stats.RecordCowardTrapPressure(bonusFear);
        }

        public void RecordPriestHealingPrevented(int preventedHealing)
        {
            if (_isWaveRunning && preventedHealing > 0)
                _stats.RecordPriestHealingPrevented(preventedHealing);
        }

        public int ActiveEnemyCount => _activeEnemies.Count;
        public int PendingSpawnCount => Mathf.Max(0, _remainingSpawns);
        public int RemainingThreatCount => ActiveEnemyCount + PendingSpawnCount;
        public int FinalDay => waveConfig != null ? waveConfig.FinalDay : 0;

        public int GetPreviewEnemyCount(int day)
        {
            var baseEnemyCount = GetBasePreviewEnemyCount(day);
            // 예고와 실제가 같은 계산을 타야 어긋나지 않는다. 한쪽만 고치면 화면이 거짓말을 한다.
            return ResolveWaveEnemyCount(baseEnemyCount);
        }

        /// <summary>
        /// 이 습격에 실제로 오는 인원. 마을 장악으로 줄고, 연속 방어로 늘어난다.
        ///
        /// 예고 화면과 실제 스폰이 반드시 이 하나를 거쳐야 한다.
        /// 한쪽에만 보정을 더하면 "12명 온다"고 적어 놓고 15명을 보내게 된다.
        /// </summary>
        private int ResolveWaveEnemyCount(int baseEnemyCount) => Mathf.Max(0, baseEnemyCount);

        public WaveThreatPreview GetThreatPreview(int day) =>
            waveConfig != null ? waveConfig.GetThreatPreview(day) : default;

        /// <summary>
        /// 마을 장악 보정을 적용하기 전의 원래 습격 인원.
        ///
        /// 날짜가 아니라 던전 등급이 정한다. 여기서 죽어 나간 수와 금고에 쌓은 돈과 지어 둔
        /// 건물이 소문을 만들고, 그 소문이 몇 명을 부르는지 결정한다. 일차는 날짜를 셀 뿐이다.
        /// </summary>
        public int GetBasePreviewEnemyCount(int day)
        {
            return DungeonGradeRules.ResolveVisitorCount(DungeonGradeRules.CalculateGrade());
        }

        public bool IsBossDay(int day) => waveConfig != null && waveConfig.IsBossDay(day);

        /// <summary>결과 화면을 띄우는 연출 담당. 게임오버 쪽에서도 같은 패널을 쓴다.</summary>
        public BossWavePresenter BossPresenter => bossPresenter;

        private int _currentDay;

        /// <summary>이 습격을 시작할 때 잰 던전 등급. 인원과 모험가의 숙련 단계가 이 값을 본다.</summary>
        private int _currentGrade;
        private int _remainingSpawns;
        private int _currentClearGoldReward;
        private bool _isWaveRunning;
        private Coroutine _waveCoroutine;
        private Coroutine _groupSpawnCoroutine;
        private Coroutine _reinforcementSpawnCoroutine;
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
        private bool _bossFinalPhaseStarted;
        private bool _bossReinforcementStarted;
        private int _reservedReinforcementSpawns;
        private bool _isGameCleared;
        private readonly WaveRunStats _stats = new();
        private bool _unitConditionWearPending;

        public int WaveFacilityGold => _stats.Exploitation.FacilityGold;
        public int ExploitationTargetGold => _stats.Exploitation.TargetGold;
        public bool IsExploitationObjectiveCompleted => _stats.Exploitation.IsCompleted;
        public float ExploitationProgress01 => _stats.Exploitation.Progress01;
        public WaveObjectiveKind ObjectiveOptionA { get; private set; }
        public WaveObjectiveKind ObjectiveOptionB { get; private set; }
        public WaveObjectiveKind SelectedObjective { get; private set; }
        public bool LastObjectiveCompleted { get; private set; }
        public string LastObjectiveTitle { get; private set; } = string.Empty;
        public int LastObjectiveRewardGold { get; private set; }
        private int _objectiveDay;

        public void PrepareObjectiveChoices(int day)
        {
            if (day <= 0 || _objectiveDay == day)
                return;

            _objectiveDay = day;
            ObjectiveOptionA = WaveObjectiveRules.GetFirstOffer(day);
            ObjectiveOptionB = WaveObjectiveRules.GetSecondOffer(day);
            SelectedObjective = ObjectiveOptionA;
        }

        public bool SelectObjective(WaveObjectiveKind objective)
        {
            if (_isWaveRunning || (objective != ObjectiveOptionA && objective != ObjectiveOptionB))
                return false;

            SelectedObjective = objective;
            return true;
        }

        public string GetSelectedObjectiveSummary(int enemyCount)
        {
            return $"{WaveObjectiveRules.GetTitle(SelectedObjective)} · "
                   + WaveObjectiveRules.GetDescription(SelectedObjective, enemyCount)
                   + $" · 성공 +{WaveObjectiveRules.GetRewardGold(SelectedObjective)}G";
        }

        public string GetSelectedObjectiveProgressText()
        {
            var target = WaveObjectiveRules.GetTarget(SelectedObjective, _stats.EnemyCount);
            var progress = WaveObjectiveRules.GetProgress(
                SelectedObjective,
                _stats.EnemyCount,
                _stats.KillCount,
                _stats.Exploitation.FacilityGold,
                _stats.TrapDamage,
                _stats.CriticalHitCount);
            return $"{WaveObjectiveRules.GetTitle(SelectedObjective)} {Mathf.Min(progress, target)}/{target}"
                   + $" · 보너스 +{WaveObjectiveRules.GetRewardGold(SelectedObjective)}G";
        }

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
            RegisterListeners();
        }

        private void OnDisable()
        {
            UnregisterListeners();
            StopRunningWave();
            ClearEnemyTrackers();
        }

        private void RegisterListeners()
        {
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            Health.AnyDamaged += HandleAnyDamage;
        }

        private void UnregisterListeners()
        {
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            Health.AnyDamaged -= HandleAnyDamage;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;

            _isDestroying = true;
            StopRunningWave();
            ClearEnemyTrackers();
        }

        public bool CanStartWave(int day)
        {
            return string.IsNullOrEmpty(GetWaveStartBlockedReason(day));
        }

        /// <summary>영업 시작 버튼이 잠긴 이유.</summary>
        public string GetWaveStartBlockedReason(int day)
        {
            var entryNode = EntryNode;
            if (entryNode == null)
                return "입구 문을 불러오는 중";

            var hasInteriorRoom = false;
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null || node == entryNode || node.Data == null)
                    continue;
                hasInteriorRoom = true;
                break;
            }
            if (!hasInteriorRoom)
                return "방을 하나 확장하세요";

            if (waveConfig == null)
                return "오늘 방문객 정보를 불러오는 중";

            return waveConfig.GetWaveForDay(day) == null
                ? "오늘은 예약된 방문객이 없습니다"
                : string.Empty;
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            _currentDay = evt.Day;

            if (_entryNode == null || waveConfig == null)
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
            // 오늘 오는 인원은 영업 시작 시점의 등급으로 확정한다.
            _currentGrade = DungeonGradeRules.CalculateGrade();

            var adjustedEnemyCount = ResolveWaveEnemyCount(
                DungeonGradeRules.ResolveVisitorCount(_currentGrade));

            PrepareObjectiveChoices(_currentDay);
            ResetWaveResults(adjustedEnemyCount);
            // 벌이는 막아낸 만큼이다. 하나 쓰러뜨릴 때마다 전리품이 쌓인다 —
            // 그냥 들여보내면 시설에서 쓰고 간 푼돈만 남는다.
            _currentClearGoldReward = 0;
            DungeonGradeManager.Current?.BeginBusinessDay();
            _isWaveRunning = true;
            _unitConditionWearPending = true;
            _activeEnemies.Clear();
            _isBossWave = waveConfig != null && waveConfig.IsBossDay(_currentDay);
            _isFinalWave = waveConfig != null && waveConfig.IsFinalDay(_currentDay);
            // 이 날의 보스가 누구인지 웨이브가 도는 내내 같은 값을 봐야 호위·배율·배너가 어긋나지 않는다.
            _currentBoss = waveConfig != null ? waveConfig.GetBossForDay(_currentDay) : null;
            _remainingSpawns = adjustedEnemyCount;
            _reservedReinforcementSpawns = _currentBoss?.GetReservedReinforcementCount(adjustedEnemyCount) ?? 0;
            _bossEnemy = null;
            _bossSpawned = false;
            _bossFinalPhaseStarted = false;
            _bossReinforcementStarted = false;
            SetupPartyForWave();

            GameSfxPlayer.Play(GameSfxCue.WaveStart);

            GameMusicPlayer.Play(_isBossWave ? MusicCue.Boss : MusicCue.Wave);

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
            var holdTimer = 0f;
            // 하루 방문객의 앞 절반은 낮, 뒤 절반은 밤으로 본다. 실제 시간 대신 스폰 진행도를
            // 기준으로 잡아 입구 전투 때문에 스폰이 잠시 밀려도 밤 전환이 엉뚱하게 앞서지 않는다.
            var nightAtRemainingSpawns = Mathf.CeilToInt(adjustedEnemyCount * (1f - daytimeSpawnShare));

            while (_isWaveRunning)
            {
                yield return null;

                if (!_isWaveRunning)
                    break;

                // 입구 방에서 전투가 붙어 있으면 스폰을 미룬다.
                // 그대로 밀어 넣으면 스폰 지점에 적이 겹겹이 쌓여 싸움이 보이지 않는다.
                //
                // 다만 무한정 미루면 안 된다. 선두가 좀처럼 안 죽는 적이면 그 전투가 끝나지 않아
                // 웨이브 전체가 영구히 멈추고, 그동안 선두는 코어를 깬다. 실측에서 12일·20일 보스가
                // 정확히 그렇게 끝냈다 — 열세 마리가 스폰도 못 한 채 활성1로 굳었다.
                // 쌓임을 막는 데는 잠깐 미루는 것으로 충분하므로 상한을 둔다.
                if (IsEntryNodeInCombat())
                {
                    holdTimer += Time.deltaTime;
                    if (holdTimer < maxSpawnHoldSeconds)
                        continue;
                }
                else
                {
                    holdTimer = 0f;
                }

                spawnTimer += Time.deltaTime;

                var isNight = DayManager.Current != null && DayManager.Current.Phase == DayManager.OperationPhase.Night;
                var activeSpawnInterval = spawnInterval * (isNight ? nightSpawnIntervalMultiplier : 1f);
                if (spawnTimer >= activeSpawnInterval)
                {
                    spawnTimer = 0f;
                    // 한 번 내보냈으니 미룰 여유를 다시 준다. 교착일 때는 (간격 + 상한) 속도로 흘러간다.
                    holdTimer = 0f;
                    if (spawnAsGroup)
                        SpawnNextGroup(activeSpawnInterval);
                    else
                        SpawnNextEnemyIfNeeded(false);
                }

                RemoveMissingEnemies();
                RefreshPauseLock();
                if (DayManager.Current != null && DayManager.Current.Phase == DayManager.OperationPhase.Day
                    && _remainingSpawns <= nightAtRemainingSpawns)
                {
                    DayManager.Current.SetPhase(DayManager.OperationPhase.Night);
                    // 전환을 알아챌 틈을 준다. 주요 사건 창은 따로 시간을 세우므로 여기서는 1초만.
                    GameSpeedController.Current?.PauseForTransition(PauseLockRules.DayNightTransitionPauseSeconds);
                }
                CompleteWaveIfCleared(false);
            }

            CompleteWave(false);
        }

        /// <summary>입구 방에서 아군과 적이 맞붙어 있는 상태인가.</summary>
        private bool IsEntryNodeInCombat()
        {
            if (_entryNode == null)
                return false;

            var battlefield = _entryNode.GetComponent<NodeBattlefield>();
            return battlefield != null && battlefield.PlayerCount > 0 && battlefield.EnemyCount > 0;
        }

        private void SpawnNextEnemyIfNeeded(bool stopRunningCoroutine)
        {
            if (_entryNode == null || !HasRegularSpawnsPending)
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
            if (_entryNode == null || !HasRegularSpawnsPending)
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
            groupSize = Mathf.Min(groupSize, RegularSpawnCount);
            _currentGroupInterval = spawnInterval * groupSize;

            if (_groupSpawnCoroutine != null)
                StopCoroutine(_groupSpawnCoroutine);
            _groupSpawnCoroutine = StartCoroutine(SpawnGroupRoutine(groupSize, spawnInterval));
        }

        private IEnumerator SpawnGroupRoutine(int groupSize, float spawnInterval)
        {
            var spawned = 0;
            var partyOccupancyId = EnemyMover.CreatePartyOccupancyId();

            for (var i = 0; i < groupSize; i++)
            {
                if (!_isWaveRunning || _entryNode == null)
                    break;

                // 그룹을 쏟는 "도중에" 전투가 붙으면 남은 인원은 다음 기회로 미룬다.
                // 첫 마리까지 막으면 안 된다 — 여기까지 왔다는 건 바깥 루프가 내보내기로 정한
                // 것이고, i == 0 에서 되돌아가면 한 마리도 안 나가 웨이브가 그대로 굳는다.
                if (i > 0 && IsEntryNodeInCombat())
                    break;

                if (!SpawnEnemy(FormationOffsetFor(i, groupSize), null, partyOccupancyId))
                    break;

                spawned++;

                if (memberSpawnDelay > 0f && i < groupSize - 1)
                    yield return new WaitForSeconds(memberSpawnDelay);
            }

            // 다음 그룹까지의 간격은 "실제로 내보낸 수"에 맞춘다.
            //
            // 간격은 그룹 정원(spawnInterval × groupSize)으로 잡아 두는데, 위에서 전투 때문에
            // 중간에 끊기면 정원이 아니라 한 마리만 나간다. 그대로 두면 여섯 자리 시간을 기다리고
            // 한 마리를 내보내게 되어 웨이브가 정원 배수만큼 느려진다. 실측에서 16일차가 그랬다 —
            // 6인 파티가 전투에 물려 30마리를 29번에 나눠 내보내며 336초를 썼다(이웃한 날은 40초).
            _currentGroupInterval = spawnInterval * Mathf.Max(1, spawned);

            _groupSpawnCoroutine = null;
            CompleteWaveIfCleared(false);
        }

        /// <summary>멤버를 입구 문 주위에 원형으로 흩어 배치해 같은 노드에서도 겹쳐 보이지 않게 한다.</summary>
        private Vector3 FormationOffsetFor(int index, int groupSize)
        {
            if (groupSize <= 1 || formationSpread <= 0f)
                return Vector3.zero;

            var angle = 360f / groupSize * index * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * formationSpread;
        }

        private int RegularSpawnCount => Mathf.Max(0, _remainingSpawns - _reservedReinforcementSpawns);
        private bool HasRegularSpawnsPending => RegularSpawnCount > 0;

        private bool SpawnEnemy(
            Vector3 formationOffset,
            EnemyDataSO forcedData = null,
            int partyOccupancyId = 0)
        {
            if (_entryNode == null || _remainingSpawns <= 0)
                return false;

            var entryPoint = _entryNode.EntryDoorSpawnPoint != null
                ? _entryNode.EntryDoorSpawnPoint.position
                : _entryNode.EnemyPosition.position;
            var spawnPos = entryPoint + formationOffset;

            // 데이터를 먼저 뽑고, 그 데이터 전용 프리팹이 있으면 그것을 스폰(종류↔프리팹 짝 보장).
            // 없으면 기존 방식(공용 프리팹 풀)으로 폴백한다.
            // 보스 웨이브의 첫 스폰은 보스 — 전용 파티가 없어도 풀에서 가장 강한 적을 승격시킨다.
            var isBossSpawn = _isBossWave && !_bossSpawned;
            var enemyData = forcedData != null
                ? forcedData
                : isBossSpawn ? ResolveBossData() : ResolveEnemyData();
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
            enemy.FacilityGoldSpent += HandleEnemyFacilityGoldSpent;
            enemy.ConfigureData(enemyData);
            // 날짜가 아니라 등급이 모험가의 성장 단계를 정한다. 웅크린 던전에는 오래 버텨도
            // 약한 모험가가 오고, 크게 키운 던전에는 이틀 만에도 강한 자가 온다.
            var visitorLevel = DungeonGradeRules.ResolveVisitorLevel(_currentGrade);
            enemy.ApplyWaveLevel(visitorLevel, enemyHealthPerLevel, enemyAttackPerLevel);
            var visitorIndex = Mathf.Max(0, _stats.EnemyCount - _remainingSpawns);
            var purpose = isBossSpawn
                ? AdventurerVisitPurpose.TreasureHunt
                : AdventurerVisitRules.ResolvePurpose(enemy.Trait, visitorIndex);
            enemy.ConfigureVisitProfile(
                purpose,
                AdventurerVisitRules.ResolveBudget(_currentGrade, visitorLevel, purpose, enemy.Trait));
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
                if (enemy.Health != null)
                    enemy.Health.Changed += HandleBossHealthChanged;
            }

            // Initialize가 mover 위치를 노드 위치로 스냅하므로, 그 전에 대형 오프셋을 넣어야 한다
            if (enemy.Mover != null)
            {
                enemy.Mover.FormationOffset = formationOffset;
                enemy.Mover.InitialSpawnPosition = spawnPos;
                if (partyOccupancyId > 0)
                    enemy.Mover.ConfigurePartyOccupancy(partyOccupancyId);
            }

            enemy.Initialize(_entryNode, costEventChannel, treasuryGoldLoss, nodeEventChannel);
            EnemyMoodHud.Attach(enemy);
            if (enemy != null)
            {
                _activeEnemies.Add(enemy);
                ApplyPartyTraitSupport(enemy);
            }

            return true;
        }

        private void ApplyPartyTraitSupport(Enemy spawnedEnemy)
        {
            if (spawnedEnemy == null)
                return;

            var newPriestCalm = AdventurerTraitRules.GetPartyCalmAmount(spawnedEnemy.Trait);
            if (newPriestCalm > 0)
            {
                foreach (var ally in _activeEnemies)
                {
                    if (ally != null && ally != spawnedEnemy && ally.IsAlive)
                        ally.ReduceFear(newPriestCalm);
                }
            }

            foreach (var ally in _activeEnemies)
            {
                if (ally == null || ally == spawnedEnemy || !ally.IsAlive)
                    continue;

                var calm = AdventurerTraitRules.GetPartyCalmAmount(ally.Trait);
                if (calm <= 0)
                    continue;

                spawnedEnemy.ReduceFear(calm);
                break;
            }
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

            var authoredParty = waveConfig != null ? waveConfig.GetWaveForDay(_currentDay)?.party : null;
            if (authoredParty != null && authoredParty.Members != null)
            {
                foreach (var member in authoredParty.Members)
                {
                    if (member != null)
                        _partyQueue.Add(member);
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
            if (enemy != null)
                enemy.FacilityGoldSpent -= HandleEnemyFacilityGoldSpent;

            if (this == null || _isDestroying)
                return;

            if (!_isWaveRunning)
                return;

            _activeEnemies.Remove(enemy);
            TryActivateBossFinalPhase();
            CompleteWaveIfCleared(false);
        }

        private void HandleEnemyFacilityGoldSpent(Enemy enemy, int amount, int baseAmount, GoldChangeSource source)
        {
            if (!_isWaveRunning || enemy == null)
                return;

            _stats.Exploitation.RecordFacilityGold(amount);
            if (enemy.Trait == AdventurerTrait.Shopaholic)
                _stats.RecordShopaholicBonusGold(amount - baseAmount);
        }

        private void HandleBossHealthChanged(float healthRatio)
        {
            if (!_isWaveRunning || _bossReinforcementStarted || _currentBoss == null
                || !_currentBoss.enableReinforcementPhase
                || healthRatio > Mathf.Clamp(_currentBoss.reinforcementHealthRatio, 0.1f, 0.9f))
                return;

            StartBossReinforcements();
        }

        private void StartBossReinforcements()
        {
            if (_bossReinforcementStarted || _reservedReinforcementSpawns <= 0)
                return;

            var party = _currentBoss?.reinforcementParty;
            if (party == null || party.Members == null || party.Members.Length == 0)
            {
                _reservedReinforcementSpawns = 0;
                return;
            }

            _bossReinforcementStarted = true;
            EnsureBossPresenter().ShowBossBanner(
                _currentDay,
                false,
                string.IsNullOrWhiteSpace(_currentBoss.title) ? "증원 도착" : $"{_currentBoss.title} · 증원",
                string.IsNullOrWhiteSpace(_currentBoss.reinforcementSubtitle)
                    ? "두목의 호각에 후열 사냥꾼이 전투에 합류했다"
                    : _currentBoss.reinforcementSubtitle);

            _reinforcementSpawnCoroutine = StartCoroutine(SpawnBossReinforcements(party));
        }

        private IEnumerator SpawnBossReinforcements(AdventurerPartySO party)
        {
            var count = _reservedReinforcementSpawns;
            var partyOccupancyId = EnemyMover.CreatePartyOccupancyId();
            for (var i = 0; i < count; i++)
            {
                if (!_isWaveRunning || _entryNode == null)
                    break;

                var data = party.Members[i % party.Members.Length];
                if (data != null && SpawnEnemy(FormationOffsetFor(i, count), data, partyOccupancyId))
                    _reservedReinforcementSpawns--;

                if (memberSpawnDelay > 0f && i < count - 1)
                    yield return new WaitForSeconds(memberSpawnDelay);
            }

            // 잘못 비어 있는 멤버가 있어도 보류 수 때문에 웨이브가 영원히 끝나지 않게 한다.
            _reservedReinforcementSpawns = 0;
            _reinforcementSpawnCoroutine = null;
            CompleteWaveIfCleared(false);
        }

        private void TryActivateBossFinalPhase()
        {
            if (!_isBossWave || _bossFinalPhaseStarted || _currentBoss == null
                || !_currentBoss.enableFinalPhase || _bossEnemy == null || !_bossEnemy.IsAlive
                || _remainingSpawns > 0)
                return;

            RemoveMissingEnemies();
            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null && enemy != _bossEnemy && enemy.IsAlive)
                    return;
            }

            _bossFinalPhaseStarted = true;
            _bossEnemy.ActivateBossFinalPhase(
                _currentBoss.phaseDefensePenalty,
                _currentBoss.phaseAttackIntervalMultiplier);

            EnsureBossPresenter().ShowBossBanner(
                _currentDay,
                false,
                string.IsNullOrWhiteSpace(_currentBoss.title) ? "보스 격노" : $"{_currentBoss.title} · 격노",
                string.IsNullOrWhiteSpace(_currentBoss.phaseSubtitle)
                    ? "호위가 무너지자 공격이 거세지고 방어의 빈틈이 드러났다"
                    : _currentBoss.phaseSubtitle);
        }

        /// <summary>엘리트·보스가 던전에 남아 있는 동안 플레이어 일시정지를 잠근다.</summary>
        private void RefreshPauseLock()
        {
            var speed = GameSpeedController.Current;
            if (speed == null)
                return;

            var lockingBoss = false;
            var lockingElite = false;
            foreach (var enemy in _activeEnemies)
            {
                if (!PauseLockRules.LocksPause(enemy))
                    continue;

                if (enemy.IsBoss)
                    lockingBoss = true;
                else
                    lockingElite = true;
            }

            if (lockingBoss || lockingElite)
                speed.SetPauseLock(this, true, lockingBoss ? PauseLockRules.BossLockReason : PauseLockRules.EliteLockReason);
            else
                speed.SetPauseLock(this, false, null);
        }

        private void ReleasePauseLock() => GameSpeedController.Current?.SetPauseLock(this, false, null);

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
            _reservedReinforcementSpawns = 0;
            _activeEnemies.Clear();
            ReleasePauseLock();

            if (stopRunningCoroutine && _waveCoroutine != null)
            {
                StopCoroutine(_waveCoroutine);
                _waveCoroutine = null;
            }
            else
            {
                _waveCoroutine = null;
            }

            ResolveObjectiveReward();

            // 최종 보스 웨이브 클리어 = 승리 — 보상 패널 대신 승리 패널(시네마틱이 끝난 뒤).
            if (_isFinalWave && !_isGameCleared)
            {
                _isGameCleared = true;

                if (_currentClearGoldReward > 0)
                    costEventChannel?.RaiseEvent(new GoldEarnedEvent(_currentClearGoldReward, GoldChangeSource.Bounty));

                waveEventChannel?.RaiseEvent(new GameClearedEvent(_currentDay));
                StartCoroutine(ShowVictoryAfterCinematic());
                RaiseWaveEnded();
                return;
            }

            // 보상 선택 없이 정산으로만 마무리한다.
            // 여기서 발행한 수입은 CostManager가 바로 반영하지 않고 정산 장부에만 쌓인다.
            if (_currentClearGoldReward > 0)
                costEventChannel?.RaiseEvent(new GoldEarnedEvent(_currentClearGoldReward, GoldChangeSource.Bounty));

            RaiseWaveEnded();
        }

        private void ResolveObjectiveReward()
        {
            LastObjectiveTitle = WaveObjectiveRules.GetTitle(SelectedObjective);
            LastObjectiveCompleted = WaveObjectiveRules.IsCompleted(
                SelectedObjective,
                _stats.EnemyCount,
                _stats.KillCount,
                _stats.Exploitation.FacilityGold,
                _stats.TrapDamage,
                _stats.CriticalHitCount);
            LastObjectiveRewardGold = LastObjectiveCompleted
                ? WaveObjectiveRules.GetRewardGold(SelectedObjective)
                : 0;

            if (LastObjectiveRewardGold > 0)
                costEventChannel?.RaiseEvent(
                    new GoldEarnedEvent(LastObjectiveRewardGold, GoldChangeSource.WaveObjective));
        }

        private BossWavePresenter EnsureBossPresenter()
        {
            return bossPresenter;
        }

        /// <summary>보스 사망 시작(디졸브 직전) — 슬로모 + 카메라 줌인으로 쓰러지는 모습을 보여준다.</summary>
        private void HandleBossDeathStarted(Enemy boss)
        {
            if (boss != null)
            {
                boss.DeathStarted -= HandleBossDeathStarted;
                if (boss.Health != null)
                    boss.Health.Changed -= HandleBossHealthChanged;
            }

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

            DungeonGradeManager.Current?.FinalizeBusinessDay();
            ApplyUnitConditionWear();
            // 웨이브 집계는 다음 웨이브에서 초기화되므로, 판 전체 전과는 여기서 넘겨 둔다.
            RunSummarySystem.Current?.RecordWave(_stats.EnemyCount, _stats.KillCount, _stats.DamageDealt, _stats.DamageTaken, _stats.CriticalHitCount);
            GameSfxPlayer.Play(GameSfxCue.WaveClear);
            // 웨이브가 끝나면 다시 짓고 배치하는 시간으로 돌아간다.
            GameMusicPlayer.Play(MusicCue.Management);
            waveEventChannel.RaiseEvent(
                new WaveEndedEvent(_currentDay, _currentClearGoldReward, _stats.EnemyCount, _stats.KillCount));
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
            DungeonGradeManager.Current?.BeginBusinessDay();
            _stats.Reset(enemyCount);
        }

        private void HandleWaveEnemyDeathStarted(Enemy enemy)
        {
            if (enemy != null)
                enemy.DeathStarted -= HandleWaveEnemyDeathStarted;

            if (!_isWaveRunning)
                return;

            _stats.RecordKill();

            // 막아낸 하나가 전리품이 되고, 동시에 이 던전이 위험하다는 소문이 된다.
            // 벌이와 난이도가 같은 행동에서 나온다 — 잘 막을수록 더 센 자들이 찾아온다.
            var level = enemy != null ? enemy.Level : 1;
            _currentClearGoldReward += DungeonGradeRules.ResolveBounty(level);
            DungeonGradeManager.Current?.RecordKill();
        }

        private void HandleAnyDamage(Health damagedHealth, int damage, bool isCritical)
        {
            if (!_isWaveRunning || damagedHealth == null || damage <= 0)
                return;

            if (damagedHealth.GetComponentInParent<Enemy>() != null)
            {
                _stats.RecordDamageDealt(damage, isCritical);
                return;
            }

            if (damagedHealth.GetComponentInParent<Unit>() != null)
                _stats.RecordDamageTaken(damage);
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


            if (_reinforcementSpawnCoroutine != null)
            {
                StopCoroutine(_reinforcementSpawnCoroutine);
                _reinforcementSpawnCoroutine = null;
            }

            _isWaveRunning = false;
        }

        /// <summary>승리·패배로 판이 끝날 때 진행 중인 습격만 정리한다.</summary>
        public void StopForRunEnd()
        {
            StopRunningWave();
            _remainingSpawns = 0;
            _reservedReinforcementSpawns = 0;
            _unitConditionWearPending = false;
            _partyQueue.Clear();
            _partyIndex = 0;
            ClearEnemyTrackers();
            _bossEnemy = null;
        }

        private void ClearEnemyTrackers()
        {
            if (_bossEnemy != null)
            {
                _bossEnemy.DeathStarted -= HandleBossDeathStarted;
                if (_bossEnemy.Health != null)
                    _bossEnemy.Health.Changed -= HandleBossHealthChanged;
            }

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null)
                    continue;

                enemy.DeathStarted -= HandleWaveEnemyDeathStarted;
                enemy.Removed -= HandleEnemyRemoved;
                enemy.FacilityGoldSpent -= HandleEnemyFacilityGoldSpent;
            }

            _activeEnemies.Clear();
            ReleasePauseLock();
        }

        /// <summary>
        /// 한 웨이브에서 누적되는 전투/시설 지표의 단일 소유자.
        /// 스폰 순서나 Unity 생명주기와 무관한 값은 여기에서만 변경한다.
        /// </summary>
        private sealed class WaveRunStats
        {
            public int EnemyCount { get; private set; }
            public int KillCount { get; private set; }
            public int DamageDealt { get; private set; }
            public int DamageTaken { get; private set; }
            public int CriticalHitCount { get; private set; }
            public int TrapDamage { get; private set; }
            public int CowardTrapTriggers { get; private set; }
            public int CowardBonusFear { get; private set; }
            public int PriestHealingPrevented { get; private set; }
            public int ShopaholicBonusGold { get; private set; }
            public WaveExploitationProgress Exploitation { get; } = new();

            public void Reset(int enemyCount)
            {
                EnemyCount = Mathf.Max(0, enemyCount);
                KillCount = 0;
                DamageDealt = 0;
                DamageTaken = 0;
                CriticalHitCount = 0;
                TrapDamage = 0;
                CowardTrapTriggers = 0;
                CowardBonusFear = 0;
                PriestHealingPrevented = 0;
                ShopaholicBonusGold = 0;
                Exploitation.Reset();
            }

            public void RecordKill() => KillCount++;

            public void RecordDamageDealt(int damage, bool isCritical)
            {
                DamageDealt += Mathf.Max(0, damage);
                if (isCritical)
                    CriticalHitCount++;
            }

            public void RecordDamageTaken(int damage) => DamageTaken += Mathf.Max(0, damage);
            public void RecordTrapDamage(int damage) => TrapDamage += Mathf.Max(0, damage);

            public void RecordCowardTrapPressure(int bonusFear)
            {
                CowardTrapTriggers++;
                CowardBonusFear += Mathf.Max(0, bonusFear);
            }

            public void RecordPriestHealingPrevented(int preventedHealing) =>
                PriestHealingPrevented += Mathf.Max(0, preventedHealing);

            public void RecordShopaholicBonusGold(int bonusGold) =>
                ShopaholicBonusGold += Mathf.Max(0, bonusGold);
        }
    }
}
