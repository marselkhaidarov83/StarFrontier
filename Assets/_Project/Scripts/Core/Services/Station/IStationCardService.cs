using System.Collections.Generic;
public interface IStationCardService
{
    StationCardSnapshot GetCard(string systemId);
    IReadOnlyList<StationActionOffer> GetOffers(string systemId);
    bool Prepare(string systemId, StationOperationKind kind, string optionId, out StationActionConfirmation confirmation, out string reason);
    bool Confirm(string ticketId, out string reason);
    void CancelConfirmation();
}
