using UnityEngine;

namespace _01.Code.Manager
{
    public abstract class BaseManager : MonoBehaviour, IManageable
    {
        protected IManagerContainer ManagerContainer { get; private set; }
        protected bool IsManagerInitialized { get; private set; }

        public void Initialize(IManagerContainer managerContainer)
        {
            if (IsManagerInitialized)
            {
                return;
            }

            ManagerContainer = managerContainer;
            OnInitialize(managerContainer);
            IsManagerInitialized = true;
        }

        protected abstract void OnInitialize(IManagerContainer managerContainer);
    }
}
