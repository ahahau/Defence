using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Manager
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour, IManagerContainer
    {
        private Dictionary<Type, IManageable> _managerMap;
        
        private LogManager _logManager;
        private SaveManager _saveManager;
        public LogManager LogManager => _logManager;
        public SaveManager SaveManager => _saveManager;

        private void Awake()
        {
            ValidateHierarchy();
            BuildManagerMap();
            ResolveCoreManagers();
            InitializeManagers();
            AfterInitializeManagers();
        }

        private void OnValidate()
        {
            ValidateHierarchy();
        }

        public T GetManager<T>() where T : class, IManageable
        {
            if (_managerMap == null)
            {
                return null;
            }

            if (_managerMap.TryGetValue(typeof(T), out IManageable manager))
            {
                return manager as T;
            }

            return _managerMap.Values.OfType<T>().FirstOrDefault();
        }

        private void BuildManagerMap()
        {
            _managerMap = GetComponentsInChildren<Transform>(true)
                .Where(IsDirectChildOfGameManager)
                .SelectMany(child => child.GetComponents<MonoBehaviour>())
                .Where(IsRegisteredManagerComponent)
                .OfType<IManageable>()
                .ToDictionary(manageable => manageable.GetType(), manageable => manageable);
        }

        private bool IsDirectChildOfGameManager(Transform target)
        {
            return target != null && target != transform && target.parent == transform;
        }

        private bool IsRegisteredManagerComponent(MonoBehaviour behaviour)
        {
            if (behaviour == null || behaviour == this || behaviour is not IManageable)
            {
                return false;
            }

            Type behaviourType = behaviour.GetType();
            return behaviourType.Namespace == typeof(GameManager).Namespace;
        }

        private void ValidateHierarchy()
        {
            if (transform == null)
            {
                return;
            }

            foreach (Transform child in transform)
            {
                if (child == null)
                {
                    continue;
                }

                MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
                bool hasRegisteredManager = behaviours.Any(IsRegisteredManagerComponent);
                if (!hasRegisteredManager)
                {
                    Debug.LogWarning($"`{child.name}` is under GameManager but is not a manager. Move it out of the GameManager hierarchy.", child);
                }
            }
        }

        private void ResolveCoreManagers()
        {
            _logManager = GetManager<LogManager>();
            _saveManager = GetManager<SaveManager>();
        }

        private void InitializeManagers()
        {
            if (_managerMap == null)
            {
                return;
            }

            foreach (IManageable manageable in _managerMap.Values)
            {
                manageable.Initialize(this);
            }
        }

        private void AfterInitializeManagers()
        {
            if (_managerMap == null)
            {
                return;
            }

            foreach (IAfterManageable manageable in _managerMap.Values.OfType<IAfterManageable>())
            {
                manageable.AfterInitialize(this);
            }
        }
    }
}
