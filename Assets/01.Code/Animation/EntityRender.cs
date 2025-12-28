using _01.Code.Enemies;
using UnityEngine;

namespace _01.Code.Animation
{
    public class EntityRender : MonoBehaviour
    {
        private Animator animator;
        [SerializeField] protected SpriteRenderer renderer;
        private void Awake()
        {
            animator = GetComponent<Animator>();
        }
        
        public void SetParameter(ParamSO param, float value)
            => animator.SetFloat(param.HashValue, value);
        public void SetParameter(ParamSO param, float value, float dampTime)
            => animator.SetFloat(param.HashValue, value, dampTime, Time.deltaTime);
        public void SetParameter(ParamSO param, int value)
            => animator.SetInteger(param.HashValue, value);
        public void SetParameter(ParamSO param, bool value)
            => animator.SetBool(param.HashValue, value);
        public void SetTrigger(ParamSO param)
            => animator.SetTrigger(param.HashValue);
        public void ReSetTrigger(ParamSO param)
            => animator.ResetTrigger(param.HashValue);
    }
}
