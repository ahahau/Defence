using System;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Manager;

namespace _01.Code.Buildings
{
    public class BattleTilePlacementEvaluator
    {
        private readonly GridManager _gridManager;

        public BattleTilePlacementEvaluator(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public bool CanPlace(BattleTileBuildingDataSO buildingData, PlaceableEntity selectedEntity)
        {
            if (selectedEntity == null || buildingData == null)
            {
                return false;
            }

            if (buildingData.BattlePlacementKind == BattleTilePlacementKind.AnyObstacle)
            {
                return true;
            }

            BattleTilePlacementKind selectedKind = ResolvePlacementKind(selectedEntity);
            if (selectedKind == BattleTilePlacementKind.None)
            {
                return false;
            }

            if (buildingData.RequiredSourcePrefab != null &&
                MatchesResourcePrefab(selectedEntity, buildingData.RequiredSourcePrefab))
            {
                return true;
            }

            BattleTilePlacementKind requiredKind = ResolvePlacementKind(buildingData.RequiredSourcePrefab);
            if (requiredKind != BattleTilePlacementKind.None)
            {
                return requiredKind == selectedKind;
            }

            return buildingData.BattlePlacementKind switch
            {
                BattleTilePlacementKind.Tree => selectedKind == BattleTilePlacementKind.Tree,
                BattleTilePlacementKind.Rock => selectedKind == BattleTilePlacementKind.Rock,
                _ => false
            };
        }

        private BattleTilePlacementKind ResolvePlacementKind(PlaceableEntity entity)
        {
            if (entity == null)
            {
                return BattleTilePlacementKind.None;
            }

            if (MatchesResourcePrefab(entity, _gridManager != null ? _gridManager.TreePrefab : null))
            {
                return BattleTilePlacementKind.Tree;
            }

            if (MatchesResourcePrefab(entity, _gridManager != null ? _gridManager.RockPrefab : null))
            {
                return BattleTilePlacementKind.Rock;
            }

            string normalizedName = NormalizeEntityName(entity.name);
            if (normalizedName.Contains("tree"))
            {
                return BattleTilePlacementKind.Tree;
            }

            if (normalizedName.Contains("rock"))
            {
                return BattleTilePlacementKind.Rock;
            }

            return BattleTilePlacementKind.None;
        }

        private bool MatchesResourcePrefab(PlaceableEntity entity, PlaceableEntity resourcePrefab)
        {
            if (entity == null || resourcePrefab == null)
            {
                return false;
            }

            string entityName = NormalizeEntityName(entity.name);
            string prefabName = NormalizeEntityName(resourcePrefab.name);
            return !string.IsNullOrWhiteSpace(prefabName) &&
                   (entityName.Contains(prefabName) || prefabName.Contains(entityName));
        }

        private string NormalizeEntityName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("(Clone)", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim()
                .ToLowerInvariant();
        }
    }
}
