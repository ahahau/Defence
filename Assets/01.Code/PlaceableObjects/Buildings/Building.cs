using System;

namespace _01.Code.PlaceableObjects.Buildings
{
    public class Building : PlaceableEntity
    {
        private BuildingAttackCompo _attackCompo;

        private void Awake()
        {
            _attackCompo = GetCompo<BuildingAttackCompo>();
        }
    }
}