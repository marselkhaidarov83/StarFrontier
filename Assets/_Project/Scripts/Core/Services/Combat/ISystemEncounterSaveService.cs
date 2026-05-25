    public interface ISystemEncounterSaveService
    {
        SystemEncounterSaveData Capture();
        void Restore(SystemEncounterSaveData saveData);
    }