using System.Collections.Generic;

namespace Code.UI.ViewModels
{
    /// <summary>정산표 "오늘 정산" 칸 전체 값(<see cref="SettlementOverviewPresenter"/>가 만든다).</summary>
    public sealed class SettlementOverviewViewModel
    {
        public string Title;
        /// <summary>제목 오른쪽 "빚 · 청산일까지 N일".</summary>
        public string Status;
        /// <summary>빚이 있으면 경고 색으로 칠한다.</summary>
        public bool StatusWarn;
        public string IncomeTotal;
        public string ExpenseTotal;
        /// <summary>낮에 이미 운영 자금에 반영됐거나 금고로 간 몫. 없으면 "없음".</summary>
        public string Deferred;
        public string Remaining;
        /// <summary>정산으로 운영 자금이 실제 움직인 액수(보고서의 Net).</summary>
        public string NetChange;
        public bool NetPositive;
        /// <summary>수입·지출을 한 장부로(리치 텍스트, 이름 왼쪽·금액 오른쪽).</summary>
        public string Ledger;
        public readonly List<SettlementChipViewModel> Chips = new();
        public string UnlockSummary;
        /// <summary>"목록"을 펼치면 보이는 몬스터·시설 해금 전체(리치 텍스트).</summary>
        public string UnlockList;
    }
}
