using UnityEngine;

namespace Code.UI.ViewModels
{
    /// <summary>결과 화면 "새로 열림" 한 줄. 이름·종류와 아이콘(없으면 빈 칸).</summary>
    public sealed class RunEndUnlockViewModel
    {
        public string Name;
        public string Detail;
        public Sprite Icon;
        /// <summary>줄이 모자라 남은 개수만 적는 마지막 줄("+2개 더").</summary>
        public bool IsMore;
    }
}
