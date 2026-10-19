/// <summary>
/// A positive damage impact on a live player ship, before god-mode damage blocking.
/// Presentation notification only; does not mean hull or shield has changed.
/// </summary>
public readonly struct PlayerDamageImpactEvent2A
{
    public readonly string SystemId;
    public readonly int IncomingDamage;

    public PlayerDamageImpactEvent2A(string systemId, int incomingDamage)
    {
        SystemId = systemId ?? string.Empty;
        IncomingDamage = incomingDamage;
    }
}
