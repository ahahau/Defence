using UnityEngine;

namespace Code.Entities
{
    /// <summary>
    /// 캐릭터를 두르는 네모 테두리에 쓰는 그림표.
    ///
    /// 테두리 그림은 UI 팩 안에 있어서 <c>Resources</c> 밖에 놓여 있다. 실행 중에는 그쪽을
    /// 직접 집을 수 없으므로, Resources 에 둔 이 에셋이 다리 역할을 한다. 설정 창이
    /// <c>UiSkinSO</c>로 같은 문제를 푸는 것과 같은 방식이다.
    ///
    /// 프리팹마다 그림을 물리지 않는 이유는 수 때문이다. 유닛과 적 프리팹이 수십 개라,
    /// 테두리를 한 번 바꾸려면 그만큼을 다 열어야 한다. 한 곳만 바꾸면 전부 따라오게 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/UI/Character Plate Skin", fileName = "CharacterPlateSkin")]
    public sealed class CharacterPlateSkinSO : ScriptableObject
    {
        [field: SerializeField, Tooltip("캐릭터를 두르는 네모 테두리. 9슬라이스 경계가 있어야 늘려도 모서리가 안 찌그러진다.")]
        public Sprite Frame { get; private set; }

        [field: SerializeField, Tooltip("테두리 기본색. 캐릭터를 가리지 않게 옅게 둔다.")]
        public Color FrameColor { get; private set; } = new(0.82f, 0.76f, 0.66f, 0.55f);

        [field: SerializeField, Min(1f), Tooltip("캐릭터 크기 대비 테두리 크기. 1이면 딱 붙는다.")]
        public float FramePadding { get; private set; } = 1.18f;
    }
}
