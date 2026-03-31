using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Cost
{
    [CreateAssetMenu(fileName = "CostCatalog", menuName = "SO/Cost/Catalog", order = 1)]
    public class CostCatalogSO : ScriptableObject
    {
        [SerializeField] private List<CostDefinitionSO> defaultCosts = new();
        [SerializeField] private List<CostDefinitionSO> resourceCosts = new();

        public List<CostDefinitionSO> DefaultCosts => defaultCosts;
        public List<CostDefinitionSO> ResourceCosts => resourceCosts;
    }
}
