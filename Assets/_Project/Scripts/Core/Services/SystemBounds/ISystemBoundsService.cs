using UnityEngine;

/// <summary>
/// Управляет runtime-границами локальной сцены системы.
/// </summary>
public interface ISystemBoundsService
{
    SystemBoundsRuntimeState State { get; }

    void InitializeFromConfig();

    void SetBounds(
        Vector2 center,
        Vector2 halfSize,
        bool isEnabled);

    Vector2 ClampPosition(Vector2 position);

    bool Contains(Vector2 position);

    void ResetAll();
}