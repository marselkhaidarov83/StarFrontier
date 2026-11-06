public sealed class StationResourcePaymentGateway : IStationPaymentGateway
{
    private readonly IGameSessionService _session;
    public StationResourcePaymentGateway(IGameSessionService session) { _session = session; }
    public bool TryPay(string systemId, StationOperationRuntimeState quote, out string reason) =>
        StationPaymentRules.TrySpend(_session.State?.Player, quote, out reason);
}
