using System.Collections.Generic;
using _01.Code.Cost;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Tiles
{
    public class TownObstacle : TownTileObject
    {
        public bool TryRemove(CostManager costManager)
        {
            TownObstacleDataSO data = Data as TownObstacleDataSO;
            if (costManager == null || data == null || costManager.PrimarySpendCost == null)
            {
                return false;
            }

            if (!costManager.TryPay(costManager.PrimarySpendCost, data.RemoveCost))
            {
                return false;
            }

            RefundReturnCosts(costManager, data.ReturnCosts);

            if (GridManager != null)
            {
                GridManager.TryClear(GridPosition, this);
            }

            Destroy(gameObject);
            return true;
        }

        private void RefundReturnCosts(CostManager costManager, List<TownTileObjectDataSO.Entry> returnCosts)
        {
            if (returnCosts == null)
            {
                return;
            }

            for (int i = 0; i < returnCosts.Count; i++)
            {
                TownTileObjectDataSO.Entry entry = returnCosts[i];
                if (entry == null || entry.Amount <= 0)
                {
                    continue;
                }

                CostDefinitionSO resolvedType = entry.ResolveType();
                if (resolvedType == null)
                {
                    continue;
                }

                costManager.Add(resolvedType, entry.Amount);
            }
        }
    }
}
