using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Entities
{
    public class Entity : MonoBehaviour
    {
        public UnityEvent OnHitEvent;
        public UnityEvent OnDeathEvent;
        
        public bool IsDead { get; set; }
        protected Dictionary<Type, IEntityComponent> _components;

        public void EntityDestroy()
        {
            Destroy(gameObject);
        }
        protected virtual void Initialize()
        {
            _components = new Dictionary<Type, IEntityComponent>();
            AddComponents();
            InitializeComponents();
        }
        

        protected virtual void AddComponents()
        {
            GetComponentsInChildren<IEntityComponent>().ToList()
                .ForEach(component => _components.Add(component.GetType(), component));
        }

        protected virtual void InitializeComponents()
        {
            _components.Values.ToList().ForEach(component => component.Initialize(this));
        }
        
        public T GetCompo<T>() where T : IEntityComponent
            => (T)_components.GetValueOrDefault(typeof(T));

        public IEntityComponent GetCompo(Type type)
            => _components.GetValueOrDefault(type);
    }
}