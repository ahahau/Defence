using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "TownBuildingCatalog", menuName = "SO/Town/Building Catalog", order = 1)]
    public class TownBuildingCatalogSO : ScriptableObject
    {
        [SerializeField] private List<TownBuildingDataSO> buildings = new();

        public List<TownBuildingDataSO> Buildings
        {
            get { return buildings; }
        }
    }
}
