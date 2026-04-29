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
        [SerializeField] private TextMesh damageTextPrefab;

        private Health _health;
        private SpriteRenderer[] _spriteRenderers;
        private Color[] _originalColors;
        private Sequence _bloomSequence;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            _originalColors = new Color[_spriteRenderers.Length];

            for (var i = 0; i < _spriteRenderers.Length; i++)
                _originalColors[i] = _spriteRenderers[i].color;
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.Damaged += Play;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.Damaged -= Play;

            _bloomSequence?.Kill();
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
            _bloomSequence?.Kill();
            _bloomSequence = DOTween.Sequence();

            for (var i = 0; i < _spriteRenderers.Length; i++)
            {
                var spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer == null || spriteRenderer.sortingOrder >= 40)
                    continue;

                var originalColor = _originalColors[i];
                _bloomSequence.Join(spriteRenderer.DOColor(bloomColor, bloomDuration));
                _bloomSequence.Insert(bloomDuration, spriteRenderer.DOColor(originalColor, bloomDuration));
            }
        }

        private void CreateDamageText(int damage)
        {
            var textMesh = Instantiate(damageTextPrefab, transform.position + Vector3.up * 0.85f, Quaternion.identity);
            textMesh.text = damage.ToString();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.18f;
            textMesh.fontSize = 32;
            textMesh.color = Color.red;

            var meshRenderer = textMesh.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = textSortingOrder;

            var endPosition = textMesh.transform.position + Vector3.up * textFloatDistance;
            DOTween.Sequence()
                .Append(textMesh.transform.DOMove(endPosition, textDuration).SetEase(Ease.OutQuad))
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
                .OnComplete(() => Destroy(textMesh.gameObject));
        }
    }
}
