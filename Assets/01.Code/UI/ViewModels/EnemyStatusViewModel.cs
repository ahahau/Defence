namespace Code.UI.ViewModels
{
    /// <summary>모험가 상태 창에 그릴 값. 상태이상은 최대 <see cref="MaxEffects"/>줄이고, 넘치면 마지막 줄이 "외 N개"다.</summary>
    public struct EnemyStatusViewModel
    {
        public const int MaxEffects = 4;

        public bool IsValid;
        public string Title;
        public string State;
        public string Hp;
        public string Attack;
        public string Purpose;
        public string Party;
        public string Mood;
        public string Satisfaction;
        public int EffectCount;
        public string Effect0;
        public string Effect1;
        public string Effect2;
        public string Effect3;

        public string EffectAt(int index) => index switch
        {
            0 => Effect0,
            1 => Effect1,
            2 => Effect2,
            3 => Effect3,
            _ => string.Empty
        };
    }
}
