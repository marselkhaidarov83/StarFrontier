using System.Collections.Generic;
using UnityEngine;

public sealed class TravelRoutePlan
{
    private readonly List<TravelRouteStep> _steps =
        new List<TravelRouteStep>(128);

    private readonly List<Vector3> _samplePath =
        new List<Vector3>(256);

    public IReadOnlyList<TravelRouteStep> Steps => _steps;
    public IReadOnlyList<Vector3> SamplePath => _samplePath;

    public bool HasTurnInPlaceAtStart =>
        _steps.Count > 0 &&
        _steps[0].Type == TravelRouteStepType.TurnInPlace;

    public void Clear()
    {
        _steps.Clear();
        _samplePath.Clear();
    }

    public void AddTurnInPlace(
        Vector3 position,
        Vector2 fromDirection,
        Vector2 toDirection)
    {
        _steps.Add(
            TravelRouteStep.TurnInPlace(
                position,
                fromDirection,
                toDirection));

        if (_samplePath.Count == 0)
            _samplePath.Add(position);
    }

    public void SetMovePath(
        IReadOnlyList<Vector3> path)
    {
        _samplePath.Clear();

        if (path == null)
            return;

        for (int i = 0; i < path.Count; i++)
            _samplePath.Add(path[i]);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from =
                path[i - 1];

            Vector3 to =
                path[i];

            Vector3 segment =
                to - from;

            Vector2 direction =
                new Vector2(
                    segment.x,
                    segment.y);

            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            _steps.Add(
                TravelRouteStep.Move(
                    to,
                    direction.normalized,
                    segment.magnitude));
        }
    }
}