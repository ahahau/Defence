using _01.Code.StatusEffects;
using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>권능이 노드의 어느 편을 겨냥하는가.</summary>
    public enum DungeonPowerTarget
    {
        Intruders,
        Minions
    }

    /// <summary>
    /// 웨이브 도중 플레이어가 직접 쓰는 던전의 힘.
    /// 부하의 스킬(<see cref="SkillDataSO"/>)은 시전자의 전투필드를 기준으로 도는 데 반해
    /// 권능은 플레이어가 고른 노드를 그대로 겨냥하므로 별도의 데이터로 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Skill/Dungeon Power", fileName = "DungeonPower")]
    public sealed class DungeonPowerSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; } = "권능";

        [field: SerializeField, TextArea]
        public string Description { get; private set; } = string.Empty;

        [field: SerializeField] public Sprite Icon { get; private set; }

        [field: SerializeField, Min(1), Tooltip("시전에 드는 권능")]
        public int Cost { get; private set; } = 20;

        [field: SerializeField, Min(0f), Tooltip("재사용 대기시간(초)")]
        public float Cooldown { get; private set; } = 8f;

        [field: SerializeField, Tooltip("노드의 침입자를 겨냥할지 부하를 겨냥할지")]
        public DungeonPowerTarget Target { get; private set; } = DungeonPowerTarget.Intruders;

        [field: SerializeField, Min(0), Tooltip("대상에게 주는 피해")]
        public int Damage { get; private set; }

        [field: SerializeField, Min(0), Tooltip("대상의 체력 회복량")]
        public int Heal { get; private set; }

        [field: SerializeField, Tooltip("대상에게 거는 상태이상. 비워도 된다.")]
        public StatusEffectDataSO StatusEffect { get; private set; }

        [field: SerializeField, Min(0f),
                Tooltip("이 시간(초) 동안 노드를 통행 불가로 만든다. 0이면 봉쇄하지 않는다. " +
                        "봉쇄 권능은 대상 편(Target)과 무관하게 노드 자체를 겨냥한다.")]
        public float BlockDuration { get; private set; }

        /// <summary>노드 자체를 겨냥하는가. 봉쇄는 아무도 없는 길목에 미리 걸어야 의미가 있다.</summary>
        public bool TargetsNode => BlockDuration > 0f;

        [field: SerializeField]
        public Color FlashColor { get; private set; } = new(0.6f, 0.4f, 1f, 1f);

        /// <summary>이 권능이 실제로 무언가를 하는가. 전부 0이면 눌러도 아무 일이 없다.</summary>
        public bool HasEffect => Damage > 0 || Heal > 0 || StatusEffect != null || BlockDuration > 0f;

        public string BuildSummary()
        {
            if (TargetsNode)
                return $"구역 대상 · {BlockDuration:0.#}초 통행 차단";

            var target = Target == DungeonPowerTarget.Intruders ? "침입자" : "부하";
            var parts = new System.Text.StringBuilder($"{target} 대상");
            if (Damage > 0)
                parts.Append($" · 피해 {Damage}");
            if (Heal > 0)
                parts.Append($" · 회복 {Heal}");
            if (StatusEffect != null)
                parts.Append($" · {StatusEffect.DisplayName}");
            return parts.ToString();
        }
    }
}
