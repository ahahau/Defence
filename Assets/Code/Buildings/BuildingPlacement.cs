using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Buildings
{
    /// <summary>
    /// 건물이 노드의 어디에 놓이는지 판정하고 실제로 생성하는 공통 절차.
    /// 플레이어가 설치할 때(노드 패널)와 세이브를 불러올 때가 같은 경로를 타도록 한곳에 모았다.
    /// 이벤트 발행은 호출한 쪽 몫이다. 여기서는 오브젝트만 만든다.
    /// </summary>
    public static class BuildingPlacement
    {
        /// <summary>
        /// 건물이 중앙 슬롯을 채우는 비율. 슬롯을 넘지 않게 줄이는 값이지 여백을 두려는 값이 아니다.
        /// 0.92 는 방 안에서 건물이 작아 보였다 — 그림 자체에 이미 여백이 있어 두 번 줄어든 셈이다.
        /// </summary>
        public const float DefaultCentralSlotFill = 1.12f;

        /// <summary>중앙 슬롯 바깥의 작은 칸에 여러 개 놓이는 건물인가.</summary>
        public static bool UsesGridCell(BuildingDataSO buildingData)
        {
            return buildingData != null
                   && !buildingData.Unique
                   && !buildingData.CentralOnly
                   && !buildingData.InstallOnEdge;
        }

        /// <summary>노드당 하나만 놓이는 중앙 슬롯 건물인가.</summary>
        public static bool IsCentralBuilding(BuildingDataSO buildingData)
        {
            return buildingData != null
                   && buildingData.Category == InstallCategory.Building
                   && !UsesGridCell(buildingData);
        }

        /// <summary>
        /// 유닛과 자리를 다투는 건물인가. 함정은 아니고(같이 둘 수 있다), 금고도 아니다
        /// (지켜야 할 곳이라 유닛을 세울 수 있어야 한다).
        /// </summary>
        public static bool IsRoomBuilding(BuildingDataSO buildingData) =>
            buildingData != null && buildingData.Category == InstallCategory.Building
            && buildingData.Prefab is not Treasury;

        /// <summary>중앙 슬롯에 건물을 세우고 노드에 등록한다. 이미 차 있으면 null.</summary>
        public static Building InstallCentral(
            Node node,
            BuildingDataSO buildingData,
            float centralSlotFill = DefaultCentralSlotFill)
        {
            if (node == null || node.HasAssignedBuilding)
                return null;

            // 유닛이 선 방은 경비 몫이다. 같은 규칙을 반대편에서도 지켜야 방 하나가
            // 벌이와 경비를 겸하지 못한다 — 한쪽만 막으면 순서만 바꿔 둘 다 놓을 수 있다.
            if (node.HasAssignedUnit && IsRoomBuilding(buildingData))
                return null;

            var building = CreateCentral(node, buildingData, centralSlotFill);
            if (building == null)
                return null;

            building.Initialize(buildingData);
            // Initialize가 등급별 BoardSprite를 교체하므로 최종 그림 기준으로 중앙 크기를 다시 맞춘다.
            if (building is not Portal && IsCentralBuilding(buildingData))
                FitToCentralSlot(building, node.TrapGrid, centralSlotFill);
            node.AssignBuilding(building);

            if (building is Portal portal)
                portal.Initialize(node);

            return building;
        }

        /// <summary>지정한 칸에 건물을 세운다. 칸이 차 있으면 null.</summary>
        public static Building InstallOnCell(Node node, int column, int row, BuildingDataSO buildingData)
        {
            var grid = node != null ? node.TrapGrid : null;
            if (grid == null || buildingData == null || buildingData.Prefab == null)
                return null;

            // 금고처럼 칸에 서는 건물도 같은 규칙을 지킨다. 함정만 유닛과 자리를 나눠 쓴다.
            if (node.HasAssignedUnit && IsRoomBuilding(buildingData))
                return null;

            var building = grid.TryPlace(column, row, buildingData.Prefab);
            if (building == null)
                return null;

            building.Initialize(buildingData);
            return building;
        }

        /// <summary>노드 사이 라인에 건물을 세운다. 이미 차 있으면 null.</summary>
        public static Building InstallOnEdge(EdgeLine edge, BuildingDataSO buildingData)
        {
            if (edge == null || buildingData == null || buildingData.Prefab == null)
                return null;

            var building = edge.TryInstall(buildingData.Prefab);
            building?.Initialize(buildingData);
            return building;
        }

        /// <summary>중앙 슬롯 위치에 프리팹만 생성한다(등록·초기화는 하지 않는다).</summary>
        public static Building CreateCentral(
            Node node,
            BuildingDataSO buildingData,
            float centralSlotFill = DefaultCentralSlotFill)
        {
            if (node == null || buildingData == null || buildingData.Prefab == null)
                return null;

            var grid = node.TrapGrid;
            var useCentralSlot = IsCentralBuilding(buildingData) && grid != null;
            var spawnPosition = useCentralSlot
                ? grid.CentralBuildingWorldPosition()
                : node.transform.position;

            var building = Object.Instantiate(buildingData.Prefab, spawnPosition, Quaternion.identity);
            building.transform.SetParent(node.transform, true);

            // 포탈은 자체 연출 크기를 유지해야 해서 슬롯에 맞추지 않는다.
            if (useCentralSlot && building is not Portal)
                FitToCentralSlot(building, grid, centralSlotFill);

            return building;
        }

        /// <summary>건물 스프라이트가 중앙 슬롯을 넘지 않도록 균일 축소한다.</summary>
        public static void FitToCentralSlot(Building building, NodeTrapGrid grid, float centralSlotFill)
        {
            if (building == null || grid == null)
                return;

            if (!TryGetVisualBounds(building, out var visualBounds))
                return;

            var slotSize = grid.CentralBuildingSlotWorldSize * centralSlotFill;
            var scaleFactor = Mathf.Min(
                slotSize.x / visualBounds.size.x,
                slotSize.y / visualBounds.size.y);
            if (scaleFactor <= 0f || float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor))
                return;

            building.transform.localScale *= scaleFactor;
        }

        private static bool TryGetVisualBounds(Building building, out Bounds bounds)
        {
            bounds = new Bounds();
            var renderers = building.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
                return false;

            var hasBounds = false;
            foreach (var spriteRenderer in renderers)
            {
                if (spriteRenderer == null || spriteRenderer.sprite == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = spriteRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(spriteRenderer.bounds);
                }
            }

            return hasBounds
                   && bounds.size.x > Mathf.Epsilon
                   && bounds.size.y > Mathf.Epsilon;
        }
    }
}
