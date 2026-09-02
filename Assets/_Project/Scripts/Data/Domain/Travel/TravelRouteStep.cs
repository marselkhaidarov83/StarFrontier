using UnityEngine;

public readonly struct TravelRouteStep
{
    public TravelRouteStep(
        TravelRouteStepType type,
        Vector3 position,
        Vector2 fromDirection,
        Vector2 toDirection,
        float distance)
    {
        Type = type;
        Position = position;
        FromDirection = fromDirection;
        ToDirection = toDirection;
        Distance = Mathf.Max(0f, distance);
    }

    public TravelRouteStepType Type { get; }
    public Vector3 Position { get; }
    public Vector2 FromDirection { get; }
    public Vector2 ToDirection { get; }
    public float Distance { get; }

    public static TravelRouteStep Move(
        Vector3 position,
        Vector2 direction,
        float distance)
    {
        return new TravelRouteStep(
            TravelRouteStepType.Move,
            position,
            direction,
            direction,
            distance);
    }

    public static TravelRouteStep TurnInPlace(
        Vector3 position,
        Vector2 fromDirection,
        Vector2 toDirection)
    {
        return new TravelRouteStep(
            TravelRouteStepType.TurnInPlace,
            position,
            fromDirection,
            toDirection,
            0f);
    }
}