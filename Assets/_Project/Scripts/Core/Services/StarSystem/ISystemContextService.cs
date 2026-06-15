public interface ISystemContextService
{
    string CurrentSystemId { get; }

    StarSystemRuntimeState GetCurrentSystemState();
    StarSystemRuntimeState GetSystemState(string systemId);
    void RefreshCurrentSystem();
}