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
