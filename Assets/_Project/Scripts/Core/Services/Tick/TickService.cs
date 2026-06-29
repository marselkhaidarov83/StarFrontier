using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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

        if (IsTicking)
        {
            throw new InvalidOperationException(
                "TickService.Tick cannot be called recursively.");
        }

        if (_entries.Count == 0)
            return;

        IsTicking = true;

        _executionBuffer.Clear();
        _executionBuffer.AddRange(_entries);

        try
        {
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
                    continue;
                }

                if (!ReferenceEquals(
                        bufferedEntry,
                        activeEntry))
                {
                    continue;
                }

                bufferedEntry.Tickable.Tick(deltaTime);
            }
        }
        finally
        {
            _executionBuffer.Clear();
            IsTicking = false;
        }
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