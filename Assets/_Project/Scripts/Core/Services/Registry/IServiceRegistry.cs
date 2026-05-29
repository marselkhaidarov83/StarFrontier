public interface IServiceRegistry
{
    void Register<T>(T service);
    T Get<T>();
    bool TryGet<T>(out T service);
}