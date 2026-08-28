using System.Collections;
using _01.Code.Combat;
using MoreMountains.Feedbacks;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Building : MonoBehaviour
    {
        public BuildingDataSO Data { get; private set; }
        [field: SerializeField, Min(0)] public int DangerRating { get; private set; }

        [Header("Durability")]
        [SerializeField] private bool destructible;
        [SerializeField, Min(1)] private int maxDurability = 20;
        [SerializeField] private Transform damageAnimationTarget;
        [SerializeField, Min(0f)] private float damageShakeDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float damageShakeDuration = 0.16f;
        [SerializeField, Min(0f)] private float destroyDelay = 0.05f;

        private int currentDurability;
        private bool isDestroyed;
        private Vector3 damageAnimationBaseLocalPosition;
        private Tween damageTween;

        public bool IsDestructible => destructible;
        public bool IsDestroyed => isDestroyed;
        public int CurrentDurability => currentDurability;
        public int MaxDurability => maxDurability;

        protected virtual void Awake()
        {
            if (damageAnimationTarget == null)
                damageAnimationTarget = transform;

            damageAnimationBaseLocalPosition = damageAnimationTarget.localPosition;
            currentDurability = Mathf.Max(1, maxDurability);
        }

        public virtual void Initialize(BuildingDataSO data)
        {
            Data = data;
            DangerRating = data.BaseDanger;
            currentDurability = Mathf.Max(1, maxDurability);
            isDestroyed = false;
        }

        public void RestoreDurability(int durability)
        {
            currentDurability = Mathf.Clamp(durability, 1, Mathf.Max(1, maxDurability));
            isDestroyed = false;
        }

        public bool TakeBuildingDamage(int damage)
        {
            if (!destructible || isDestroyed || damage <= 0)
                return false;

            currentDurability = Mathf.Max(0, currentDurability - damage);
            PlayHitAnimation();

            if (currentDurability > 0)
                return false;

            BreakBuilding();
            return true;
        }

        protected virtual void BreakBuilding()
        {
            if (isDestroyed)
                return;

            isDestroyed = true;
            damageTween?.Kill();
            if (destroyDelay <= 0f)
                Destroy(gameObject);
            else
                Destroy(gameObject, destroyDelay);
        }

        protected void PlayPassEffectFeedback(
            Combatant target,
            Color flashColor,
            float duration,
            MMF_Player feelFeedback = null)
        {
            if (target == null)
                return;

            if (feelFeedback != null)
                feelFeedback.PlayFeedbacks(target.transform.position);
            StartCoroutine(FlashTargetColor(target, flashColor, duration));
        }

        private IEnumerator FlashTargetColor(Combatant target, Color flashColor, float duration)
        {
            if (target == null)
                yield break;

            var renderers = target.GetComponentsInChildren<SpriteRenderer>();
            if (renderers == null || renderers.Length == 0)
                yield break;

            var originalColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                originalColors[i] = renderers[i].color;
                renderers[i].color = flashColor;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = originalColors[i];
            }
        }

        private void PlayHitAnimation()
        {
            if (damageAnimationTarget == null || damageShakeDistance <= 0f)
                return;

            damageTween?.Kill();
            damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition;
            damageTween = damageAnimationTarget
                .DOLocalMoveX(damageAnimationBaseLocalPosition.x + damageShakeDistance, damageShakeDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition)
                .SetLink(gameObject);
        }

        private void OnDisable()
        {
            damageTween?.Kill();
            damageTween = null;

            if (damageAnimationTarget != null)
                damageAnimationTarget.localPosition = damageAnimationBaseLocalPosition;
        }
    }
}
