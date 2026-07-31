using System.Collections.Generic;

public class GalaxyMapSystemSelectedEvent
{
    public string TargetSystemId { get; }
    public string CurrentSystemId { get; }
    public List<string> Path { get; }
    public string NextSystemId { get; }
    public TravelFailReason TravelFailReason { get; }

    public GalaxyMapSystemSelectedEvent(
        string targetSystemId,
        string currentSystemId,
        List<string> path,
        string nextSystemId,
        TravelFailReason travelFailReason)
    {
        TargetSystemId = targetSystemId;
        CurrentSystemId = currentSystemId;
        Path = path != null
            ? new List<string>(path)
            : new List<string>();
        NextSystemId = nextSystemId;
        TravelFailReason = travelFailReason;
    }
}