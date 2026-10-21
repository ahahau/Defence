using UnityEngine;

namespace Code.Buildings
{
    /// <summary>
    /// 함정 몸체(<c>Visual</c>)의 프레임을 게임 시간으로 반복한다(웅덩이 물결·화로 불꽃·수정 반짝임). 일시정지 중에는 멈춘다.
    /// 첫 프레임이 데이터의 보드 그림과 같아 설치 카드·미리보기는 그 그림을 쓴다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TrapBodyAnimation : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.12f;

        private SpriteRenderer _renderer;
        private float _elapsed;

        public int FrameCount => frames?.Length ?? 0;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
                return;

            // 한 바퀴마다 되감아 긴 판에서도 시간 값이 커지지 않게 한다.
            _elapsed = Mathf.Repeat(_elapsed + Time.deltaTime, frameSeconds * frames.Length);
            var index = TrapEffectRules.FrameAt(_elapsed, frameSeconds, frames.Length, true);
            if (index >= 0 && frames[index] != null)
                _renderer.sprite = frames[index];
        }
    }
}
