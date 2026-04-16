using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Units
{
    public class Unit : PlaceableEntity
    {
        [field: SerializeField] public int level { get; set; } = 1;

    }
}
