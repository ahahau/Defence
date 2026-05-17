using System.Reflection;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.UI
{
    public static class EnemyStatusPanelInstaller
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<EnemyStatusPanelView>(FindObjectsInactive.Include) != null)
                return;

            var unitView = UnitStatusPanelView.ActiveInstance;
            if (unitView == null)
                return;

            var enemyStatus = new GameObject("EnemyStatus");
            enemyStatus.transform.SetParent(unitView.transform.parent, false);
            enemyStatus.name = "EnemyStatus";

            var nodeEventChannel = GetField<GameEventChannelSO>(unitView, "nodeEventChannel");

            var enemyView = enemyStatus.AddComponent<EnemyStatusPanelView>();
            enemyView.Configure(nodeEventChannel);
        }

        private static T GetField<T>(UnitStatusPanelView view, string fieldName) where T : Object
        {
            return typeof(UnitStatusPanelView).GetField(fieldName, FieldFlags)?.GetValue(view) as T;
        }
    }
}
