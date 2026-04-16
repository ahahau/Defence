using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Building : PlaceableEntity
    {
        [field: SerializeField] public int level { get; set; } = 1;

    }
}
