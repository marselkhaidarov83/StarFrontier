using System;

[Serializable]
public enum MissionType
{
    // Saved numeric values must never be reused or reordered.
    Delivery = 0,
    Elimination = 1, // Legacy only. Generation and acceptance are disabled.
    Recon = 2,
    Defense = 3,
    Liberation = 4,
    RouteSearch = 5
}
