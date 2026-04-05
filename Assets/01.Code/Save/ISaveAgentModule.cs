using _01.Code.Manager;

namespace _01.Code.Save
{
    public interface ISaveAgentModule
    {
        int Order { get; }
        void Configure(SaveManager saveManager);
    }
}
