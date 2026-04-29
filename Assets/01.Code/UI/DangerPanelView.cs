using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class DangerPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text totalDangerText;
        [SerializeField] private Text unitDangerText;
        [SerializeField] private Text trapDangerText;

        private int _lastTotalDanger = -1;
        private int _lastUnitDanger = -1;
        private int _lastTrapDanger = -1;
        private int _lastUnitCount = -1;
        private int _lastTrapCount = -1;

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
            var unitCount = 0;
            var trapCount = 0;

            foreach (var node in Node.ActiveNodes)
            {
                if (node.AssignedUnit != null)
                {
                    unitCount++;
                    unitDanger += node.AssignedUnit.BaseDanger;
                }

                if (node.AssignedBuilding is Trap trap)
                {
                    trapCount++;
                    trapDanger += trap.DangerRating;
                }
            }

            var totalDanger = unitDanger + trapDanger;
            if (totalDanger == _lastTotalDanger
                && unitDanger == _lastUnitDanger
                && trapDanger == _lastTrapDanger
                && unitCount == _lastUnitCount
                && trapCount == _lastTrapCount)
                return;

            _lastTotalDanger = totalDanger;
            _lastUnitDanger = unitDanger;
            _lastTrapDanger = trapDanger;
            _lastUnitCount = unitCount;
            _lastTrapCount = trapCount;

            totalDangerText.text = $"위험도 {totalDanger}";
            unitDangerText.text = $"유닛 {unitCount} / +{unitDanger}";
            trapDangerText.text = $"트랩 {trapCount} / +{trapDanger}";
        }
    }
}
