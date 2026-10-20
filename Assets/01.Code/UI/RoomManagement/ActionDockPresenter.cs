using System.Collections.Generic;
using System.Text;
using Code.Manager;
using Code.Units;
using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 왼쪽 아래 행동 도크의 값을 만든다. 문구·조건은 지운 Toolkit HUD 하단 바와 같다.
    /// 관리 버튼은 관리 시간(대기·일시정지)에 방을 골랐을 때만 눌린다.
    /// </summary>
    public sealed class ActionDockPresenter
    {
        private readonly Dictionary<UnitDataSO, int> _counts = new();
        private readonly StringBuilder _builder = new();

        public ActionDockViewModel Build(
            NodePanelView panel,
            DayManager dayManager,
            GameSpeedController speed,
            HiredUnitRoster roster,
            UnitRevivalSystem revival,
            MerchantPanelView merchant,
            bool rosterOpen)
        {
            var isManagementWindow = DayManager.IsManagementWindow;
            var pauseLocked = speed != null && speed.IsPauseLocked;
            return new ActionDockViewModel
            {
                // 엘리트·보스가 있으면 멈출 수 없다. 관리 안내와 같은 자리에 잠긴 이유를 보여 준다.
                ManagementText = pauseLocked
                    ? $"일시정지 잠김 · {speed.PauseLockReason}"
                    : isManagementWindow
                        ? "관리 중 · 방을 선택해 건설·배치·함정을 관리하세요"
                        : "영업 중 · 배치와 회수는 일시정지에서 가능합니다",
                ManagementTone = pauseLocked ? ActionDockTone.PauseLocked
                    : isManagementWindow ? ActionDockTone.Management
                    : ActionDockTone.Operating,
                CanManage = isManagementWindow && panel != null && panel.SelectedNode != null,
                MerchantVisible = merchant != null && merchant.IsMerchantHere,
                MerchantEnabled = isManagementWindow && merchant != null && merchant.IsMerchantHere,
                RosterSummary = roster == null
                    ? "보유 유닛 정보를 불러오는 중"
                    : $"보유 {roster.TotalHiredCount}명 · 방을 선택한 뒤 배치할 수 있습니다",
                RevivalQueue = BuildRevivalQueue(revival),
                RosterList = rosterOpen ? BuildRosterList(roster) : string.Empty,
            };
        }

        private string BuildRevivalQueue(UnitRevivalSystem revival)
        {
            if (revival == null)
                return "부활 대기열 준비 중";
            if (revival.PendingCount == 0)
                return "부활 대기열 없음";

            _builder.Clear();
            foreach (var unit in revival.PendingUnits)
            {
                if (unit == null)
                    continue;

                if (_builder.Length > 0)
                    _builder.Append(" → ");
                var remaining = Mathf.CeilToInt(revival.GetRemainingSeconds(unit));
                _builder.Append(UnitName(unit.Data)).Append(' ')
                    .Append(remaining > 0 ? $"{remaining}초" : $"마력 {revival.GetRevivalCost(unit)}");
            }

            return _builder.Length > 0 ? $"부활 대기: {_builder}" : "부활 대기열 없음";
        }

        /// <summary>목록은 보기만 한다. 배치는 방을 고른 뒤 '유닛 배치' 설치 창에서 한다.</summary>
        private string BuildRosterList(HiredUnitRoster roster)
        {
            if (roster == null || roster.AvailableUnits.Count == 0)
                return "지금 배치할 수 있는 대기 유닛이 없습니다.";

            _counts.Clear();
            foreach (var unit in roster.AvailableUnits)
            {
                if (unit == null)
                    continue;
                _counts.TryGetValue(unit, out var count);
                _counts[unit] = count + 1;
            }

            _builder.Clear();
            foreach (var pair in _counts)
            {
                var condition = roster.GetBestAvailableCondition(pair.Key);
                _builder.Append(UnitName(pair.Key)).Append(" ×").Append(pair.Value)
                    .Append(" · Lv").Append(condition.Level).Append(" · 마력 ").Append(pair.Key.MagicCost).Append('\n');
            }

            return _builder.ToString();
        }

        private static string UnitName(UnitDataSO unit) =>
            unit == null || string.IsNullOrWhiteSpace(unit.Name) ? "유닛" : unit.Name;
    }
}
