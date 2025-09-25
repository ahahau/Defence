using System;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Entities
{
    public class Entity : MonoBehaviour
    {
        [SerializeField]protected int _hp;
        public UnityEvent OnHitEvent;
        public UnityEvent OnDeathEvent;
        public Action OnDeath;

        public void EntityDestroy()
        {
            Destroy(gameObject);
        } 
    }
}