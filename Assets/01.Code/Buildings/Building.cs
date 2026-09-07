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
            ApplyBoardSprite(data);
        }

        /// <summary>
        /// 데이터가 들고 있는 그림을 실제로 보드에 올린다.
        ///
        /// 종류 열하나가 프리팹 여덟을 나눠 쓴다 — 주점과 큰 주점이 Inn 하나를, 광산과 깊은 광산이
        /// Mine 하나를, 상점과 무기 상점이 Store 하나를 쓴다. 프리팹에만 그림을 두면 승급해도
        /// 화면이 그대로라 무엇이 지어졌는지 알 수 없다. 침입자 쪽이 이미 같은 방식으로 돌고 있다.
        /// </summary>
        private void ApplyBoardSprite(BuildingDataSO data)
        {
            if (data == null || data.BoardSprite == null)
                return;

            // 프리팹에는 SpriteRenderer가 둘이다 — 몸통 Visual 과 테두리 Border.
            // 이름으로 몸통을 집는다. 아무거나 집으면 테두리에 건물 그림이 들어간다.
            SpriteRenderer body = null;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name != "Visual")
                    continue;

                body = renderer;
                break;
            }

            // 이름이 다른 프리팹이 섞여 있어도 아무것도 안 나오는 것보다는 낫다.
            if (body == null)
                body = GetComponentInChildren<SpriteRenderer>(true);

            if (body != null)
                body.sprite = data.BoardSprite;
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
