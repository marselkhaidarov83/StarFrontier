using UnityEngine;

public readonly struct SystemTravelMathResult
{
    public readonly Vector3 NewPosition;
    public readonly float Progress01;
    public readonly bool Arrived;

    public SystemTravelMathResult(
        Vector3 newPosition,
        float progress01,
        bool arrived)
    {
        NewPosition = newPosition;
        Progress01 = progress01;
        Arrived = arrived;
    }
}