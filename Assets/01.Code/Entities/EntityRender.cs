using System;
using _01.Code.Enemies;
using _01.Code.Modules;
using UnityEngine;

namespace _01.Code.Entities
{
    public class EntityRender : MonoBehaviour, IRenderer, IModule
    {
        private Entity _owner;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        
        [field: SerializeField] public float FacingDirection { get; private set; } = 1f;//1f가 오른쪽 보는거다.
        
        public void Initialize(ModuleOwner owner)
        {
            _owner = owner as Entity;
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        public void FlipController(float xMove)
        {
            if (Mathf.Abs(FacingDirection + xMove) < 0.5f)
                Flip();
        }

        private void Flip()
        {
            FacingDirection *= -1;
            //float targetYRotation = FacingDirection > 0 ? 0 : 180f;
            //_owner.transform.rotation = Quaternion.Euler(0, targetYRotation, 0);
            _spriteRenderer.flipX = FacingDirection > 0;
        }
        
        public void PlayClip(int clipHash, int layer = -1, float normalPosition = float.NegativeInfinity)
            => _animator.Play(clipHash, layer, normalPosition);
        
        public void SetBool(ParamSO param, bool value) 
            => _animator.SetBool(param.HashValue, value);
        public void SetFloat(ParamSO param, float value) 
            => _animator.SetFloat(param.HashValue, value);
        public void SetInt(ParamSO param, int value) 
            => _animator.SetInteger(param.HashValue, value);
        public void SetTrigger(ParamSO param)
            => _animator.SetTrigger(param.HashValue);

        #region 트리거 섹션

        public event Action OnAnimationEnd;
        public event Action OnAttackTrigger;
        public event Action<bool> OnCounterStateChange;

        private void EndTrigger() => OnAnimationEnd?.Invoke();
        private void AttackTrigger() => OnAttackTrigger?.Invoke();
        
        private void OpenCounterWindow() => OnCounterStateChange?.Invoke(true);
        private void CloseCounterWindow() => OnCounterStateChange?.Invoke(false);

        #endregion
    }
}
