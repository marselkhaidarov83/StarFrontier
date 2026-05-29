    public interface IStarSystemService
    {
        StarSystemState GetSystem(string systemId);
        StarSystemState GetCurrentSystem();
    }