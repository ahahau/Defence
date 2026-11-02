using System;
using System.Collections.Generic;
using Code.Buildings;
using Code.Enemies;
using Code.Units;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>고정 스테이지의 진행/재도전과 금화/코어. 구 일차/보스/정산 이벤트를 발행하지 않는다.</summary>
    public sealed class StageSession : MonoBehaviour
    {
        [field: SerializeField] public StageCatalogSO Catalog { get; private set; }
        [SerializeField] private StageMapView mapView;
        [SerializeField] private Transform actorsRoot;
        [SerializeField] private StageBarricade barricadePrefab;
        [SerializeField] private Code.Manager.GameSpeedController speedController;
        private readonly HashSet<string> _loadout = new(StringComparer.Ordinal);
        private StageActor _boss;
        private StageParty _party;
        private float _spawnClock;
        private int _spawned;
        private bool _locked;
        private StageCheckpoint _checkpoint;
        private bool _leaving;
        public StageDefinitionSO Definition { get; private set; }
        public StageBoard Board { get; private set; }
        public StagePathfinder Paths { get; private set; }
        public StageSkillField SkillField { get; private set; }
        public StageDevices Devices { get; private set; }
        public StageProfile Profile { get; private set; }
        public StageArtifacts Artifacts { get; private set; }
        public StageWaveResults Results { get; private set; } = new();
        public StagePreparationEvents PreparationEvents { get; } = new();
        public StagePhase Phase { get; private set; } = StagePhase.Unavailable;
        public int Gold { get; private set; }
        public int WaveIndex { get; private set; }
        public int StageIndex { get; private set; }
        public string Message { get; private set; }
        public string SaveError => _checkpoint?.Error;
        public int CoreHealth => mapView.CoreHealth.CurrentHealth;
        /// <summary>실제 코어 체력의 최대치. 보수 미리보기와 적용이 같은 값을 쓴다.</summary>
        public int MaxCoreHealth => mapView.CoreHealth.MaxHealth;
        public StagePlacement Placement { get; private set; }
        public IReadOnlyList<Trap> Traps => Placement.Traps;
        public int SelectedCount => _loadout.Count;
        public bool CanChoose => Phase == StagePhase.Preparing && !_locked;
        public bool CanUpgrade => Phase == StagePhase.Cleared || Phase == StagePhase.Failed
            || CanChoose && !Placement.HasPlacements;
        public bool CanChangeStage => Phase == StagePhase.Cleared || Phase == StagePhase.Failed
            || CanChoose && !Placement.HasPlacements && Board.Barricades.Count == 0;
        public event Action Changed;
        public event Action<int> CoreDamaged;
        public event Action<Vector2Int> BarricadeDestroyed;

        internal void ReportBarricadeDestroyed(Vector2Int cell) => BarricadeDestroyed?.Invoke(cell);

        public bool CanSelectStage(int index) => Catalog != null && index >= 0 && index < Catalog.Stages.Length
            && (index == 0 || Profile.IsCleared(Catalog.Stages[index - 1].Id));

        private void Start()
        {
            Profile = new StageProfile(Application.persistentDataPath);
            Artifacts = new StageArtifacts(this);
            _checkpoint = new StageCheckpoint(this, Application.persistentDataPath);
            if (_checkpoint.Restore())
                return;
            if (!string.IsNullOrEmpty(_checkpoint.Error))
            {
                Phase = StagePhase.Unavailable;
                Tell(_checkpoint.Error);
                return;
            }
            SelectStage(0);
        }

        public int LoadoutSlots => Profile.LoadoutSlots(Catalog);

        public bool IsSelected(StageOption option) => option != null && _loadout.Contains(option.Id);

        public bool ToggleOption(StageOption option)
        {
            if (!CanChoose || !Profile.IsUnlocked(option))
                return false;
            if (Placement.HasPlaced(option))
            {
                Tell("이미 배치한 종류입니다. 먼저 회수/철거하세요");
                return false;
            }
            if (_loadout.Remove(option.Id))
            {
                Notify();
                return true;
            }
            if (_loadout.Count >= LoadoutSlots)
            {
                Tell("편성 슬롯이 가득 찼습니다");
                return false;
            }
            _loadout.Add(option.Id);
            Notify();
            return true;
        }

        public bool HasArtifact(StageArtifact artifact) => artifact != null && artifact.Artifact != null
            && Profile.HasArtifact(artifact.Id);

        /// <summary>현재 카탈로그에 있는 후보가 하나라도 남았는가. 카탈로그에서 빠진 후보만 남으면 고를 것이 없다.</summary>
        public bool HasArtifactChoice
        {
            get
            {
                if (Profile == null || Catalog == null)
                    return false;
                foreach (string id in Profile.ArtifactChoices)
                    if (Catalog.FindArtifact(id) != null)
                        return true;
                return false;
            }
        }

        /// <summary>편성과 같이 첫 웨이브 전까지만 바꾼다. 가진 유물만, 장착 칸 수까지.</summary>
        public bool ToggleArtifact(StageArtifact artifact)
        {
            if (!CanChoose || !HasArtifact(artifact))
                return false;
            if (Artifacts.IsEquipped(artifact))
            {
                Artifacts.Unequip(artifact);
                Notify();
                return true;
            }
            if (Artifacts.Equipped.Count >= Catalog.ArtifactSlots)
            {
                Tell("유물 칸이 가득 찼습니다");
                return false;
            }
            Artifacts.Equip(artifact);
            Notify();
            return true;
        }

        public bool TryPickArtifact(StageArtifact artifact)
        {
            if (artifact == null || !Profile.TryPickArtifact(artifact.Id))
            {
                Tell(!string.IsNullOrEmpty(Profile.Error) ? Profile.Error : "고를 수 없는 유물입니다");
                return false;
            }
            Tell($"유물 획득: {artifact.Label}");
            return true;
        }

        /// <summary>아직 없는 유물 중 무작위 후보. 남은 것이 적으면 있는 만큼만 준다.</summary>
        private List<string> DrawArtifactChoices()
        {
            List<string> pool = new();
            foreach (StageArtifact artifact in Catalog.Artifacts)
                if (artifact != null && artifact.Artifact != null && !Profile.HasArtifact(artifact.Id) && !pool.Contains(artifact.Id))
                    pool.Add(artifact.Id);
            int count = Mathf.Min(Catalog.ArtifactChoices, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = UnityEngine.Random.Range(i, pool.Count);
                (pool[i], pool[pick]) = (pool[pick], pool[i]);
            }
            pool.RemoveRange(count, pool.Count - count);
            return pool;
        }

        public bool SelectStage(int index)
        {
            if (Phase == StagePhase.Fighting)
                return false;
            if (Profile == null || Catalog == null || Catalog.Stages == null || index < 0 || index >= Catalog.Stages.Length)
                return false;
            if (index > 0 && !Profile.IsCleared(Catalog.Stages[index - 1].Id))
                return false;
            StageDefinitionSO definition = Catalog.Stages[index];
            string reason = "스테이지 데이터가 없습니다";
            if (definition == null || !definition.TryValidate(out reason))
            {
                Phase = StagePhase.Unavailable;
                Tell(definition == null ? "스테이지 데이터가 없습니다" : reason);
                return false;
            }
            StopActors();
            foreach (Transform child in actorsRoot)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            _loadout.Clear();
            Artifacts.Clear();
            Definition = definition;
            StageIndex = index;
            Board = new StageBoard(definition);
            Paths = new StagePathfinder(Board);
            // 스킬 대상 반경은 임시로 교전 반경과 같다(2026-10-28 승인)
            SkillField = new StageSkillField(Board, definition.EngageRadius);
            Placement = new StagePlacement(this, actorsRoot, barricadePrefab);
            Devices = new StageDevices(Board);
            List<Vector2Int> path = new();
            // 여닫는 문이 모두 닫혀도 모든 던전문에서 코어로 가는 길이 남아야 한다
            Devices.CloseAllGates();
            foreach (Vector2Int door in definition.Doors)
            {
                if (!Paths.Find(door, definition.Core, null, false, path))
                {
                    Devices.Reset();
                    Phase = StagePhase.Unavailable;
                    Tell("고정 지형의 던전문과 코어가 연결되지 않았습니다");
                    return false;
                }
            }
            Devices.Reset();
            Gold = definition.StartingGold;
            Results.Reset();
            PreparationEvents.Reset();
            WaveIndex = 0;
            _spawned = 0;
            _locked = false;
            Phase = StagePhase.Preparing;
            mapView.Draw(Board);
            mapView.ShowDoors(definition, WaveIndex);
            _checkpoint?.NewStage();
            Tell("편성을 고르고 바리케이드·부하·함정을 배치하세요");
            return true;
        }

        public void BeginWave()
        {
            if (Phase != StagePhase.Preparing || WaveIndex >= Definition.Waves.Length)
                return;
            if (_loadout.Count == 0)
            {
                Tell("가져갈 부하/함정을 먼저 선택하세요");
                return;
            }
            _checkpoint?.Save();
            Results.Begin();
            _locked = true;
            _spawned = 0;
            _spawnClock = 0;
            _party = null;
            Phase = StagePhase.Fighting;
            Tell("침입자가 코어로 접근합니다");
        }

        /// <summary>
        /// 사건 "남은 보급품"을 고른다(2026-11-02 사용자 승인). 사건을 연 시도의 같은 준비 단계에서만, 한 번만 적용한다.
        /// 저장이 막혀 있으면 이어하기 뒤 두 번 적용되지 않도록 효과를 주지 않는다.
        /// </summary>
        public bool TryChooseSupply(StageSupplyChoice choice, int attempt)
        {
            if (choice == StageSupplyChoice.None || attempt != PreparationEvents.Attempt || !PreparationEvents.IsSupplyOffered(this))
                return false;
            if (!string.IsNullOrEmpty(SaveError))
            {
                Tell(SaveError);
                return false;
            }
            int repaired = 0;
            if (choice == StageSupplyChoice.Repair)
            {
                repaired = Mathf.Min(StagePreparationEvents.SupplyRepair, MaxCoreHealth - CoreHealth);
                if (repaired <= 0)
                {
                    Tell("코어가 가득 차 보수할 수 없습니다");
                    return false;
                }
                mapView.CoreHealth.Heal(repaired);
            }
            else if (choice == StageSupplyChoice.Gold)
                Gold += StagePreparationEvents.SupplyGold;
            PreparationEvents.Set(choice, repaired);
            Tell(choice switch
            {
                StageSupplyChoice.Gold => $"보급품을 금화로 바꿨습니다 +{StagePreparationEvents.SupplyGold}G",
                StageSupplyChoice.Repair => $"보급품으로 금고를 보수했습니다 코어 +{repaired}",
                _ => "보급품을 쓰지 않았습니다"
            });
            return true;
        }

        public bool IsBossAlive => _boss != null && _boss.IsAlive;

        private void Update()
        {
            // 보스·정예가 살아 있는 동안만 멈출 수 없다. 웨이브 종료·실패로 사라지면 바로 풀린다.
            if (speedController != null)
            {
                bool locked = Phase == StagePhase.Fighting && (IsBossAlive || IsEliteAlive());
                speedController.SetPauseLock(this, locked, IsBossAlive
                    ? Code.Manager.PauseLockRules.BossLockReason : Code.Manager.PauseLockRules.EliteLockReason);
            }
            if (Phase != StagePhase.Fighting)
                return;
            Devices.Tick(Time.deltaTime);
            if (Phase != StagePhase.Fighting)
                return;
            StageWave wave = Definition.Waves[WaveIndex];
            _spawnClock -= Time.deltaTime;
            if (_spawned < wave.Count && _spawnClock <= 0)
            {
                EnemyDataSO data = wave.At(_spawned, out bool isBoss);
                int open = Definition.OpenDoorCount(WaveIndex);
                // 문은 파티마다 돌아가며 고른다. 한 파티는 같은 문으로 들어온다.
                Vector2Int door = Definition.Doors[Definition.OpenDoorIndex(WaveIndex, wave.PartyIndex(_spawned) % open)];
                Enemy enemy = Instantiate(data.Prefab, Board.World(door), Quaternion.identity, actorsRoot);
                enemy.ConfigureData(data);
                if (isBoss)
                    enemy.PromoteToBoss(wave.BossHealthMultiplier, wave.BossAttackMultiplier, wave.BossScale);
                StageActor actor = enemy.gameObject.AddComponent<StageActor>();
                actor.Initialize(this, door, null, 0);
                actor.SetRule(wave.RuleAt(_spawned));
                if (wave.StartsParty(_spawned))
                    _party = wave.PartyLength(_spawned) > 1 ? new StageParty() : null;
                // 표식 적은 같은 문으로 나오되 자기 규칙대로 혼자 움직인다
                if (_party != null && actor.Rule == StageEnemyRule.None)
                    actor.JoinParty(_party);
                if (isBoss)
                {
                    _boss = actor;
                    Tell($"보스 {data.Name} 등장");
                }
                else if (Catalog != null && Catalog.IsElite(data))
                    Tell($"{data.Name} 등장");
                _spawnClock = NextSpawnDelay(wave, _spawned);
                _spawned++;
                Notify();
            }
            if (_spawned >= wave.Count && EnemyCount() == 0)
                EndWave();
        }

        /// <summary>
        /// 파티 안은 짧게 연달아, 파티 사이는 기존 간격 × 파티 인원에서 파티 안 간격을 뺀 만큼 기다린다.
        /// 웨이브 전체 길이를 파티가 없던 때와 같게 둔다(2026-11-01 사용자 결정).
        /// </summary>
        private static float NextSpawnDelay(StageWave wave, int spawned)
        {
            int length = wave.PartyLength(spawned);
            if (length <= 1)
                return wave.SpawnInterval;
            if (spawned + 1 < wave.Count && !wave.StartsParty(spawned + 1))
                return StageParty.SpawnGap;
            return Mathf.Max(0.1f, wave.SpawnInterval * length - StageParty.SpawnGap * (length - 1));
        }

        public int EnemyCount()
        {
            int count = 0;
            if (Board != null)
                foreach (StageActor actor in Board.Actors)
                    if (actor != null && actor.IsEnemy && actor.IsAlive)
                        count++;
            return count;
        }

        /// <summary>카탈로그 정예가 살아 있는가. 등급 4인 일반 검사·성기사는 잠그지 않는다.</summary>
        private bool IsEliteAlive()
        {
            if (Board != null && Catalog != null)
                foreach (StageActor actor in Board.Actors)
                    if (actor != null && actor.IsEnemy && actor != _boss && actor.IsAlive && Catalog.IsElite(actor.Enemy.Data))
                        return true;
            return false;
        }

        public int Remaining => Phase == StagePhase.Fighting
            ? EnemyCount() + Definition.Waves[WaveIndex].Count - _spawned : 0;

        public void FinishEnemy(StageActor actor, bool arrived)
        {
            if (Board == null || !Board.Actors.Remove(actor))
                return;
            actor.Stop();
            int coreDamage = 0;
            if (Phase == StagePhase.Fighting)
            {
                if (arrived)
                {
                    int before = CoreHealth;
                    mapView.CoreHealth.TakeDamage(actor.Enemy.Data.CoreArrivalDamage);
                    coreDamage = before - CoreHealth;
                }
                else
                    Gold += Definition.KillReward;
                Results.RecordEnemy(arrived, coreDamage, arrived ? 0 : Definition.KillReward, actor == _boss);
                if (CoreHealth <= 0)
                {
                    Results.Complete(WaveIndex + 1, false);
                    Phase = StagePhase.Failed;
                    _checkpoint?.End();
                    StopActors();
                    // 끝까지 막은 웨이브 수 = 지금 웨이브 번호. 깨지 못한 스테이지에서만 준다(2026-11-02 사용자 결정).
                    bool saved = Profile.TryAwardFailure(Definition, Catalog.FailureReward(Definition, WaveIndex), out int awarded);
                    Results.RecordFailureReward(awarded, saved);
                    Tell(saved ? "코어가 파괴됐습니다. 스테이지 처음부터 다시 편성할 수 있습니다" : Profile.Error);
                }
            }
            actor.gameObject.SetActive(false);
            Destroy(actor.gameObject);
            Notify();
            if (coreDamage > 0)
                CoreDamaged?.Invoke(coreDamage);
        }

        private void EndWave()
        {
            Results.Complete(WaveIndex + 1, true);
            WaveIndex++;
            Devices.Reset();
            foreach (StageActor actor in Board.Actors)
                if (actor != null && !actor.IsEnemy)
                    actor.Recover();
            if (WaveIndex == Definition.Waves.Length)
            {
                Phase = StagePhase.Cleared;
                RetryClearReward();
            }
            else
            {
                Phase = StagePhase.Preparing;
                mapView.ShowDoors(Definition, WaveIndex);
                Tell(Definition.OpensDoorAt(WaveIndex)
                    ? "웨이브 방어 완료. 다음 웨이브에 새 던전문이 열립니다 — 길을 다시 확인하세요"
                    : "웨이브 방어 완료. 코어 체력은 유지되며 부하는 복구되었습니다");
            }
        }

        public void RetryClearReward()
        {
            if (Phase != StagePhase.Cleared)
                return;
            bool previouslyClaimed = Profile.IsCleared(Definition.Id);
            // 유물은 초회 클리어에만 3택1 후보를 준다(2026-10-29 사용자 결정).
            if (Profile.TryClear(Definition, previouslyClaimed ? null : DrawArtifactChoices(), HasArtifactChoice, out int awarded))
            {
                Results.ConfirmReward(awarded, previouslyClaimed);
                _checkpoint?.End();
                Tell(StageIndex + 1 < Catalog.Stages.Length ? "다음 스테이지를 준비하세요" : "모든 스테이지를 방어했습니다");
            }
            else
                Tell(Profile.Error);
        }

        public void TryUpgrade(StageOption option)
        {
            if (!CanUpgrade)
            {
                Tell("강화는 스테이지 밖이나 첫 배치 전에 가능합니다");
                return;
            }
            bool changed = Profile.TryUpgrade(option, Catalog);
            Tell(changed ? "영구 강화 완료" : !string.IsNullOrEmpty(Profile.Error) ? Profile.Error : "강화 재화/해금/최대 단계를 확인하세요");
        }

        public bool TryPlace(Vector2Int cell, StageOption option, bool barricade) => Placement.TryPlace(cell, option, barricade);

        public void Demolish(Vector2Int cell) => Placement.Demolish(cell);

        /// <summary>준비 단계에만 배치한 부하 한 명의 명령을 바꾼다(2026-11-01 사용자 결정).</summary>
        public bool TrySetCommand(StageActor actor, StageUnitCommand command)
        {
            if (Phase != StagePhase.Preparing || actor == null || actor.IsEnemy || !actor.IsAlive
                || Board == null || !Board.Actors.Contains(actor))
                return false;
            if (actor.Command == command)
                return true;
            actor.SetCommand(command);
            Tell($"{actor.Option?.Label} 명령: {StageUnitCommandRules.Label(command)}");
            return true;
        }

        public StageActor FindOpponent(StageActor self, Vector2Int home)
        {
            StageActor best = null;
            // 부하는 명령(사수·기본·추격)에 따라 알아채는 거리와 배치 칸에서 쫓는 거리가 달라진다
            float range = Mathf.Max(self.EngageRadius, self.Combat.StrikeRange);
            float score = range * range;
            float leash = self.IsEnemy ? 0f : Mathf.Max(self.LeashRadius, self.Combat.StrikeRange);
            foreach (StageActor other in Board.Actors)
            {
                if (other == null || other == self || !other.IsAlive || other.IsEnemy == self.IsEnemy)
                    continue;
                if (!self.IsEnemy && Vector2.Distance(Board.World(home), other.transform.position) > leash)
                    continue;
                float distance = (self.transform.position - other.transform.position).sqrMagnitude;
                if (distance < score && CanSee(self.transform.position, other.transform.position))
                {
                    best = other;
                    score = distance;
                }
            }
            return best;
        }

        public bool CanAdvance(StageActor self, Vector3 next)
        {
            float spacing = Definition.CellSize * 0.4f;
            foreach (StageActor other in Board.Actors)
            {
                if (other == null || other == self || !other.IsAlive || other.IsEnemy != self.IsEnemy)
                    continue;
                float before = (self.transform.position - other.transform.position).sqrMagnitude;
                float after = (next - other.transform.position).sqrMagnitude;
                if (after < spacing * spacing && after < before)
                    return false;
            }
            return true;
        }

        public bool CanSee(Vector3 from, Vector3 to)
        {
            float length = Vector2.Distance(from, to);
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / (Definition.CellSize * 0.25f)));
            for (int i = 1; i < samples; i++)
            {
                Vector2Int cell = Board.Cell(Vector3.Lerp(from, to, (float)i / samples));
                if (!Board.IsPassable(cell))
                    return false;
                if (Board.Barricades.TryGetValue(cell, out StageBarricade wall) && wall != null && wall.IsAlive)
                    return false;
            }
            return true;
        }

        public void CheckTraps(StageActor enemy) => Placement.CheckTraps(enemy);

        /// <summary>전투 중 준비된 낙석 칸을 누르면 터뜨린다.</summary>
        public bool TryTriggerDevice(Vector2Int cell)
        {
            if (Phase != StagePhase.Fighting || Devices == null || !Devices.TryTrigger(cell))
                return false;
            Tell("낙석");
            return true;
        }

        public void Refund(int amount)
        {
            if (Phase != StagePhase.Preparing && Phase != StagePhase.Fighting)
                return;
            int refund = Mathf.Max(0, amount);
            Gold += refund;
            if (Phase == StagePhase.Fighting)
                Results.AddGold(refund);
            Notify();
        }

        public void Tell(string message)
        {
            Message = message;
            Notify();
        }

        internal void Pay(int amount) => Gold -= amount;
        internal void RefreshUI() => Notify();
        private void Notify()
        {
            _checkpoint?.MarkDirty();
            Changed?.Invoke();
        }

        internal void RestorePreparation(int nextWave, bool locked, int coreHealth, int gold, IEnumerable<string> loadout,
            IEnumerable<string> artifacts)
        {
            WaveIndex = nextWave;
            _locked = locked;
            Gold = gold;
            _loadout.Clear();
            foreach (string id in loadout)
                _loadout.Add(id);
            Artifacts.Clear();
            foreach (string id in artifacts)
                Artifacts.Equip(Catalog.FindArtifact(id));
            mapView.CoreHealth.SetCurrentRatio((float)coreHealth / mapView.CoreHealth.MaxHealth);
            mapView.ShowDoors(Definition, WaveIndex);
        }

        private void LateUpdate() => _checkpoint?.Tick();

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                _checkpoint?.Save();
        }

        /// <summary>씬 객체가 파괴되기 전에 준비 상태를 저장한다.</summary>
        public void PrepareToLeave()
        {
            if (_leaving)
                return;
            _checkpoint?.Save();
            _leaving = true;
        }

        private void OnApplicationQuit() => PrepareToLeave();

        private void StopActors()
        {
            if (Board == null)
                return;
            foreach (StageActor actor in Board.Actors)
                if (actor != null)
                    actor.Stop();
        }

        private void OnDisable()
        {
            if (!_leaving)
                _checkpoint?.Save();
            StopActors();
            speedController?.SetPauseLock(this, false, null);
        }

        private void OnDestroy() => Artifacts?.Dispose();
    }
}
