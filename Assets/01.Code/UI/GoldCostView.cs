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
        private int _pendingNet;
        private int _debt;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            DungeonHudStyle.ApplyPanel(gameObject);
            DungeonHudStyle.ApplyTopRightCard(gameObject, goldText, 0, new Color(1f, 0.7f, 0.2f, 1f));
            if (goldText == null)
                return;

            _baseColor = goldText.color;
            _baseScale = goldText.transform.localScale;
        }

        private void OnEnable()
        {
            costEventChannel.AddListener<GoldChangedEvent>(HandleGoldChanged);
            costEventChannel.AddListener<SettlementPreviewChangedEvent>(HandleSettlementPreview);
            costEventChannel.AddListener<DebtChangedEvent>(HandleDebtChanged);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<GoldChangedEvent>(HandleGoldChanged);
            costEventChannel.RemoveListener<SettlementPreviewChangedEvent>(HandleSettlementPreview);
            costEventChannel.RemoveListener<DebtChangedEvent>(HandleDebtChanged);
            ResetVisual();
        }

        private void HandleGoldChanged(GoldChangedEvent evt)
        {
            var delta = _hasValue ? evt.CurrentGold - _lastGold : 0;
            _lastGold = evt.CurrentGold;
            _hasValue = true;
            RefreshText();

            if (delta != 0)
                PlayChangeFeedback(delta);
        }

        private void HandleSettlementPreview(SettlementPreviewChangedEvent evt)
        {
            _pendingNet = evt.PendingNet;
            RefreshText();
        }

        private void HandleDebtChanged(DebtChangedEvent evt)
        {
            _debt = evt.CurrentDebt;
            RefreshText();
        }

        /// <summary>
        /// 보유 금화 아래에 정산 예정액과 빚을 덧붙인다.
        /// 웨이브 중에는 금화가 고정이라 예정액이 유일하게 움직이는 숫자다.
        /// </summary>
        private void RefreshText()
        {
            if (goldText == null)
                return;

            var text = string.Format(format, _lastGold);

            if (_pendingNet != 0)
            {
                var sign = _pendingNet > 0 ? "+" : "-";
                var color = _pendingNet > 0 ? "#5CE08A" : "#FF7A6B";
                text += $"\n<size=70%><color={color}>정산 예정 {sign}{Mathf.Abs(_pendingNet)}G</color></size>";
            }

            if (_debt > 0)
                text += $"\n<size=70%><color=#FF7A6B>부채 {_debt}G</color></size>";

            goldText.text = text;
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
