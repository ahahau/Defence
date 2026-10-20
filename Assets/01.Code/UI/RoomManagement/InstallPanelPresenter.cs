using System.Collections.Generic;
using Code.Buildings;
using Code.Manager;
using Code.Tutorial;
using Code.Units;
using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 설치 창의 상태(갈래·쪽·고른 카드·접힘)와 뷰 모델 만들기. 실제 설치·배치는 <see cref="NodePanelView"/>로 보낸다.
    ///
    /// 고른 카드는 순번이 아니라 데이터로 기억한다. 해금·로스터가 바뀌어 목록이 다시 짜여도 같은 카드가 골라져 있다.
    /// </summary>
    public sealed class InstallPanelPresenter
    {
        /// <summary>설치 창 한 쪽에 놓는 카드 수.</summary>
        public const int CardsPerPage = 5;

        private readonly List<InstallOption> _rawOptions = new();
        private readonly List<InstallOption> _buildingOptions = new();
        private readonly List<DeployOption> _deployOptions = new();
        private readonly List<InstallCardViewModel> _cards = new();
        private readonly InstallCardTextFormatter _formatter = new();
        private readonly Dictionary<BuildingDataSO, Sprite> _sprites = new();
        private Object _selected;
        private int _page;

        public RoomInstallKind? Kind { get; private set; }

        /// <summary>위치를 고르는 동안 접혀 있는가.</summary>
        public bool Collapsed { get; private set; }

        public bool IsOpen(RoomInstallKind kind) => Kind == kind;

        /// <summary>고른 카드가 있고 지금 설치할 수 있는가. 튜토리얼이 "다음은 설치 버튼"을 알아낸다.</summary>
        public bool HasInstallableSelection(NodePanelView panel)
        {
            Collect(panel);
            return TryGetSelected(out var index) && !IsBlocked(panel, index);
        }

        public void Open(RoomInstallKind kind)
        {
            Kind = kind;
            _page = 0;
            _selected = null;
            Collapsed = false;
        }

        public void Close()
        {
            Kind = null;
            _selected = null;
            Collapsed = false;
        }

        /// <summary>위치 고르기가 끝나 다시 펼친다. 같은 카드를 골라 둔 채라 연속 설치가 된다.</summary>
        public void Expand() => Collapsed = false;

        public void SelectSlot(NodePanelView panel, int slot)
        {
            Collect(panel);
            var index = _page * CardsPerPage + slot;
            if (index >= 0 && index < Count)
                _selected = DataAt(index);
        }

        public void ChangePage(NodePanelView panel, int delta)
        {
            Collect(panel);
            _page = Mathf.Clamp(_page + delta, 0, Mathf.Max(0, PageCount - 1));
        }

        /// <summary>
        /// 고른 카드로 위치 고르기를 시작한다. 결제·설치가 같은 프레임에 끝나 <see cref="Expand"/>가 먼저
        /// 불릴 수 있으므로 접는 것을 요청 전에 한다.
        /// </summary>
        public bool TryConfirm(NodePanelView panel)
        {
            Collect(panel);
            if (!TryGetSelected(out var index))
                return false;

            Collapsed = true;
            var started = Kind == RoomInstallKind.Unit
                ? panel.TryBeginUnitPlacement(_deployOptions[index].Unit)
                : panel.TryBeginBuildingPlacement(_buildingOptions[index].Data);
            if (!started)
                Collapsed = false;
            return started;
        }

        /// <summary>
        /// 튜토리얼이 비출 카드의 이 쪽 안 순번. 허락된 유닛이 있으면 그것을, 없으면 설치할 수 있는 첫 카드.
        /// 이 쪽에 없으면 -1.
        /// </summary>
        public int PreferredSlot(NodePanelView panel)
        {
            Collect(panel);
            var start = _page * CardsPerPage;
            var end = Mathf.Min(Count, start + CardsPerPage);
            for (var i = start; i < end; i++)
            {
                if (Kind == RoomInstallKind.Unit && TutorialInputGate.AllowedDeployUnit != null)
                {
                    if (_deployOptions[i].Unit == TutorialInputGate.AllowedDeployUnit)
                        return i - start;
                    continue;
                }

                if (!IsBlocked(panel, i))
                    return i - start;
            }

            return -1;
        }

        public InstallPanelViewModel Build(NodePanelView panel)
        {
            Collect(panel);
            _page = Mathf.Clamp(_page, 0, Mathf.Max(0, PageCount - 1));
            var hasSelection = TryGetSelected(out var selectedIndex);

            _cards.Clear();
            var start = _page * CardsPerPage;
            var end = Mathf.Min(Count, start + CardsPerPage);
            for (var i = start; i < end; i++)
                _cards.Add(BuildCard(panel, i, hasSelection && i == selectedIndex));

            var model = new InstallPanelViewModel
            {
                Title = TitleOf(Kind),
                Subtitle = Collapsed ? CollapsedHint(panel) : SubtitleOf(Kind),
                Collapsed = Collapsed,
                CloseLabel = Collapsed ? "취소" : "닫기",
                PageLabel = $"{_page + 1} / {Mathf.Max(1, PageCount)}",
                CanPrevPage = _page > 0,
                CanNextPage = _page < PageCount - 1,
                Cards = _cards,
                ConfirmLabel = Kind == RoomInstallKind.Unit ? "배치" : "설치",
            };

            if (!hasSelection)
            {
                model.DetailName = "카드를 고르면 자세한 내용이 보입니다";
                model.DetailSummary = string.Empty;
                model.DetailEffect = string.Empty;
                model.DetailReason = string.Empty;
                model.ConfirmEnabled = false;
                return model;
            }

            if (Kind == RoomInstallKind.Unit)
                FillUnitDetail(panel, _deployOptions[selectedIndex], ref model);
            else
                FillBuildingDetail(_buildingOptions[selectedIndex], ref model);
            return model;
        }

        private int Count => Kind == RoomInstallKind.Unit ? _deployOptions.Count : _buildingOptions.Count;

        private int PageCount => (Count + CardsPerPage - 1) / CardsPerPage;

        private Object DataAt(int index) =>
            Kind == RoomInstallKind.Unit ? _deployOptions[index].Unit : _buildingOptions[index].Data;

        private bool TryGetSelected(out int index)
        {
            index = -1;
            if (_selected == null)
                return false;

            for (var i = 0; i < Count; i++)
            {
                if (DataAt(i) == _selected)
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private bool IsBlocked(NodePanelView panel, int index) =>
            Kind == RoomInstallKind.Unit
                ? panel.TryGetDeployBlockReason(out _)
                : !_buildingOptions[index].CanInstall;

        private void Collect(NodePanelView panel)
        {
            _buildingOptions.Clear();
            _deployOptions.Clear();
            if (panel == null || !Kind.HasValue)
                return;

            switch (Kind.Value)
            {
                case RoomInstallKind.Unit:
                    panel.EnumerateDeployOptions(_deployOptions);
                    return;
                case RoomInstallKind.Trap:
                    panel.EnumerateInstallOptions(InstallCategory.Trap, _buildingOptions);
                    return;
            }

            // 건설과 시설은 같은 건물 갈래를 운영비·손님 체류 유무로 나눈다.
            var wantFacility = Kind.Value == RoomInstallKind.Facility;
            panel.EnumerateInstallOptions(InstallCategory.Building, _rawOptions);
            foreach (var option in _rawOptions)
            {
                if (IsFacility(option.Data) == wantFacility)
                    _buildingOptions.Add(option);
            }
        }

        private static bool IsFacility(BuildingDataSO data) => data.DailyUpkeep > 0 || data.DwellSeconds > 0f;

        private InstallCardViewModel BuildCard(NodePanelView panel, int index, bool selected)
        {
            if (Kind == RoomInstallKind.Unit)
            {
                var option = _deployOptions[index];
                var blocked = panel.TryGetDeployBlockReason(out var reason);
                return new InstallCardViewModel(
                    option.Unit.BoardSprite != null ? option.Unit.BoardSprite : option.Unit.Sprite,
                    UnitName(option.Unit),
                    $"×{option.Count} · 마력 {option.Unit.MagicCost}",
                    blocked ? reason : $"Lv{option.Condition.Level} · {option.Condition.TraitLabel}",
                    blocked,
                    selected);
            }

            var building = _buildingOptions[index];
            return new InstallCardViewModel(
                ResolvePreviewSprite(building.Data),
                BuildingName(building.Data),
                _formatter.FormatCost(building.Data, DiscountedCost(building.Data)),
                building.CanInstall ? _formatter.BuildCardNote(building.Data) : building.BlockedReason,
                !building.CanInstall,
                selected);
        }

        private void FillBuildingDetail(in InstallOption option, ref InstallPanelViewModel model)
        {
            model.DetailName = BuildingName(option.Data);
            model.DetailSummary = _formatter.BuildSummary(option.Data, DiscountedCost(option.Data));
            model.DetailEffect = _formatter.BuildEffect(option.Data);
            model.DetailReason = option.CanInstall ? string.Empty : option.BlockedReason;
            model.ConfirmEnabled = option.CanInstall;
        }

        private static void FillUnitDetail(NodePanelView panel, in DeployOption option, ref InstallPanelViewModel model)
        {
            var condition = option.Condition;
            var blocked = panel.TryGetDeployBlockReason(out var reason);
            model.DetailName = $"{UnitName(option.Unit)} Lv{condition.Level} · 대기 {option.Count}명";
            model.DetailSummary = $"배치 마력 {option.Unit.MagicCost} · {condition.TraitLabel} · {condition.PersonalityLabel}";
            model.DetailEffect = $"{UnitTraitUtility.GetDescription(condition.Trait)}\n{condition.Summary}";
            model.DetailReason = blocked ? reason : string.Empty;
            model.ConfirmEnabled = !blocked;
        }

        private static string CollapsedHint(NodePanelView panel) =>
            panel != null && panel.IsChoosingUnitCell
                ? "방 안의 밝은 칸을 눌러 배치하세요 · ESC로 취소"
                : "지도에서 놓을 자리를 누르세요 · 우클릭·ESC로 취소";

        private static string TitleOf(RoomInstallKind? kind) => kind switch
        {
            RoomInstallKind.Facility => "시설",
            RoomInstallKind.Trap => "함정",
            RoomInstallKind.Unit => "유닛 배치",
            _ => "건설",
        };

        private static string SubtitleOf(RoomInstallKind? kind) => kind switch
        {
            RoomInstallKind.Facility => "손님을 받아 돈을 버는 시설 · 유닛과 같은 방에 못 둡니다",
            RoomInstallKind.Trap => "모험가를 해치는 함정 · 유닛과 같은 방에 둘 수 있습니다",
            RoomInstallKind.Unit => "대기 명단의 유닛을 이 방에 세웁니다",
            _ => "금고·회복실 같은 운영비 없는 건물",
        };

        private static int DiscountedCost(BuildingDataSO data) =>
            CostManager.Current != null ? CostManager.Current.GetDiscountedBuildCost(data.Cost) : data.Cost;

        /// <summary>카드 그림. 프리팹 스프라이트를 먼저 쓰고 없으면 보드 그림. 다시 그릴 때마다 찾지 않게 담아 둔다.</summary>
        private Sprite ResolvePreviewSprite(BuildingDataSO data)
        {
            if (_sprites.TryGetValue(data, out var sprite))
                return sprite;

            var prefabSprite = data.Prefab != null
                ? data.Prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite
                : null;
            sprite = prefabSprite != null ? prefabSprite : data.BoardSprite;
            _sprites[data] = sprite;
            return sprite;
        }

        private static string BuildingName(BuildingDataSO data) =>
            string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName;

        private static string UnitName(UnitDataSO unit) =>
            string.IsNullOrWhiteSpace(unit.Name) ? unit.name : unit.Name;
    }
}
