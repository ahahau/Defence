using Code.MapCreateSystem;

namespace Code.Buildings
{
    /// <summary>
    /// 방 하나에 건물·함정·유닛을 지금 놓을 수 있는지와, 못 놓으면 그 이유.
    ///
    /// 설치 창은 막힌 항목도 카드로 보이고 이유를 적는다. 예전에는 막힌 항목이 목록에서 빠져서
    /// 왜 없는지 알 길이 없었다. 판정은 실제 설치 경로(<see cref="BuildingPlacement"/>)와 같아야 한다 —
    /// 여기서 통과시킨 것을 설치가 거절하면 결제 뒤 환불로 끝난다.
    /// </summary>
    public static class InstallAvailabilityRules
    {
        /// <summary>건물·함정을 이 방(라인 건물은 빈 통로)에 놓을 수 없으면 true와 이유를 돌려준다.</summary>
        public static bool TryGetBlockReason(Node node, BuildingDataSO data, bool hasFreeEdge, out string reason)
        {
            reason = string.Empty;
            if (data == null || data.Prefab == null)
            {
                reason = "설치할 수 없는 항목입니다";
                return true;
            }

            if (data.Prefab is Portal)
            {
                reason = "입구는 따로 짓지 않습니다";
                return true;
            }

            // 라인 건물은 방이 아니라 방 사이 통로에 선다. 방 상태와 무관하다.
            if (data.InstallOnEdge)
            {
                if (!hasFreeEdge)
                    reason = "비어 있는 통로가 없습니다";
                return !hasFreeEdge;
            }

            if (node == null)
            {
                reason = "방을 먼저 고르세요";
                return true;
            }

            if (node.IsUnderConstruction)
            {
                reason = "공사 중인 방입니다";
                return true;
            }

            if (!BuildingPlacement.UsesGridCell(data))
            {
                if (node.HasAssignedBuilding)
                {
                    reason = "이미 중앙 시설이 있습니다";
                    return true;
                }

                if (node.IsClearingRubble)
                {
                    reason = "철거 잔해를 치우는 중입니다";
                    return true;
                }
            }
            else
            {
                var grid = node.TrapGrid;
                if (grid == null || !grid.HasRoomFor(data.Prefab.Footprint))
                {
                    reason = $"빈 칸이 모자랍니다 ({data.Prefab.Footprint.x}×{data.Prefab.Footprint.y}칸 필요)";
                    return true;
                }
            }

            // 방 하나는 벌이든 경비든 하나만 한다. 설치 경로도 유닛이 선 방의 건물을 거절한다.
            if (node.AssignedUnitCount > 0 && BuildingPlacement.IsRoomBuilding(data))
            {
                reason = "유닛이 선 방에는 지을 수 없습니다";
                return true;
            }

            if (BuildingPlacement.ViolatesFacilityTrapRule(node, data))
            {
                reason = data.Category == InstallCategory.Trap
                    ? "손님이 머무는 시설이 있는 방입니다"
                    : "함정이 있는 방에는 지을 수 없습니다";
                return true;
            }

            if (BuildingPlacement.ViolatesRoomSizeRule(node, data))
            {
                reason = "소형 방에는 시설을 지을 수 없습니다";
                return true;
            }

            return false;
        }

        /// <summary>이 방에 유닛을 한 명 더 세울 수 없으면 true와 이유를 돌려준다.</summary>
        public static bool TryGetDeployBlockReason(Node node, out string reason)
        {
            reason = string.Empty;
            if (node == null)
                reason = "방을 먼저 고르세요";
            else if (node.IsEnemySpawnNode)
                reason = "침입 입구 방에는 배치할 수 없습니다";
            else if (node.IsUnderConstruction)
                reason = "공사 중인 방입니다";
            else if (node.HasInstalledBuilding)
                reason = "건물이 있는 방에는 배치할 수 없습니다";
            else if (!node.CanAcceptAdditionalUnit || !node.TryGetFirstFreeUnitSlot(out _, out _))
                reason = "방 정원이 가득 찼습니다";

            return !string.IsNullOrEmpty(reason);
        }
    }
}
