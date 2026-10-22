using System.Collections.Generic;

namespace Code.UI.ViewModels
{
    /// <summary>판 종료 결과 화면 전체 값(<see cref="RunEndPresenter"/>가 만든다).</summary>
    public sealed class RunEndViewModel
    {
        public bool Victory;
        public string Title;
        public string Headline;
        public string Days;
        /// <summary>버틴 날이 최고 기록을 넘겼다("최고 기록" 칩).</summary>
        public bool NewBest;
        public string BestNote;
        public string StatLabels;
        public string StatValues;
        public string ProgressGain;
        public string ProgressRange;
        /// <summary>진행도 막대에서 이번 판 전까지 찬 몫.</summary>
        public float BeforeRatio;
        /// <summary>진행도 막대에서 이번 판까지 더해 찬 몫.</summary>
        public float AfterRatio;
        public string ProgressNote;
        public readonly List<RunEndUnlockViewModel> Unlocks = new();
        /// <summary>새로 열린 것이 없을 때 목록 자리에 쓰는 말. 있으면 비운다.</summary>
        public string UnlockEmpty;
    }
}
