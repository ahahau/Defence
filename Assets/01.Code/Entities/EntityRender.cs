using System;
using Code.Core;
using UnityEngine;

namespace Code.Entities
{
    public enum EntityState
    {
        Idle,
        Attack,
        Defeated
    }
    public class EntityRender : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite attackSprite;
        [SerializeField] private Sprite defeatedSprite;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Tooltip("있으면 정지 그림 대신 프레임 애니메이션을 튼다. 비우면 위 세 포즈를 바꿔 끼운다.")]
        private CharacterAnimationSetSO animationSet;

        private EntityState _state = EntityState.Idle;
        private CharacterClip _clip = CharacterClip.Idle;
        private float _clipTime;
        private int _frameIndex = -1;
        private Transform _motionSource;
        private Vector3 _lastPosition;
        private float _walkHold;

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public bool IsAnimated => animationSet != null;

        /// <summary>보이는 그림이 바뀔 때마다. 테두리처럼 그림을 따라 그리는 쪽이 듣는다.</summary>
        public event Action SpriteChanged;

        public void ConfigureSprites(Sprite idle, Sprite attack, Sprite defeated)
        {
            if (idle != null)
                idleSprite = idle;

            if (attack != null)
                attackSprite = attack;

            if (defeated != null)
                defeatedSprite = defeated;

            SetUnitSprite(EntityState.Idle);
        }

        /// <summary>데이터가 정한 애니메이션 묶음으로 바꾼다. null이면 지금 묶음을 유지한다.</summary>
        public void ConfigureAnimation(CharacterAnimationSetSO set)
        {
            if (set == null)
                return;

            animationSet = set;
            _frameIndex = -1;
            PlayClip(ResolveClip());
        }

        public void SetUnitSprite(EntityState state = EntityState.Defeated)
        {
            if (animationSet != null)
            {
                SetAnimatedState(state);
                return;
            }

            switch (state)
            {
                case EntityState.Defeated:
                    Apply(defeatedSprite);
                    break;
                case EntityState.Attack:
                    Apply(attackSprite);
                    break;
                case EntityState.Idle:
                    Apply(idleSprite);
                    break;
            }
        }

        /// <summary>그림이 오른쪽을 본다고 보고, 음수면 왼쪽으로 뒤집는다.</summary>
        public void FaceTowards(float directionX)
        {
            if (spriteRenderer == null || Mathf.Abs(directionX) <= 0.001f)
                return;

            var flip = directionX < 0f;
            if (spriteRenderer.flipX == flip)
                return;

            spriteRenderer.flipX = flip;
            SpriteChanged?.Invoke();
        }

        private void Awake()
        {
            _motionSource = transform.parent != null ? transform.parent : transform;
            _lastPosition = _motionSource.position;
        }

        private void OnEnable()
        {
            if (_motionSource != null)
                _lastPosition = _motionSource.position;
            _walkHold = 0f;
        }

        private void Update()
        {
            if (animationSet == null)
                return;

            var deltaTime = Time.deltaTime;
            TrackMotion(deltaTime);

            var desired = ResolveClip();
            if (desired != _clip)
                PlayClip(desired);
            else
                _clipTime += deltaTime;

            // 공격은 끝까지 튼 뒤 스스로 대기로 돌아간다. 공격 포즈를 되돌리는 쪽이 동작을 자르지 않게 한다.
            if (_clip == CharacterClip.Attack
                && CharacterAnimationRules.IsFinished(animationSet.GetFrames(_clip).Length, _clipTime, animationSet.GetFramesPerSecond(_clip)))
            {
                _state = EntityState.Idle;
                PlayClip(ResolveClip());
            }

            RefreshFrame();
        }

        private void SetAnimatedState(EntityState state)
        {
            // 공격 동작 중의 대기 요청은 무시한다. 동작이 끝나면 Update가 대기로 돌린다.
            if (state == EntityState.Idle && _state == EntityState.Attack && _clip == CharacterClip.Attack)
                return;

            // 쓰러진 뒤에 들어온 공격·대기 요청은 되살아난 경우(대기)만 받는다.
            if (_state == EntityState.Defeated && state == EntityState.Attack)
                return;

            var restart = state == EntityState.Attack || _state != state;
            _state = state;
            if (restart)
                PlayClip(ResolveClip());
        }

        private CharacterClip ResolveClip()
        {
            return _state switch
            {
                EntityState.Defeated => CharacterClip.Defeated,
                EntityState.Attack => CharacterClip.Attack,
                _ => _walkHold > 0f ? CharacterClip.Walk : CharacterClip.Idle
            };
        }

        private void TrackMotion(float deltaTime)
        {
            if (_motionSource == null || deltaTime <= 0f)
                return;

            var position = _motionSource.position;
            var delta = position - _lastPosition;
            _lastPosition = position;

            if (_state == EntityState.Defeated)
            {
                _walkHold = 0f;
                return;
            }

            if (delta.magnitude / deltaTime >= CharacterAnimationRules.WalkSpeedThreshold)
            {
                _walkHold = CharacterAnimationRules.WalkHoldSeconds;
                FaceTowards(delta.x);
            }
            else
            {
                _walkHold = Mathf.Max(0f, _walkHold - deltaTime);
            }
        }

        private void PlayClip(CharacterClip clip)
        {
            _clip = clip;
            _clipTime = 0f;
            _frameIndex = -1;
            RefreshFrame();
        }

        private void RefreshFrame()
        {
            if (animationSet == null)
                return;

            var frames = animationSet.GetFrames(_clip);
            if (frames.Length == 0)
                return;

            var index = CharacterAnimationRules.GetFrameIndex(
                frames.Length, _clipTime, animationSet.GetFramesPerSecond(_clip), CharacterAnimationRules.IsLooping(_clip));
            if (index == _frameIndex)
                return;

            _frameIndex = index;
            Apply(frames[index]);
        }

        /// <summary>
        /// 비어 있는 포즈로는 갈아끼우지 않는다. 그대로 대입하면 스프라이트가 null이 되어
        /// 캐릭터가 화면에서 사라진다 — 포즈 그림이 아직 없는 쪽은 있던 모습을 유지하는 편이 낫다.
        /// </summary>
        private void Apply(Sprite sprite)
        {
            if (spriteRenderer == null || sprite == null || spriteRenderer.sprite == sprite)
                return;

            spriteRenderer.sprite = sprite;
            SpriteChanged?.Invoke();
        }
    }
}
