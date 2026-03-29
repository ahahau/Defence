using UnityEngine;

namespace _01.Code.Manager
{
    public enum CostCategory
    {
        Default,
        Resource
    }

    [CreateAssetMenu(fileName = "CostDefinition", menuName = "SO/Cost/Definition", order = 0)]
    public class CostDefinitionSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public CostCategory Category { get; private set; }
        [field: SerializeField] public int InitialCurrent { get; private set; }
        [field: SerializeField] public int InitialMax { get; private set; } = 99999;
        [field: SerializeField] public int SortOrder { get; private set; }
    }
}
