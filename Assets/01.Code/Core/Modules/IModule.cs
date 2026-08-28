namespace _01.Code.Core.Modules
{
    /// <summary>
    /// 소유자(ModuleOwner) 밑에 붙어 한 가지 일만 맡는 조각.
    ///
    /// 여태 Defence의 엔티티는 Enemy·Unit이 Combatant·Health·Mover를 전부
    /// [SerializeField]로 직접 들고 있었다. 프리팹마다 배선을 손으로 맞춰야 했고,
    /// 하나를 빼먹으면 런타임에 NullReference로만 드러났다.
    /// 모듈은 자식으로 붙어 있기만 하면 소유자가 알아서 찾아 물린다.
    /// </summary>
    public interface IModule
    {
        void Initialize(ModuleOwner owner);
    }
}
