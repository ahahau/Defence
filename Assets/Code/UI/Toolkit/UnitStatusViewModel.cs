using UnityEngine;

namespace Code.UI.Toolkit
{
    /// <summary>부하 상태 패널에 그릴 값. 판단(회복·회수 가능 여부)은 넘기는 쪽이 끝내고 글과 상태만 담는다.</summary>
    public struct UnitStatusViewModel
    {
        public string Title;
        public string State;
        public string Hp;
        public float HpRatio;
        public string Combat;
        public string Command;
        public string Traits;
        public string TraitsTooltip;
        public string Fatigue;
        public float FatigueRatio;
        public Sprite Portrait;
        public string Hint;

        public bool ShowRecover;
        public bool RecoverEnabled;
        public string RecoverLabel;

        public bool ShowRecall;
        public bool RecallEnabled;
        public string RecallLabel;
    }
}
