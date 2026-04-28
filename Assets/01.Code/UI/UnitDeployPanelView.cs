using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
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
        [SerializeField] private Text hintText;

        [Header("Data")]
        [SerializeField] private UnitDataSO[] deployableUnits;
        [SerializeField] private int maxHirePerDay = 5;

        [Header("Event Channels")]
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO dayEventChannel;

        private readonly List<UnitDataSO> _dailyPool = new();
        private readonly List<UnitDeployEntryView> _entries = new();

        private void Awake()
        {
            panelRoot.SetActive(false);
            InitDailyPool();
            RefreshHireEntries();
        }

        private void OnEnable()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.AddListener(HandleClose);
            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.AddListener<RosterHireRejectedEvent>(HandleHireRejected);
            if (dayEventChannel != null)
                dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.RemoveListener(HandleClose);
            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.RemoveListener<RosterHireRejectedEvent>(HandleHireRejected);
            if (dayEventChannel != null)
                dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
        }

        private void InitDailyPool()
        {
            _dailyPool.Clear();
            var limit = Mathf.Min(deployableUnits.Length, maxHirePerDay);
            for (var i = 0; i < limit; i++)
            {
                if (deployableUnits[i] != null)
                    _dailyPool.Add(deployableUnits[i]);
            }
        }

        private void RefreshHireEntries()
        {
            foreach (var e in _entries)
                if (e != null) Destroy(e.gameObject);
            _entries.Clear();

            if (entryPrefab == null || contentRoot == null) return;

            foreach (var unit in _dailyPool)
            {
                var entry = Instantiate(entryPrefab, contentRoot);
                entry.Initialize(unit, HandleHireRequested);
                _entries.Add(entry);
            }

            UpdateHint(_dailyPool.Count == 0 ? "오늘 고용 가능한 유닛 없음" : string.Empty);
        }

        private void HandleToggle()
        {
            panelRoot.SetActive(!panelRoot.activeSelf);
        }

        private void HandleClose()
        {
            panelRoot.SetActive(false);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            InitDailyPool();
            RefreshHireEntries();
        }

        private void HandleHireRequested(UnitDataSO unit)
        {
            costEventChannel.RaiseEvent(new RosterHireRequestedEvent(unit, unit.Cost));
        }

        private void HandleHirePaid(RosterHirePaidEvent evt)
        {
            _dailyPool.Remove(evt.Unit);
            RefreshHireEntries();
            var name = !string.IsNullOrWhiteSpace(evt.Unit.Name) ? evt.Unit.Name : evt.Unit.name;
            UpdateHint(_dailyPool.Count == 0 ? "오늘 고용 완료!" : $"{name} 고용 완료!");
        }

        private void HandleHireRejected(RosterHireRejectedEvent evt)
        {
            UpdateHint($"골드 부족! ({evt.CurrentGold}/{evt.GoldAmount})");
        }

        private void UpdateHint(string message)
        {
            if (hintText != null) hintText.text = message;
        }
    }
}
