using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Combat
{
    [RequireComponent(typeof(Health))]
    public class Combatant : MonoBehaviour
    {
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float attackInterval = 1f;
        [SerializeField] private float bodySlamDistance = 0.3f;
        [SerializeField] private float bodySlamDuration = 0.12f;

        private SpriteRenderer healthFill;
        private SpriteRenderer attackFill;
        private Coroutine attackRoutine;
        private Tween bodySlamTween;
        private Health health;
        private DamageFeedback damageFeedback;
        private bool isAttacking;

        public bool IsAlive => health != null && health.IsAlive;
        public bool IsAttacking => isAttacking;
        public Health Health => health;

        public void AddAttackDamage(int amount)
        {
            if (amount > 0)
                attackDamage += amount;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            if (health == null)
                health = gameObject.AddComponent<Health>();

            damageFeedback = GetComponent<DamageFeedback>();
            if (damageFeedback == null)
                damageFeedback = gameObject.AddComponent<DamageFeedback>();

            CreateBars();
            health.Changed += RefreshHealthBar;
            RefreshBars(0f);
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Changed -= RefreshHealthBar;
        }

        public void BeginCombat(Combatant target, Action<Combatant> targetDefeated)
        {
            if (target == null || !target.IsAlive || !IsAlive)
                return;

            if (attackRoutine != null)
                StopCoroutine(attackRoutine);

            attackRoutine = StartCoroutine(AttackLoop(target, targetDefeated));
        }

        public void StopCombat()
        {
            if (attackRoutine != null)
                StopCoroutine(attackRoutine);

            attackRoutine = null;
            isAttacking = false;
            bodySlamTween?.Kill();
            RefreshBars(0f);
        }

        private IEnumerator AttackLoop(Combatant target, Action<Combatant> targetDefeated)
        {
            var attackTimer = 0f;
            RefreshBars(attackTimer);

            while (target != null && target.IsAlive && IsAlive)
            {
                attackTimer += Time.deltaTime;
                RefreshAttackBar(attackTimer / attackInterval);

                if (attackTimer >= attackInterval && !isAttacking)
                {
                    isAttacking = true;
                    yield return PlayBodySlam(target);

                    if (target != null && target.Health != null)
                        target.Health.TakeDamage(attackDamage);

                    attackTimer = 0f;
                    RefreshAttackBar(0f);
                    isAttacking = false;

                    if (target == null || !target.IsAlive)
                    {
                        targetDefeated?.Invoke(target);
                        break;
                    }
                }

                yield return null;
            }

            StopCombat();
        }

        private IEnumerator PlayBodySlam(Combatant target)
        {
            if (target == null)
                yield break;

            bodySlamTween?.Kill();

            var startPosition = transform.position;
            var targetPos = target != null ? target.transform.position : startPosition;
            var direction = targetPos - startPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector3.right;

            var hitPosition = startPosition + direction.normalized * bodySlamDistance;
            bodySlamTween = DOTween.Sequence()
                .Append(transform.DOMove(hitPosition, bodySlamDuration).SetEase(Ease.OutQuad))
                .Append(transform.DOMove(startPosition, bodySlamDuration).SetEase(Ease.InQuad))
                .SetLink(gameObject);

            yield return bodySlamTween.WaitForCompletion();
        }

        private void CreateBars()
        {
            var barSprite = CreateBarSprite();
            var healthRoot = CreateBar("HealthBar", new Vector3(0f, 0.72f, 0f), Color.red, barSprite);
            healthFill = healthRoot.transform.GetChild(1).GetComponent<SpriteRenderer>();

            var attackRoot = CreateBar("AttackSpeedBar", new Vector3(0f, -0.62f, 0f), Color.yellow, barSprite);
            attackFill = attackRoot.transform.GetChild(1).GetComponent<SpriteRenderer>();
        }

        private GameObject CreateBar(string objectName, Vector3 localPosition, Color fillColor, Sprite sprite)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = localPosition;

            var background = new GameObject("Background");
            background.transform.SetParent(root.transform, false);
            background.transform.localScale = new Vector3(0.72f, 0.09f, 1f);
            var backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = sprite;
            backgroundRenderer.color = Color.black;
            backgroundRenderer.sortingOrder = 40;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(root.transform, false);
            fill.transform.localPosition = new Vector3(-0.36f, 0f, -0.01f);
            fill.transform.localScale = new Vector3(0.72f, 0.06f, 1f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = sprite;
            fillRenderer.color = fillColor;
            fillRenderer.sortingOrder = 41;

            return root;
        }

        private Sprite CreateBarSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        }

        private void RefreshBars(float attackRatio)
        {
            RefreshHealthBar(health != null ? health.CurrentRatio : 0f);
            RefreshAttackBar(attackRatio);
        }

        private void RefreshHealthBar(float ratio)
        {
            if (healthFill == null)
                return;
            SetFillRatio(healthFill.transform, ratio);
        }

        private void RefreshAttackBar(float ratio)
        {
            if (attackFill == null)
                return;
            SetFillRatio(attackFill.transform, Mathf.Clamp01(ratio));
        }

        private void SetFillRatio(Transform fillTransform, float ratio)
        {
            fillTransform.localScale = new Vector3(0.72f * ratio, fillTransform.localScale.y, fillTransform.localScale.z);
            fillTransform.localPosition = new Vector3(-0.36f + 0.36f * ratio, fillTransform.localPosition.y, fillTransform.localPosition.z);
        }
    }
}
