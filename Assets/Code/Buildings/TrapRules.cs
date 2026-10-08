using UnityEngine;

namespace Code.Buildings
{
    /// <summary>함정이 터지는 방식.</summary>
    public enum TrapTriggerMode
    {
        /// <summary>밟은 한 명에게만 피해를 준다.</summary>
        Step,

        /// <summary>밟으면 함정 칸과 그 주변의 적·아군 모두에게 피해를 준다.</summary>
        Area,

        /// <summary>밟으면 잠시 작동하며 그 위에 선 적·아군에게 일정 간격으로 피해를 나눠 준다.</summary>
        Persistent
    }

    /// <summary>
    /// 함정의 칸·범위·지속 피해 계산.
    ///
    /// 예전 함정은 칸 하나에 한 명만 때리고, 방문 한 번에 한 번만 터졌다. 종류가 여덟이어도 피해량만
    /// 달라서 어디에 무엇을 까는지가 판단거리가 되지 못했다. 칸 수·발동 방식·재사용 시간이 다르면
    /// 좁은 길목, 전투가 벌어지는 칸, 아군이 서는 칸을 가려 깔아야 한다.
    /// </summary>
    public static class TrapRules
    {
        /// <summary>지속 함정이 피해를 나눠 주는 간격(게임 초).</summary>
        public const float PersistentTickSeconds = 1f;

        /// <summary>지속 함정의 한 번 피해. 총 피해를 작동 시간 동안의 횟수로 나누고, 최소 1이다.</summary>
        public static int PersistentTickDamage(int totalDamage, float activeSeconds)
        {
            if (totalDamage <= 0)
                return 0;

            var ticks = Mathf.Max(1, Mathf.RoundToInt(activeSeconds / PersistentTickSeconds));
            return Mathf.Max(1, Mathf.CeilToInt(totalDamage / (float)ticks));
        }

        /// <summary>지속 함정이 한 번 작동하는 동안 피해를 주는 횟수.</summary>
        public static int PersistentTickCount(float activeSeconds) =>
            Mathf.Max(1, Mathf.RoundToInt(activeSeconds / PersistentTickSeconds));

        /// <summary>
        /// 그 자리가 함정을 밟은 것인가. 칸 하나짜리는 예전처럼 중심에서 <paramref name="contactRadius"/> 안,
        /// 여러 칸짜리는 덮은 칸들의 중심 사이 사각형에 같은 여유를 더한 범위다.
        /// </summary>
        public static bool CoversFootprint(
            Vector2 center, Vector2Int footprint, float cellSize, float contactRadius, Vector2 point)
        {
            var halfX = (Mathf.Max(1, footprint.x) - 1) * cellSize * 0.5f + contactRadius;
            var halfY = (Mathf.Max(1, footprint.y) - 1) * cellSize * 0.5f + contactRadius;
            var delta = point - center;
            if (footprint.x <= 1 && footprint.y <= 1)
                return delta.sqrMagnitude <= contactRadius * contactRadius;

            return Mathf.Abs(delta.x) <= halfX && Mathf.Abs(delta.y) <= halfY;
        }

        /// <summary>범위 함정이 닿는 자리인가. 함정이 덮은 칸에서 사방으로 <paramref name="radiusCells"/>칸까지다.</summary>
        public static bool InArea(
            Vector2 center, Vector2Int footprint, float cellSize, float radiusCells, Vector2 point)
        {
            var halfX = (Mathf.Max(1, footprint.x) * 0.5f + Mathf.Max(0f, radiusCells)) * cellSize;
            var halfY = (Mathf.Max(1, footprint.y) * 0.5f + Mathf.Max(0f, radiusCells)) * cellSize;
            var delta = point - center;
            return Mathf.Abs(delta.x) <= halfX && Mathf.Abs(delta.y) <= halfY;
        }

        /// <summary>
        /// 마우스가 가리킨 칸을 가운데로 두는 기준 칸(왼쪽 아래). 짝수 칸이면 가리킨 칸이 왼쪽 아래 쪽 가운데가 된다.
        /// </summary>
        public static Vector2Int AnchorForHover(int column, int row, Vector2Int footprint) =>
            new(column - (Mathf.Max(1, footprint.x) - 1) / 2, row - (Mathf.Max(1, footprint.y) - 1) / 2);

        /// <summary>
        /// 손님이 머무는 시설과 함정은 한 방에 함께 두지 않는다. 시설 안에서는 모험가가 공격받지 않는다는
        /// 기획이라, 함정이 있으면 손님이 들어갔다 나오며 맞는 칸이 된다.
        /// </summary>
        public static bool ConflictsWithFacility(BuildingDataSO placing, Building centralBuilding, bool roomHasTrap)
        {
            if (placing == null)
                return false;

            if (placing.Category == InstallCategory.Trap)
                return centralBuilding != null && !centralBuilding.IsDestroyed && centralBuilding.DwellSeconds > 0f;

            return placing.DwellSeconds > 0f && roomHasTrap;
        }
    }
}
