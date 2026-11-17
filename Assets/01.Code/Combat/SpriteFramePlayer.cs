using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 연출용 짧은 프레임 그림(날아가는 발사체·닿은 자리 폭발)을 SpriteRenderer에 튼다.
    /// 반복이 아니면 마지막 프레임에서 오브젝트를 지운다. 캐릭터 동작은 <c>EntityRender</c>가 맡고, 이것은 그림만 도는 일회용이다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFramePlayer : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _framesPerSecond;
        private bool _loop;
        private float _elapsed;

        /// <summary>
        /// 그 자리에 프레임 그림을 한 번 틀고 스스로 지우는 오브젝트를 만든다(타격 불꽃·폭발·사망·스킬 연출 공용).
        /// 그림이 없으면 아무것도 만들지 않고 null.
        /// </summary>
        public static SpriteFramePlayer Spawn(string name, Sprite[] frames, float framesPerSecond, Vector3 position,
            float scale = 1f, int sortingOrder = 101, bool flipX = false)
        {
            if (frames == null || frames.Length == 0)
                return null;

            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.flipX = flipX;
            var player = go.AddComponent<SpriteFramePlayer>();
            player.Play(frames, framesPerSecond, false);
            return player;
        }

        public void Play(Sprite[] frames, float framesPerSecond, bool loop)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _frames = frames;
            _framesPerSecond = Mathf.Max(1f, framesPerSecond);
            _loop = loop;
            _elapsed = 0f;
            Apply();
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0)
                return;

            _elapsed += Time.deltaTime;
            if (!_loop && _elapsed >= _frames.Length / _framesPerSecond)
            {
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            if (_frames == null || _frames.Length == 0)
                return;

            var index = Code.Entities.CharacterAnimationRules.GetFrameIndex(_frames.Length, _elapsed, _framesPerSecond, _loop);
            _renderer.sprite = _frames[index];
        }
    }
}
