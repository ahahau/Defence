using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Tutorial;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitDeployPanelView : MonoBehaviour
    {
        public static UnitDeployPanelView Current { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private UnitDeployEntryView entryPrefab;
        [SerializeField] private TMP_Text hintText;
        [SerializeField, Min(1)] private int gridColumns = 5;
        [SerializeField, Min(120f)] private float minCardWidth = 150f;
        [SerializeField, Min(120f)] private float maxCardWidth = 190f;
        [SerializeField, Min(1f)] private float cardHeightRatio = 1.45f;

        [Header("Data")]
        [SerializeField] private UnitDataSO[] deployableUnits;
        [SerializeField, Min(0)] private int fallbackStartingCopiesOfFirstUnit = 1;

        [Header("Event Channels")]
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private DayManager dayManager;

        private readonly List<UnitDataSO> _hireableUnits = new();
        private readonly List<UnitDeployEntryView> _entries = new();
        private readonly Dictionary<UnitDataSO, int> _ownedUnitCounts = new();
        private UnitDataSO selectedUnit;
        private int _currentGold;

        public RectTransform ToggleButtonRect => toggleButton != null ? toggleButton.transform as RectTransform : null;
        public RectTransform FirstEntryRect => _entries.Count > 0 && _entries[0] != null ? _entries[0].transform as RectTransform : null;
        public UnitDataSO FirstEntryUnit => _entries.Count > 0 && _entries[0] != null ? _entries[0].Unit : null;
        public UnitDataSO FirstOwnedUnit
        {
            get
            {
                foreach (var unit in _hireableUnits)
                {
                    if (unit != null && GetOwnedUnitCount(unit) > 0)
                        return unit;
                }

                return FirstEntryUnit;
            }
        }
        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;

        public RectTransform GetEntryRect(UnitDataSO unit)
        {
            if (unit == null)
                return null;

            foreach (var entry in _entries)
            {
                if (entry != null && entry.Unit == unit)
                    return entry.transform as RectTransform;
            }

            return null;
        }

        private void Awake()
        {
            dayManager ??= DayManager.Current;
            ConfigureStaticTextLayout();
            EnsurePanelPlacement(); // 씬 위치 덮어쓰기와 무관하게 시작부터 우측 정렬로 고정

            if (panelRoot != null)
                panelRoot.SetActive(false);

            InitHireableUnitsFromData();
            RefreshHireEntries();
        }

        private void Update()
        {
            if (panelRoot != null && panelRoot.activeSelf && !IsManagementAllowed())
                panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            Current = this;

            if (toggleButton != null) toggleButton.onClick.AddListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.AddListener(HandleClose);

            if (costEventChannel == null)
                return;

            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.AddListener<RosterHireRejectedEvent>(HandleHireRejected);
            costEventChannel.AddListener<UnitUnlockChangedEvent>(HandleUnitUnlockChanged);
            costEventChannel.AddListener<UnitInventoryChangedEvent>(HandleUnitInventoryChanged);
            costEventChannel.AddListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel.AddListener<GoldChangedEvent>(HandleGoldChanged);
            SyncInventoryFromRoster();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            if (toggleButton != null) toggleButton.onClick.RemoveListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.RemoveListener(HandleClose);

            if (costEventChannel == null)
                return;

            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.RemoveListener<RosterHireRejectedEvent>(HandleHireRejected);
            costEventChannel.RemoveListener<UnitUnlockChangedEvent>(HandleUnitUnlockChanged);
            costEventChannel.RemoveListener<UnitInventoryChangedEvent>(HandleUnitInventoryChanged);
            costEventChannel.RemoveListener<RosterChangedEvent>(HandleRosterChanged);
            costEventChannel.RemoveListener<GoldChangedEvent>(HandleGoldChanged);
        }

        private void InitHireableUnitsFromData()
        {
            _hireableUnits.Clear();

            if (deployableUnits == null)
                return;

            for (var i = 0; i < deployableUnits.Length; i++)
            {
                var unit = deployableUnits[i];
                if (unit != null && !_hireableUnits.Contains(unit))
                {
                    _hireableUnits.Add(unit);
                    if (!_ownedUnitCounts.ContainsKey(unit))
                        _ownedUnitCounts[unit] = i == 0 ? fallbackStartingCopiesOfFirstUnit : 0;
                }
            }
        }

        private void RefreshHireEntries()
        {
            var previousSelection = selectedUnit;
            foreach (var e in _entries)
                if (e != null) Destroy(e.gameObject);
            _entries.Clear();

            if (entryPrefab == null || contentRoot == null) return;

            foreach (var unit in _hireableUnits)
            {
                var entry = Instantiate(entryPrefab, contentRoot);
                entry.Initialize(
                    unit,
                    HandleUnitSelected,
                    GetOwnedUnitCount(unit),
                    GetAvailableUnitCount(unit),
                    GetDeployedUnitCount(unit));
                if (TutorialInputGate.IsActive && !TutorialInputGate.AllowsHireUnit(unit))
                    entry.SetInteractable(false);
                _entries.Add(entry);
            }

            ConfigureHireGrid();
            ScrollViewContentSizer.ResizeToGridItemCount(contentRoot, _entries.Count);
            if (_hireableUnits.Count == 0)
            {
                selectedUnit = null;
                SetDetailVisible(true);
                UpdateHint("고용 가능한 유닛 없음");
            }
            else
            {
                selectedUnit = previousSelection != null && _hireableUnits.Contains(previousSelection)
                    ? previousSelection
                    : null;
                SetEntrySelection(selectedUnit);
                SetDetailVisible(selectedUnit != null);
                UpdateHint(selectedUnit != null ? BuildUnitDetailText(selectedUnit) : string.Empty);
            }
        }

        private void HandleToggle()
        {
            if (panelRoot == null || !IsManagementAllowed())
                return;

            if (!TutorialInputGate.AllowsHirePanel())
                return;

            var shouldShow = !panelRoot.activeSelf;
            if (shouldShow)
                transform.SetAsLastSibling();

            panelRoot.SetActive(shouldShow);

            if (shouldShow)
            {
                EnsurePanelPlacement();
                SyncInventoryFromRoster();
                RefreshHireEntries();
                RefreshEntryInteractableStates();
                selectedUnit = null;
                SetEntrySelection(null);
                SetDetailVisible(false);
                UpdateHint(string.Empty);
                ConfigureHireGrid();
                ScrollViewContentSizer.ResizeToGridItemCount(contentRoot, _entries.Count);
            }
        }

        private void HandleClose()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        /// <summary>고용 패널(루트=토글버튼+패널 서랍)을 프리팹 의도대로 화면 우측에 붙인다.
        /// 씬에서 루트 위치가 (-238,20) 등으로 어긋나게 덮어써져도 항상 우측 정렬로 복원한다.
        /// (중앙/좌측 등 다른 배치를 원하면 anchor/pivot만 바꾸면 된다.)</summary>
        private void EnsurePanelPlacement()
        {
            if (transform is not RectTransform rect)
                return;

            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private void HandleUnitUnlockChanged(UnitUnlockChangedEvent evt)
        {
            InitHireableUnitsFromData();
            RefreshHireEntries();
        }

        private void HandleUnitInventoryChanged(UnitInventoryChangedEvent evt)
        {
            _ownedUnitCounts.Clear();
            if (evt.OwnedUnits != null)
            {
                foreach (var pair in evt.OwnedUnits)
                {
                    if (pair.Key != null)
                        _ownedUnitCounts[pair.Key] = pair.Value;
                }
            }

            RefreshHireEntries();
        }

        private void HandleRosterChanged(RosterChangedEvent evt)
        {
            RefreshHireEntries();
        }

        private void HandleGoldChanged(GoldChangedEvent evt)
        {
            _currentGold = Mathf.Max(0, evt.CurrentGold);
            if (selectedUnit != null)
                UpdateHint(BuildUnitDetailText(selectedUnit));
        }

        private void RefreshEntryInteractableStates()
        {
            foreach (var entry in _entries)
            {
                if (entry == null || entry.Unit == null)
                    continue;

                entry.SetInteractable(GetOwnedUnitCount(entry.Unit) > 0 && TutorialInputGate.AllowsHireUnit(entry.Unit));
            }
        }

        private void HandleUnitSelected(UnitDataSO unit)
        {
            if (unit == null)
                return;

            if (!TutorialInputGate.AllowsHireUnit(unit))
                return;

            if (GetOwnedUnitCount(unit) <= 0)
            {
                SelectUnit(unit);
                UpdateHint($"{BuildUnitDetailText(unit)}\n\n고용 가능한 후보가 없습니다. 웨이브 보상으로 후보 계약서를 획득하세요.");
                return;
            }

            if (selectedUnit == unit)
            {
                HandleHireRequested(unit);
                return;
            }

            SelectUnit(unit);
        }

        private void HandleHireRequested(UnitDataSO unit)
        {
            if (costEventChannel == null || unit == null)
                return;

            costEventChannel.RaiseEvent(new RosterHireRequestedEvent(unit, Mathf.Max(0, unit.Cost)));
        }

        private void HandleHirePaid(RosterHirePaidEvent evt)
        {
            RefreshHireEntries();
            var name = !string.IsNullOrWhiteSpace(evt.Unit.Name) ? evt.Unit.Name : evt.Unit.name;
            selectedUnit = evt.Unit;
            SetEntrySelection(selectedUnit);
            SetDetailVisible(true);
            UpdateHint($"{BuildUnitDetailText(evt.Unit)}\n\n{name} 고용 완료! 남은 골드 {evt.RemainingGold}G");
        }

        private void HandleHireRejected(RosterHireRejectedEvent evt)
        {
            SetDetailVisible(true);
            var reason = GetOwnedUnitCount(evt.Unit) <= 0
                ? "고용 가능한 후보가 없습니다."
                : $"골드가 부족합니다. 필요 {evt.GoldAmount}G / 보유 {evt.CurrentGold}G";
            UpdateHint($"{BuildUnitDetailText(evt.Unit)}\n\n{reason}");
        }

        private void UpdateHint(string message)
        {
            if (hintText != null) hintText.text = message;
        }

        private void ConfigureStaticTextLayout()
        {
            if (toggleButton != null)
                TmpTextLayoutUtility.KeepHorizontal(toggleButton.GetComponentInChildren<TMP_Text>(true), true);

            if (closeButton != null)
                TmpTextLayoutUtility.KeepHorizontal(closeButton.GetComponentInChildren<TMP_Text>(true), true);
        }

        private void SelectUnit(UnitDataSO unit)
        {
            selectedUnit = unit;
            SetEntrySelection(selectedUnit);
            SetDetailVisible(true);
            UpdateHint(BuildUnitDetailText(unit));
        }

        private void SetEntrySelection(UnitDataSO unit)
        {
            foreach (var entry in _entries)
                if (entry != null)
                    entry.SetSelected(entry.Unit == unit);
        }

        private void SetDetailVisible(bool visible)
        {
            if (hintText != null)
                hintText.gameObject.SetActive(visible);
        }

        private string BuildUnitDetailText(UnitDataSO unit)
        {
            if (unit == null)
                return "유닛을 선택하세요";

            var displayName = !string.IsNullOrWhiteSpace(unit.Name) ? unit.Name : unit.name;
            var combatant = ResolvePreviewComponent<Combatant>(unit);
            var health = ResolvePreviewComponent<Health>(unit);

            var attackText = combatant != null ? combatant.AttackDamage.ToString() : "-";
            var defense = combatant != null ? combatant.Defense : unit.Defense;
            var healthText = health != null ? health.MaxHealth.ToString() : "-";
            var intervalText = combatant != null ? $"{combatant.AttackInterval:0.##}초" : "-";

            return $"{displayName}\n" +
                   $"등급: {(int)unit.Grade}\n" +
                   $"고용 후보: {GetOwnedUnitCount(unit)}\n" +
                   $"대기 인원: {GetAvailableUnitCount(unit)}\n" +
                   $"배치 중: {GetDeployedUnitCount(unit)}\n" +
                   $"고용 비용: {unit.Cost}G (보유 {_currentGold}G)\n" +
                   $"일일 급여: {Mathf.Max(1, Mathf.CeilToInt(unit.Cost / 5f))}G\n" +
                   $"마력: {unit.MagicCost}\n" +
                   $"공격력: {attackText}\n" +
                   $"방어력: {defense}\n" +
                   $"체력: {healthText}\n" +
                   $"공격속도: {intervalText}\n" +
                   $"기본 위험도: {unit.BaseDanger}\n" +
                   $"전투 위험 증가: {unit.DangerIncreaseOnCombat}\n\n" +
                   "선택한 후보를 한 번 더 클릭하면 골드를 지불하고 고용";
        }

        private int GetOwnedUnitCount(UnitDataSO unit)
        {
            return unit != null && _ownedUnitCounts.TryGetValue(unit, out var count) ? count : 0;
        }

        private int GetAvailableUnitCount(UnitDataSO unit)
        {
            var roster = HiredUnitRoster.Current;
            return roster != null ? roster.GetAvailableUnitCount(unit) : 0;
        }

        private int GetDeployedUnitCount(UnitDataSO unit)
        {
            var roster = HiredUnitRoster.Current;
            return roster != null ? roster.GetDeployedUnitCount(unit) : 0;
        }

        private void SyncInventoryFromRoster()
        {
            var roster = HiredUnitRoster.Current;
            if (roster == null)
                return;

            _ownedUnitCounts.Clear();
            foreach (var pair in roster.OwnedUnits)
            {
                if (pair.Key != null)
                    _ownedUnitCounts[pair.Key] = pair.Value;
            }
        }

        private T ResolvePreviewComponent<T>(UnitDataSO unit) where T : Component
        {
            if (unit == null || unit.Prefab == null)
                return null;

            var component = unit.Prefab.GetComponent<T>();
            return component != null ? component : unit.Prefab.GetComponentInChildren<T>(true);
        }

        private void ConfigureHireGrid()
        {
            if (contentRoot == null || contentRoot is not RectTransform contentRect)
                return;

            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            if (contentRoot.TryGetComponent<ContentSizeFitter>(out var fitter))
                fitter.enabled = false;

            var grid = contentRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
                return;

            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            var viewportWidth = ResolveViewportWidth(contentRect);
            var desiredWidth = Mathf.Clamp((minCardWidth + maxCardWidth) * 0.5f, minCardWidth, maxCardWidth);
            var maxColumns = Mathf.Max(1, gridColumns);
            var columnsByWidth = Mathf.FloorToInt((viewportWidth - grid.padding.left - grid.padding.right + grid.spacing.x)
                                                  / (desiredWidth + grid.spacing.x));
            var columns = Mathf.Clamp(columnsByWidth, 1, maxColumns);
            var availableWidth = viewportWidth
                                 - grid.padding.left
                                 - grid.padding.right
                                 - Mathf.Max(0, columns - 1) * grid.spacing.x;
            var cardWidth = Mathf.Clamp(Mathf.Floor(availableWidth / columns), minCardWidth, maxCardWidth);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cardWidth, Mathf.Round(cardWidth * cardHeightRatio));
        }

        private float ResolveViewportWidth(RectTransform contentRect)
        {
            if (contentRect.parent is RectTransform viewport)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                if (viewport.rect.width > 1f)
                    return viewport.rect.width;
            }

            return maxCardWidth * Mathf.Max(1, gridColumns);
        }

        private bool IsManagementAllowed()
        {
            dayManager ??= DayManager.Current;
            return dayManager != null && dayManager.IsStandby;
        }
    }
}
