using System;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Entities
{
    public class Entity : MonoBehaviour
    {
        [SerializeField] protected int currentHp;
        [SerializeField] protected int maxHp = 100;
        private void Awake()
        {
            currentHp = maxHp;
        }

        public UnityEvent onHitEvent;
        public UnityEvent onDeathEvent;
        public Action OnDeath;
    }
}