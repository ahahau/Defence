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

            // 영입 카드도 건물 카드와 같은 크기로. 프리팹 값 176x230 은 나란히 두면 눈에 띄게 작다.
            ScrollViewContentSizer.ConfigureHorizontalCards(contentRoot);
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
            // 카드에서 운영 자금 줄을 뺐지만, 영입 가능 여부 안내는 금화에 따라 달라진다.
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
                UpdateHint(BuildUnitDetailText(unit));
                SetStatus("계약서가 없습니다  ·  습격 보상으로 얻습니다", StatusWarn);
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
            UpdateHint(BuildUnitDetailText(evt.Unit));
            SetStatus($"{name} 영입  ·  남은 자금 {evt.RemainingGold}G", StatusGood);
        }

        private void HandleHireRejected(RosterHireRejectedEvent evt)
        {
            SetDetailVisible(true);
            var reason = GetOwnedUnitCount(evt.Unit) <= 0
                ? "계약서가 없습니다"
                : $"자금 부족  ·  필요 {evt.GoldAmount}G / 보유 {evt.CurrentGold}G";
            UpdateHint(BuildUnitDetailText(evt.Unit));
            SetStatus(reason, StatusWarn);
        }

        private void UpdateHint(string message)
        {
            if (hintText != null) hintText.text = message;

            // 유닛을 새로 고르면 앞선 결과 알림은 지운다. 남겨 두면 다른 유닛의 결과가
            // 이 유닛의 설명인 것처럼 붙어 보인다.
            if (statusText != null && string.IsNullOrEmpty(message))
                SetStatus(string.Empty, StatusGood);
        }

        private static readonly Color StatusGood = new(0.55f, 0.92f, 0.62f, 1f);
        private static readonly Color StatusWarn = new(1f, 0.72f, 0.42f, 1f);

        private TMP_Text statusText;

        /// <summary>
        /// 결과 알림을 설명과 갈라 놓는다.
        ///
        /// 예전에는 "영입 완료", "자금 부족" 같은 말을 유닛 설명 뒤에 이어 붙였다.
        /// 그러면 한 덩어리가 되어 어디까지가 이 유닛의 값이고 어디부터가 방금 벌어진 일인지
        /// 구분되지 않는다. 자리를 따로 두고 색으로 성패를 구분한다.
        /// </summary>
        private void SetStatus(string message, Color color)
        {
            EnsureStatusText();
            if (statusText == null)
                return;

            statusText.text = message;
            statusText.color = color;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void EnsureStatusText()
        {
            if (statusText != null || hintText == null)
                return;

            var go = new GameObject("Status", typeof(RectTransform));
            go.transform.SetParent(hintText.transform.parent, false);
            go.transform.SetSiblingIndex(hintText.transform.GetSiblingIndex() + 1);

            statusText = go.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = Mathf.Max(12f, hintText.fontSize * 0.95f);
            statusText.alignment = TextAlignmentOptions.TopLeft;
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.overflowMode = TextOverflowModes.Ellipsis;
            statusText.raycastTarget = false;

            // 설명 바로 아래에 한 줄로 눕힌다. 설명 칸을 침범하지 않게 아래쪽에 붙인다.
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, 6f);
            rect.offsetMax = new Vector2(0f, 34f);

            go.SetActive(false);
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

            // 여섯 줄이 빽빽해서 정작 고를 때 보는 값이 묻혔다. 판단에 쓰는 것만 남긴다.
            // 뺀 것: 경계 수치(부차적), 운영 자금(상단 카드에 이미 있다), 전투 줄의 분리.
            // 값을 가운뎃점으로 이어 붙이면 줄줄이 나열돼 읽는 데 눈이 오래 걸린다.
            // 왼쪽에 분류를 세우고 값을 같은 자리에 맞춰 세로줄을 만들면 표처럼 훑힌다.
            // <pos=%>는 칸 너비 기준이라 패널 크기가 달라져도 열이 유지된다.
            var upkeep = Mathf.Max(1, Mathf.CeilToInt(unit.Cost / 5f));

            return $"<size=115%>{displayName}</size>  <color=#9C9088>등급 {(int)unit.Grade}</color>\n" +
                   $"<color=#5A4E45>────────────────</color>\n" +
                   Row("전투", $"공격 {attackText}", $"방어 {defense}", $"체력 {healthText}", $"간격 {intervalText}") +
                   Row("자원", $"마력 {unit.MagicCost}", $"영입 {unit.Cost}G", $"급여 {upkeep}G") +
                   Row("보유", $"계약서 {GetOwnedUnitCount(unit)}", $"대기 {GetAvailableUnitCount(unit)}",
                       $"배치 {GetDeployedUnitCount(unit)}") +
                   BuildApplicantText(unit);
        }

        /// <summary>
        /// 분류 하나와 값들을 한 줄에 세로 맞춰 늘어놓는다.
        ///
        /// 열 간격을 고정값으로 두면 값이 넷일 때 마지막이 78%에서 시작해 오른쪽으로 넘친다.
        /// 남은 폭을 값 개수로 나눠, 몇 개가 오든 마지막 열이 화면 안에 들어오게 한다.
        /// </summary>
        private const float LabelColumnPercent = 12f;

        private static string Row(string label, params string[] values)
        {
            var line = $"<color=#9C9088>{label}</color>";
            if (values.Length == 0)
                return line + "\n";

            var step = (100f - LabelColumnPercent) / values.Length;
            for (var i = 0; i < values.Length; i++)
                line += $"<pos={LabelColumnPercent + i * step:0.#}%>{values[i]}";

            return line + "\n";
        }

        /// <summary>
        /// 지금 뽑으면 누가 오는지. 특성과 성격이 스탯을 바꾸는데도 여태 고용한 뒤에야 알 수 있었다.
        /// 미리 보여야 "무엇을 뽑을까"가 아니라 "누구를 뽑을까"가 된다.
        /// </summary>
        private string BuildApplicantText(UnitDataSO unit)
        {
            var roster = HiredUnitRoster.Current;
            // 계약서가 없다는 사실은 아래 상태 줄이 색까지 붙여 알린다. 여기서 또 적으면
            // 같은 말이 한 화면에 두 번 나온다.
            if (roster == null || GetOwnedUnitCount(unit) <= 0)
                return string.Empty;

            var applicant = roster.PeekApplicant(unit);
            var daysLeft = roster.GetApplicantDaysLeft(unit);
            // 기한이 하루 남으면 붉게. 미루는 데 대가가 있다는 걸 눈에 띄게 알린다.
            var deadline = daysLeft <= 0
                ? string.Empty
                : daysLeft <= 1
                    ? "  ·  <color=#FF7A6B>오늘까지</color>"
                    : $"  ·  {daysLeft}일 남음";

            // 지원자는 위의 수치 표와 성격이 다른 정보라 한 칸 띄우고 색으로 갈라 놓는다.
            // 특성·성격 설명 두 줄은 표와 같은 열에 맞춰 붙여, 읽는 눈이 왼쪽으로 돌아오게 한다.
            return $"\n<color=#FFC85A>지원자</color><pos=12%><color=#FFC85A>{applicant.TraitLabel}</color>" +
                   $"<pos=40%><color=#FFC85A>{applicant.PersonalityLabel}</color>{deadline}\n" +
                   $"<size=85%><color=#A79C92><pos=12%>{UnitTraitUtility.GetDescription(applicant.Trait)}\n" +
                   $"<pos=12%>{UnitPersonalityUtility.GetDescription(applicant.Personality)}</color></size>\n";
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
