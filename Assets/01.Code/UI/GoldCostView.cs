using _01.Code.Events;
using _01.Code.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public class GoldCostView : MonoBehaviour
    {
        [SerializeField]
        private GameEventChannelSO costEventChannel;

        [SerializeField]
        private TMP_Text goldText;

        [SerializeField]
        private string format = "운영 자금 {0}G";

        private int _lastGold;
        private bool _hasValue;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            NestHudStyle.ApplyPanel(gameObject);
            NestHudStyle.ApplyTopRightCard(gameObject, goldText, 0, new Color(1f, 0.7f, 0.2f, 1f));
            if (goldText == null)
                return;

            _baseColor = goldText.color;
            _baseScale = goldText.transform.localScale;
        }

        private void OnEnable()
        {
            costEventChannel.AddListener<GoldChangedEvent>(HandleGoldChanged);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<GoldChangedEvent>(HandleGoldChanged);
            ResetVisual();
        }
        
        private void HandleGoldChanged(GoldChangedEvent evt)
        {
            goldText.text = string.Format(format, evt.CurrentGold);
            if (_hasValue && evt.CurrentGold != _lastGold)
                PlayChangeFeedback(evt.CurrentGold - _lastGold);

            _lastGold = evt.CurrentGold;
            _hasValue = true;
        }

        private void PlayChangeFeedback(int delta)
        {
            if (goldText == null)
                return;

            var accent = delta > 0
                ? new Color(0.36f, 1f, 0.52f, 1f)
                : new Color(1f, 0.32f, 0.28f, 1f);
            goldText.DOKill();
            goldText.transform.DOKill();
            goldText.color = accent;
            goldText.DOColor(_baseColor, 0.55f).SetUpdate(true).SetLink(goldText.gameObject);
            goldText.transform.localScale = _baseScale;
            goldText.transform.DOPunchScale(Vector3.one * 0.16f, 0.32f, 7, 0.7f)
                .SetUpdate(true).SetLink(goldText.gameObject);
        }

        private void ResetVisual()
        {
            if (goldText == null)
                return;

            goldText.DOKill();
            goldText.transform.DOKill();
            goldText.color = _baseColor;
            goldText.transform.localScale = _baseScale;
        }
    }
}
