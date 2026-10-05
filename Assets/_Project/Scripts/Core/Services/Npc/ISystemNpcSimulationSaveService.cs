public interface ISystemNpcSimulationSaveService
{
    SystemNpcSimulationSaveData Capture();

    SystemNpcSimulationCaptureSession BeginIncrementalCapture();

    bool ContinueIncrementalCapture(
        SystemNpcSimulationCaptureSession session,
        float budgetMs,
        int maxNpcItemsPerStep);

    void Restore(SystemNpcSimulationSaveData saveData);
}