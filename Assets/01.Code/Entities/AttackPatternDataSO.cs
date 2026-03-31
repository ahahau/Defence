using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Entities
{
    [Serializable]
    public class AttackPatternData
    {
        public List<Vector2Int> attackOffsets = new();
        public int cellSearchSize = 1;
    }
    
    [CreateAssetMenu(fileName = "AttackPattern", menuName = "SO/Combat/Attack Pattern", order = 0)]
    public class AttackPatternDataSO : ScriptableObject
    {
        [SerializeField] private List<AttackPatternData> attackOffsets = new();
        public List<AttackPatternData> AttackOffsets => attackOffsets;
    }
}
