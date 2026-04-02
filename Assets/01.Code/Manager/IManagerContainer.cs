namespace _01.Code.Manager
{
    public interface IManagerContainer
    {
        T GetManager<T>() where T : class, IManageable;
    }
}
