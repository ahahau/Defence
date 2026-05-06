using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public class DangerPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text totalDangerText;

        private int _lastTotalDanger = -1;

        private void Awake()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            var unitDanger = 0;
            var trapDanger = 0;

            foreach (var node in Node.ActiveNodes)
            {
                if (node.AssignedUnit != null)
                    unitDanger += node.AssignedUnit.BaseDanger;

                if (node.AssignedBuilding is Trap trap)
                    trapDanger += trap.DangerRating;
            }

            var totalDanger = unitDanger + trapDanger;
            if (totalDanger == _lastTotalDanger)
                return;

            _lastTotalDanger = totalDanger;

            if (totalDangerText != null)
                totalDangerText.text = $"위험도 {totalDanger}";
        }
    }
}
