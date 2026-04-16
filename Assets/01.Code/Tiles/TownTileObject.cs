using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Tiles
{
    public class TownTileObject : PlaceableEntity
    {
        [field: SerializeField] public TownTileObjectDataSO Data { get; private set; }

        public virtual void BindData(TownTileObjectDataSO data)
        {
            Data = data;
        }
    }
}
