using System;
using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Cost
{
    [CreateAssetMenu(fileName = "CostBundle", menuName = "SO/Cost/Bundle", order = 0)]
    public class CostBundleSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public CostDefinitionSO type;
            public int amount;
        }

        [field:SerializeField] public List<Entry> Entries { get; private set; } = new();

    }
}
