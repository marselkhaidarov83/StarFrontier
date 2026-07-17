using System.Collections.Generic;
using UnityEngine;

public enum TravelRoutePreviewDotType2A
{
    Small,
    BigTick
}

public readonly struct TravelRoutePreviewDot2A
{
    public readonly Vector3 Position;
    public readonly TravelRoutePreviewDotType2A Type;
    public readonly int TickIndex;

    public TravelRoutePreviewDot2A(
        Vector3 position,
        TravelRoutePreviewDotType2A type,
        int tickIndex
    )
    {
        Position = position;
        Type = type;
        TickIndex = tickIndex;
    }
}

public sealed class TravelRoutePreview2A
{
    private readonly List<TravelRoutePreviewDot2A> _dots =
        new List<TravelRoutePreviewDot2A>();

    public List<TravelRoutePreviewDot2A> Dots => _dots;

    public int BigDotCount { get; private set; }
    public int SmallDotCount { get; private set; }

    public bool HasDots => _dots.Count > 0;

    public void AddBigDot(Vector3 position, int tickIndex)
    {
        _dots.Add(new TravelRoutePreviewDot2A(
            position,
            TravelRoutePreviewDotType2A.BigTick,
            tickIndex
        ));

        BigDotCount++;
    }

    public void AddSmallDot(Vector3 position, int tickIndex)
    {
        _dots.Add(new TravelRoutePreviewDot2A(
            position,
            TravelRoutePreviewDotType2A.Small,
            tickIndex
        ));

        SmallDotCount++;
    }

    public void Clear()
    {
        _dots.Clear();
        BigDotCount = 0;
        SmallDotCount = 0;
    }
}