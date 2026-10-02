using System.Collections.Generic;
using UnityEngine;

public static class VisualUpdateAggregateLog
{
    private sealed class Aggregate
    {
        public int Frame = -1;
        public int Count;
        public double TotalMs;
        public double MaxSingleMs;
        public string MaxDetails = string.Empty;
    }

    private static readonly Dictionary<string, Aggregate> AggregatesByMarker =
        new Dictionary<string, Aggregate>();

    public static void Record(
        string marker,
        double elapsedMs,
        string details)
    {
        if (string.IsNullOrWhiteSpace(marker))
            return;

        int frame = Time.frameCount;

        if (!AggregatesByMarker.TryGetValue(marker, out Aggregate aggregate))
        {
            aggregate = new Aggregate();
            AggregatesByMarker.Add(marker, aggregate);
        }

        if (aggregate.Frame != frame)
        {
            Flush(marker, aggregate);
            Reset(aggregate, frame);
        }

        aggregate.Count++;
        aggregate.TotalMs += elapsedMs;

        if (elapsedMs > aggregate.MaxSingleMs)
        {
            aggregate.MaxSingleMs = elapsedMs;
            aggregate.MaxDetails = details ?? string.Empty;
        }
    }

    public static void FlushAll()
    {
        foreach (KeyValuePair<string, Aggregate> pair in AggregatesByMarker)
            Flush(pair.Key, pair.Value);

        int frame = Time.frameCount;

        foreach (Aggregate aggregate in AggregatesByMarker.Values)
            Reset(aggregate, frame);
    }

    private static void Reset(Aggregate aggregate, int frame)
    {
        aggregate.Frame = frame;
        aggregate.Count = 0;
        aggregate.TotalMs = 0.0;
        aggregate.MaxSingleMs = 0.0;
        aggregate.MaxDetails = string.Empty;
    }

    private static void Flush(string marker, Aggregate aggregate)
    {
        if (aggregate.Frame < 0 || aggregate.Count <= 0)
            return;

        if (!VisualUpdatePerfLog.ShouldLog(aggregate.TotalMs))
            return;

        VisualUpdatePerfLog.LogMeasured(
            marker + ".Aggregate",
            aggregate.TotalMs,
            "AggregateFrame=" + aggregate.Frame +
            " | Count=" + aggregate.Count +
            " | MaxSingleMs=" + aggregate.MaxSingleMs.ToString("F3") +
            " | MaxDetails=" + aggregate.MaxDetails);
    }
}