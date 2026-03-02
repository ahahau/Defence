using System;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class AttackableBuilding : Building
    {
        public EntitySensor Sensor { get; private set; }
        protected override void Awake()
        {
            base.Awake();
            Sensor = GetComponentInChildren<EntitySensor>();
        }

        private void Update()
        {
            if (Sensor.IsTargetInRange() && CanAttack())
            {
                
            }
        }

        protected virtual bool CanAttack()
        {
            return true;
        }
    }
}