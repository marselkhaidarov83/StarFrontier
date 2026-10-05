using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Единый покадровый планировщик обычных C#-объектов.
/// Не зависит от Bootstrapper, CustomService и Unity-сцены.
/// </summary>
public sealed class TickService : ITickService
{
    private readonly List<TickEntry> _entries = new();
    private readonly List<TickEntry> _executionBuffer = new();

    private readonly Dictionary<ITickable, TickEntry> _entriesByTickable =
        new(TickableReferenceComparer.Instance);

    private long _nextRegistrationSequence;

    public int Count => _entriesByTickable.Count;

    public bool IsTicking { get; private set; }

    public void Register(ITickable tickable, int order = 0)
    {
        if (tickable == null)
            throw new ArgumentNullException(nameof(tickable));

        if (_entriesByTickable.ContainsKey(tickable))
        {
            throw new InvalidOperationException(
                $"Tickable of type {tickable.GetType().Name} " +
                "is already registered.");
        }

        TickEntry entry = new TickEntry(
            tickable,
            order,
            _nextRegistrationSequence++);

        _entriesByTickable.Add(tickable, entry);
        _entries.Add(entry);

        _entries.Sort(TickEntryComparer.Instance);
    }

    public bool Unregister(ITickable tickable)
    {
        if (tickable == null)
            return false;

        if (!_entriesByTickable.TryGetValue(
                tickable,
                out TickEntry entry))
        {
            return false;
        }

        _entriesByTickable.Remove(tickable);
        _entries.Remove(entry);

        return true;
    }

    public bool Contains(ITickable tickable)
    {
        return tickable != null
            && _entriesByTickable.ContainsKey(tickable);
    }

    public void Tick(float deltaTime)
    {
        ValidateDeltaTime(deltaTime);

        double tickStartedAt =
            Time.realtimeSinceStartupAsDouble;

        double copyBufferMs = 0.0;
        double entriesTotalMs = 0.0;
        double maxEntryMs = 0.0;

        int executedCount = 0;
        int skippedMissingCount = 0;
        int skippedReplacedCount = 0;

        string maxEntryType = string.Empty;
        string maxEntryOrder = string.Empty;

        if (IsTicking)
        {
            throw new InvalidOperationException(
                "TickService.Tick cannot be called recursively.");
        }

        if (_entries.Count == 0)
            return;

        IsTicking = true;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            _executionBuffer.Clear();
            _executionBuffer.AddRange(_entries);

            copyBufferMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            for (int index = 0;
                 index < _executionBuffer.Count;
                 index++)
            {
                TickEntry bufferedEntry =
                    _executionBuffer[index];

                if (!_entriesByTickable.TryGetValue(
                        bufferedEntry.Tickable,
                        out TickEntry activeEntry))
                {
                    skippedMissingCount++;
                    continue;
                }

                if (!ReferenceEquals(
                        bufferedEntry,
                        activeEntry))
                {
                    skippedReplacedCount++;
                    continue;
                }

                double entryStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                bufferedEntry.Tickable.Tick(deltaTime);

                double entryMs =
                    (Time.realtimeSinceStartupAsDouble - entryStartedAt) * 1000.0;

                entriesTotalMs += entryMs;
                executedCount++;

                if (entryMs > maxEntryMs)
                {
                    maxEntryMs = entryMs;
                    maxEntryType = bufferedEntry.Tickable.GetType().Name;
                    maxEntryOrder = bufferedEntry.Order.ToString();
                }

                LogTickEntryIfSlow(
                    bufferedEntry,
                    entryMs,
                    deltaTime,
                    index);
            }
        }
        finally
        {
            _executionBuffer.Clear();
            IsTicking = false;

            double totalMs =
                (Time.realtimeSinceStartupAsDouble - tickStartedAt) * 1000.0;

            LogTickSummaryIfSlow(
                totalMs,
                copyBufferMs,
                entriesTotalMs,
                maxEntryMs,
                maxEntryType,
                maxEntryOrder,
                executedCount,
                skippedMissingCount,
                skippedReplacedCount,
                deltaTime);
        }
    }
    private static void LogTickEntryIfSlow(
    TickEntry entry,
    double entryMs,
    float deltaTime,
    int index)
    {
        if (entryMs < 1.0)
            return;

        Bootstrapper bootstrapper =
            Bootstrapper.Instance;

        if (bootstrapper == null ||
            !bootstrapper.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        bootstrapper.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[TICK_SERVICE_ENTRY]" +
            " UnityFrame=" + Time.frameCount +
            " | Ms=" + entryMs.ToString("F2") +
            " | Tickable=" + entry.Tickable.GetType().Name +
            " | Order=" + entry.Order +
            " | Index=" + index +
            " | DeltaTime=" + deltaTime.ToString("F4"));
    }

    private static void LogTickSummaryIfSlow(
        double totalMs,
        double copyBufferMs,
        double entriesTotalMs,
        double maxEntryMs,
        string maxEntryType,
        string maxEntryOrder,
        int executedCount,
        int skippedMissingCount,
        int skippedReplacedCount,
        float deltaTime)
    {
        if (totalMs < 1.0)
            return;

        Bootstrapper bootstrapper =
            Bootstrapper.Instance;

        if (bootstrapper == null ||
            !bootstrapper.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        bootstrapper.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[TICK_SERVICE_SUMMARY]" +
            " UnityFrame=" + Time.frameCount +
            " | Ms=" + totalMs.ToString("F2") +
            " | CopyBufferMs=" + copyBufferMs.ToString("F3") +
            " | EntriesTotalMs=" + entriesTotalMs.ToString("F3") +
            " | MaxEntryMs=" + maxEntryMs.ToString("F3") +
            " | MaxEntryType=" + (maxEntryType ?? string.Empty) +
            " | MaxEntryOrder=" + (maxEntryOrder ?? string.Empty) +
            " | ExecutedCount=" + executedCount +
            " | SkippedMissingCount=" + skippedMissingCount +
            " | SkippedReplacedCount=" + skippedReplacedCount +
            " | DeltaTime=" + deltaTime.ToString("F4"));
    }

    public void Clear()
    {
        _entriesByTickable.Clear();
        _entries.Clear();

        if (!IsTicking)
            _executionBuffer.Clear();
    }

    private static void ValidateDeltaTime(float deltaTime)
    {
        if (float.IsNaN(deltaTime)
            || float.IsInfinity(deltaTime)
            || deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaTime),
                deltaTime,
                "Delta time must be a finite non-negative value.");
        }
    }

    private sealed class TickEntry
    {
        public TickEntry(
            ITickable tickable,
            int order,
            long registrationSequence)
        {
            Tickable = tickable;
            Order = order;
            RegistrationSequence = registrationSequence;
        }

        public ITickable Tickable { get; }

        public int Order { get; }

        public long RegistrationSequence { get; }
    }

    private sealed class TickEntryComparer :
        IComparer<TickEntry>
    {
        public static readonly TickEntryComparer Instance = new();

        public int Compare(
            TickEntry left,
            TickEntry right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return -1;

            if (right == null)
                return 1;

            int orderComparison =
                left.Order.CompareTo(right.Order);

            if (orderComparison != 0)
                return orderComparison;

            return left.RegistrationSequence.CompareTo(
                right.RegistrationSequence);
        }
    }

    private sealed class TickableReferenceComparer :
        IEqualityComparer<ITickable>
    {
        public static readonly TickableReferenceComparer Instance =
            new();

        public bool Equals(
            ITickable left,
            ITickable right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(ITickable tickable)
        {
            return RuntimeHelpers.GetHashCode(tickable);
        }
    }
}