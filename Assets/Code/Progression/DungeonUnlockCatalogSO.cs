using System;
using System.Collections.Generic;
using Code.Buildings;
using Code.Units;
using UnityEngine;

namespace Code.Progression
{
    [CreateAssetMenu(menuName = "SO/Progression/Dungeon Unlock Catalog", fileName = "DungeonUnlockCatalog")]
    public sealed class DungeonUnlockCatalogSO : ScriptableObject
    {
        [SerializeField] private List<DungeonUnlockEntry> entries = new();

        public IReadOnlyList<DungeonUnlockEntry> Entries => entries;

        public DungeonUnlockEntry Find(UnitDataSO unit)
        {
            return entries.Find(entry => entry != null && entry.Unit == unit);
        }

        public DungeonUnlockEntry Find(BuildingDataSO building)
        {
            return entries.Find(entry => entry != null && entry.Building == building);
        }

        public void ReplaceEntries(List<DungeonUnlockEntry> values)
        {
            entries = values ?? new List<DungeonUnlockEntry>();
        }
    }

    [Serializable]
    public sealed class DungeonUnlockEntry
    {
        [SerializeField] private UnitDataSO unit;
        [SerializeField] private BuildingDataSO building;
        [SerializeField] private bool startsUnlocked = true;

        [SerializeField, Min(0), Tooltip("이 일차가 되면 해금된다. 0이면 일차로는 열리지 않는다.")]
        private int unlockDay;

        [SerializeField, TextArea] private string unlockHint = "던전 발전 조건을 충족하면 해금됩니다.";

        [SerializeField, Min(0), Tooltip("메타 진행도가 이만큼 쌓여야 새 판 후보에 들어온다. 0이면 처음부터 후보다.")]
        private int metaProgressRequired;

        public int UnlockDay => unlockDay;
        public int MetaProgressRequired => metaProgressRequired;

        /// <summary>메타 진행도 조건을 채워 이번 판 후보에 들어왔는가.</summary>
        public bool IsMetaUnlocked => MetaUnlockRules.IsMet(metaProgressRequired);

        /// <summary>그 일차에 이 항목이 열려야 하는가. 메타 진행도가 모자라면 이번 판에는 열리지 않는다.</summary>
        public bool IsUnlockedOn(int day) =>
            IsMetaUnlocked && (startsUnlocked || (unlockDay > 0 && day >= unlockDay));

        public UnitDataSO Unit => unit;
        public BuildingDataSO Building => building;
        public bool StartsUnlocked => startsUnlocked;

        /// <summary>해금 안내. 메타 진행도가 모자라면 필요한 진행도를 앞에 붙인다.</summary>
        public string UnlockHint => IsMetaUnlocked
            ? unlockHint
            : $"{MetaUnlockRules.LockPrefix(metaProgressRequired)} · {unlockHint}";

        /// <summary>표시 이름(유닛 이름 또는 건물 이름).</summary>
        public string DisplayName => unit != null ? unit.Name : building != null ? building.DisplayName : string.Empty;

        public DungeonUnlockEntry() { }

        public DungeonUnlockEntry(UnitDataSO value, bool unlocked, string hint)
        {
            unit = value;
            startsUnlocked = unlocked;
            unlockHint = hint;
        }

        public DungeonUnlockEntry(BuildingDataSO value, bool unlocked, string hint)
        {
            building = value;
            startsUnlocked = unlocked;
            unlockHint = hint;
        }
    }
}
