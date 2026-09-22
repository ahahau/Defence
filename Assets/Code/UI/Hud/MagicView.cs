using DG.Tweening;
using Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class MagicView : MonoBehaviour
    {
        [SerializeField] private MagicManager magicManager;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text magicText;
        [SerializeField] private string format = "주둔 마력 {0}/{1}";

        private int _lastUsedMagic;
        private bool _hasValue;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (magicText == null)
                return;
            _baseColor = magicText.color;
            _baseScale = magicText.transform.localScale;
        }

        private void OnEnable()
        {
            if (magicManager == null) return;
            magicManager.MagicChanged += HandleMagicChanged;
            HandleMagicChanged(magicManager.UsedMagic, magicManager.MaxMagic);
        }

        private void OnDisable()
        {
            if (magicManager != null)
                magicManager.MagicChanged -= HandleMagicChanged;
            if (magicText != null)
            {
                magicText.DOKill();
                magicText.transform.DOKill();
                magicText.color = _baseColor;
                magicText.transform.localScale = _baseScale;
            }
        }

        private void HandleMagicChanged(int usedMagic, int maxMagic)
        {
            Render(new MagicHudState(usedMagic, maxMagic));
        }

        public void Render(MagicHudState state)
        {
            magicText.text = string.Format(format, state.Available, state.Maximum);
            if (_hasValue && state.Used != _lastUsedMagic)
            {
                var released = state.Used < _lastUsedMagic;
                magicText.DOKill();
                magicText.transform.DOKill();
                magicText.color = released
                    ? new Color(0.35f, 1f, 0.88f, 1f)
                    : new Color(1f, 0.64f, 0.22f, 1f);
                magicText.DOColor(_baseColor, 0.5f).SetUpdate(true).SetLink(magicText.gameObject);
                magicText.transform.localScale = _baseScale;
                magicText.transform.DOPunchScale(Vector3.one * 0.12f, 0.28f, 6, 0.65f)
                    .SetUpdate(true).SetLink(magicText.gameObject);
            }

            _lastUsedMagic = state.Used;
            _hasValue = true;
        }
    }
}
