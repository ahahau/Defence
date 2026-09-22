using System.Collections.Generic;

namespace Code.Manager
{
    /// <summary>정산표의 한 줄. 어떤 화면이 그리든 이름과 금액만 갖는다.</summary>
    public readonly struct SettlementLedgerLine
    {
        public SettlementLedgerLine(string label, int amount)
        {
            Label = label;
            Amount = amount;
        }

        public string Label { get; }
        public int Amount { get; }
    }

    /// <summary>
    /// 정산표 아래에 붙는 설명 한 줄의 성격.
    /// 색을 여기서 정하지 않는 이유는 UGUI 리치 텍스트와 UI Toolkit이 색을 다루는 방식이 달라서다.
    /// 어느 쪽이든 이 성격만 보고 자기 방식으로 칠한다.
    /// </summary>
    public enum SettlementNoteTone
    {
        Neutral,
        Good,
        Caution,
        Danger,
        Accent
    }

    /// <summary>정산표 아래에 붙는 설명 한 줄.</summary>
    public readonly struct SettlementNote
    {
        public SettlementNote(string text, SettlementNoteTone tone = SettlementNoteTone.Neutral)
        {
            Text = text;
            Tone = tone;
        }

        public string Text { get; }
        public SettlementNoteTone Tone { get; }
    }

    /// <summary>오늘 내보냈던 부하 한 명의 피로.</summary>
    public readonly struct SettlementFatigueLine
    {
        /// <summary>이 피로부터는 정산에서 눈에 띄게 표시한다. 100이면 탈진.</summary>
        public const int WarningFatigue = 70;
        public const int ExhaustedFatigue = 100;

        public SettlementFatigueLine(string label, int fatigue)
        {
            Label = label;
            Fatigue = fatigue;
        }

        public string Label { get; }
        public int Fatigue { get; }

        public bool IsExhausted => Fatigue >= ExhaustedFatigue;
        public bool IsTired => Fatigue >= WarningFatigue;
    }

    /// <summary>해금 로드맵의 한 줄. 이미 열었으면 조건 문구는 비어 있다.</summary>
    public readonly struct SettlementUnlockLine
    {
        public SettlementUnlockLine(string name, bool isUnlocked, string hint)
        {
            Name = name;
            IsUnlocked = isUnlocked;
            Hint = hint;
        }

        public string Name { get; }
        public bool IsUnlocked { get; }
        public string Hint { get; }
    }

    /// <summary>
    /// 몬스터 또는 시설의 해금 현황 한 묶음.
    ///
    /// 총계는 해금 카탈로그로 센다. 표시용 목록으로 세면 실제보다 적은 수가 나온다.
    /// </summary>
    public sealed class SettlementUnlockSection
    {
        public SettlementUnlockSection(
            string title,
            string unlockedStateLabel,
            int unlockedCount,
            int totalCount,
            IReadOnlyList<SettlementUnlockLine> lines)
        {
            Title = title;
            UnlockedStateLabel = unlockedStateLabel;
            UnlockedCount = unlockedCount;
            TotalCount = totalCount;
            Lines = lines;
        }

        public string Title { get; }

        /// <summary>이미 연 항목 뒤에 붙는 말. 부하는 "고용 가능", 시설은 "설치 가능".</summary>
        public string UnlockedStateLabel { get; }

        public int UnlockedCount { get; }
        public int TotalCount { get; }
        public IReadOnlyList<SettlementUnlockLine> Lines { get; }
    }

    /// <summary>
    /// 하루 정산의 결과 전체. 정산 관리자가 만들고 화면은 읽기만 한다.
    ///
    /// 화면마다 장부를 다시 읽으면 같은 하루를 두 가지로 설명하게 된다. 표시 문구를
    /// 여기 한 번만 만들어 두고, UGUI든 UI Toolkit이든 이 보고서만 그린다.
    /// </summary>
    public sealed class SettlementReport
    {
        public SettlementReport(
            int day,
            IReadOnlyList<SettlementLedgerLine> income,
            IReadOnlyList<SettlementLedgerLine> expense,
            int totalIncome,
            int totalExpense,
            int net,
            IReadOnlyList<SettlementFatigueLine> fatigue,
            IReadOnlyList<SettlementNote> notes,
            SettlementUnlockSection unitUnlocks,
            SettlementUnlockSection buildingUnlocks)
        {
            UnitUnlocks = unitUnlocks;
            BuildingUnlocks = buildingUnlocks;
            Day = day;
            Income = income;
            Expense = expense;
            TotalIncome = totalIncome;
            TotalExpense = totalExpense;
            Net = net;
            Fatigue = fatigue;
            Notes = notes;
        }

        public int Day { get; }
        public IReadOnlyList<SettlementLedgerLine> Income { get; }
        public IReadOnlyList<SettlementLedgerLine> Expense { get; }
        public int TotalIncome { get; }
        public int TotalExpense { get; }

        /// <summary>운영 자금이 실제로 움직인 액수. 내일 쓸 수 있는 돈은 이 값이다.</summary>
        public int Net { get; }

        /// <summary>표시 합계와 실제 이동의 차이. 금고에 쌓였거나 이미 선지출된 몫이다.</summary>
        public int DeferredDelta => TotalIncome - TotalExpense - Net;

        public IReadOnlyList<SettlementFatigueLine> Fatigue { get; }
        public IReadOnlyList<SettlementNote> Notes { get; }

        /// <summary>몬스터 해금 현황. 고용 명단을 아직 못 읽었으면 줄이 비어 있다.</summary>
        public SettlementUnlockSection UnitUnlocks { get; }

        /// <summary>시설 해금 현황.</summary>
        public SettlementUnlockSection BuildingUnlocks { get; }
    }
}
