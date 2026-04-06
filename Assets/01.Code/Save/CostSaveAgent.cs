using System;
using System.Collections.Generic;
using _01.Code.Cost;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Save
{
    [Serializable]
    public struct CostSaveEntry
    {
        public string id;
        public int current;
        public int max;
    }

    [Serializable]
    public struct CostSaveCollection
    {
        public List<CostSaveEntry> costs;
    }

    public class CostSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "cost.state";
        private CostManager _costManager;

        public string SaveKey => saveKey;
        public int RestoreOrder => 100;

        public string GetSaveData()
        {
            _costManager = GetComponent<CostManager>();
            List<CostSaveEntry> entries = new List<CostSaveEntry>();

            AppendCosts(entries, _costManager.AllCosts, _costManager);

            return JsonUtility.ToJson(new CostSaveCollection { costs = entries });
        }

        public void RestoreData(string savedData)
        {
            _costManager = GetComponent<CostManager>();
            if (string.IsNullOrWhiteSpace(savedData))
            {
                return;
            }

            CostSaveCollection collection = JsonUtility.FromJson<CostSaveCollection>(savedData);
            if (collection.costs == null)
            {
                return;
            }

            Dictionary<string, CostDefinitionSO> registry = BuildRegistry();
            for (int i = 0; i < collection.costs.Count; i++)
            {
                CostSaveEntry entry = collection.costs[i];
                if (string.IsNullOrWhiteSpace(entry.id) || !registry.TryGetValue(entry.id, out CostDefinitionSO definition))
                {
                    continue;
                }

                _costManager.SetMax(definition, entry.max);
                _costManager.SetCurrent(definition, entry.current);
            }
        }

        private void AppendCosts(List<CostSaveEntry> entries, List<CostDefinitionSO> definitions, CostManager costManager)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                CostDefinitionSO definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                entries.Add(new CostSaveEntry
                {
                    id = definition.name,
                    current = costManager.GetCurrent(definition),
                    max = costManager.GetMax(definition)
                });
            }
        }

        private Dictionary<string, CostDefinitionSO> BuildRegistry()
        {
            Dictionary<string, CostDefinitionSO> registry = new Dictionary<string, CostDefinitionSO>();
            RegisterDefinitions(registry, _costManager.AllCosts);
            return registry;
        }

        private void RegisterDefinitions(Dictionary<string, CostDefinitionSO> registry, List<CostDefinitionSO> definitions)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                CostDefinitionSO definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                registry[definition.name] = definition;
            }
        }
    }
}
