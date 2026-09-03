using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 코드로 세우는 창이 쓸 UI 그림 묶음.
    ///
    /// UI 팩(Layer Lab)은 Resources 밖에 있어서 실행 중에 직접 못 집는다.
    /// 이 에셋만 Resources에 두고 거기서 팩 스프라이트를 참조하면, 그림을 복사해 늘리지 않고도
    /// 실행 중에 꺼내 쓸 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSkin", menuName = "Defence/UI/UI Skin")]
    public sealed class UiSkinSO : ScriptableObject
    {
        [field: SerializeField, Tooltip("창 배경. 9-슬라이스 경계가 잡힌 프레임이어야 한다.")]
        public Sprite WindowFrame { get; private set; }

        [field: SerializeField, Tooltip("버튼 배경.")]
        public Sprite ButtonFrame { get; private set; }

        [field: SerializeField, Tooltip("슬라이더 홈.")]
        public Sprite SliderTrack { get; private set; }

        [field: SerializeField, Tooltip("슬라이더 채움.")]
        public Sprite SliderFill { get; private set; }
    }
}
