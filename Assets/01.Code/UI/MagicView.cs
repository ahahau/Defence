using _01.Code.Core;
using _01.Code.Events;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public class MagicView : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text magicText;
        [SerializeField] private string format = "주둔 마력 {0}/{1}";

        private int _lastUsedMagic;
        private bool _hasValue;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            DungeonHudStyle.ApplyPanel(panelRoot != null ? panelRoot : gameObject);
            DungeonHudStyle.ApplyTopRightCard(panelRoot != null ? panelRoot : gameObject, magicText, 1,
                new Color(0.48f, 0.4f, 1f, 1f));
            if (magicText == null)
                return;
            _baseColor = magicText.color;
            _baseScale = magicText.transform.localScale;
        }

        private void OnEnable()
        {
            costEventChannel.AddListener<MagicChangedEvent>(HandleMagicChanged);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<MagicChangedEvent>(HandleMagicChanged);
            if (magicText != null)
            {
                magicText.DOKill();
                magicText.transform.DOKill();
                magicText.color = _baseColor;
                magicText.transform.localScale = _baseScale;
            }
        }

        private void HandleMagicChanged(MagicChangedEvent evt)
        {
            var availableMagic = Mathf.Max(0, evt.MaxMagic - evt.UsedMagic);
            magicText.text = string.Format(format, availableMagic, evt.MaxMagic);
            if (_hasValue && evt.UsedMagic != _lastUsedMagic)
            {
                var released = evt.UsedMagic < _lastUsedMagic;
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

            _lastUsedMagic = evt.UsedMagic;
            _hasValue = true;
        }
    }
}
