using UnityEngine;

namespace _01.Code.UI
{
    public abstract class UISectionController : MonoBehaviour
    {
        private bool _initialized;

        public void InitializeSection()
        {
            CacheReferences();
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            OnInitializeSection();
        }

        protected virtual void Awake()
        {
            InitializeSection();
        }

        protected virtual void CacheReferences()
        {
        }

        protected virtual void OnInitializeSection()
        {
        }
    }
}
