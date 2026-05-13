using System.Reflection;
using _01.Code.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public static class EnemyStatusPanelInstaller
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var unitView = UnitStatusPanelView.ActiveInstance;
            if (unitView == null)
                return;

            var enemyStatus = Object.Instantiate(unitView.gameObject, unitView.transform.parent);
            enemyStatus.name = "EnemyStatus";

            var clonedUnitView = enemyStatus.GetComponent<UnitStatusPanelView>();
            if (clonedUnitView == null)
            {
                Object.Destroy(enemyStatus);
                return;
            }

            var nodeEventChannel = GetField<GameEventChannelSO>(clonedUnitView, "nodeEventChannel");
            var panelRoot = GetField<GameObject>(clonedUnitView, "panelRoot");
            var titleText = GetField<TMP_Text>(clonedUnitView, "titleText");
            var statusText = GetField<TMP_Text>(clonedUnitView, "statusText");
            var hpText = GetField<TMP_Text>(clonedUnitView, "hpText");
            var attackText = GetField<TMP_Text>(clonedUnitView, "levelText");
            var locationText = GetField<TMP_Text>(clonedUnitView, "hintText");
            var recoverButton = GetField<Button>(clonedUnitView, "recoverButton");
            var closeButton = GetField<Button>(clonedUnitView, "closeButton");
            var panelCanvas = GetField<Canvas>(clonedUnitView, "panelCanvas");

            Object.Destroy(clonedUnitView);

            var enemyView = enemyStatus.AddComponent<EnemyStatusPanelView>();
            enemyView.Configure(
                nodeEventChannel,
                panelRoot,
                titleText,
                statusText,
                hpText,
                attackText,
                locationText,
                closeButton,
                panelCanvas);

            if (recoverButton != null)
                recoverButton.gameObject.SetActive(false);

            SetText(titleText, "Enemy");
            SetText(statusText, "이동 중");
            SetText(hpText, "HP -");
            SetText(attackText, "ATK -  SPD -");
            SetText(locationText, "위치 -");
            panelRoot?.SetActive(false);
        }

        private static T GetField<T>(UnitStatusPanelView view, string fieldName) where T : Object
        {
            return typeof(UnitStatusPanelView).GetField(fieldName, FieldFlags)?.GetValue(view) as T;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
