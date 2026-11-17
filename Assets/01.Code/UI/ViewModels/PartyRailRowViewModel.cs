using Code.Enemies;

namespace Code.UI.ViewModels
{
    /// <summary>HUD 우측 파티 목록 한 줄. 파티 줄은 누르면 <see cref="Target"/>의 상태 패널을 연다.</summary>
    public readonly struct PartyRailRowViewModel
    {
        public PartyRailRowViewModel(string name, string risk, Enemy target = null, bool alert = false)
        {
            Name = name ?? string.Empty;
            Risk = risk ?? string.Empty;
            Target = target;
            Alert = alert;
        }

        public string Name { get; }
        public string Risk { get; }

        /// <summary>누르면 상태를 보여 줄 모험가(파티 리더). 안내 줄이면 null.</summary>
        public Enemy Target { get; }

        /// <summary>보스·엘리트처럼 눈여겨볼 파티인가.</summary>
        public bool Alert { get; }
    }
}
