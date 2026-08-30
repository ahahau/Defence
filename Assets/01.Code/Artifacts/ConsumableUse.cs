using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Artifacts
{
    /// <summary>
    /// 물약처럼 사서 그 자리에서 쓰는 물건의 처리.
    ///
    /// 주인공은 대기 회복의 대상이 아니다 — 명부에 선 부하만 쉬면서 낫는다.
    /// 그래서 판이 길어질수록 주인공의 체력만 한 방향으로 깎여 내려가고,
    /// 부하가 멀쩡해도 주인공이 쓰러지면 판이 끝났다. 물약은 그 시계를 되돌리는 수단이다.
    /// </summary>
    public static class ConsumableUse
    {
        /// <summary>산 물약을 바로 쓴다. 회복한 유닛 수를 돌려준다.</summary>
        public static int Consume(ArtifactDataSO consumable)
        {
            if (consumable == null || !consumable.IsConsumable || consumable.HealRatio <= 0f)
                return 0;

            var healed = 0;
            foreach (var unit in Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (unit == null || !consumable.AppliesTo(unit))
                    continue;

                var health = unit.Health;
                if (health == null)
                    continue;

                // 쓰러진 유닛은 물약으로 일으키지 않는다. 부활은 명시적인 회복 수단만 할 수 있다.
                if (!health.IsAlive)
                    continue;

                var amount = Mathf.CeilToInt(health.MaxHealth * consumable.HealRatio);
                if (amount <= 0 || health.CurrentHealth >= health.MaxHealth)
                    continue;

                health.Heal(amount);
                healed++;
            }

            return healed;
        }
    }
}
