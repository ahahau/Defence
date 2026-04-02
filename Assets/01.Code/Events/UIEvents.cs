using _01.Code.Core;
using System.Collections.Generic;
using _01.Code.Cost;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Events
{
    public static class UIEvents
    {
        public static readonly ShowDamageTextRequestedEvent ShowDamageTextRequestedEvent = new ShowDamageTextRequestedEvent();
        public static readonly UiClockStateChangedEvent UiClockStateChangedEvent = new UiClockStateChangedEvent();
        public static readonly UiDefaultCostBarStateChangedEvent UiDefaultCostBarStateChangedEvent = new UiDefaultCostBarStateChangedEvent();
        public static readonly UiUnitInventoryStateChangedEvent UiUnitInventoryStateChangedEvent = new UiUnitInventoryStateChangedEvent();
        public static readonly UiSkipDayRequestedEvent UiSkipDayRequestedEvent = new UiSkipDayRequestedEvent();
        public static readonly UiUnitSlotRequestedEvent UiUnitSlotRequestedEvent = new UiUnitSlotRequestedEvent();
        public static readonly UiBuildAtWorldPositionRequestedEvent UiBuildAtWorldPositionRequestedEvent = new UiBuildAtWorldPositionRequestedEvent();
        public static readonly UiCancelSelectionRequestedEvent UiCancelSelectionRequestedEvent = new UiCancelSelectionRequestedEvent();
        public static readonly UiHoverCellChangedEvent UiHoverCellChangedEvent = new UiHoverCellChangedEvent();
        public static readonly UiRefreshRequestedEvent UiRefreshRequestedEvent = new UiRefreshRequestedEvent();
        public static readonly UiUnitCatalogQueryEvent UiUnitCatalogQueryEvent = new UiUnitCatalogQueryEvent();
        public static readonly UiClockStateQueryEvent UiClockStateQueryEvent = new UiClockStateQueryEvent();
    }

    public class ShowDamageTextRequestedEvent : GameEvent
    {
        public Vector3 WorldPosition { get; private set; }
        public float Damage { get; private set; }
        public Transform FollowTarget { get; private set; }

        public ShowDamageTextRequestedEvent Initializer(Vector3 worldPosition, float damage, Transform followTarget = null)
        {
            WorldPosition = worldPosition;
            Damage = damage;
            FollowTarget = followTarget;
            return this;
        }
    }

    public class LeftUpperPanelChange : GameEvent
    {
        public int PanelIndex{get; private set;}

        public LeftUpperPanelChange Initializer(int panelIndex)
        {
            PanelIndex = panelIndex;
            return this;
        }
    }

    public class UiClockStateChangedEvent : GameEvent
    {
        public int Day { get; private set; }
        public bool IsDay { get; private set; }

        public UiClockStateChangedEvent Initializer(int day, bool isDay)
        {
            Day = day;
            IsDay = isDay;
            return this;
        }
    }

    public class UiCostValueEntry
    {
        public CostDefinitionSO Definition { get; private set; }
        public int Current { get; private set; }
        public int Max { get; private set; }

        public UiCostValueEntry Initialize(CostDefinitionSO definition, int current, int max)
        {
            Definition = definition;
            Current = current;
            Max = max;
            return this;
        }
    }

    public class UiDefaultCostBarStateChangedEvent : GameEvent
    {
        public List<UiCostValueEntry> Costs { get; private set; }

        public UiDefaultCostBarStateChangedEvent Initializer(List<UiCostValueEntry> costs)
        {
            Costs = costs;
            return this;
        }
    }

    public class UiUnitInventoryStateChangedEvent : GameEvent
    {
        public List<UnitDataSO> Units { get; private set; }
        public UnitDataSO SelectedUnit { get; private set; }
        public bool CanUseDayActions { get; private set; }
        public int CurrentPrimaryCost { get; private set; }

        public UiUnitInventoryStateChangedEvent Initializer(
            List<UnitDataSO> units,
            UnitDataSO selectedUnit,
            bool canUseDayActions,
            int currentPrimaryCost)
        {
            Units = units;
            SelectedUnit = selectedUnit;
            CanUseDayActions = canUseDayActions;
            CurrentPrimaryCost = currentPrimaryCost;
            return this;
        }
    }

    public class UiSkipDayRequestedEvent : GameEvent { }

    public class UiBuildAtWorldPositionRequestedEvent : GameEvent
    {
        public Vector3 WorldPosition { get; private set; }
        public bool Succeeded { get; set; }

        public UiBuildAtWorldPositionRequestedEvent Initializer(Vector3 worldPosition)
        {
            WorldPosition = worldPosition;
            Succeeded = false;
            return this;
        }
    }

    public class UiCancelSelectionRequestedEvent : GameEvent { }

    public class UiHoverCellChangedEvent : GameEvent
    {
        public Vector2Int CellPosition { get; private set; }

        public UiHoverCellChangedEvent Initializer(Vector2Int cellPosition)
        {
            CellPosition = cellPosition;
            return this;
        }
    }

    public class UiRefreshRequestedEvent : GameEvent { }

    public class UiUnitCatalogQueryEvent : GameEvent
    {
        public List<UnitDataSO> Units { get; set; }

        public UiUnitCatalogQueryEvent Initializer()
        {
            Units = null;
            return this;
        }
    }

    public class UiClockStateQueryEvent : GameEvent
    {
        public int Day { get; set; }
        public bool IsDay { get; set; }

        public UiClockStateQueryEvent Initializer()
        {
            Day = 1;
            IsDay = true;
            return this;
        }
    }

    public class UiUnitSlotRequestedEvent : GameEvent
    {
        public UnitDataSO UnitData { get; private set; }

        public UiUnitSlotRequestedEvent Initializer(UnitDataSO unitData)
        {
            UnitData = unitData;
            return this;
        }
    }
}
