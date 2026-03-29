using _01.Code.Core;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Events
{
    public static class UIEvents
    {
        public static readonly ShowDamageTextRequestedEvent ShowDamageTextRequestedEvent = new ShowDamageTextRequestedEvent();
        public static readonly UiClockStateChangedEvent UiClockStateChangedEvent = new UiClockStateChangedEvent();
        public static readonly UiDefaultCostBarStateChangedEvent UiDefaultCostBarStateChangedEvent = new UiDefaultCostBarStateChangedEvent();
        public static readonly UiResourceGridStateChangedEvent UiResourceGridStateChangedEvent = new UiResourceGridStateChangedEvent();
        public static readonly UiUnitInventoryStateChangedEvent UiUnitInventoryStateChangedEvent = new UiUnitInventoryStateChangedEvent();
        public static readonly UiInventoryPageChangedEvent UiInventoryPageChangedEvent = new UiInventoryPageChangedEvent();
        public static readonly UiSkipDayRequestedEvent UiSkipDayRequestedEvent = new UiSkipDayRequestedEvent();
        public static readonly UiInventoryPageRequestedEvent UiInventoryPageRequestedEvent = new UiInventoryPageRequestedEvent();
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
        public _01.Code.Manager.CostDefinitionSO Definition { get; private set; }
        public int Current { get; private set; }
        public int Max { get; private set; }

        public UiCostValueEntry Initialize(_01.Code.Manager.CostDefinitionSO definition, int current, int max)
        {
            Definition = definition;
            Current = current;
            Max = max;
            return this;
        }
    }

    public class UiDefaultCostBarStateChangedEvent : GameEvent
    {
        public IReadOnlyList<UiCostValueEntry> Costs { get; private set; }

        public UiDefaultCostBarStateChangedEvent Initializer(IReadOnlyList<UiCostValueEntry> costs)
        {
            Costs = costs;
            return this;
        }
    }

    public class UiResourceStackEntry
    {
        public _01.Code.Manager.CostDefinitionSO Definition { get; private set; }
        public int StackAmount { get; private set; }

        public UiResourceStackEntry Initialize(_01.Code.Manager.CostDefinitionSO definition, int stackAmount)
        {
            Definition = definition;
            StackAmount = stackAmount;
            return this;
        }
    }

    public class UiResourceGridStateChangedEvent : GameEvent
    {
        public IReadOnlyList<UiResourceStackEntry> Stacks { get; private set; }

        public UiResourceGridStateChangedEvent Initializer(IReadOnlyList<UiResourceStackEntry> stacks)
        {
            Stacks = stacks;
            return this;
        }
    }

    public class UiUnitInventoryStateChangedEvent : GameEvent
    {
        public IReadOnlyList<_01.Code.Unit.UnitDataSO> Units { get; private set; }
        public _01.Code.Unit.UnitDataSO SelectedUnit { get; private set; }
        public bool CanUseDayActions { get; private set; }
        public int CurrentPrimaryCost { get; private set; }

        public UiUnitInventoryStateChangedEvent Initializer(
            IReadOnlyList<_01.Code.Unit.UnitDataSO> units,
            _01.Code.Unit.UnitDataSO selectedUnit,
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

    public class UiInventoryPageChangedEvent : GameEvent
    {
        public bool ShowResources { get; private set; }

        public UiInventoryPageChangedEvent Initializer(bool showResources)
        {
            ShowResources = showResources;
            return this;
        }
    }

    public class UiSkipDayRequestedEvent : GameEvent { }

    public class UiInventoryPageRequestedEvent : GameEvent
    {
        public bool ShowResources { get; private set; }

        public UiInventoryPageRequestedEvent Initializer(bool showResources)
        {
            ShowResources = showResources;
            return this;
        }
    }

    public class UiUnitSlotRequestedEvent : GameEvent
    {
        public _01.Code.Unit.UnitDataSO UnitData { get; private set; }

        public UiUnitSlotRequestedEvent Initializer(_01.Code.Unit.UnitDataSO unitData)
        {
            UnitData = unitData;
            return this;
        }
    }
}
