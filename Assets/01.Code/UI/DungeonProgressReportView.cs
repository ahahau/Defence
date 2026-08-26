using System.Text;
using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Manager;
using _01.Code.Progression;
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
            RefreshMonsters(roster);
            RefreshBuildings(roster);
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
            var unlocked = roster?.UnlockedUnits;
            // 총계는 해금 카탈로그로 센다. 표시용 목록으로 세면 실제보다 적은 수가 나온다.
            var total = roster != null ? roster.UnlockableUnitCount : 0;
            var unlockedCount = unlocked?.Count ?? 0;
            SetText(monsterHeaderText, $"몬스터 해금  {unlockedCount}/{total}  {(activeCategory == ReportCategory.Monsters ? "▲" : "▼")}");
            if (monsterContentText == null)
                return;

            // 줄도 총계와 같은 곳에서 나와야 한다. 표시용 목록(UnitCatalog)으로 만들면
            // "1/9" 밑에 3줄만 서고 해금 예정인 유닛이 통째로 안 보인다.
            var entries = CollectEntries(roster, true);
            if (entries.Count == 0)
            {
                monsterContentText.text = "고용 목록을 불러오는 중입니다.";
                return;
            }

            var lines = new StringBuilder();
            foreach (var entry in entries)
            {
                var isUnlocked = roster.IsUnlocked(entry.Unit);
                lines.Append(isUnlocked ? "● " : "○ ");
                lines.Append(entry.Unit.Name);
                lines.AppendLine(isUnlocked ? "  고용 가능" : $"  미발견 — {ResolveHint(entry.UnlockHint)}");
            }
            monsterContentText.text = lines.ToString().TrimEnd();
        }

        private void RefreshBuildings(HiredUnitRoster roster)
        {
            var unlocked = roster?.UnlockedBuildings;
            var total = roster != null ? roster.UnlockableBuildingCount : 0;
            var unlockedCount = unlocked?.Count ?? 0;
            SetText(buildingHeaderText, $"시설 해금  {unlockedCount}/{total}  {(activeCategory == ReportCategory.Buildings ? "▲" : "▼")}");
            if (buildingContentText == null)
                return;

            var entries = CollectEntries(roster, false);
            if (entries.Count == 0)
            {
                buildingContentText.text = "시설 목록을 불러오는 중입니다.";
                return;
            }

            var lines = new StringBuilder();
            foreach (var entry in entries)
            {
                var isUnlocked = Contains(unlocked, entry.Building);
                lines.Append(isUnlocked ? "● " : "○ ");
                lines.Append(entry.Building.DisplayName);
                lines.AppendLine(isUnlocked ? "  설치 가능" : $"  미발견 — {ResolveHint(entry.UnlockHint)}");
            }
            buildingContentText.text = lines.ToString().TrimEnd();
        }

        /// <summary>해금 카탈로그에서 유닛(또는 시설) 항목만 골라 해금 일차 순으로 세운다.
        /// 카탈로그 순서는 작성 순서라 일차가 뒤섞여 있어 그대로 쓰면 "다음에 뭐가 열리나"가 안 읽힌다.</summary>
        private static List<DungeonUnlockEntry> CollectEntries(HiredUnitRoster roster, bool forUnits)
        {
            var result = new List<DungeonUnlockEntry>();
            var catalog = roster?.UnlockCatalog?.Entries;
            if (catalog == null)
                return result;

            foreach (var entry in catalog)
            {
                if (entry == null)
                    continue;

                if (forUnits ? entry.Unit == null : entry.Building == null)
                    continue;

                result.Add(entry);
            }

            result.Sort((a, b) => UnlockOrder(a).CompareTo(UnlockOrder(b)));
            return result;
        }

        /// <summary>처음부터 열려 있는 항목이 맨 위, 나머지는 해금 일차 순.</summary>
        private static int UnlockOrder(DungeonUnlockEntry entry)
        {
            return entry.StartsUnlocked ? 0 : Mathf.Max(1, entry.UnlockDay);
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
