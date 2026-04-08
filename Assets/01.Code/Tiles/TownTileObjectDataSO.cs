using _01.Code.Cost;
using UnityEngine;

namespace _01.Code.Tiles
{
    public abstract class TownTileObjectDataSO : ScriptableObject
    {
        [field: SerializeField] public string SaveKey { get; private set; }
        [field: SerializeField] public TownTileObject Prefab { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public Color Color { get; private set; } = Color.white;
        [field: SerializeField] public CostBundleSO BuildCosts { get; private set; }
    }
}
