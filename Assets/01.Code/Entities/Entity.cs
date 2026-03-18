using _01.Code.Modules;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Entities
{
    public class Entity : ModuleOwner
    { 
        public bool IsDead { get; set; }
        
        public UnityEvent OnHit;
        public UnityEvent OnDeath;

        
        protected virtual void Start()
        {
            IsDead = false;
        }
    }
}