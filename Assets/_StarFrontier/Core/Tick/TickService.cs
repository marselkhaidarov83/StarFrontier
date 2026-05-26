using System.Collections.Generic;
using UnityEngine;

public sealed class TickService
{
    private readonly List<ITickable> _tickables = new();

    public bool IsRunning { get; private set; }

    public int RegisteredCount => _tickables.Count;

    public void Register(ITickable tickable)
    {
        if (tickable == null)
        {
            Debug.LogWarning("TickService: null tickable skipped.");
            return;
        }

        if (_tickables.Contains(tickable))
        {
            Debug.LogWarning($"TickService: tickable {tickable.GetType().Name} already registered.");
            return;
        }

        _tickables.Add(tickable);
    }

    public void Unregister(ITickable tickable)
    {
        if (tickable == null)
        {
            return;
        }

        _tickables.Remove(tickable);
    }

    public void Start()
    {
        IsRunning = true;
    }

    public void Stop()
    {
        IsRunning = false;
    }

    public void Tick(float deltaTime)
    {
        if (!IsRunning)
        {
            return;
        }

        for (int i = 0; i < _tickables.Count; i++)
        {
            _tickables[i].Tick(deltaTime);
        }
    }

    public void Clear()
    {
        _tickables.Clear();
    }
}