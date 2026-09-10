using UnityEngine;

namespace _01.Code.Entities
{
    /// <summary>
    /// 캐릭터 발밑 받침에 쓰는 그림표.
    ///
    /// 받침 그림은 UI 팩 안에 있어서 <c>Resources</c> 밖에 놓여 있다. 실행 중에는 그쪽을
    /// 직접 집을 수 없으므로, Resources 에 둔 이 에셋이 다리 역할을 한다. 설정 창이
    /// <c>UiSkinSO</c>로 같은 문제를 푸는 것과 같은 방식이다.
    ///
    /// 프리팹마다 그림을 물리지 않는 이유는 수 때문이다. 유닛과 적 프리팹이 수십 개라,
    /// 받침을 한 번 바꾸려면 그만큼을 다 열어야 한다. 한 곳만 바꾸면 전부 따라오게 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/UI/Character Plate Skin", fileName = "CharacterPlateSkin")]
    public sealed class CharacterPlateSkinSO : ScriptableObject
    {
        [field: SerializeField, Tooltip("발밑에 까는 받침.")]
        public Sprite Plate { get; private set; }

        [field: SerializeField, Tooltip("받침 색. 캐릭터를 가리지 않게 어둡고 옅게 둔다.")]
        public Color PlateColor { get; private set; } = new(0.16f, 0.13f, 0.15f, 0.72f);

        [field: SerializeField, Min(0.1f), Tooltip("캐릭터 폭 대비 받침 폭.")]
        public float PlateWidthFactor { get; private set; } = 1.35f;

        [field: SerializeField, Min(0.05f), Tooltip("받침의 세로 납작함. 1이면 원, 작을수록 타원.")]
        public float PlateFlatten { get; private set; } = 0.42f;

        [field: SerializeField, Min(1f), Tooltip("발광 링이 받침보다 얼마나 큰가. 1보다 커야 바깥으로 나온다.")]
        public float GlowScale { get; private set; } = 1.28f;
    }
}
