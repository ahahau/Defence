namespace Code.UI.ViewModels
{
    /// <summary>유물 줄 설명 카드 값: 이름·대상·설명·조합 줄(성립 금색, 아직이면 흐리게 "짝 필요").</summary>
    public sealed class ArtifactDetailViewModel
    {
        public string Name;
        public string Target;
        public string Description;
        public string Combos;
    }
}
