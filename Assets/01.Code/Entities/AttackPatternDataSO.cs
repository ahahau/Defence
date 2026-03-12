using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Entities
{
    [CreateAssetMenu(fileName = "AttackPattern", menuName = "SO/Combat/Attack Pattern", order = 0)]
    public class AttackPatternDataSO : ScriptableObject
    {
        [SerializeField] private List<Vector2Int> attackOffsets = new();
        public IReadOnlyList<Vector2Int> AttackOffsets => attackOffsets;
    }
}
