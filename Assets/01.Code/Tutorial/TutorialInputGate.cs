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
            if (node == null)
            {
                Clear();
                return;
            }

            ClearAllowedTargets();
            IsActive = true;
            AllowedLockedNode = node;
        }

        public static void OnlyUnlockedNode(Node node)
        {
            if (node == null)
            {
                Clear();
                return;
            }

            ClearAllowedTargets();
            IsActive = true;
            AllowedUnlockedNode = node;
        }

        public static void OnlyHireUnit(UnitDataSO unit)
        {
            if (unit == null)
            {
                Clear();
                return;
            }

            ClearAllowedTargets();
            IsActive = true;
            AllowedHireUnit = unit;
            AllowHirePanel = true;
        }

        public static void OnlyDeployUnit(Node node, UnitDataSO unit)
        {
            if (node == null || unit == null)
            {
                Clear();
                return;
            }

            ClearAllowedTargets();
            IsActive = true;
            AllowedUnlockedNode = node;
            AllowedDeployUnit = unit;
            AllowedInstallCategory = InstallCategory.Unit;
            AllowInstallMenu = true;
        }

        public static void OnlyInstallPortal(Node node)
        {
            if (node == null)
            {
                Clear();
                return;
            }

            ClearAllowedTargets();
            IsActive = true;
            AllowedUnlockedNode = node;
            AllowedInstallCategory = InstallCategory.Building;
            AllowInstallMenu = true;
        }

        public static void OnlyInstallTrap(Node node, BuildingDataSO building = null)
        {
            if (node == null)
            {
                Clear();
                return;
            }

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
            // "습격 개시" 안내 단계는 플레이어의 준비 시간을 막지 않는다.
            // 포탈을 중앙 슬롯에 설치한 직후에도 다른 노드를 고르고,
            // 유닛·함정을 배치한 뒤 원하는 때에 습격을 시작할 수 있어야 한다.
            return !IsActive
                   || AllowWaveStart
                   || (AllowedUnlockedNode != null && node == AllowedUnlockedNode);
        }

        public static bool AllowsHirePanel()
        {
            // 포탈 설치 뒤 "습격 개시" 안내 단계에서도 전투 준비(고용·배치)를
            // 제한하면 플레이어가 아무 행동도 할 수 없다.
            return !IsActive || AllowWaveStart || AllowHirePanel;
        }

        public static bool AllowsHireUnit(UnitDataSO unit)
        {
            return !IsActive
                   || AllowWaveStart
                   || (AllowedHireUnit != null && unit == AllowedHireUnit);
        }

        public static bool AllowsInstallMenu()
        {
            return !IsActive || AllowWaveStart || AllowInstallMenu;
        }

        public static bool AllowsInstallCategory(InstallCategory category)
        {
            return !IsActive
                   || AllowWaveStart
                   || !AllowedInstallCategory.HasValue
                   || category == AllowedInstallCategory.Value;
        }

        public static bool AllowsRosterDeployUnit(UnitDataSO unit)
        {
            return !IsActive
                   || AllowWaveStart
                   || (AllowedDeployUnit != null && unit == AllowedDeployUnit);
        }

        public static bool AllowsBuildingInstall(BuildingDataSO building)
        {
            if (!IsActive || AllowWaveStart)
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
