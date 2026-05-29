using UnityEngine;

public sealed class CoreArchitectureTestTickable : ITickable
{
    private float _elapsedTime;

    public void Tick(float deltaTime)
    {
        _elapsedTime += deltaTime;

        if (_elapsedTime >= 1f)
        {
            _elapsedTime = 0f;
            Debug.Log("CoreArchitectureTestTickable: tick");
        }
    }
}