using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Tutorial;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class UnitDeployPanelView : MonoBehaviour
    {
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

        private readonly List<UnitDataSO> _hireableUnits = new();
        private readonly List<UnitDeployEntryView> _entries = new();
        private readonly Dictionary<UnitDataSO, int> _ownedUnitCounts = new();
        private UnitDataSO selectedUnit;

        public RectTransform ToggleButtonRect => toggleButton != null ? toggleButton.transform as RectTransform : null;
        public RectTransform FirstEntryRect => _entries.Count > 0 && _entries[0] != null ? _entries[0].transform as RectTransform : null;
        public UnitDataSO FirstEntryUnit => _entries.Count > 0 && _entries[0] != null ? _entries[0].Unit : null;
        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            InitHireableUnitsFromData();
            RefreshHireEntries();
        }

        private void OnEnable()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.AddListener(HandleClose);

            if (costEventChannel == null)
                return;

            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.AddListener<RosterHireRejectedEvent>(HandleHireRejected);
            costEventChannel.AddListener<UnitUnlockChangedEvent>(HandleUnitUnlockChanged);
            costEventChannel.AddListener<UnitInventoryChangedEvent>(HandleUnitInventoryChanged);
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.RemoveListener(HandleClose);

            if (costEventChannel == null)
                return;

            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.RemoveListener<RosterHireRejectedEvent>(HandleHireRejected);
            costEventChannel.RemoveListener<UnitUnlockChangedEvent>(HandleUnitUnlockChanged);
            costEventChannel.RemoveListener<UnitInventoryChangedEvent>(HandleUnitInventoryChanged);
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
            foreach (var e in _entries)
                if (e != null) Destroy(e.gameObject);
            _entries.Clear();

            if (entryPrefab == null || contentRoot == null) return;

            foreach (var unit in _hireableUnits)
            {
                var entry = Instantiate(entryPrefab, contentRoot);
                entry.Initialize(unit, HandleUnitSelected, GetOwnedUnitCount(unit));
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
                selectedUnit = null;
                SetDetailVisible(false);
                UpdateHint(string.Empty);
            }
        }

        private void HandleToggle()
        {
            if (panelRoot == null)
                return;

            if (!TutorialInputGate.AllowsHirePanel())
                return;

            var shouldShow = !panelRoot.activeSelf;
            if (shouldShow)
                transform.SetAsLastSibling();

            panelRoot.SetActive(shouldShow);

            if (shouldShow)
            {
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

        private void HandleUnitSelected(UnitDataSO unit)
        {
            if (unit == null)
                return;

            if (!TutorialInputGate.AllowsHireUnit(unit))
                return;

            if (GetOwnedUnitCount(unit) <= 0)
            {
                SelectUnit(unit);
                UpdateHint($"{BuildUnitDetailText(unit)}\n\n보유 수량이 없습니다. 웨이브 보상이나 원정으로 획득하세요.");
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

            costEventChannel.RaiseEvent(new RosterHireRequestedEvent(unit, 0));
        }

        private void HandleHirePaid(RosterHirePaidEvent evt)
        {
            RefreshHireEntries();
            var name = !string.IsNullOrWhiteSpace(evt.Unit.Name) ? evt.Unit.Name : evt.Unit.name;
            SetDetailVisible(true);
            UpdateHint($"{BuildUnitDetailText(evt.Unit)}\n\n{name} 대기 로스터 합류!");

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void HandleHireRejected(RosterHireRejectedEvent evt)
        {
            SetDetailVisible(true);
            UpdateHint($"{BuildUnitDetailText(evt.Unit)}\n\n보유 수량이 없습니다.");
        }

        private void UpdateHint(string message)
        {
            if (hintText != null) hintText.text = message;
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
                   $"보유: {GetOwnedUnitCount(unit)}\n" +
                   $"마력: {unit.MagicCost}\n" +
                   $"공격력: {attackText}\n" +
                   $"방어력: {defense}\n" +
                   $"체력: {healthText}\n" +
                   $"공격속도: {intervalText}\n" +
                   $"기본 위험도: {unit.BaseDanger}\n" +
                   $"전투 위험 증가: {unit.DangerIncreaseOnCombat}\n\n" +
                   "선택된 유닛을 한 번 더 클릭하면 대기 로스터로 이동";
        }

        private int GetOwnedUnitCount(UnitDataSO unit)
        {
            return unit != null && _ownedUnitCounts.TryGetValue(unit, out var count) ? count : 0;
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
    }
}
