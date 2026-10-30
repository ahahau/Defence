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
        private bool _dirty;
        private float _nextSave;
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
                if (!Validate(data, out int stage, out string reason))
                {
                    _blocked = true;
                    _store.Block($"이어하기 불가: {reason}\n타이틀에서 새 게임을 선택하세요.\n기존 저장·백업을 보호합니다.");
                    return false;
                }
                if (!_session.SelectStage(stage))
                    throw new InvalidOperationException("스테이지를 생성하지 못했습니다");
                _session.RestorePreparation(data.nextWave, data.locked, data.coreHealth, data.gold, data.loadout,
                    data.artifacts ?? new List<string>());
                _session.Results.Restore(data.hasWaveReport ? data.waveReport : null);
                foreach (StageSavedPlacement saved in data.placements)
                    _session.Placement.Restore(saved, Option(saved.optionId));
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
            if (!_dirty)
                _nextSave = Mathf.Min(_nextSave, Time.unscaledTime + 0.35f);
            _dirty = true;
        }

        internal void Tick()
        {
            // 바뀐 것이 없으면 쓰지 않는다. 매번 쓰면 .bak까지 직전 상태로 밀려 백업이 의미를 잃는다.
            if (_dirty && Time.unscaledTime >= _nextSave)
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
                    nextWave = _session.WaveIndex,
                    locked = !_session.CanChoose,
                    coreHealth = _session.CoreHealth,
                    gold = _session.Gold,
                    loadout = new List<string>(),
                    placements = new List<StageSavedPlacement>(),
                    hasWaveReport = _session.Results.Latest != null,
                    waveReport = _session.Results.Latest,
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
                if (!_store.Write(data) && !string.IsNullOrEmpty(Error))
                {
                    _blocked = true;
                    _session.Tell(Error);
                }
                _dirty = false;
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
            if (data.nextWave < 0 || data.nextWave >= definition.Waves.Length
                || data.locked != (data.nextWave > 0) || data.coreHealth <= 0 || data.coreHealth > definition.CoreHealth)
                return Reject("웨이브·편성 잠금 또는 코어 체력이 유효하지 않습니다.", out reason);
            HashSet<string> selected = new(StringComparer.Ordinal);
            if (data.hasWaveReport && (data.waveReport == null
                || !data.waveReport.MatchesPreparation(definition, data.nextWave, data.coreHealth)))
                return Reject("마지막 결과 요약이 직전 완료 웨이브와 맞지 않습니다.", out reason);
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
            StageBoard board = new(definition);
            HashSet<Vector2Int> occupied = new();
            foreach (StageSavedPlacement saved in data.placements)
            {
                if (saved == null || saved.paid < 0 || !definition.IsBuildable(saved.cell) || board.Reserved(saved.cell)
                    || !Finite(saved.cooldown) || saved.cooldown < 0)
                    return Reject("배치 좌표·설치비 또는 대기시간이 유효하지 않습니다.", out reason);
                if (saved.kind == "barricade")
                {
                    if (!string.IsNullOrEmpty(saved.optionId) || saved.health <= 0 || saved.health > definition.BarricadeHealth
                        || !Finite(saved.recoveryDelay) || saved.recoveryDelay < 0 || saved.recoveryDelay > definition.RecoveryDelay
                        || !Finite(saved.recoveryFraction) || saved.recoveryFraction < 0 || saved.recoveryFraction >= 1
                        || !occupied.Add(saved.cell))
                        return Reject("바리케이드 체력·회복 또는 점유 값이 유효하지 않습니다.", out reason);
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
                        || !Enum.IsDefined(typeof(UnitCommand), saved.condition.Command))
                        return Reject("부하 위치·체력 또는 개체 상태가 유효하지 않습니다.", out reason);
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
                    for (int y = 0; y < template.Footprint.y; y++)
                    for (int x = 0; x < template.Footprint.x; x++)
                    {
                        Vector2Int cell = saved.cell + new Vector2Int(x, y);
                        if (!definition.IsBuildable(cell) || board.Reserved(cell) || !occupied.Add(cell))
                            return Reject("함정 면적이 설치 불가 칸을 포함하거나 다른 배치와 겹칩니다.", out reason);
                    }
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

        private static string Signature(StageDefinitionSO definition)
        {
            StringBuilder text = new();
            foreach (string row in definition.Layout)
                text.Append(row).Append('\n');
            foreach (Vector2Int door in definition.Doors)
                text.Append(door.x).Append(',').Append(door.y).Append(';');
            foreach (int wave in definition.DoorOpensAtWave)
                text.Append(wave).Append(';');
            text.Append(definition.Core.x).Append(',').Append(definition.Core.y).Append(';')
                .Append(definition.CoreHealth).Append(';').Append(definition.BarricadeHealth).Append(';')
                .Append(definition.Waves.Length).Append(';').Append(definition.CellSize.ToString("R", CultureInfo.InvariantCulture));
            foreach (StageDevice device in definition.Devices)
                text.Append(JsonUtility.ToJson(device));
            using SHA256 hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }
}
