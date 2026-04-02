namespace _01.Code.Manager
{
    public interface ISaveAgentModule
    {
        int Order { get; }
        void Configure(SaveManager saveManager);
    }
}
