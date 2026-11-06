using System;

/// <summary>Repair/recovery have no duration setting. All accepted variants complete immediately.</summary>
[Serializable]
public sealed class StationInstantPaymentOption
{
    public string Id = string.Empty;
    public StationPaymentCost[] Costs = Array.Empty<StationPaymentCost>();
    public bool TryCreateSnapshot(StationOperationKind kind, int level, out StationOperationRuntimeState quote) =>
        new StationPaymentOption { Id = Id, Costs = Costs, DurationSeconds = 0 }.TryCreateSnapshot(kind, level, out quote);
}
