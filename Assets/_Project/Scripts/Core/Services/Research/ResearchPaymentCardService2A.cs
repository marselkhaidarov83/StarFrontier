using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Adapts research quotes to the existing two-option station payment form, without starting a station upgrade.</summary>
public sealed class ResearchPaymentCardService2A : IStationCardService
{
    private readonly IResearchService _research;
    private readonly IStationCardService _stations;
    private readonly IGameSessionService _session;
    private readonly IStationInfrastructureService _infrastructure;
    private readonly string _systemId, _stationId;
    private readonly ResearchUpgrade _upgrade;
    private readonly GameRuntimeState _openedSession;
    private readonly int _level;
    private StationActionConfirmation _pending;
    public string Title => (ResearchRules.Find(_research.Tree, _upgrade)?.Title ?? "Исследование") + " (уровень " + (_level + 1) + ")";
    public Sprite Icon => ResearchRules.Find(_research.Tree, _upgrade)?.Icon;
    public double ScienceRate => _infrastructure.GetScienceRate();
    public ResearchPaymentCardService2A(IResearchService research, IStationCardService stations,
        IGameSessionService session, IStationInfrastructureService infrastructure, string systemId, ResearchUpgrade upgrade)
    {
        _research = research; _stations = stations; _session = session; _infrastructure = infrastructure;
        _systemId = systemId; _upgrade = upgrade; _openedSession = session.State;
        _level = research.GetSnapshot()?.GetLevel(upgrade) ?? 10;
        _stationId = stations.GetCard(systemId)?.Station?.ConfigId;
    }
    public bool CanOpen(StationCardSnapshot card)
    {
        if (!ReferenceEquals(_openedSession, _session.State) || card?.Station == null || !card.IsInside ||
            card.Station.ConfigId != _stationId || !card.Station.CanDock || card.Station.ConfigMissing || card.IsCaptured ||
            !_research.TryEnterStation(_systemId, out _)) return false;
        var state = _research.GetSnapshot(); var node = ResearchRules.Find(_research.Tree, _upgrade);
        var branch = _research.Tree?.Branches?.FirstOrDefault(b => b != null && (b.Nodes ?? Array.Empty<ResearchNode>()).Contains(node));
        return state != null && node != null && state.Active == null && state.GetLevel(_upgrade) == _level && _level < 10 &&
            card.GalaxyLevel >= ResearchRules.RequiredGalaxyLevel(node, _level + 1) &&
            ResearchRules.VisibleNodes(branch, card.GalaxyLevel).Contains(node) &&
            ResearchRules.CanAccessUpgrade(_research.Tree, card.Station.StationType, _upgrade);
    }
    public StationCardSnapshot GetCard(string systemId) => systemId == _systemId ? _stations.GetCard(systemId) : null;
    public IReadOnlyList<StationActionOffer> GetOffers(string systemId)
    {
        var result = new List<StationActionOffer>(); var card = GetCard(systemId);
        if (!CanOpen(card)) return result;
        var state = _research.GetSnapshot(); var node = ResearchRules.Find(_research.Tree, _upgrade);
        var next = node?.Levels?.FirstOrDefault(l => l != null && l.Level == _level + 1);
        double rate = ScienceRate;
        foreach (var option in next?.PaymentOptions ?? Array.Empty<StationPaymentOption>())
        {
            if (option == null) continue;
            bool valid = ResearchRules.TryQuote(_research.Tree, state, _upgrade, option.Id, out var quote, out string reason, card.GalaxyLevel);
            if (valid && rate <= 0) { valid = false; reason = "ScienceUnavailable"; }
            if (valid) valid = StationPaymentRules.CanAfford(_session.State.Player, quote, out reason);
            result.Add(new StationActionOffer { SystemId = _systemId, StationId = _stationId, Level = _level,
                Revision = state.Revision, Damage = card.Damage, Kind = StationOperationKind.Upgrade,
                OptionId = option.Id, Quote = quote, UnavailableReason = valid ? string.Empty : reason });
        }
        return result;
    }
    public bool Prepare(string systemId, StationOperationKind kind, string optionId,
        out StationActionConfirmation confirmation, out string reason)
    {
        confirmation = null; CancelConfirmation(); reason = "ResearchUnavailable";
        if (kind != StationOperationKind.Upgrade) return false;
        var offer = GetOffers(systemId).FirstOrDefault(o => o.OptionId == optionId);
        if (offer == null || !offer.Available) { reason = offer?.UnavailableReason ?? reason; return false; }
        _pending = new StationActionConfirmation { TicketId = Guid.NewGuid().ToString("N"), Offer = Copy(offer) };
        confirmation = new StationActionConfirmation { TicketId = _pending.TicketId, Offer = Copy(offer) };
        reason = string.Empty; return true;
    }
    public bool Confirm(string ticketId, out string reason)
    {
        reason = "ConfirmationExpired";
        if (_pending == null || _pending.TicketId != ticketId) return false;
        var accepted = _pending; CancelConfirmation();
        var current = GetOffers(_systemId).FirstOrDefault(o => o.OptionId == accepted.Offer.OptionId);
        if (current == null || current.Revision != accepted.Offer.Revision || current.Signature() != accepted.Offer.Signature())
        { reason = "ResearchStateChanged"; return false; }
        if (!current.Available) { reason = current.UnavailableReason; return false; }
        return _research.TryStart(_systemId, _upgrade, current.OptionId, accepted.Offer.Revision, out reason);
    }
    public void CancelConfirmation() { _pending = null; }
    private static StationActionOffer Copy(StationActionOffer source) => new StationActionOffer {
        SystemId = source.SystemId, StationId = source.StationId, Level = source.Level, Damage = source.Damage,
        Revision = source.Revision, Kind = source.Kind, OptionId = source.OptionId,
        Quote = source.Quote?.Copy(), UnavailableReason = source.UnavailableReason };
}
