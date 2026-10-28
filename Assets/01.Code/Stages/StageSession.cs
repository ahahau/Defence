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
        private float _spawnClock;
        private int _spawned;
        private bool _locked;
        public StageDefinitionSO Definition { get; private set; }
        public StageBoard Board { get; private set; }
        public StagePathfinder Paths { get; private set; }
        public StageSkillField SkillField { get; private set; }
        public StageDevices Devices { get; private set; }
        public StageProfile Profile { get; private set; }
        public StagePhase Phase { get; private set; } = StagePhase.Unavailable;
        public int Gold { get; private set; }
        public int WaveIndex { get; private set; }
        public int StageIndex { get; private set; }
        public string Message { get; private set; }
        public int CoreHealth => mapView.CoreHealth.CurrentHealth;
        public StagePlacement Placement { get; private set; }
        public IReadOnlyList<Trap> Traps => Placement.Traps;
        public int SelectedCount => _loadout.Count;
        public bool CanChoose => Phase == StagePhase.Preparing && !_locked;
        public bool CanUpgrade => Phase == StagePhase.Cleared || Phase == StagePhase.Failed
            || CanChoose && !Placement.HasPlacements;
        public bool CanChangeStage => Phase == StagePhase.Cleared || Phase == StagePhase.Failed
            || CanChoose && !Placement.HasPlacements && Board.Barricades.Count == 0;
        public event Action Changed;

        public bool CanSelectStage(int index) => Catalog != null && index >= 0 && index < Catalog.Stages.Length
            && (index == 0 || Profile.IsCleared(Catalog.Stages[index - 1].Id));

        private void Start()
        {
            Profile = new StageProfile(Application.persistentDataPath);
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
            WaveIndex = 0;
            _spawned = 0;
            _locked = false;
            Phase = StagePhase.Preparing;
            mapView.Draw(Board);
            mapView.ShowDoors(definition, WaveIndex);
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
            _locked = true;
            _spawned = 0;
            _spawnClock = 0;
            Phase = StagePhase.Fighting;
            Tell("침입자가 코어로 접근합니다");
        }

        public bool IsBossAlive => _boss != null && _boss.IsAlive;

        private void Update()
        {
            // 보스가 살아 있는 동안만 멈출 수 없다. 웨이브 종료·실패로 보스가 사라지면 바로 풀린다.
            speedController?.SetPauseLock(this, Phase == StagePhase.Fighting && IsBossAlive, Code.Manager.PauseLockRules.BossLockReason);
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
                Vector2Int door = Definition.Doors[Definition.OpenDoorIndex(WaveIndex, _spawned % open)];
                Enemy enemy = Instantiate(data.Prefab, Board.World(door), Quaternion.identity, actorsRoot);
                enemy.ConfigureData(data);
                if (isBoss)
                    enemy.PromoteToBoss(wave.BossHealthMultiplier, wave.BossAttackMultiplier, wave.BossScale);
                StageActor actor = enemy.gameObject.AddComponent<StageActor>();
                actor.Initialize(this, door, null, 0);
                actor.SetRule(wave.RuleAt(_spawned));
                if (isBoss)
                {
                    _boss = actor;
                    Tell($"보스 {data.Name} 등장");
                }
                _spawned++;
                _spawnClock = wave.SpawnInterval;
                Notify();
            }
            if (_spawned >= wave.Count && EnemyCount() == 0)
                EndWave();
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

        public int Remaining => Phase == StagePhase.Fighting
            ? EnemyCount() + Definition.Waves[WaveIndex].Count - _spawned : 0;

        public void FinishEnemy(StageActor actor, bool arrived)
        {
            if (Board == null || !Board.Actors.Remove(actor))
                return;
            actor.Stop();
            if (Phase == StagePhase.Fighting)
            {
                if (arrived)
                    mapView.CoreHealth.TakeDamage(actor.Enemy.Data.CoreArrivalDamage);
                else
                    Gold += Definition.KillReward;
                if (CoreHealth <= 0)
                {
                    Phase = StagePhase.Failed;
                    StopActors();
                    Tell("코어가 파괴됐습니다. 스테이지 처음부터 다시 편성할 수 있습니다");
                }
            }
            actor.gameObject.SetActive(false);
            Destroy(actor.gameObject);
            Notify();
        }

        private void EndWave()
        {
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
            if (Profile.TryClear(Definition, out int awarded))
                Tell(awarded > 0 ? $"스테이지 클리어 · 강화 재화 +{awarded}" : "스테이지 클리어 · 초회 보상은 이미 받았습니다");
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

        public StageActor FindOpponent(StageActor self, Vector2Int home)
        {
            StageActor best = null;
            float range = Mathf.Max(Definition.EngageRadius, self.Combat.StrikeRange);
            float score = range * range;
            foreach (StageActor other in Board.Actors)
            {
                if (other == null || other == self || !other.IsAlive || other.IsEnemy == self.IsEnemy)
                    continue;
                if (!self.IsEnemy && Vector2.Distance(Board.World(home), other.transform.position) > Mathf.Max(Definition.LeashRadius, self.Combat.StrikeRange))
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
                if (!Definition.IsFloor(cell))
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
            Gold += Mathf.Max(0, amount);
            Notify();
        }

        public void Tell(string message)
        {
            Message = message;
            Notify();
        }

        internal void Pay(int amount) => Gold -= amount;
        internal void RefreshUI() => Notify();
        private void Notify() => Changed?.Invoke();

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
            StopActors();
            speedController?.SetPauseLock(this, false, null);
        }
    }
}
