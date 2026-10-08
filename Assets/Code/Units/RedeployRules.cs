using UnityEngine;

namespace Code.Units
{
    /// <summary>
    /// 재배치 준비 규칙. 옮기기는 즉시 끝나지만 옮긴 방 수만큼 자리 잡는 시간이 붙는다.
    ///
    /// 이 값이 없으면 멈춰 놓고 적이 오는 방마다 경비를 돌려 막는 것이 정답이 된다.
    /// 준비 중에는 공격·스킬을 못 쓰고, 대신 받는 피해는 절반이라 옮기자마자 녹지는 않는다.
    /// </summary>
    public static class RedeployRules
    {
        /// <summary>이동한 방 하나마다 붙는 준비 시간(게임 시간 초).</summary>
        public const float SecondsPerRoom = 1f;

        /// <summary>준비 중 받는 피해 배율.</summary>
        public const float PreparationDamageTakenMultiplier = 0.5f;

        /// <summary>이동한 방 수에 따른 준비 시간. 최소 한 방으로 센다.</summary>
        public static float SecondsFor(int roomsMoved) => Mathf.Max(1, roomsMoved) * SecondsPerRoom;

        /// <summary>준비 중 받는 피해. 들어온 피해가 있으면 최소 1은 받는다.</summary>
        public static int ApplyPreparationDamage(int damage) =>
            damage <= 0 ? 0 : Mathf.Max(1, Mathf.CeilToInt(damage * PreparationDamageTakenMultiplier));
    }
}
