using UnityEngine;

namespace Code.Buildings
{
    /// <summary>
    /// 함정 발동 효과 하나. 프레임을 한 번 돌리거나, 정한 시간 동안 반복하거나, 화살처럼 두 점 사이를 난다.
    /// 게임 시간으로 흘러 일시정지 중에는 멈춘다. <see cref="TrapTriggerVisual"/>가 만들어 돌려 쓴다.
    /// </summary>
    public class TrapEffectSprite : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _frameSeconds;
        private float _loopSeconds;
        private float _elapsed;
        private bool _flying;
        private Vector3 _from;
        private Vector3 _to;
        private float _flySeconds;

        public bool IsPlaying => gameObject.activeSelf;

        public void Initialize(Material material, int sortingOrder)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            if (material != null)
                _renderer.sharedMaterial = material;
            _renderer.sortingOrder = sortingOrder;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 프레임을 돌린다. <paramref name="loopSeconds"/>가 0이면 한 번, 크면 그동안 반복한다.
        /// <paramref name="anchorBottom"/>이면 효과의 아래 끝을 칸의 아래 끝에 맞춘다(솟는 불기둥).
        /// </summary>
        public void Play(Sprite[] frames, float frameSeconds, Vector3 position, float width, float loopSeconds, bool anchorBottom)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
                return;

            _frames = frames;
            _frameSeconds = frameSeconds;
            _loopSeconds = loopSeconds;
            _elapsed = 0f;
            _flying = false;

            var scale = ScaleFor(frames[0], width);
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.rotation = Quaternion.identity;
            if (anchorBottom)
                position.y += (frames[0].bounds.size.y * scale - width) * 0.5f;
            transform.position = position;

            _renderer.sprite = frames[0];
            gameObject.SetActive(true);
        }

        /// <summary>그림 한 장을 <paramref name="from"/>에서 <paramref name="to"/>로 날린다. 그림은 오른쪽을 향해 있어야 한다.</summary>
        public void Fly(Sprite sprite, Vector3 from, Vector3 to, float width, float seconds)
        {
            if (sprite == null)
                return;

            _frames = null;
            _elapsed = 0f;
            _flying = true;
            _from = from;
            _to = to;
            _flySeconds = Mathf.Max(0.01f, seconds);

            var scale = ScaleFor(sprite, width);
            transform.localScale = new Vector3(scale, scale, 1f);
            var direction = to - from;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            transform.position = from;

            _renderer.sprite = sprite;
            gameObject.SetActive(true);
        }

        public void Stop()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_flying)
            {
                var t = Mathf.Clamp01(_elapsed / _flySeconds);
                transform.position = Vector3.Lerp(_from, _to, t);
                if (t >= 1f)
                    Stop();
                return;
            }

            var loop = _loopSeconds > 0f;
            if (loop && _elapsed >= _loopSeconds)
            {
                Stop();
                return;
            }

            var index = TrapEffectRules.FrameAt(_elapsed, _frameSeconds, _frames.Length, loop);
            if (index < 0)
            {
                Stop();
                return;
            }

            _renderer.sprite = _frames[index];
        }

        private static float ScaleFor(Sprite sprite, float width)
        {
            var spriteWidth = sprite.bounds.size.x;
            return spriteWidth > 0f ? width / spriteWidth : 1f;
        }
    }
}
