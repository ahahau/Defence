using System;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Entities
{
    public class Entity : MonoBehaviour
    {
        public UnityEvent onHitEvent;
        public UnityEvent onDeathEvent;
        public Action OnDeath;
    }
}