using System.Text;
using Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>정산 화면에서 몬스터와 시설 해금 현황을 접고 펼쳐 보여준다.</summary>
    public class DungeonProgressReportView : MonoBehaviour
    {
        [SerializeField] private Button monsterHeaderButton;
        [SerializeField] private TMP_Text monsterHeaderText;
        [SerializeField] private GameObject monsterContentRoot;
        [SerializeField] private TMP_Text monsterContentText;
        [SerializeField] private Button buildingHeaderButton;
        [SerializeField] private TMP_Text buildingHeaderText;
        [SerializeField] private GameObject buildingContentRoot;
        [SerializeField] private TMP_Text buildingContentText;

        // 초기화 안 된 enum이라 None(둘 다 접힘)으로 시작했다. 그러면 첫 정산에서
        // 350px짜리 보고 영역이 통째로 비어 뜬다 — 해금 로드맵을 한 번도 안 본 사람에게
        // 가장 필요한 화면인데. 한쪽을 열어 둔 채 시작한다. 누르면 그대로 접힌다.
        private ReportCategory activeCategory = ReportCategory.Monsters;

        private void OnEnable()
        {
            monsterHeaderButton?.onClick.AddListener(ToggleMonster);
            buildingHeaderButton?.onClick.AddListener(ToggleBuilding);
        }

        private void OnDisable()
        {
            monsterHeaderButton?.onClick.RemoveListener(ToggleMonster);
            buildingHeaderButton?.onClick.RemoveListener(ToggleBuilding);
        }

        public void RefreshReport(SettlementReport report)
        {
            if (report == null)
                return;

            foreach (var content in new[] { monsterContentRoot, buildingContentRoot })
                if (content != null && content.transform is RectTransform rect)
                {
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0f, -78f);
                    rect.sizeDelta = new Vector2(760f, 166f);
                }

            RenderSection(report.UnitUnlocks, ReportCategory.Monsters, monsterHeaderText, monsterContentText,
                "고용 목록을 불러오는 중입니다.");
            RenderSection(report.BuildingUnlocks, ReportCategory.Buildings, buildingHeaderText, buildingContentText,
                "시설 목록을 불러오는 중입니다.");
            ApplyFoldState();
        }

        private void ToggleMonster()
        {
            activeCategory = activeCategory == ReportCategory.Monsters
                ? ReportCategory.None
                : ReportCategory.Monsters;
            ApplyFoldState();
        }

        private void ToggleBuilding()
        {
            activeCategory = activeCategory == ReportCategory.Buildings
                ? ReportCategory.None
                : ReportCategory.Buildings;
            ApplyFoldState();
        }

        private void RenderSection(
            SettlementUnlockSection section,
            ReportCategory category,
            TMP_Text headerText,
            TMP_Text contentText,
            string emptyMessage)
        {
            if (section == null)
                return;

            SetText(headerText,
                $"{section.Title}  {section.UnlockedCount}/{section.TotalCount}  {(activeCategory == category ? "▲" : "▼")}");
            if (contentText == null)
                return;

            if (section.Lines.Count == 0)
            {
                contentText.text = emptyMessage;
                return;
            }

            var lines = new StringBuilder();
            foreach (var line in section.Lines)
            {
                lines.Append(line.IsUnlocked ? "● " : "○ ");
                lines.Append(line.Name);
                lines.AppendLine(line.IsUnlocked ? $"  {section.UnlockedStateLabel}" : $"  미발견 — {line.Hint}");
            }

            contentText.text = lines.ToString().TrimEnd();
        }

        private void ApplyFoldState()
        {
            if (monsterContentRoot != null)
                monsterContentRoot.SetActive(activeCategory == ReportCategory.Monsters);
            if (buildingContentRoot != null)
                buildingContentRoot.SetActive(activeCategory == ReportCategory.Buildings);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        private enum ReportCategory
        {
            None,
            Monsters,
            Buildings
        }
    }
}
