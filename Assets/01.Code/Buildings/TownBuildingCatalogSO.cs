using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "TownBuildingCatalog", menuName = "SO/Town/Building Catalog", order = 1)]
    public class TownBuildingCatalogSO : ScriptableObject
    {
        [field: SerializeField] public List<TownBuildingDataSO> Buildings { get; private set; } = new();
    }
}
