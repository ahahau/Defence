using UnityEngine;

namespace Code.Skills
{
    /// <summary>스킬 데이터. ArtifactDataSO와 같은 구조 — 메타데이터 + 조합형 효과 배열.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Data", fileName = "SkillData", order = 0)]
    public class SkillDataSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, Min(0f), Tooltip("재사용 대기시간(초). 궁극기는 무시.")]
        public float Cooldown { get; private set; } = 5f;
        [field: SerializeField, Tooltip("켜면 전투당 1회만 사용(쿨다운 무시).")]
        public bool IsUltimate { get; private set; }
        [field: SerializeField] public SkillEffectSO[] Effects { get; private set; }

        // 시전 고리만으로는 스물여덟 기술이 색만 다른 원으로 보였다. 주문 그림(Magic Spell Effects 6 등)을 한 번 튼다.
        [field: Header("Spell Visual")]
        [field: SerializeField, Tooltip("시전할 때 한 번 트는 주문 그림(발밑 기준점). 비우면 시전 고리만 퍼진다.")]
        public Sprite[] SpellFrames { get; private set; } = System.Array.Empty<Sprite>();

        [field: SerializeField, Min(1f)] public float SpellFramesPerSecond { get; private set; } = 18f;
        [field: SerializeField] public SkillVisualAnchor SpellAnchor { get; private set; } = SkillVisualAnchor.Target;
        [field: SerializeField, Min(0.1f)] public float SpellScale { get; private set; } = 1f;

        // 던전 FTL 전투용. 함정 본편 Cooldown·Effects와 따로 둬 함정 본편 스킬이 그대로 돈다.
        [field: Header("Dungeon Run Battle")]
        [field: SerializeField, Min(1), Tooltip("쿨 = 이 값 × 시전자 공격 간격(초). 3이면 쓰고·평타·평타·다시.")]
        public int CooldownTurns { get; private set; } = 1;

        [field: SerializeField] public Code.Run.RunTurnSkillEffectSO[] TurnEffects { get; private set; } =
            System.Array.Empty<Code.Run.RunTurnSkillEffectSO>();

        /// <summary>던전 FTL 전투 효과 조각을 차례대로 적용한다.</summary>
        public void ExecuteTurn(Code.Run.RunBattle battle, Code.Run.RunCombatant caster)
        {
            if (TurnEffects == null) return;
            foreach (Code.Run.RunTurnSkillEffectSO effect in TurnEffects)
                effect?.Apply(battle, caster);
        }

        /// <summary>
        /// 시전 순간 퍼지는 고리의 색. 첫 번째 효과 조각이 정한다 —
        /// 한 스킬 안에서 색이 갈리면 무슨 기술인지가 아니라 몇 겹인지가 보인다.
        /// </summary>
        public Color CastColor
        {
            get
            {
                if (Effects != null)
                    foreach (var effect in Effects)
                        if (effect != null)
                            return effect.SignatureColor;

                return new Color(0.6f, 0.85f, 1f, 1f);
            }
        }

        public void Execute(SkillContext context)
        {
            if (Effects == null) return;
            foreach (var effect in Effects)
                effect?.Execute(context);
        }
    }
}
