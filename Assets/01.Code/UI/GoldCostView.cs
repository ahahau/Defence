using _01.Code.Events;
using _01.Code.Core;
using _01.Code.Manager;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public class GoldCostView : MonoBehaviour
    {
        [SerializeField]
        private GameEventChannelSO costEventChannel;

        [SerializeField, Tooltip("청산일까지 며칠 남았는지 세려면 날짜가 바뀌는 것을 알아야 한다.")]
        private GameEventChannelSO dayEventChannel;

        [SerializeField]
        private TMP_Text goldText;

        [SerializeField]
        private string format = "운영 자금 {0}G";

        private int _lastGold;
        private bool _hasValue;
        private int _pendingNet;
        private int _debt;
        private int _weeklyDue;
        private int _currentDay;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            DungeonHudStyle.ApplyPanel(gameObject);
            DungeonHudStyle.ApplyTopRightCard(gameObject, goldText, 0, new Color(1f, 0.7f, 0.2f, 1f));
            DungeonHudIcon.Attach(gameObject, goldText, DungeonHudIcon.Skin != null ? DungeonHudIcon.Skin.GoldIcon : null);
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
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<GoldChangedEvent>(HandleGoldChanged);
            costEventChannel.RemoveListener<SettlementPreviewChangedEvent>(HandleSettlementPreview);
            costEventChannel.RemoveListener<DebtChangedEvent>(HandleDebtChanged);
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
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
            _weeklyDue = evt.WeeklyDue;
            RefreshText();
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            _currentDay = evt.Day;
            RefreshText();
        }

        /// <summary>
        /// 보유 금화 아래에 정산 예정액과 빚을 덧붙인다.
        /// 웨이브 중에는 금화가 고정이라 예정액이 유일하게 움직이는 숫자다.
        ///
        /// 빚은 액수만으로는 판단할 수 없다. 청산일에 이자까지 얹어 한 번에 내야 하므로,
        /// 며칠 남았고 그때 얼마가 되는지를 같이 봐야 오늘 쓸 돈을 정할 수 있다.
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
                text += $"\n<size=70%><color={ResolveDebtColor()}>{BuildDebtLine()}</color></size>";

            goldText.text = text;
        }

        private string BuildDebtLine()
        {
            var due = _weeklyDue > 0 ? _weeklyDue : _debt;
            var daysLeft = DayManager.DaysUntilSettlementFrom(_currentDay);

            if (daysLeft <= 0)
                return $"빚 {_debt}G · 오늘 {due}G 청산";

            return daysLeft == 1
                ? $"빚 {_debt}G · 내일 {due}G 청산"
                : $"빚 {_debt}G · {daysLeft}일 뒤 {due}G 청산";
        }

        /// <summary>마감이 가까울수록 붉어진다. 하루 남았을 때와 닷새 남았을 때가 같아 보이면 예고가 아니다.</summary>
        private string ResolveDebtColor()
        {
            var daysLeft = DayManager.DaysUntilSettlementFrom(_currentDay);
            if (daysLeft <= 0)
                return "#FF4A3A";

            return daysLeft <= 2 ? "#FF7A6B" : "#E0B070";
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
