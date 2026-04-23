using DG.Tweening;
using UnityEngine;

namespace _01.Code.Combat
{
    [RequireComponent(typeof(Health))]
    public class DamageFeedback : MonoBehaviour
    {
        [SerializeField] private Color bloomColor = new(3f, 0.25f, 0.15f, 1f);
        [SerializeField] private float bloomDuration = 0.12f;
        [SerializeField] private float textFloatDistance = 0.45f;
        [SerializeField] private float textDuration = 0.55f;
        [SerializeField] private int textSortingOrder = 60;

        private Health health;
        private SpriteRenderer[] spriteRenderers;
        private Color[] originalColors;
        private Sequence bloomSequence;

        private void Awake()
        {
            health = GetComponent<Health>();
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[spriteRenderers.Length];

            for (var i = 0; i < spriteRenderers.Length; i++)
                originalColors[i] = spriteRenderers[i].color;
        }

        private void OnEnable()
        {
            if (health != null)
                health.Damaged += Play;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= Play;

            bloomSequence?.Kill();
        }

        private void Play(int damage)
        {
            if (damage <= 0)
                return;

            PlayBloom();
            CreateDamageText(damage);
        }

        private void PlayBloom()
        {
            bloomSequence?.Kill();
            bloomSequence = DOTween.Sequence();

            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null || spriteRenderer.sortingOrder >= 40)
                    continue;

                var originalColor = originalColors[i];
                bloomSequence.Join(spriteRenderer.DOColor(bloomColor, bloomDuration));
                bloomSequence.Insert(bloomDuration, spriteRenderer.DOColor(originalColor, bloomDuration));
            }
        }

        private void CreateDamageText(int damage)
        {
            var textObject = new GameObject("DamageText");
            textObject.transform.position = transform.position + Vector3.up * 0.85f;

            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = damage.ToString();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.18f;
            textMesh.fontSize = 32;
            textMesh.color = Color.red;

            var meshRenderer = textObject.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = textSortingOrder;

            var endPosition = textObject.transform.position + Vector3.up * textFloatDistance;
            DOTween.Sequence()
                .Append(textObject.transform.DOMove(endPosition, textDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(
                    () => textMesh.color.a,
                    alpha =>
                    {
                        var color = textMesh.color;
                        color.a = alpha;
                        textMesh.color = color;
                    },
                    0f,
                    textDuration))
                .OnComplete(() => Destroy(textObject));
        }
    }
}
