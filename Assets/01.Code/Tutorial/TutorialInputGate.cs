using _01.Code.Buildings;
using _01.Code.MapCreateSystem;
using _01.Code.UI;
using _01.Code.Units;

namespace _01.Code.Tutorial
{
    public static class TutorialInputGate
    {
        public static bool IsActive { get; private set; }
        public static Node AllowedLockedNode { get; private set; }
        public static Node AllowedUnlockedNode { get; private set; }
        public static UnitDataSO AllowedHireUnit { get; private set; }
        public static UnitDataSO AllowedDeployUnit { get; private set; }
        public static BuildingDataSO AllowedBuilding { get; private set; }
        public static InstallCategory? AllowedInstallCategory { get; private set; }
        public static bool AllowHirePanel { get; private set; }
        public static bool AllowInstallMenu { get; private set; }
        public static bool AllowPolicyChoice { get; private set; }
        public static bool AllowWaveStart { get; private set; }
        public static UnlockRewardKind? ForcedUnlockRewardKind { get; private set; }

        public static void Clear()
        {
            IsActive = false;
            AllowedLockedNode = null;
            AllowedUnlockedNode = null;
            AllowedHireUnit = null;
            AllowedDeployUnit = null;
            AllowedBuilding = null;
            AllowedInstallCategory = null;
            AllowHirePanel = false;
            AllowInstallMenu = false;
            AllowPolicyChoice = false;
            AllowWaveStart = false;
            ForcedUnlockRewardKind = null;
        }

        public static void OnlyLockedNode(Node node)
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowedLockedNode = node;
        }

        public static void OnlyUnlockedNode(Node node)
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowedUnlockedNode = node;
        }

        public static void OnlyHireUnit(UnitDataSO unit)
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowedHireUnit = unit;
            AllowHirePanel = true;
        }

        public static void OnlyDeployUnit(Node node, UnitDataSO unit)
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowedUnlockedNode = node;
            AllowedDeployUnit = unit;
            AllowedInstallCategory = InstallCategory.Unit;
            AllowInstallMenu = true;
        }

        public static void OnlyInstallPortal(Node node)
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowedUnlockedNode = node;
            AllowedInstallCategory = InstallCategory.Building;
            AllowInstallMenu = true;
        }

        public static void OnlyInstallTrap(Node node, BuildingDataSO building = null)
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowedUnlockedNode = node;
            AllowedBuilding = building;
            AllowedInstallCategory = InstallCategory.Trap;
            AllowInstallMenu = true;
        }

        public static void OnlyWaveStart()
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowWaveStart = true;
        }

        public static void OnlyUnlockReward(UnlockRewardKind rewardKind)
        {
            ClearAllowedTargets();
            IsActive = true;
            ForcedUnlockRewardKind = rewardKind;
        }

        public static void OnlyPolicyChoice()
        {
            ClearAllowedTargets();
            IsActive = true;
            AllowPolicyChoice = true;
        }

        public static bool AllowsLockedNode(Node node)
        {
            return !IsActive || (AllowedLockedNode != null && node == AllowedLockedNode);
        }

        public static bool AllowsUnlockedNode(Node node)
        {
            return !IsActive || (AllowedUnlockedNode != null && node == AllowedUnlockedNode);
        }

        public static bool AllowsHirePanel()
        {
            return !IsActive || AllowHirePanel;
        }

        public static bool AllowsHireUnit(UnitDataSO unit)
        {
            return !IsActive || (AllowedHireUnit != null && unit == AllowedHireUnit);
        }

        public static bool AllowsInstallMenu()
        {
            return !IsActive || AllowInstallMenu;
        }

        public static bool AllowsInstallCategory(InstallCategory category)
        {
            return !IsActive || !AllowedInstallCategory.HasValue || category == AllowedInstallCategory.Value;
        }

        public static bool AllowsRosterDeployUnit(UnitDataSO unit)
        {
            return !IsActive || (AllowedDeployUnit != null && unit == AllowedDeployUnit);
        }

        public static bool AllowsBuildingInstall(BuildingDataSO building)
        {
            if (!IsActive)
                return true;

            if (!AllowedInstallCategory.HasValue)
                return false;

            if (building == null || building.Category != AllowedInstallCategory.Value)
                return false;

            if (AllowedBuilding != null && building != AllowedBuilding)
                return false;

            if (AllowedInstallCategory.Value == InstallCategory.Building)
                return building.Prefab is Portal;

            return true;
        }

        public static bool AllowsWaveStartClick()
        {
            return !IsActive || AllowWaveStart;
        }

        public static bool AllowsUnlockReward(UnlockRewardKind rewardKind)
        {
            return !IsActive || (ForcedUnlockRewardKind.HasValue && rewardKind == ForcedUnlockRewardKind.Value);
        }

        public static bool AllowsPolicyChoice(int index)
        {
            return !IsActive || (AllowPolicyChoice && index == 0);
        }

        public static bool AllowsPolicyPanelClose()
        {
            return !IsActive || !AllowPolicyChoice;
        }

        private static void ClearAllowedTargets()
        {
            AllowedLockedNode = null;
            AllowedUnlockedNode = null;
            AllowedHireUnit = null;
            AllowedDeployUnit = null;
            AllowedBuilding = null;
            AllowedInstallCategory = null;
            AllowHirePanel = false;
            AllowInstallMenu = false;
            AllowPolicyChoice = false;
            AllowWaveStart = false;
            ForcedUnlockRewardKind = null;
        }
    }
}
