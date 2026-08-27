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
            DungeonHudStyle.ApplyManagementDrawer(panelRoot);

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

            ScrollViewContentSizer.ResizeToGridItemCount(contentRoot, _entries.Count);
            if (_hireableUnits.Count == 0)
            {
                selectedUnit = null;
                SetDetailVisible(true);
                UpdateHint("영입 가능한 부하 없음");
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
                SyncInventoryFromRoster();
                RefreshHireEntries();
                RefreshEntryInteractableStates();
                selectedUnit = null;
                SetEntrySelection(null);
                SetDetailVisible(false);
                UpdateHint(string.Empty);
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
                UpdateHint($"{BuildUnitDetailText(unit)}\n\n영입 가능한 후보가 없습니다. 습격 보상으로 계약서를 획득하십시오.");
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
            UpdateHint($"{BuildUnitDetailText(evt.Unit)}\n\n{name} 영입 완료 · 운영 자금 {evt.RemainingGold}G");
        }

        private void HandleHireRejected(RosterHireRejectedEvent evt)
        {
            SetDetailVisible(true);
            var reason = GetOwnedUnitCount(evt.Unit) <= 0
                ? "영입 가능한 후보가 없습니다."
                : $"운영 자금이 부족합니다. 필요 {evt.GoldAmount}G / 보유 {evt.CurrentGold}G";
            UpdateHint($"{BuildUnitDetailText(evt.Unit)}\n\n{reason}");
        }

        private void UpdateHint(string message)
        {
            if (hintText != null) hintText.text = message;
        }

        private void ConfigureStaticTextLayout()
        {
            if (toggleButton != null)
            {
                TmpTextLayoutUtility.KeepHorizontal(toggleButton.GetComponentInChildren<TMP_Text>(true), true);
                DungeonHudStyle.ApplySideActionButton(toggleButton.gameObject);
            }

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

            return $"{displayName}  ·  등급 {(int)unit.Grade}\n" +
                   $"━━━━━━━━━━━━━━━━\n" +
                   $"전투  공격 {attackText}  |  방어 {defense}  |  체력 {healthText}\n" +
                   $"전투  공격 간격 {intervalText}  |  마력 {unit.MagicCost}\n" +
                   $"운영  계약서 {GetOwnedUnitCount(unit)}  |  대기 {GetAvailableUnitCount(unit)}  |  배치 {GetDeployedUnitCount(unit)}\n" +
                   $"비용  영입 {unit.Cost}G  |  일일 급여 {Mathf.Max(1, Mathf.CeilToInt(unit.Cost / 5f))}G  |  운영 자금 {_currentGold}G\n" +
                   $"경계  기본 +{unit.BaseDanger}  |  전투 시 +{unit.DangerIncreaseOnCombat}\n" +
                   BuildApplicantText(unit) +
                   "\n선택 후 같은 카드를 다시 누르면 영입합니다.";
        }

        /// <summary>
        /// 지금 뽑으면 누가 오는지. 특성과 성격이 스탯을 바꾸는데도 여태 고용한 뒤에야 알 수 있었다.
        /// 미리 보여야 "무엇을 뽑을까"가 아니라 "누구를 뽑을까"가 된다.
        /// </summary>
        private string BuildApplicantText(UnitDataSO unit)
        {
            var roster = HiredUnitRoster.Current;
            if (roster == null || GetOwnedUnitCount(unit) <= 0)
                return "\n지원자  없음 — 계약서를 확보하십시오\n";

            var applicant = roster.PeekApplicant(unit);
            var daysLeft = roster.GetApplicantDaysLeft(unit);
            // 기한이 하루 남으면 붉게. 미루는 데 대가가 있다는 걸 눈에 띄게 알린다.
            var deadline = daysLeft <= 0
                ? string.Empty
                : daysLeft <= 1
                    ? "  ·  <color=#FF7A6B>오늘까지</color>"
                    : $"  ·  {daysLeft}일 남음";

            return $"\n<color=#FFC85A>다음 지원자  {applicant.TraitLabel}  ·  {applicant.PersonalityLabel}</color>{deadline}\n" +
                   $"<size=85%>{UnitTraitUtility.GetDescription(applicant.Trait)}\n" +
                   $"{UnitPersonalityUtility.GetDescription(applicant.Personality)}</size>\n";
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

        private bool IsManagementAllowed()
        {
            dayManager ??= DayManager.Current;
            return dayManager != null && dayManager.IsStandby;
        }
    }
}
