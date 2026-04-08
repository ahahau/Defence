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

        private void RefundReturnCosts(CostManager costManager, CostBundleSO returnCosts)
        {
            if (costManager == null || returnCosts == null)
            {
                return;
            }

            for (int i = 0; i < returnCosts.Entries.Count; i++)
            {
                CostBundleSO.Entry entry = returnCosts.Entries[i];
                if (entry == null || entry.type == null || entry.amount <= 0)
                {
                    continue;
                }

                costManager.Add(entry.type, entry.amount);
            }
        }
    }
}
