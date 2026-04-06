using System;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Units;
using UnityEngine.SceneManagement;

namespace _01.Code.UI
{
    public class UiCatalog
    {
        private readonly IList<UnitDataSO> _availableBuildings;
        private readonly IList<UnitDataSO> _availableUnits;

        public UiCatalog(IList<UnitDataSO> availableBuildings, IList<UnitDataSO> availableUnits)
        {
            _availableBuildings = availableBuildings;
            _availableUnits = availableUnits;
        }

        public IReadOnlyList<UnitDataSO> GetAvailableBuildingsForCurrentScene()
        {
            List<UnitDataSO> filtered = new List<UnitDataSO>();
            for (int i = 0; i < _availableBuildings.Count; i++)
            {
                UnitDataSO unitData = _availableBuildings[i];
                if (IsSelectableBuildingForCurrentScene(unitData))
                {
                    filtered.Add(unitData);
                }
            }

            return filtered;
        }

        public List<UnitDataSO> GetAvailableUnitsForCurrentScene()
        {
            List<UnitDataSO> filtered = new List<UnitDataSO>();
            for (int i = 0; i < _availableUnits.Count; i++)
            {
                UnitDataSO unitData = _availableUnits[i];
                if (IsUnitEntry(unitData))
                {
                    filtered.Add(unitData);
                }
            }

            return filtered;
        }

        public bool IsSelectableBuildingForCurrentScene(UnitDataSO unitData)
        {
            if (!IsBuildingEntry(unitData))
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name ?? string.Empty;
            bool isTownScene = sceneName.IndexOf("Town", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBattleScene = sceneName.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isTownScene)
            {
                return IsTownBuildingEntry(unitData);
            }

            if (isBattleScene)
            {
                return IsBattleBuildingEntry(unitData);
            }

            return true;
        }

        public bool IsSelectablePlacementForCurrentScene(UnitDataSO unitData)
        {
            if (IsUnitEntry(unitData))
            {
                return true;
            }

            return IsSelectableBuildingForCurrentScene(unitData);
        }

        private bool IsUnitEntry(UnitDataSO unitData)
        {
            return unitData != null && unitData.Prefab is Unit;
        }

        private bool IsBuildingEntry(UnitDataSO unitData)
        {
            return unitData != null && unitData.Prefab != null && unitData.Prefab is not Unit;
        }

        private bool IsBattleBuildingEntry(UnitDataSO unitData)
        {
            return unitData?.Prefab != null && unitData.Prefab.GetComponent<BattleBuilding>() != null;
        }

        private bool IsTownBuildingEntry(UnitDataSO unitData)
        {
            if (unitData?.Prefab == null)
            {
                return false;
            }

            if (unitData.Prefab.GetComponent<TownBuilding>() != null)
            {
                return true;
            }

            return unitData.Prefab.GetComponent<BattleBuilding>() == null;
        }
    }
}
