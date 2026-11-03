using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Code.Buildings;
using Code.Units;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>준비 단계만 캡처하고, 전체 검증 뒤 복원한다. 파일 보호는 저장소가 맡는다.</summary>
    internal sealed class StageCheckpoint
    {
        private readonly StageSession _session;
        private readonly StageCheckpointStore _store;
        private bool _restoring;
        private bool _blocked;
        private bool _ended;
        private float _nextSave;
        private string _lastSavedState;
        public string Error => _store.Error;

        internal StageCheckpoint(StageSession session, string folder)
        {
            _session = session;
            _store = new StageCheckpointStore(folder);
        }

        internal bool Restore()
        {
            _restoring = true;
            try
            {
                if (!_store.Read(out StageCheckpointData data))
                {
                    _blocked = !string.IsNullOrEmpty(Error);
                    return false;
                }
                _blocked = !string.IsNullOrEmpty(Error);
                if (data != null)
                    _session.UseCheckpointCatalog(data.stageId, data.layoutSignature);
                if (!Validate(data, out int stage, out string reason))
                {
                    _blocked = true;
                    _store.Block($"이어하기 불가: {reason}\n타이틀에서 새 게임을 선택하세요.\n기존 저장·백업을 보호합니다.");
                    return false;
                }
                if (!_session.SelectStage(stage))
                    throw new InvalidOperationException("스테이지를 생성하지 못했습니다");
                _session.Devices.RestoreDirections(data.pushDirections);
                _session.RestorePreparation(data.nextWave, data.locked, data.coreHealth, data.gold, data.loadout,
                    data.artifacts ?? new List<string>());
                // 누계가 없는 구 저장은 첫 웨이브 전이 아니면 누계를 모르는 채로 둔다(클리어 때 누계 줄을 숨긴다)
                _session.Results.Restore(data.hasWaveReport ? data.waveReport : null,
                    data.hasStageTotals ? data.stageTotals : data.nextWave == 0 ? StageWaveReport.Empty : null);
                _session.PreparationEvents.Set(data.supplyChoice, data.supplyRepaired);
                _session.PreparationEvents.SetFollowUp(data.followUpChoice, data.followUpRepaired);
                // 바리케이드 체력·부하 공격력 배율이 붙도록 배치보다 먼저 되살린다
                _session.Merchant.Set(_session.Catalog, data.merchantItems, data.merchantRepairWave, data.merchantRepaired);
                foreach (StageSavedPlacement saved in data.placements)
                    _session.Placement.Restore(saved, Option(saved.optionId));
                _lastSavedState = JsonUtility.ToJson(data);
                _session.Tell(_blocked ? Error : "마지막 준비 단계를 이어합니다. 전투 중 종료했다면 해당 웨이브부터 재시작합니다");
                _nextSave = Time.unscaledTime + 5f;
                return true;
            }
            catch (Exception exception)
            {
                _blocked = true;
                _store.Block($"스테이지 복원 실패: 데이터/배치가 현재 콘텐츠와 맞지 않습니다. 기존 파일을 보호합니다. {exception.Message}");
                return false;
            }
            finally
            {
                _restoring = false;
            }
        }

        internal void MarkDirty()
        {
            if (_restoring || _blocked || _ended || _session.Phase != StagePhase.Preparing)
                return;
            _nextSave = Mathf.Min(_nextSave, Time.unscaledTime + 0.35f);
        }

        internal void Tick()
        {
            if (_restoring || _blocked || _ended || _session.Phase != StagePhase.Preparing)
            {
                return;
            }
            // 조작이 없어도 회복·대기시간은 바뀐다. 파일 교체 여부는 캡처한 내용을 비교해 정한다.
            if (Time.unscaledTime >= _nextSave)
                Save();
        }

        internal void Save()
        {
            if (_restoring || _blocked || _ended || _session.Phase != StagePhase.Preparing)
                return;
            try
            {
                StageCheckpointData data = new()
                {
                    version = 1,
                    stageId = _session.Definition.Id,
                    layoutSignature = Signature(_session.Definition),
                    pushDirections = _session.Devices.CaptureDirections(),
                    nextWave = _session.WaveIndex,
                    locked = !_session.CanChoose,
                    coreHealth = _session.CoreHealth,
                    gold = _session.Gold,
                    loadout = new List<string>(),
                    placements = new List<StageSavedPlacement>(),
                    hasWaveReport = _session.Results.Latest != null,
                    waveReport = _session.Results.Latest,
                    hasStageTotals = _session.Results.Totals != null,
                    stageTotals = _session.Results.Totals,
                    supplyChoice = _session.PreparationEvents.Choice,
                    supplyRepaired = _session.PreparationEvents.CoreRepaired,
                    followUpChoice = _session.PreparationEvents.FollowUpChoice,
                    followUpRepaired = _session.PreparationEvents.FollowUpRepaired,
                    merchantItems = new List<string>(_session.Merchant.Bought),
                    merchantRepairWave = _session.Merchant.RepairWave,
                    merchantRepaired = _session.Merchant.CoreRepaired,
                    artifacts = new List<string>()
                };
                foreach (StageArtifact artifact in _session.Artifacts.Equipped)
                    data.artifacts.Add(artifact.Id);
                foreach (StageOption option in _session.Catalog.Options)
                    if (_session.IsSelected(option))
                        data.loadout.Add(option.Id);
                _session.Placement.Capture(data.placements);
                if (!Validate(data, out _, out string reason))
                    throw new InvalidOperationException(reason);
                string state = JsonUtility.ToJson(data);
                // 같은 상태를 다시 써서 직전의 다른 상태를 담은 백업까지 밀어내지 않는다.
                if (!string.Equals(state, _lastSavedState, StringComparison.Ordinal))
                {
                    if (_store.Write(data))
                    {
                        _lastSavedState = state;
                    }
                    else if (!string.IsNullOrEmpty(Error))
                    {
                        _blocked = true;
                        _session.Tell(Error);
                    }
                }
                _nextSave = Time.unscaledTime + 5f;
            }
            catch (Exception exception)
            {
                _blocked = true;
                _store.Block($"스테이지 자동 저장 중단: {exception.Message}");
                _session.Tell(Error);
            }
        }

        internal void End()
        {
            _ended = true;
            if (!StageCheckpointStore.EndRun(Application.persistentDataPath))
            {
                _blocked = true;
                _store.Block("진행 종료를 저장하지 못했습니다. 이전 준비 저장이 남아 있습니다");
                _session.Tell(Error);
            }
        }

        internal void NewStage()
        {
            // 복원 실패 상태는 씬을 다시 열거나 명시적 새 게임을 선택하기 전까지 유지한다.
            if (_blocked)
                return;
            if (_ended)
            {
                _store.Read(out _);
                _blocked = !string.IsNullOrEmpty(Error);
                _ended = false;
            }
            // 종료된 파일이 보관된 뒤에는 같은 초기 상태라도 새 체크포인트가 필요하다.
            _lastSavedState = null;
            MarkDirty();
        }

        private StageOption Option(string id)
        {
            foreach (StageOption option in _session.Catalog.Options)
                if (option != null && option.Id == id)
                    return option;
            return null;
        }

        private bool Validate(StageCheckpointData data, out int stage, out string reason)
        {
            stage = -1;
            reason = string.Empty;
            if (data == null)
                return Reject("저장 내용이 없습니다.", out reason);
            if (data.version != 1)
                return Reject("지원하지 않는 저장 버전입니다.", out reason);
            if (string.IsNullOrWhiteSpace(data.stageId) || data.loadout == null || data.placements == null)
                return Reject("스테이지 ID 또는 편성·배치 목록이 없습니다.", out reason);
            if (data.gold < 0 || data.loadout.Count > _session.LoadoutSlots)
                return Reject("금화 또는 편성 수가 현재 규칙과 맞지 않습니다.", out reason);
            for (int i = 0; i < _session.Catalog.Stages.Length; i++)
                if (_session.Catalog.Stages[i]?.Id == data.stageId)
                {
                    if (stage >= 0)
                        return Reject("현재 카탈로그에 스테이지 ID가 중복됩니다.", out reason);
                    stage = i;
                }
            if (stage < 0)
                return Reject("저장된 스테이지가 현재 콘텐츠에 없습니다.", out reason);
            if (!_session.CanSelectStage(stage))
                return Reject("저장된 스테이지가 현재 프로필에서 해금되지 않았습니다.", out reason);
            StageDefinitionSO definition = _session.Catalog.Stages[stage];
            if (!definition.TryValidate(out string definitionError))
                return Reject($"현재 스테이지 설정 오류: {definitionError}", out reason);
            if (data.layoutSignature != Signature(definition))
                return Reject("맵·설정이 변경됐습니다.", out reason);
            if (!StageDevices.ValidDirections(definition, data.pushDirections))
                return Reject("밀치기 방향 목록이 현재 장치와 맞지 않습니다.", out reason);
            if (data.nextWave < 0 || data.nextWave >= definition.Waves.Length
                || data.locked != (data.nextWave > 0) || data.coreHealth <= 0 || data.coreHealth > definition.CoreHealth)
                return Reject("웨이브·편성 잠금 또는 코어 체력이 유효하지 않습니다.", out reason);
            HashSet<string> selected = new(StringComparer.Ordinal);
            if (!StagePreparationEvents.IsValidSaved(definition, data.nextWave, data.supplyChoice, data.supplyRepaired))
                return Reject("준비 사건 기록이 진행과 맞지 않습니다.", out reason);
            if (!StagePreparationEvents.IsValidSavedFollowUp(definition, data.nextWave, data.supplyChoice, data.followUpChoice,
                    data.followUpRepaired))
                return Reject("준비 사건 후속 기록이 진행과 맞지 않습니다.", out reason);
            if (!StageMerchant.IsValidSaved(_session.Catalog, data.nextWave, data.merchantItems, data.merchantRepairWave,
                    data.merchantRepaired))
                return Reject("상인 구매 기록이 진행과 맞지 않습니다.", out reason);
            // 금고 보수(사건·상인)로 되찾은 체력을 빼야 전과의 코어 피해와 실제 손실을 맞출 수 있다
            int unrepairedCore = data.coreHealth - data.supplyRepaired - data.followUpRepaired - data.merchantRepaired;
            float barricadeMultiplier = StageMerchant.BarricadeMultiplier(_session.Catalog, data.merchantItems);
            if (data.hasWaveReport && (data.waveReport == null
                || !data.waveReport.MatchesPreparation(definition, data.nextWave, unrepairedCore)))
                return Reject("마지막 결과 요약이 직전 완료 웨이브와 맞지 않습니다.", out reason);
            if (data.hasStageTotals && (data.stageTotals == null || !data.stageTotals.MatchesTotals(definition,
                    data.nextWave, unrepairedCore, data.hasWaveReport ? data.waveReport : null)))
                return Reject("스테이지 누계가 끝난 웨이브와 맞지 않습니다.", out reason);
            foreach (string id in data.loadout)
                if (string.IsNullOrEmpty(id) || !selected.Add(id) || !_session.Profile.IsUnlocked(Option(id)))
                    return Reject("편성에 중복되거나 현재 해금되지 않은 종류가 있습니다.", out reason);
            if (data.nextWave > 0 && selected.Count == 0)
                return Reject("진행 중인 스테이지의 편성이 비어 있습니다.", out reason);
            if (data.artifacts != null)
            {
                if (data.artifacts.Count > _session.Catalog.ArtifactSlots)
                    return Reject("장착 유물 수가 현재 칸 수를 넘습니다.", out reason);
                HashSet<string> equipped = new(StringComparer.Ordinal);
                foreach (string id in data.artifacts)
                    if (!equipped.Add(id ?? string.Empty) || !_session.HasArtifact(_session.Catalog.FindArtifact(id)))
                        return Reject("장착 유물이 중복되거나 현재 보유하지 않은 유물입니다.", out reason);
            }
            StageBoard board = new(definition, _session.Catalog);
            HashSet<Vector2Int> occupied = new();
            HashSet<Vector2Int> barricades = new();
            HashSet<Vector2Int> walls = new();
            foreach (StageSavedPlacement saved in data.placements)
            {
                // 벽 함정은 벽 칸이나 앞서 저장된 바리케이드에 붙는다(바닥 배치 검사와 따로 본다)
                if (saved != null && saved.kind == "wall")
                {
                    StageOption wallOption = Option(saved.optionId);
                    if (wallOption?.WallTrap == null || !selected.Contains(saved.optionId)
                        || saved.upgrade != _session.Profile.Level(wallOption.Id))
                        return Reject("벽 함정 종류·편성 또는 강화 단계가 현재 프로필과 맞지 않습니다.", out reason);
                    bool mount = barricades.Contains(saved.cell) || definition.TileAt(saved.cell) == '#';
                    if (saved.paid < 0 || !mount || !walls.Add(saved.cell)
                        || Mathf.Abs(saved.direction.x) + Mathf.Abs(saved.direction.y) != 1
                        || !definition.IsFloor(saved.cell + saved.direction)
                        || saved.level < 0 || saved.level > (_session.Catalog.EditsTrapsInWaves ? _session.Catalog.TrapMaxLevel : 1))
                        return Reject("벽 함정 자리·방향·설치비 또는 강화 단계가 유효하지 않습니다.", out reason);
                    continue;
                }
                if (saved == null || saved.paid < 0 || !definition.IsBuildable(saved.cell) || board.Reserved(saved.cell)
                    || !Finite(saved.cooldown) || saved.cooldown < 0)
                    return Reject("배치 좌표·설치비 또는 대기시간이 유효하지 않습니다.", out reason);
                if (saved.kind == "barricade")
                {
                    if (!string.IsNullOrEmpty(saved.optionId) || saved.health <= 0 || saved.health > StageMerchant.BarricadeHealth(definition,
                            barricadeMultiplier * board.Terrain.BarricadeMultiplier(saved.cell))
                        || !Finite(saved.recoveryDelay) || saved.recoveryDelay < 0 || saved.recoveryDelay > definition.RecoveryDelay
                        || !Finite(saved.recoveryFraction) || saved.recoveryFraction < 0 || saved.recoveryFraction >= 1
                        || !occupied.Add(saved.cell))
                        return Reject("바리케이드 체력·회복 또는 점유 값이 유효하지 않습니다.", out reason);
                    barricades.Add(saved.cell);
                    continue;
                }
                StageOption option = Option(saved.optionId);
                if (option == null || !selected.Contains(saved.optionId) || saved.upgrade != _session.Profile.Level(option.Id))
                    return Reject("배치 종류·편성 또는 강화 단계가 현재 프로필과 맞지 않습니다.", out reason);
                if (saved.kind == "unit")
                {
                    if (option.Unit == null || option.Unit.Prefab == null || !Finite(saved.position.x) || !Finite(saved.position.y)
                        || !Finite(saved.position.z) || saved.position.z != 0 || !definition.IsFloor(board.Cell(saved.position))
                        || !Finite(saved.condition.HealthRatio) || saved.condition.HealthRatio < 0 || saved.condition.HealthRatio > 1
                        || !Enum.IsDefined(typeof(UnitTrait), saved.condition.Trait)
                        || !Enum.IsDefined(typeof(UnitPersonality), saved.condition.Personality)
                        || !Enum.IsDefined(typeof(UnitCommand), saved.condition.Command)
                        || !Enum.IsDefined(typeof(StageUnitCommand), saved.command))
                        return Reject("부하 위치·체력·명령 또는 개체 상태가 유효하지 않습니다.", out reason);
                    // 실제 배치(StagePlacement.Check)처럼 부하 자리도 바리케이드·함정·다른 부하와 겹칠 수 없다.
                    if (!occupied.Add(saved.cell))
                        return Reject("부하 자리가 다른 배치와 겹칩니다.", out reason);
                }
                else if (saved.kind == "trap" && option.Trap?.Prefab is Trap template)
                {
                    if (saved.health <= 0 || saved.health > template.MaxDurability || !Finite(saved.active) || saved.active < 0
                        || saved.active > template.PersistentSeconds || !Finite(saved.tick) || saved.tick < 0
                        || saved.tickDamage < 0 || saved.cooldown > template.CooldownSeconds)
                        return Reject("함정 내구도·지속시간 또는 대기시간이 유효하지 않습니다.", out reason);
                    if (saved.pendingWear < 0 || saved.pendingWear > 0 && (saved.active <= 0f || !template.IsDestructible
                        || template.TriggerMode != TrapTriggerMode.Persistent || saved.pendingWear != template.WearPerTrigger
                        || saved.pendingWear < saved.health))
                        return Reject("지속 함정의 마지막 소모 기록이 작동 상태와 맞지 않습니다.", out reason);
                    // 0은 강화 단계가 없던 구 저장. 판 안 강화가 없는 카탈로그에서는 1단계만 있다.
                    int maxLevel = _session.Catalog.EditsTrapsInWaves ? _session.Catalog.TrapMaxLevel : 1;
                    if (saved.level < 0 || saved.level > maxLevel)
                        return Reject("함정 강화 단계가 유효하지 않습니다.", out reason);
                    for (int y = 0; y < template.Footprint.y; y++)
                    for (int x = 0; x < template.Footprint.x; x++)
                    {
                        Vector2Int cell = saved.cell + new Vector2Int(x, y);
                        if (!definition.IsBuildable(cell) || board.Reserved(cell) || !occupied.Add(cell))
                            return Reject("함정 면적이 설치 불가 칸을 포함하거나 다른 배치와 겹칩니다.", out reason);
                    }
                    // 지형을 나중에 더한 맵에서 막힌 칸 위에 저장된 함정은 되살리지 않는다
                    if (board.Terrain.BlockReason(saved.cell, template.Footprint, option.Trap).Length > 0)
                        return Reject("함정이 지금 지형에서 설치할 수 없는 칸에 있습니다.", out reason);
                }
                else
                    return Reject("배치 종류 또는 프리팹이 현재 콘텐츠와 맞지 않습니다.", out reason);
            }
            return true;
        }

        private static bool Reject(string message, out string reason)
        {
            reason = message;
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private string Signature(StageDefinitionSO definition) => SignatureFor(_session.Catalog, definition);

        internal static StageCatalogSO MatchingCatalog(StageCatalogSO current, StageCatalogSO legacy,
            string stageId, string signature)
        {
            foreach (StageCatalogSO catalog in new[] { current, legacy })
            {
                if (catalog == null || catalog.Stages == null)
                    continue;
                foreach (StageDefinitionSO definition in catalog.Stages)
                    if (definition != null && definition.Id == stageId && SignatureFor(catalog, definition) == signature)
                        return catalog;
            }
            return current;
        }

        internal static string SignatureFor(StageCatalogSO catalog, StageDefinitionSO definition)
        {
            StringBuilder text = new();
            // 지형 칸을 더해도 기존 저장을 "맵 변경"으로 막지 않는다. 지형 위 함정은 Validate가 따로 본다.
            foreach (string row in definition.Layout)
                text.Append(StageTerrain.NormalizeRow(catalog, row)).Append('\n');
            foreach (Vector2Int door in definition.Doors)
                text.Append(door.x).Append(',').Append(door.y).Append(';');
            foreach (int wave in definition.DoorOpensAtWave)
                text.Append(wave).Append(';');
            text.Append(definition.Core.x).Append(',').Append(definition.Core.y).Append(';')
                .Append(definition.CoreHealth).Append(';').Append(definition.BarricadeHealth).Append(';')
                .Append(definition.Waves.Length).Append(';').Append(definition.CellSize.ToString("R", CultureInfo.InvariantCulture));
            foreach (StageDevice device in definition.Devices)
                text.Append(device.CheckpointSignature());
            using SHA256 hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }
}
