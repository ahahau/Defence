using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Manager
{
    [CreateAssetMenu(fileName = "CostCatalog", menuName = "SO/Cost/Catalog", order = 1)]
    public class CostCatalogSO : ScriptableObject
    {
        [SerializeField] private List<CostDefinitionSO> defaultCosts = new();
        [SerializeField] private List<CostDefinitionSO> resourceCosts = new();

        public IReadOnlyList<CostDefinitionSO> DefaultCosts => defaultCosts;
        public IReadOnlyList<CostDefinitionSO> ResourceCosts => resourceCosts;
    }
}
