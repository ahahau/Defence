using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 전투원이 쓰러질 때 쓰는 연출 묶음.
    /// 프리팹마다 들고 있게 하면 16개를 다시 배선해야 하므로 한 곳에 모아두고
    /// <see cref="CombatFxHooks"/>가 전역 피해 이벤트를 듣고 꺼내 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFxCatalog", menuName = "Defence/Combat/Combat FX Catalog")]
    public sealed class CombatFxCatalogSO : ScriptableObject
    {
        [field: SerializeField, Tooltip("부하가 쓰러질 때. 비우면 아무것도 띄우지 않는다.")]
        public GameObject MinionDeathEffect { get; private set; }

        [field: SerializeField, Tooltip("침입자가 쓰러질 때. 아군 죽음과 달라야 누가 죽었는지 읽힌다.")]
        public GameObject IntruderDeathEffect { get; private set; }

        [field: SerializeField, Min(0.1f)]
        public float DeathEffectScale { get; private set; } = 1.5f;

        [field: SerializeField, Tooltip("유닛 스프라이트(20)보다 위에 그린다.")]
        public int SortingOrder { get; private set; } = 30;
    }
}
