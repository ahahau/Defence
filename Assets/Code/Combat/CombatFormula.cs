using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 한 대의 피해가 얼마인지 정하는 규칙.
    ///
    /// 상태도 의존성도 없는 순수 계산이라 Combatant(코루틴·연출·프리팹 배선)에서 떼어 두었다.
    /// 수치를 고치거나 검증할 때 MonoBehaviour와 씬을 세울 필요가 없다.
    /// </summary>
    public static class CombatFormula
    {
        /// <summary>
        /// 방어의 체감 기준점. 방어가 이 값과 같아지면 피해가 정확히 절반으로 줄고,
        /// 그 위로는 완만해져 100%에 닿지 않는다.
        ///
        /// 예전 값은 100이었다. 그런데 실제로 쓰이는 방어값은 적 10~14, 유닛 20~35라
        /// 곡선의 거의 평평한 밑동(9~26%)만 쓰고 있었고, 피해가 1~8의 정수라
        /// 그 몫이 반올림에 통째로 먹혔다. 감소가 실질적으로 0 아니면 1로만 튀었다:
        ///   방패병(방어 14)이 석궁수의 5를 맞으면 4, 창병의 3을 맞으면 3 — 무감소
        ///   수호자가 방어 20이면 잡몹의 2를 그대로 2로 맞고, 35가 되는 순간 1로 절반
        /// 방어를 올려도 아무 일이 없다가 어느 지점에서 갑자기 절반이 되는 셈이었다.
        ///
        /// 30으로 낮춰 authored 방어값이 곡선의 의미 있는 구간에 들어오게 했다.
        /// 유닛 쪽 체감은 그대로 두려고 특성·명령 보정도 같은 비율(x0.3)로 줄였다 —
        /// 20/(20+100)과 6/(6+30)은 정확히 같은 16.7%다. 적의 방어값은 데이터 그대로
        /// 두었으므로, 이 변경으로 실제로 달라지는 것은 "방패를 든 적이 단단해진다" 하나다.
        /// </summary>
        public const float DefenseHalvingPoint = 30f;

        /// <summary>방어가 깎아 내는 피해 비율. 0 이상 1 미만이다.</summary>
        public static float GetDefenseReduction(int defense) =>
            defense <= 0 ? 0f : defense / (defense + DefenseHalvingPoint);

        /// <summary>방어를 통과한 뒤 실제로 들어가는 피해. 아무리 단단해도 1은 남는다.</summary>
        public static int ApplyDefense(int damage, int defense)
        {
            if (defense <= 0)
                return Mathf.Max(1, damage);

            var reducedDamage = damage - damage * (defense / (defense + DefenseHalvingPoint));
            return Mathf.Max(1, Mathf.RoundToInt(reducedDamage));
        }

        /// <summary>치명타 배율을 얹은 원피해. 방어를 계산하기 전에 적용한다.</summary>
        public static int ApplyCritical(int damage, float criticalDamageMultiplier) =>
            Mathf.RoundToInt(damage * Mathf.Max(1f, criticalDamageMultiplier));
    }
}
