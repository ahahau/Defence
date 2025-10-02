using System;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Entities
{
    public class Entity : MonoBehaviour
    {
        [SerializeField] protected int hp;
        public UnityEvent onHitEvent;
        public UnityEvent onDeathEvent;
        public Action OnDeath;
    }
}