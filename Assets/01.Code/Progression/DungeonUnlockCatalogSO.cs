using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Progression
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

        public int UnlockDay => unlockDay;

        /// <summary>그 일차에 이 항목이 열려야 하는가.</summary>
        public bool IsUnlockedOn(int day) => startsUnlocked || (unlockDay > 0 && day >= unlockDay);

        public UnitDataSO Unit => unit;
        public BuildingDataSO Building => building;
        public bool StartsUnlocked => startsUnlocked;
        public string UnlockHint => unlockHint;

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
