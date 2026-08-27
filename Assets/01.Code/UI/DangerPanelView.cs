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
            DungeonHudStyle.ApplyPanel(panelRoot != null ? panelRoot : gameObject);
            DungeonHudStyle.ApplyTopRightCard(panelRoot != null ? panelRoot : gameObject, totalDangerText, 2,
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
            var buildingDanger = 0;
            var deedDanger = 0;

            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                // 설치물의 위험도(재고)와 별개로, 그 구역에서 실제로 벌어진 일이 악명을 남긴다.
                // 유닛 카드가 "전투 시 +N"을 약속하고 함정에 발동당 위험도가 적혀 있는데
                // 이걸 세지 않으면 그 숫자들이 어디에도 나타나지 않는다.
                deedDanger += node.DangerLevel;

                foreach (var placement in node.UnitPlacements)
                {
                    if (placement?.Data != null)
                        unitDanger += placement.Data.BaseDanger;
                }

                // 함정·금고·벽은 중앙 슬롯에 설 수 없다. 중앙만 보면 아무리 깔아도 악명이 꿈쩍하지 않는다.
                buildingDanger += DangerOf(node.AssignedBuilding);

                var grid = node.TrapGrid;
                if (grid == null)
                    continue;

                var placed = grid.PlacedBuildings;
                for (var i = 0; i < placed.Count; i++)
                    buildingDanger += DangerOf(placed[i]);
            }

            // 통로 건물은 어느 노드에도 속하지 않아 노드만 훑으면 통째로 빠진다.
            foreach (var edge in EdgeLine.ActiveEdges)
                buildingDanger += DangerOf(edge != null ? edge.InstalledBuilding : null);

            var totalDanger = unitDanger + buildingDanger + deedDanger;
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

        /// <summary>설치 카드가 건물마다 "경계 +N"을 약속하므로 함정만이 아니라 건물 전부를 센다.
        /// 부서진 건물은 더 이상 위협이 아니므로 빠진다.</summary>
        private static int DangerOf(Building building)
        {
            return building != null && !building.IsDestroyed ? building.DangerRating : 0;
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
