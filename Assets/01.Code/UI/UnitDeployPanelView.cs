using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
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
        [SerializeField, Min(1)] private int gridColumns = 8;
        [SerializeField, Min(120f)] private float minCardWidth = 180f;
        [SerializeField, Min(120f)] private float maxCardWidth = 240f;
        [SerializeField, Min(1f)] private float cardHeightRatio = 2f;

        [Header("Data")]
        [SerializeField] private UnitDataSO[] deployableUnits;

        [Header("Event Channels")]
        [SerializeField] private GameEventChannelSO costEventChannel;

        private readonly List<UnitDataSO> _unlockedUnits = new();
        private readonly List<UnitDeployEntryView> _entries = new();

        private void Awake()
        {
            panelRoot.SetActive(false);
            InitUnlockedUnitsFromData();
            RefreshHireEntries();
        }

        private void OnEnable()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.AddListener(HandleClose);
            costEventChannel.AddListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.AddListener<RosterHireRejectedEvent>(HandleHireRejected);
            costEventChannel.AddListener<UnitUnlockChangedEvent>(HandleUnitUnlockChanged);
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(HandleToggle);
            if (closeButton != null)  closeButton.onClick.RemoveListener(HandleClose);
            costEventChannel.RemoveListener<RosterHirePaidEvent>(HandleHirePaid);
            costEventChannel.RemoveListener<RosterHireRejectedEvent>(HandleHireRejected);
            costEventChannel.RemoveListener<UnitUnlockChangedEvent>(HandleUnitUnlockChanged);
        }

        private void InitUnlockedUnitsFromData()
        {
            _unlockedUnits.Clear();

            if (deployableUnits == null)
                return;

            foreach (var unit in deployableUnits)
            {
                if (unit != null && !unit.Locked && !_unlockedUnits.Contains(unit))
                    _unlockedUnits.Add(unit);
            }
        }

        private void RefreshHireEntries()
        {
            foreach (var e in _entries)
                if (e != null) Destroy(e.gameObject);
            _entries.Clear();

            if (entryPrefab == null || contentRoot == null) return;

            foreach (var unit in _unlockedUnits)
            {
                var entry = Instantiate(entryPrefab, contentRoot);
                entry.Initialize(unit, HandleHireRequested);
                _entries.Add(entry);
            }

            ConfigureHireGrid();
            ScrollViewContentSizer.ResizeToGridItemCount(contentRoot, _entries.Count);
            UpdateHint(_unlockedUnits.Count == 0 ? "해금된 유닛 없음" : string.Empty);
        }

        private void HandleToggle()
        {
            var shouldShow = !panelRoot.activeSelf;
            if (shouldShow)
                transform.SetAsLastSibling();

            panelRoot.SetActive(shouldShow);

            if (shouldShow)
            {
                ConfigureHireGrid();
                ScrollViewContentSizer.ResizeToGridItemCount(contentRoot, _entries.Count);
            }
        }

        private void HandleClose()
        {
            panelRoot.SetActive(false);
        }

        private void HandleUnitUnlockChanged(UnitUnlockChangedEvent evt)
        {
            _unlockedUnits.Clear();

            if (evt.UnlockedUnits != null)
            {
                foreach (var unit in evt.UnlockedUnits)
                {
                    if (unit != null && !_unlockedUnits.Contains(unit))
                        _unlockedUnits.Add(unit);
                }
            }

            RefreshHireEntries();
        }

        private void HandleHireRequested(UnitDataSO unit)
        {
            costEventChannel.RaiseEvent(new RosterHireRequestedEvent(unit, unit.Cost));
        }

        private void HandleHirePaid(RosterHirePaidEvent evt)
        {
            RefreshHireEntries();
            var name = !string.IsNullOrWhiteSpace(evt.Unit.Name) ? evt.Unit.Name : evt.Unit.name;
            UpdateHint($"{name} 고용 완료!");
        }

        private void HandleHireRejected(RosterHireRejectedEvent evt)
        {
            UpdateHint($"골드 부족! ({evt.CurrentGold}/{evt.GoldAmount})");
        }

        private void UpdateHint(string message)
        {
            if (hintText != null) hintText.text = message;
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

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, gridColumns);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            var viewportWidth = ResolveViewportWidth(contentRect);
            var columns = Mathf.Max(1, grid.constraintCount);
            var availableWidth = viewportWidth
                                 - grid.padding.left
                                 - grid.padding.right
                                 - Mathf.Max(0, columns - 1) * grid.spacing.x;
            var cardWidth = Mathf.Clamp(Mathf.Floor(availableWidth / columns), minCardWidth, maxCardWidth);
            grid.cellSize = new Vector2(cardWidth, Mathf.Round(cardWidth * cardHeightRatio));
        }

        private static float ResolveViewportWidth(RectTransform contentRect)
        {
            if (contentRect.parent is RectTransform viewport)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                if (viewport.rect.width > 1f)
                    return viewport.rect.width;
            }

            return 2040f;
        }
    }
}
