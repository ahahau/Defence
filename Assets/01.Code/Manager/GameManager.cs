using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Manager
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour, IManagerContainer
    {
        public static GameManager Instance { get; private set; }

        private Dictionary<Type, IManageable> _managerMap;
        
        private LogManager _logManager;
        private SaveManager _saveManager;
        public LogManager LogManager
        {
            get { return _logManager; }
        }

        public SaveManager SaveManager
        {
            get { return _saveManager; }
        }

        private void Awake()
        {
            Instance = this;
            ValidateHierarchy();
            BuildManagerMap();
            ResolveCoreManagers();
            InitializeManagers();
            AfterInitializeManagers();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnValidate()
        {
            ValidateHierarchy();
        }

        public T GetManager<T>() where T : class, IManageable
        {
            if (_managerMap == null)
            {
                _managerMap = new Dictionary<Type, IManageable>();
            }

            if (_managerMap.TryGetValue(typeof(T), out IManageable manager))
            {
                return manager as T;
            }

            T resolvedManager = _managerMap.Values.OfType<T>().FirstOrDefault() ?? FindManagerInScene<T>();
            if (resolvedManager is IManageable manageable)
            {
                _managerMap[typeof(T)] = manageable;
            }

            return resolvedManager;
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

        private T FindManagerInScene<T>() where T : class, IManageable
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<T>()
                .FirstOrDefault();
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
