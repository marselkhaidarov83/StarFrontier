using System;

public enum StationOperationKind { None = 0, Construction = 1, Upgrade = 2, Recovery = 3, Repair = 4 }
public enum StationOperationPauseReason { None = 0, Destroyed = 1, UnderAttack = 2, Damaged = 3 }
public enum StationPaymentResource { Credits = 0, Astralites = 1, Diamonds = 2 }

[Serializable]
public sealed class StationPaymentCostSnapshot
{
    public StationPaymentResource Resource;
    public string ItemConfigId = string.Empty;
    public int Amount;

    public StationPaymentCostSnapshot Copy() => new StationPaymentCostSnapshot
    { Resource = Resource, ItemConfigId = ItemConfigId, Amount = Amount };
}

/// <summary>Owned snapshot of the accepted quote. Asset edits never reprice a running operation.</summary>
[Serializable]
public sealed class StationOperationRuntimeState
{
    public string OperationId = string.Empty;
    public StationOperationKind Kind;
    public int TargetLevel;
    public string PaymentOptionId = string.Empty;
    public StationPaymentCostSnapshot[] PaidCosts = Array.Empty<StationPaymentCostSnapshot>();
    public double DurationSeconds;
    public double ElapsedSeconds;
    public StationOperationPauseReason PauseReason;

    public StationOperationRuntimeState Copy()
    {
        var copy = (StationOperationRuntimeState)MemberwiseClone();
        copy.PaidCosts = new StationPaymentCostSnapshot[PaidCosts?.Length ?? 0];
        for (int i = 0; i < copy.PaidCosts.Length; i++)
            copy.PaidCosts[i] = PaidCosts[i]?.Copy();
        return copy;
    }
}

/// <summary>One station per StarSystemRuntimeState, serialized inside the galaxy save part.</summary>
[Serializable]
public sealed class StationRuntimeState
{
    public string ConfigId = string.Empty;
    public StationType StationType;
    public int Level;
    public bool IsEnabled = true;
    public bool IsDestroyed;
    public bool ConfigMissing;
    public long Revision;
    // Legacy fractional repair credit: read for old saves, no longer accumulated by tick repair.
    public double RepairDamageCredit;
    // Absolute health is authoritative after migration. InfrastructureDamage is a UI percentage cache.
    public int HealthVersion;
    public int HealthLevel;
    public int MaxHealth;
    public int Health;

    // Construction/upgrade survives destruction and recovery cancellation.
    public StationOperationRuntimeState Development;
    // Recovery has its own payment/progress and may be discarded on capture.
    public StationOperationRuntimeState Recovery;

    public bool IsOperational => IsEnabled && Level > 0 && !IsDestroyed;
    public bool CanDock => IsOperational && Recovery == null;
    public bool CanBeAttacked => IsOperational && Recovery == null;

    public StationRuntimeState Copy()
    {
        var copy = (StationRuntimeState)MemberwiseClone();
        copy.Development = Development?.Copy();
        copy.Recovery = Recovery?.Copy();
        return copy;
    }
}
