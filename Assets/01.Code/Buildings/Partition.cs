using Code.MapCreateSystem;

namespace Code.Buildings
{
    /// <summary>
    /// 칸막이 벽: 방 칸 하나를 막아 모험가의 동선을 꺾는다. 그림은 방 타일(기둥 룰 타일)이 그리고, 이 건물은 칸을 차지하기만 한다.
    ///
    /// 방을 두 동강 내거나 문 앞을 막는 자리에는 놓을 수 없다(<see cref="NodeTrapGrid.KeepsRoomOpen"/>).
    /// 부서지는 건물이라 크게 돌아가야 하는 모험가는 돌아가지 않고 때려 부순다(<see cref="PartitionRules"/>).
    /// </summary>
    public class Partition : Building
    {
        protected override void BreakBuilding()
        {
            if (IsDestroyed)
                return;

            base.BreakBuilding();

            // 부서진 칸은 바로 바닥이 된다. 오브젝트가 사라지기를 기다리면 그 사이 길찾기가 벽으로 본다.
            var grid = GetComponentInParent<NodeTrapGrid>();
            if (grid != null)
                grid.Release(this);
        }
    }
}
