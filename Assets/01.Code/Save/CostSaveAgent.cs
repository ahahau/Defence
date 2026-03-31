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

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            CostManager costManager = GameManager.Instance.CostManager;
            List<CostSaveEntry> entries = new List<CostSaveEntry>();

            AppendCosts(entries, costManager.DefaultCosts, costManager);
            AppendCosts(entries, costManager.ResourceCosts, costManager);

            return JsonUtility.ToJson(new CostSaveCollection { costs = entries });
        }

        public void RestoreData(string savedData)
        {
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

                GameManager.Instance.CostManager.SetMax(definition, entry.max);
                GameManager.Instance.CostManager.SetCurrent(definition, entry.current);
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
            RegisterDefinitions(registry, GameManager.Instance.CostManager.DefaultCosts);
            RegisterDefinitions(registry, GameManager.Instance.CostManager.ResourceCosts);
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
