public interface IStationService
{
    void InitializeSession(bool newGame);
    bool TryGetStation(string systemId, out StationRuntimeState snapshot);
    bool TryStartOperation(string systemId, StationOperationKind kind, string paymentOptionId,
        int expectedLevel, long expectedRevision, out string reason);
    void ReconcileSystem(string systemId);
    void AdvanceTime(double seconds);
    void ProcessOffline(GameRuntimeState state, long nowUtcTicks);
}

/// <summary>
/// Integration point for S07-03's atomic wallet/inventory transaction. A missing gateway denies new operations.
/// Implementations must validate/spend the whole quote atomically, synchronously and without re-entering world mutations.
/// </summary>
public interface IStationPaymentGateway
{
    bool TryPay(string systemId, StationOperationRuntimeState quote, out string reason);
}
