using _01.Code.Save;
using UnityEngine;

namespace _01.Code.Manager
{
    public class BattleSceneSaveAgentModule : MonoBehaviour, ISaveAgentModule
    {
        public int Order => 100;

        public void Configure(SaveManager saveManager)
        {
            PlacementSaveAgent placementSaveAgent = EnsureAgentOnObject<PlacementSaveAgent>(gameObject);
            UintAgentSaveAgent unitSaveAgent = EnsureAgentOnObject<UintAgentSaveAgent>(gameObject);
            saveManager.RegisterSaveable(placementSaveAgent);
            saveManager.RegisterSaveable(unitSaveAgent);
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
