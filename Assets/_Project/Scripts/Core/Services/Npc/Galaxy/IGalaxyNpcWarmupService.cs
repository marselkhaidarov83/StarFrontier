using System.Collections;

public interface IGalaxyNpcWarmupService
{
    void RunInitialWarmup(string reason);

    IEnumerator RunInitialWarmupRoutine(
        string reason,
        float progressFrom01,
        float progressTo01);
}