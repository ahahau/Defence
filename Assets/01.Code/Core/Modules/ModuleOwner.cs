using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _01.Code.Core.Modules
{
    /// <summary>
    /// 자식에 붙은 모듈을 스스로 모아 두 단계로 깨우는 엔티티의 뿌리.
    ///
    /// 배선을 인스펙터가 아니라 계층 구조가 정한다. 프리팹에 모듈을 붙이면 끝이고,
    /// 빼먹은 모듈은 GetModule이 null을 돌려주는 것으로 바로 드러난다.
    /// </summary>
    public abstract class ModuleOwner : MonoBehaviour
    {
        private Dictionary<Type, IModule> _moduleByType;

        protected virtual void Awake()
        {
            CollectModules();
            InitializeModules();
            AfterInitializeModules();
        }

        private void CollectModules()
        {
            _moduleByType = new Dictionary<Type, IModule>();

            // 같은 타입이 둘 붙어 있으면 사전 구성에서 예외가 난다. 조용히 덮어쓰는 대신
            // 어느 오브젝트가 중복인지 알려 주고 첫 번째만 쓴다.
            foreach (var module in GetComponentsInChildren<IModule>(true))
            {
                var type = module.GetType();
                if (_moduleByType.ContainsKey(type))
                {
                    Debug.LogError($"{name}에 {type.Name} 모듈이 둘 이상 붙어 있습니다. 첫 번째만 사용합니다.", this);
                    continue;
                }

                _moduleByType.Add(type, module);
            }
        }

        protected virtual void InitializeModules()
        {
            foreach (var module in _moduleByType.Values)
                module.Initialize(this);
        }

        protected virtual void AfterInitializeModules()
        {
            foreach (var module in _moduleByType.Values.OfType<IAfterInitModule>())
                module.AfterInitialize();
        }

        /// <summary>
        /// 모듈을 꺼낸다. 구체 타입으로 먼저 찾고, 없으면 인터페이스로 훑는다 —
        /// 호출부가 IStatModule 같은 계약만 알고도 쓸 수 있어야 교체가 가능해진다.
        /// </summary>
        public T GetModule<T>() where T : class
        {
            if (_moduleByType == null)
                CollectModules();

            if (_moduleByType.TryGetValue(typeof(T), out var exact))
                return exact as T;

            foreach (var module in _moduleByType.Values)
            {
                if (module is T matched)
                    return matched;
            }

            return null;
        }

        public bool TryGetModule<T>(out T module) where T : class
        {
            module = GetModule<T>();
            return module != null;
        }
    }
}
