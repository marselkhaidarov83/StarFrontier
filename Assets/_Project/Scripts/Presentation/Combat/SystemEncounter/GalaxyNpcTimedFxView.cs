using UnityEngine;

public sealed class GalaxyNpcTimedFxView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 0.35f;

    private float _remainingSeconds;
    private GalaxyNpcCombatVisualController _owner;

    public bool IsActive { get; private set; }

    public void Init(
        GalaxyNpcCombatVisualController owner,
        Vector3 position,
        float lifetimeOverrideSeconds)
    {
        _owner = owner;
        _remainingSeconds = lifetimeOverrideSeconds > 0f
            ? lifetimeOverrideSeconds
            : lifetimeSeconds;

        IsActive = true;
        transform.position = position;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public void Tick(float deltaTime)
    {
        if (!IsActive)
            return;

        _remainingSeconds -= deltaTime;

        if (_remainingSeconds > 0f)
            return;

        Complete();
    }

    public void Complete()
    {
        if (!IsActive)
            return;

        IsActive = false;
        gameObject.SetActive(false);

        if (_owner != null)
            _owner.ReturnFxToPool(this);
    }
}