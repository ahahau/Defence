using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 전투 연출 묶음(사망·피격 점멸·베기 섬광·파티클 재질).
    /// 프리팹마다 들고 있게 하면 16개를 다시 배선해야 하므로 한 곳에 모아두고
    /// <see cref="CombatFxHooks"/>가 전역 피해 이벤트를 듣고 꺼내 쓴다. 채우는 생성기는 <c>CombatPolishBuilder</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatFxCatalog", menuName = "Defence/Combat/Combat FX Catalog")]
    public sealed class CombatFxCatalogSO : ScriptableObject
    {
        // 예전에는 3D 파티클 팩 프리팹을 걸었는데, 도트 그림으로 바꾸며 팩을 지워 참조가 끊겼다. 이제 도트 프레임을 직접 튼다.
        [field: SerializeField, Tooltip("부하가 쓰러질 때 발밑에서 솟는 혼불(해골 왕 팩 effect2_standard). 비우면 띄우지 않는다.")]
        public Sprite[] MinionDeathFrames { get; private set; } = System.Array.Empty<Sprite>();

        [field: SerializeField, Tooltip("모험가가 쓰러질 때 몸 가운데서 터지는 폭발(해골 전사 팩 plash explore 주황). 아군 죽음과 달라야 누가 죽었는지 읽힌다.")]
        public Sprite[] IntruderDeathFrames { get; private set; } = System.Array.Empty<Sprite>();

        [field: SerializeField, Min(1f)]
        public float DeathFramesPerSecond { get; private set; } = 12f;

        // 몸통 크기에 곱하는 배율. 혼불 그림은 64×96 칸이라 그대로면 해골(약 50px)의 두 배로 솟았다.
        [field: SerializeField, Min(0.1f)]
        public float MinionDeathScale { get; private set; } = 0.5f;

        [field: SerializeField, Min(0.1f)]
        public float IntruderDeathScale { get; private set; } = 1f;

        [field: SerializeField, Tooltip("유닛 스프라이트(20)보다 위에 그린다.")]
        public int SortingOrder { get; private set; } = 30;

        [field: Header("Hit")]
        [field: SerializeField, Tooltip("맞은 캐릭터 위에 덮는 단색 실루엣 재질(Defence/Sprite Flash). 비우면 예전처럼 색을 곱해 붉게 칠한다.")]
        public Material HitFlashMaterial { get; private set; }

        [field: SerializeField, Tooltip("근접 평타가 닿은 자리에 잠깐 그리는 베기 섬광.")]
        public Sprite MeleeSlashSprite { get; private set; }

        [field: SerializeField, Tooltip("베기 섬광 색. 블룸 임계(0.85)를 넘게 밝게 둔다.")]
        public Color MeleeSlashColor { get; private set; } = new(1f, 0.97f, 0.85f, 1f);

        [field: SerializeField, Min(0.1f)]
        public float MeleeSlashScale { get; private set; } = 1f;

        [field: SerializeField, Tooltip("코드로 만드는 피격·회복 파티클 재질(URP Particles/Unlit, 도트 사각 그림). 비면 재질 없는 파티클이 자홍색으로 그려진다.")]
        public Material ParticleMaterial { get; private set; }

    }
}
