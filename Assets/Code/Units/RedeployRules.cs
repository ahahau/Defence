using UnityEngine;

namespace Code.Units
{
    /// <summary>
    /// 재배치 준비 규칙. 옮기기는 즉시 끝나지만 옮긴 방 수만큼 자리 잡는 시간이 붙는다.
    ///
    /// 이 값이 없으면 멈춰 놓고 적이 오는 방마다 경비를 돌려 막는 것이 정답이 된다.
    /// 준비 중에는 공격·스킬을 못 쓰고, 대신 받는 피해는 절반이라 옮기자마자 녹지는 않는다.
    /// 특성마다 추가 반응이 하나씩 있다(기획서: 추가 반응은 고유 능력이 정한다) — 누구를 옮길지가 선택이 된다.
    /// </summary>
    public static class RedeployRules
    {
        /// <summary>이동한 방 하나마다 붙는 준비 시간(게임 시간 초).</summary>
        public const float SecondsPerRoom = 1f;

        /// <summary>준비 중 받는 피해 배율.</summary>
        public const float PreparationDamageTakenMultiplier = 0.5f;

        /// <summary>강인함: 준비 시간 배율.</summary>
        public const float TirelessSecondsMultiplier = 0.5f;

        /// <summary>신중함: 준비 시간 배율. 대신 준비 중에는 피해를 받지 않는다.</summary>
        public const float CautiousSecondsMultiplier = 1.5f;

        /// <summary>수호자: 준비 중 받는 피해 배율.</summary>
        public const float GuardianDamageTakenMultiplier = 0.25f;

        /// <summary>공격적: 준비가 끝난 뒤 첫 평타 피해 배율.</summary>
        public const float AggressiveFirstStrikeMultiplier = 1.5f;

        /// <summary>야전 의무병: 도착한 방 아군이 회복하는 최대 체력 비율.</summary>
        public const float FieldMedicHealRatio = 0.1f;

        /// <summary>이동한 방 수에 따른 준비 시간. 최소 한 방으로 센다.</summary>
        public static float SecondsFor(int roomsMoved) => Mathf.Max(1, roomsMoved) * SecondsPerRoom;

        /// <summary>특성까지 반영한 준비 시간.</summary>
        public static float SecondsFor(int roomsMoved, UnitTrait trait) => SecondsFor(roomsMoved) * trait switch
        {
            UnitTrait.Tireless => TirelessSecondsMultiplier,
            UnitTrait.Cautious => CautiousSecondsMultiplier,
            _ => 1f
        };

        /// <summary>준비 중 받는 피해. 들어온 피해가 있으면 최소 1은 받는다.</summary>
        public static int ApplyPreparationDamage(int damage) =>
            damage <= 0 ? 0 : Mathf.Max(1, Mathf.CeilToInt(damage * PreparationDamageTakenMultiplier));

        /// <summary>특성까지 반영한 준비 중 피해. 신중함은 0, 수호자는 25%(최소 1).</summary>
        public static int ApplyPreparationDamage(int damage, UnitTrait trait)
        {
            if (damage <= 0)
                return 0;

            return trait switch
            {
                UnitTrait.Cautious => 0,
                UnitTrait.Guardian => Mathf.Max(1, Mathf.CeilToInt(damage * GuardianDamageTakenMultiplier)),
                _ => ApplyPreparationDamage(damage)
            };
        }

        /// <summary>공격적인 부하의 준비 뒤 첫 평타.</summary>
        public static int ApplyFirstStrike(int damage) =>
            damage <= 0 ? damage : Mathf.RoundToInt(damage * AggressiveFirstStrikeMultiplier);

        /// <summary>야전 의무병이 도착한 방 아군 한 명에게 주는 회복량(최소 1).</summary>
        public static int FieldMedicHeal(int maxHealth) => Mathf.Max(1, Mathf.RoundToInt(maxHealth * FieldMedicHealRatio));

        public static string DescribeReaction(UnitTrait trait) => trait switch
        {
            UnitTrait.Tireless => "준비 시간 절반",
            UnitTrait.Cautious => "준비 시간 1.5배 · 준비 중 피해 없음",
            UnitTrait.Guardian => "준비 중 피해 75% 감소",
            UnitTrait.Aggressive => "준비 뒤 첫 공격 +50%",
            UnitTrait.FieldMedic => "도착한 방 아군 체력 10% 회복",
            _ => string.Empty
        };
    }
}
