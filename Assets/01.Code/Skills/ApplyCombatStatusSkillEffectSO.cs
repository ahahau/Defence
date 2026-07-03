using UnityEngine;
using _01.Code.Combat;

namespace _01.Code.Skills
{
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Apply Combat Status", fileName = "ApplyCombatStatusSkillEffect", order = 0)]
    public class ApplyCombatStatusSkillEffectSO : SkillEffectSO
    {
        [SerializeField] private string statusId = "Vulnerable";
        [SerializeField, Min(0f)] private float duration = 3f;
        [SerializeField, Min(0.05f)] private float moveSpeedMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float damageTakenMultiplier = 1.25f;
        [SerializeField] private bool affectAllEnemies;
        [SerializeField, Min(0f)] private float radius = 3f;

        public override void Execute(SkillContext context)
        {
            if (context.Caster == null) return;

            if (affectAllEnemies)
            {
                foreach (var enemy in context.EnemiesInField())
                    ApplyIfInRange(context, enemy);
                return;
            }

            ApplyIfInRange(context, context.Target);
        }

        private void ApplyIfInRange(SkillContext context, BT.BattleAgent target)
        {
            if (target == null) return;
            if (radius > 0f && Vector2.Distance(context.Caster.transform.position, target.transform.position) > radius)
                return;

            var status = target.GetComponent<CombatStatusController>();
            if (status == null)
            {
                Debug.LogError($"{target.name} is missing required {nameof(CombatStatusController)}.", target);
                return;
            }

            status.Apply(statusId, duration, moveSpeedMultiplier, damageTakenMultiplier);
        }
    }
}
