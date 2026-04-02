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
        private GridManager _gridManager;
        private CostManager _costManager;
        private LogManager _logManager;
        private SaveManager _saveManager;

        public GridManager GridManager => _gridManager;
        public CostManager CostManager => _costManager;
        public LogManager LogManager => _logManager;
        public SaveManager SaveManager => _saveManager;

        private void Awake()
        {
            BuildManagerMap();
            ResolveCoreManagers();
            InitializeManagers();
            AfterInitializeManagers();
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
            _managerMap = GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IManageable>()
                .ToDictionary(manageable => manageable.GetType(), manageable => manageable);
        }

        private void ResolveCoreManagers()
        {
            _gridManager = GetManager<GridManager>();
            _costManager = GetManager<CostManager>();
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
