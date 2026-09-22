using Code.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Blade.Core
{
    public class GoldCostView : MonoBehaviour
    {
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
            DungeonHudIcon.Attach(gameObject, goldText, DungeonHudIcon.Skin != null ? DungeonHudIcon.Skin.GoldIcon : null);
            if (goldText == null)
                return;

            _baseColor = goldText.color;
            _baseScale = goldText.transform.localScale;
        }

        private void OnDisable()
        {
            ResetVisual();
        }

        public void Render(Code.UI.GoldHudState state)
        {
            if (goldText == null)
                return;

            var text = string.Format(format, state.Gold);

            if (state.PendingNet != 0)
            {
                var sign = state.PendingNet > 0 ? "+" : "-";
                var color = state.PendingNet > 0 ? "#5CE08A" : "#FF7A6B";
                text += $"\n<size=70%><color={color}>정산 예정 {sign}{Mathf.Abs(state.PendingNet)}G</color></size>";
            }

            if (state.Debt > 0)
                text += $"\n<size=70%><color={ResolveDebtColor(state.DaysUntilSettlement)}>{BuildDebtLine(state)}</color></size>";

            goldText.text = text;

            var delta = _hasValue ? state.Gold - _lastGold : 0;
            _lastGold = state.Gold;
            _hasValue = true;
            if (delta != 0)
                PlayChangeFeedback(delta);
        }

        private static string BuildDebtLine(Code.UI.GoldHudState state)
        {
            var due = state.WeeklyDue > 0 ? state.WeeklyDue : state.Debt;
            var daysLeft = state.DaysUntilSettlement;

            if (daysLeft <= 0)
                return $"빚 {state.Debt}G · 오늘 {due}G 청산";

            return daysLeft == 1
                ? $"빚 {state.Debt}G · 내일 {due}G 청산"
                : $"빚 {state.Debt}G · {daysLeft}일 뒤 {due}G 청산";
        }

        /// <summary>마감이 가까울수록 붉어진다. 하루 남았을 때와 닷새 남았을 때가 같아 보이면 예고가 아니다.</summary>
        private static string ResolveDebtColor(int daysLeft)
        {
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
