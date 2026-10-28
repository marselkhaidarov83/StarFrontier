/// <summary>Raised when a system enters Captured so mission outcomes are recorded immediately.</summary>
public readonly struct SystemCapturedMissionEvent
{
    public readonly string SystemId;

    public SystemCapturedMissionEvent(string systemId) { SystemId = systemId; }
}
