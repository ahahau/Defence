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
        private bool _facingLeft;

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public bool IsAnimated => animationSet != null;

        /// <summary>보는 쪽. 오른쪽 1, 왼쪽 -1.</summary>
        public float FacingX => _facingLeft ? -1f : 1f;

        /// <summary>
        /// 움직이는 쪽으로 돌아설지. 싸우는 동안은 끈다 — 넉백·뒷걸음으로 뒤로 밀릴 때마다 등을 돌려,
        /// 실측에서 교전 프레임의 23%가 대상 반대쪽을 봤다. 싸울 때 방향은 전투 쪽이 대상으로 정한다.
        /// </summary>
        public bool FaceMotion { get; set; } = true;

        /// <summary>공격 동작에서 무기가 닿는 때(초). 애니메이션이 없으면 0.</summary>
        public float AttackImpactSeconds => animationSet != null ? animationSet.AttackImpactSeconds : 0f;

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

        /// <summary>
        /// 그림이 오른쪽을 본다고 보고, 음수면 왼쪽(회전 Y 180)으로 돌린다. 방향은 여기 한 곳만 정한다.
        /// 예전에는 이 컴포넌트가 flipX를, <c>BattleAgent</c>가 몸통 크기 x를 따로 뒤집어 둘이 겹치면 두 번 뒤집혀
        /// 원래 방향을 봤다(한쪽만 보는 캐릭터). 자식(테두리 등)도 같이 돌도록 그림 대신 몸통을 돌린다.
        /// </summary>
        public void FaceTowards(float directionX)
        {
            if (Mathf.Abs(directionX) <= 0.001f)
                return;

            _facingLeft = directionX < 0f;
            ApplyFacing();
        }

        private void Awake()
        {
            _motionSource = transform.parent != null ? transform.parent : transform;
            _lastPosition = _motionSource.position;
            // 예전 프리팹·씬에 남은 flipX가 회전과 겹치지 않게 한 번 지운다.
            if (spriteRenderer != null)
                spriteRenderer.flipX = false;
        }

        // 피격 흔들림·이동 연출이 회전을 자기 시작값으로 되돌리며 Y를 덮을 수 있다. 연출이 끝난 뒤 매 프레임 방향만 다시 얹는다.
        private void LateUpdate() => ApplyFacing();

        /// <summary>z 기울기(흔들림)는 살리고 Y만 보는 쪽(0/180)으로 맞춘다.</summary>
        private void ApplyFacing()
        {
            var rotation = transform.localRotation;
            var targetY = _facingLeft ? 180f : 0f;
            var tilt = CharacterFacingRules.TiltZ(rotation * Vector3.up, rotation * Vector3.forward);
            var desired = Quaternion.Euler(0f, targetY, tilt);
            if (Quaternion.Angle(rotation, desired) > 0.01f)
                transform.localRotation = desired;
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
                if (FaceMotion)
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
