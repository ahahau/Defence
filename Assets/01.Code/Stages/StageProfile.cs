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
        }

        private readonly string _path;
        private Document _document = new() { version = 1 };
        private bool _canWrite = true;
        public string Error { get; private set; }
        public int Currency => _document.currency;

        public StageProfile(string folder)
        {
            _path = Path.Combine(folder, "defence-stage-profile-v1.json");
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

        public bool TryClear(StageDefinitionSO definition, out int awarded)
        {
            awarded = 0;
            if (IsCleared(definition.Id))
                return true;
            Document next = Clone();
            int reward = Mathf.Max(0, definition.ClearReward);
            next.cleared.Add(definition.Id);
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
