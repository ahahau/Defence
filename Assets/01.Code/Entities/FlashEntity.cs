using _01.Code.Modules;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Entities
{
    public class FlashEntity : MonoBehaviour, IModule
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        [SerializeField] private float blinkDuration = 0.15f;
        [SerializeField] private float blinkIntensity = 0.15f;
        public void Initialize(ModuleOwner owner)
        {
            
        }
        
        //public override void CreateFeedback()
        //{
        //    spriteRenderer.material.SetFloat(_blinkHash, blinkIntensity);
        //    DOVirtual.DelayedCall(blinkDuration, StopFeedback);
        //}
        //
        //public override void StopFeedback()
        //{
        //    if (spriteRenderer != null) 
        //    {
        //        spriteRenderer.material.SetFloat(_blinkHash, 0);
        //    }
        //}
    }
}