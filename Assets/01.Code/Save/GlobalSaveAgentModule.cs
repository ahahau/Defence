using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Save
{
    public class GlobalSaveAgentModule : MonoBehaviour, ISaveAgentModule
    {
        public int Order => 0;

        public void Configure(SaveManager saveManager)
        {
            TimeManager timeManager = FindFirstObjectByType<TimeManager>();
            if (timeManager != null)
            {
                TimeSaveAgent timeSaveAgent = EnsureAgentOnObject<TimeSaveAgent>(timeManager.gameObject);
                saveManager.RegisterSaveable(timeSaveAgent);
            }

            CostManager costManager = FindFirstObjectByType<CostManager>();
            if (costManager != null)
            {
                CostSaveAgent costSaveAgent = EnsureAgentOnObject<CostSaveAgent>(costManager.gameObject);
                saveManager.RegisterSaveable(costSaveAgent);
            }
        }

        private T EnsureAgentOnObject<T>(GameObject target) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            T component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return target.AddComponent<T>();
        }
    }
}
