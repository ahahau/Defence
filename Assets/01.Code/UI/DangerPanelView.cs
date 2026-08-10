using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public class DangerPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text totalDangerText;

        private int _lastTotalDanger = -1;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            NestHudStyle.ApplyPanel(panelRoot != null ? panelRoot : gameObject);
            NestHudStyle.ApplyTopRightCard(panelRoot != null ? panelRoot : gameObject, totalDangerText, 2,
                new Color(1f, 0.28f, 0.2f, 1f));
            if (totalDangerText != null)
            {
                _baseColor = totalDangerText.color;
                _baseScale = totalDangerText.transform.localScale;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (totalDangerText == null)
                return;
            totalDangerText.DOKill();
            totalDangerText.transform.DOKill();
            totalDangerText.color = _baseColor;
            totalDangerText.transform.localScale = _baseScale;
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
                foreach (var placement in node.UnitPlacements)
                {
                    if (placement?.Data != null)
                        unitDanger += placement.Data.BaseDanger;
                }

                if (node.AssignedBuilding is Trap trap)
                    trapDanger += trap.DangerRating;
            }

            var totalDanger = unitDanger + trapDanger;
            if (totalDanger == _lastTotalDanger)
                return;

            var previousDanger = _lastTotalDanger;
            _lastTotalDanger = totalDanger;

            if (totalDangerText != null)
            {
                totalDangerText.text = $"던전 악명 {totalDanger}";
                if (previousDanger >= 0)
                    PlayDangerFeedback(totalDanger > previousDanger);
            }
        }

        private void PlayDangerFeedback(bool increased)
        {
            totalDangerText.DOKill();
            totalDangerText.transform.DOKill();
            totalDangerText.color = increased
                ? new Color(1f, 0.34f, 0.26f, 1f)
                : new Color(0.38f, 0.86f, 1f, 1f);
            totalDangerText.DOColor(_baseColor, 0.52f).SetUpdate(true).SetLink(totalDangerText.gameObject);
            totalDangerText.transform.localScale = _baseScale;
            totalDangerText.transform.DOPunchScale(Vector3.one * 0.11f, 0.26f, 6, 0.65f)
                .SetUpdate(true).SetLink(totalDangerText.gameObject);
        }
    }
}
