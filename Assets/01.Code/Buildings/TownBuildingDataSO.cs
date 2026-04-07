using _01.Code.Cost;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "TownBuildingData", menuName = "SO/Town/Building Data", order = 0)]
    public class TownBuildingDataSO : ScriptableObject
    {
        [SerializeField] private string saveKey;
        [SerializeField] private TownBuilding prefab;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private CostBundleSO buildCosts;

        public string SaveKey
        {
            get { return string.IsNullOrWhiteSpace(saveKey) ? name : saveKey; }
        }

        public TownBuilding Prefab
        {
            get { return prefab; }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public Sprite Icon
        {
            get { return icon; }
        }

        public Color Color
        {
            get { return color; }
        }

        public CostBundleSO BuildCosts
        {
            get { return buildCosts; }
        }
    }
}
