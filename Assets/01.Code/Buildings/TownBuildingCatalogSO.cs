using System.Collections.Generic;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "BattleBuildingCatalog", menuName = "SO/Battle/Building Catalog", order = 1)]
    public class BattleBuildingCatalogSO : ScriptableObject
    {
        [field: SerializeField] public List<BattleTileBuildingDataSO> Buildings { get; private set; } = new();

        public List<BattleTileBuildingDataSO> GetBuildingsForScope(BuildSceneScope sceneScope)
        {
            List<BattleTileBuildingDataSO> results = new List<BattleTileBuildingDataSO>();
            for (int i = 0; i < Buildings.Count; i++)
            {
                BattleTileBuildingDataSO building = Buildings[i];
                if (building == null)
                {
                    continue;
                }

                if (sceneScope != BuildSceneScope.Any &&
                    building.SceneScope != BuildSceneScope.Any &&
                    building.SceneScope != sceneScope)
                {
                    continue;
                }

                results.Add(building);
            }

            return results;
        }
    }
}
