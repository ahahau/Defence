using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>구 런/메타 파일과 분리한 강화 기록. 저장 성공 뒤에만 구매/보상 상태를 바꾼다.</summary>
    public sealed class StageProfile
    {
        [Serializable]
        private sealed class Upgrade
        {
            [SerializeField] internal string id;
            [SerializeField] internal int level;
        }

        [Serializable]
        private sealed class Document
        {
            [SerializeField] internal int version;
            [SerializeField] internal int currency;
            [SerializeField] internal List<string> cleared = new();
            [SerializeField] internal List<Upgrade> upgrades = new();
            // 유물(2026-10-29): 없던 기존 파일은 빈 목록으로 읽는다. 후보는 고르기 전에 나가도 다시 뽑지 않게 남긴다.
            [SerializeField] internal List<string> artifacts = new();
            [SerializeField] internal List<string> artifactChoices = new();
        }

        private const string FileName = "defence-stage-profile-v1.json";
        private readonly string _path;
        private Document _document = new() { version = 1 };
        private bool _canWrite = true;
        public string Error { get; private set; }
        public int Currency => _document.currency;
        public IReadOnlyList<string> Artifacts => _document.artifacts;
        public IReadOnlyList<string> ArtifactChoices => _document.artifactChoices;
        public bool HasArtifact(string id) => !string.IsNullOrEmpty(id) && _document.artifacts.Contains(id);

        public StageProfile(string folder)
        {
            _path = Path.Combine(folder, FileName);
            if (!File.Exists(_path) && !File.Exists(_path + ".bak"))
                return;
            if (TryRead(_path, out Document loaded))
            {
                _document = loaded;
                return;
            }
            if (TryRead(_path + ".bak", out loaded))
                _document = loaded;
            _canWrite = false;
            Error = "강화 저장 원본을 읽지 못했습니다. 원본/백업 보호를 위해 저장을 중단했습니다.";
            Debug.LogError(Error);
        }

        public bool IsCleared(string id) => !string.IsNullOrEmpty(id) && _document.cleared.Contains(id);

        /// <summary>
        /// 저장된 프로필에서 클리어 여부만 읽는다(타이틀 기록 표시용). 쓰기 차단·오류 로그 없이, 원본을 못 읽으면 백업을, 둘 다 못 읽으면 거짓이다.
        /// </summary>
        public static bool IsClearedOnDisk(string folder, string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            string path = Path.Combine(folder, FileName);
            return (TryRead(path, out Document document) || TryRead(path + ".bak", out document)) && document.cleared.Contains(id);
        }
        public bool IsUnlocked(StageOption option) => option != null
            && (string.IsNullOrEmpty(option.RequiredClear) || IsCleared(option.RequiredClear));

        /// <summary>기본 슬롯에 슬롯 보상 스테이지를 처음 깬 수만큼 더한다.</summary>
        public int LoadoutSlots(StageCatalogSO catalog)
        {
            int slots = catalog.BaseLoadoutSlots;
            foreach (string id in catalog.SlotRewardStages)
                if (IsCleared(id))
                    slots++;
            return slots;
        }

        public int Level(string id)
        {
            foreach (Upgrade upgrade in _document.upgrades)
                if (upgrade.id == id)
                    return upgrade.level;
            return 0;
        }

        /// <summary>첫 클리어를 기록한다. 유물 후보도 같은 저장에 넣어 보상과 후보가 따로 남지 않게 한다.</summary>
        /// <param name="keepPendingChoices">남은 후보 중 현재 카탈로그에 있는 것이 있으면 참. 거짓이면 남은 후보를 새 후보로 바꾼다.</param>
        public bool TryClear(StageDefinitionSO definition, IReadOnlyList<string> artifactChoices, bool keepPendingChoices,
            out int awarded)
        {
            awarded = 0;
            if (IsCleared(definition.Id))
                return true;
            Document next = Clone();
            int reward = Mathf.Max(0, definition.ClearReward);
            next.cleared.Add(definition.Id);
            next.currency = checked(next.currency + reward);
            // 고를 수 있는 후보가 남아 있으면 덮지 않는다(정상 흐름에서는 창이 고를 때까지 닫히지 않는다).
            // 카탈로그에서 빠진 후보만 남았으면 고를 방법이 없으므로 새 후보로 바꾼다.
            if (artifactChoices != null && !keepPendingChoices)
            {
                next.artifactChoices.Clear();
                next.artifactChoices.AddRange(artifactChoices);
            }
            if (!Commit(next))
                return false;
            awarded = reward;
            return true;
        }

        /// <summary>
        /// 아직 깨지 못한 스테이지의 패배 보상을 더한다(2026-11-02 사용자 결정). 깬 스테이지거나 0이면 저장하지 않는다.
        /// </summary>
        public bool TryAwardFailure(StageDefinitionSO definition, int reward, out int awarded)
        {
            awarded = 0;
            if (definition == null || IsCleared(definition.Id) || reward <= 0)
                return true;
            Document next = Clone();
            next.currency = checked(next.currency + reward);
            if (!Commit(next))
                return false;
            awarded = reward;
            return true;
        }

        public bool TryUpgrade(StageOption option, StageCatalogSO catalog)
        {
            if (!IsUnlocked(option))
                return false;
            int level = Level(option.Id);
            int cost = checked(catalog.UpgradeBaseCost * (level + 1));
            if (level >= catalog.UpgradeLimit || cost < 0 || Currency < cost)
                return false;
            Document next = Clone();
            next.currency -= cost;
            Upgrade found = next.upgrades.Find(entry => entry.id == option.Id);
            if (found == null)
                next.upgrades.Add(new Upgrade { id = option.Id, level = 1 });
            else
                found.level++;
            return Commit(next);
        }

        /// <summary>남은 후보 중 하나를 영구 보유로 옮기고 후보를 비운다.</summary>
        public bool TryPickArtifact(string id)
        {
            if (string.IsNullOrEmpty(id) || !_document.artifactChoices.Contains(id))
                return false;
            Document next = Clone();
            next.artifactChoices.Clear();
            if (!next.artifacts.Contains(id))
                next.artifacts.Add(id);
            return Commit(next);
        }

        private Document Clone() => JsonUtility.FromJson<Document>(JsonUtility.ToJson(_document));

        private bool Commit(Document next)
        {
            if (!_canWrite)
                return false;
            string temp = _path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(temp, JsonUtility.ToJson(next, true), System.Text.Encoding.UTF8);
                if (File.Exists(_path))
                    File.Replace(temp, _path, _path + ".bak");
                else
                    File.Move(temp, _path);
                _document = next;
                Error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Error = "강화 기록을 저장하지 못했습니다. 보상/구매는 확정하지 않았습니다.";
                Debug.LogError(Error + " " + exception.Message);
                return false;
            }
        }

        private static bool TryRead(string path, out Document document)
        {
            document = null;
            try
            {
                if (!File.Exists(path))
                    return false;
                Document loaded = JsonUtility.FromJson<Document>(File.ReadAllText(path));
                if (loaded == null || loaded.version != 1 || loaded.currency < 0
                    || loaded.cleared == null || loaded.upgrades == null)
                    return false;
                HashSet<string> ids = new(StringComparer.Ordinal);
                foreach (string id in loaded.cleared)
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                        return false;
                ids.Clear();
                foreach (Upgrade upgrade in loaded.upgrades)
                    if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.id) || upgrade.level < 0 || !ids.Add(upgrade.id))
                        return false;
                loaded.artifacts ??= new List<string>();
                loaded.artifactChoices ??= new List<string>();
                ids.Clear();
                foreach (string id in loaded.artifacts)
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                        return false;
                HashSet<string> choices = new(StringComparer.Ordinal);
                foreach (string id in loaded.artifactChoices)
                    if (string.IsNullOrWhiteSpace(id) || ids.Contains(id) || !choices.Add(id))
                        return false;
                document = loaded;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException)
            {
                return false;
            }
        }
    }
}
