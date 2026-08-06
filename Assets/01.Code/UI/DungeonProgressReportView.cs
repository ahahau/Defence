using System.Text;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Manager;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>정산 화면에서 몬스터와 시설 해금 현황을 접고 펼쳐 보여준다.</summary>
    public sealed class DungeonProgressReportView : MonoBehaviour
    {
        [SerializeField] private Button monsterHeaderButton;
        [SerializeField] private TMP_Text monsterHeaderText;
        [SerializeField] private GameObject monsterContentRoot;
        [SerializeField] private TMP_Text monsterContentText;
        [SerializeField] private Button buildingHeaderButton;
        [SerializeField] private TMP_Text buildingHeaderText;
        [SerializeField] private GameObject buildingContentRoot;
        [SerializeField] private TMP_Text buildingContentText;

        private ReportCategory activeCategory;

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

        public void RefreshReport()
        {
            var roster = HiredUnitRoster.Current;
            var nodePanel = NodePanelView.Current;
            RefreshMonsters(roster);
            RefreshBuildings(roster, nodePanel);
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

        private void RefreshMonsters(HiredUnitRoster roster)
        {
            var catalog = roster?.UnitCatalog;
            var unlocked = roster?.UnlockedUnits;
            var total = catalog?.Count ?? 0;
            var unlockedCount = unlocked?.Count ?? 0;
            SetText(monsterHeaderText, $"몬스터 해금  {unlockedCount}/{total}  {(activeCategory == ReportCategory.Monsters ? "▲" : "▼")}");
            if (monsterContentText == null)
                return;

            if (catalog == null || catalog.Count == 0)
            {
                monsterContentText.text = "고용 목록을 불러오는 중입니다.";
                return;
            }

            var lines = new StringBuilder();
            foreach (var unit in catalog)
            {
                if (unit == null)
                    continue;

                var isUnlocked = roster.IsUnlocked(unit);
                var hint = roster.UnlockCatalog?.Find(unit)?.UnlockHint;
                lines.Append(isUnlocked ? "● " : "○ ");
                lines.Append(unit.Name);
                lines.AppendLine(isUnlocked ? "  고용 가능" : $"  미발견 — {ResolveHint(hint)}");
            }
            monsterContentText.text = lines.ToString().TrimEnd();
        }

        private void RefreshBuildings(HiredUnitRoster roster, NodePanelView nodePanel)
        {
            var catalog = nodePanel?.InstallableBuildings;
            var unlocked = roster?.UnlockedBuildings;
            var total = catalog?.Count ?? 0;
            var unlockedCount = unlocked?.Count ?? 0;
            SetText(buildingHeaderText, $"시설 해금  {unlockedCount}/{total}  {(activeCategory == ReportCategory.Buildings ? "▲" : "▼")}");
            if (buildingContentText == null)
                return;

            if (catalog == null || catalog.Count == 0)
            {
                buildingContentText.text = "시설 목록을 불러오는 중입니다.";
                return;
            }

            var lines = new StringBuilder();
            foreach (var building in catalog)
            {
                if (building == null)
                    continue;

                var isUnlocked = Contains(unlocked, building);
                var hint = roster?.UnlockCatalog?.Find(building)?.UnlockHint;
                lines.Append(isUnlocked ? "● " : "○ ");
                lines.Append(building.DisplayName);
                lines.AppendLine(isUnlocked ? "  설치 가능" : $"  미발견 — {ResolveHint(hint)}");
            }
            buildingContentText.text = lines.ToString().TrimEnd();
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

        private static bool Contains<T>(IReadOnlyList<T> entries, T value) where T : class
        {
            if (entries == null || value == null)
                return false;

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] == value)
                    return true;
            }

            return false;
        }

        private static string ResolveHint(string hint)
        {
            return string.IsNullOrWhiteSpace(hint) ? "던전 발전 조건을 충족하면 해금" : hint;
        }

        private enum ReportCategory
        {
            None,
            Monsters,
            Buildings
        }
    }
}
