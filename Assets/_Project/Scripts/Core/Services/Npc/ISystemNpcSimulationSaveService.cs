public interface ISystemNpcSimulationSaveService
    {
        SystemNpcSimulationSaveData Capture();
        void Restore(SystemNpcSimulationSaveData saveData);
    }