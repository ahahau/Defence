using System.Collections.Generic;
using System.Text;
using Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 정산표의 수입 또는 지출 상세 한쪽. 줄마다 이름은 왼쪽, 금액은 오른쪽에 둔다.
    /// 수입은 초록, 지출은 빨강 — 금액을 읽기 전에 어느 쪽인지가 먼저 보여야 한다.
    /// </summary>
    public sealed class SettlementLedgerPane : MonoBehaviour
    {
        [SerializeField] private TMP_Text lines;
        [SerializeField] private TMP_Text total;
        [SerializeField] private ScrollRect scroll;
        [SerializeField, Tooltip("수입이면 참(+, 초록), 지출이면 거짓(-, 빨강)")] private bool isIncome = true;

        private readonly StringBuilder _builder = new();

        public void Render(IReadOnlyList<SettlementLedgerLine> ledger, int sum)
        {
            var sign = isIncome ? '+' : '-';
            var color = isIncome ? HudPalette.Gain : HudPalette.Loss;

            _builder.Clear();
            if (ledger.Count == 0)
            {
                _builder.Append("· 없음");
            }
            else
            {
                foreach (var line in ledger)
                {
                    // 한 줄에 왼쪽 이름과 오른쪽 금액을 겹쳐 쓴다(첫 칸은 줄 높이 0).
                    _builder.Append("<align=left>· ").Append(line.Label).Append("<line-height=0>\n")
                        .Append("<align=right>").Append(sign).Append(GoldText.Amount(line.Amount))
                        .Append("<line-height=1.5em>\n");
                }
            }

            lines.text = _builder.ToString();
            lines.color = color;
            total.text = $"합계 {sign}{GoldText.Amount(sum)}";
            total.color = color;
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }
    }
}
