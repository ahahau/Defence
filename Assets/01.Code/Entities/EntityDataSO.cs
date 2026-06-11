using UnityEngine;

namespace _01.Code.Entities
{
    public enum EntityGrade
    {
        Grade1 = 1,
        Grade2 = 2,
        Grade3 = 3,
        Grade4 = 4,
        Grade5 = 5,
        Grade6 = 6
    }

    public abstract class EntityDataSO : ScriptableObject
    {
        [field: SerializeField]
        public EntityGrade Grade { get; private set; } = EntityGrade.Grade1;

        [field: SerializeField]
        public Sprite BoardSprite { get; private set; }

        [field: SerializeField, Min(0)]
        public int Defense { get; private set; }

        [field: SerializeField, Range(0f, 1f)]
        public float EvasionChance { get; private set; }
    }
}
