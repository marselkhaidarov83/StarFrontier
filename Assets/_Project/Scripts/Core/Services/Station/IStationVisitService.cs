public interface IStationVisitService
{
    void InitializeSession();
    bool TryEnter(string systemId, out string reason);
    bool TryTakeOff(out string reason);
    void Leave(bool forced = false);
    void Tick();
}
