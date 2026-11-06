using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class StationCardService : IStationCardService
{
    private readonly IGameSessionService _session;
    private readonly IStationService _stations;
    private readonly Func<string, StarSystemConfig> _resolve;
    private readonly Func<Vector2> _position;
    private readonly Func<string, bool> _enemies;
    private readonly Func<IReadOnlyList<SectorConfig>> _sectors;
    private readonly Func<double> _researchPower;
    private readonly Func<IReadOnlyList<SystemNpcRuntimeState>> _npcs;
    private StationActionConfirmation _pending;
    private string _pendingSignature;
    private GameRuntimeState _pendingSession;
    public StationCardService() : this(Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IStationService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>().GetStarSystemConfigById,
        () => Bootstrapper.Instance.ServiceRegistry.Get<IShipMovementService>().State.Position,
        id => Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>().GetAliveNpcsInSystem(id).Any(n => n.IsEnemy || n.IsHostileToPlayer),
        () => Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>().GetAllSectors(),
        () => Bootstrapper.Instance.ServiceRegistry.Get<IStationInfrastructureService>().GetScienceRate(),
        () => Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>().Npcs) { }
    public StationCardService(IGameSessionService session, IStationService stations, Func<string, StarSystemConfig> resolve,
        Func<Vector2> position, Func<string, bool> enemies,
        Func<IReadOnlyList<SectorConfig>> sectors = null, Func<double> researchPower = null,
        Func<IReadOnlyList<SystemNpcRuntimeState>> npcs = null)
    { _session = session; _stations = stations; _resolve = resolve; _position = position; _enemies = enemies; _sectors = sectors; _researchPower = researchPower; _npcs = npcs; }
    public StationCardSnapshot GetCard(string systemId)
    {
        var state = _session.State;
        if (state?.Player == null || state.Player.CurrentSystemId != systemId ||
            !_stations.TryGetStation(systemId, out var station)) return null;
        var system = state.Galaxy?.Systems?.Find(x => x != null && x.SystemId == systemId);
        var config = _resolve(systemId)?.Station;
        if (system == null || config == null) return null;
        int galaxyLevel = Math.Max(1, Math.Min(10, state.Galaxy.Sectors?.Count(x => x != null && x.IsUnlocked) ?? 1));
        return new StationCardSnapshot { SystemId = systemId, Config = config, Station = station,
            Damage = system.InfrastructureDamage, HasEnemies = _enemies?.Invoke(systemId) == true,
            IsCaptured = system.SystemStatus == StarSystemStatus.Captured,
            IsInside = state.Player.CurrentStationId == station.ConfigId,
            Distance = Vector2.Distance(_position(), config.LocalOffset),
            GalaxyLevel = galaxyLevel,
            ResearchPower = station.StationType == StationType.Science ? _researchPower?.Invoke() ?? 0 : 0,
            MaximumResearchPower = station.StationType == StationType.Science ? StationResearchPowerRules.Maximum(state.Galaxy, _sectors?.Invoke()) : 0,
            MilitaryPopulation = station.StationType == StationType.Military ? StationMilitaryRules.AliveFromStation(_npcs?.Invoke(), systemId, station.ConfigId) : 0,
            MaximumMilitaryPopulation = station.StationType == StationType.Military ?
                StationMilitaryRules.MaximumCapacity(config.LevelEffects, galaxyLevel) : 0 };
    }
    public IReadOnlyList<StationActionOffer> GetOffers(string systemId)
    {
        var result = new List<StationActionOffer>(); var card = GetCard(systemId);
        if (card == null) return result;
        var kind = card.Station.IsDestroyed ? StationOperationKind.Recovery : card.Station.Level == 0
            ? StationOperationKind.Construction : StationOperationKind.Upgrade;
        AddOffers(result, card, kind);
        if (!card.Station.IsDestroyed && card.Station.Level > 0 && card.Damage > 0) AddOffers(result, card, StationOperationKind.Repair);
        return result;
    }
    private void AddOffers(List<StationActionOffer> result, StationCardSnapshot card, StationOperationKind kind)
    {
        if (kind == StationOperationKind.Upgrade && card.Station.Level >= 10) return;
        var options = card.Config.GetPaymentOptions(kind, card.Station.Level);
        foreach (var option in options)
        {
            if (option == null) continue;
            StationOperationRuntimeState quote = null;
            bool valid = kind == StationOperationKind.Repair
                ? StationInfrastructureRules.TryRepairQuote(card.Config, card.Station, StationInfrastructureRules.DamagePercent(card.Station, card.Damage), option.Id, out quote)
                : card.Config.TryCreateOperation(kind, card.Station.Level, option.Id, out quote);
            if (quote != null && kind == StationOperationKind.Recovery) quote.DurationSeconds = 0;
            var offer = new StationActionOffer { SystemId = card.SystemId, StationId = card.Station.ConfigId,
                Level = card.Station.Level, Revision = card.Station.Revision, Damage = card.Damage,
                Kind = kind, OptionId = option.Id, Quote = quote };
            offer.UnavailableReason = StationInterfaceRules.Availability(card, kind, valid ? quote : null);
            if (string.IsNullOrEmpty(offer.UnavailableReason) &&
                !StationPaymentRules.CanAfford(_session.State.Player, quote, out string reason)) offer.UnavailableReason = reason;
            result.Add(offer);
        }
        if (options.Length == 0) result.Add(new StationActionOffer { SystemId = card.SystemId, StationId = card.Station.ConfigId,
            Level = card.Station.Level, Revision = card.Station.Revision, Damage = card.Damage, Kind = kind,
            UnavailableReason = StationInterfaceRules.Availability(card, kind, null) });
    }
    public bool Prepare(string systemId, StationOperationKind kind, string optionId, out StationActionConfirmation confirmation, out string reason)
    {
        confirmation = null; CancelConfirmation();
        var offer = GetOffers(systemId).FirstOrDefault(x => x.Kind == kind && x.OptionId == optionId);
        reason = offer?.UnavailableReason ?? "InvalidPaymentOption";
        if (offer == null || !offer.Available) return false;
        _pending = new StationActionConfirmation { TicketId = Guid.NewGuid().ToString("N"), Offer = offer };
        _pendingSignature = offer.Signature(); _pendingSession = _session.State;
        // The caller receives a distinct offer; mutating the UI cannot alter the accepted quote.
        confirmation = new StationActionConfirmation { TicketId = _pending.TicketId, Offer = new StationActionOffer {
            SystemId = offer.SystemId, StationId = offer.StationId, Level = offer.Level, Revision = offer.Revision,
            Damage = offer.Damage, Kind = offer.Kind, OptionId = offer.OptionId, Quote = offer.Quote.Copy() } };
        reason = string.Empty; Log("PREPARED", systemId, kind.ToString()); return true;
    }
    public bool Confirm(string ticketId, out string reason)
    {
        reason = "ConfirmationExpired";
        if (_pending == null || _pending.TicketId != ticketId) return false;
        var pending = _pending; var signature = _pendingSignature; var session = _pendingSession; CancelConfirmation(); // Consume before any observer dispatch.
        if (!ReferenceEquals(session, _session.State)) { reason = "StationStateChanged"; return false; }
        var current = GetOffers(pending.Offer.SystemId).FirstOrDefault(x => x.Kind == pending.Offer.Kind && x.OptionId == pending.Offer.OptionId);
        if (current == null || current.Revision != pending.Offer.Revision || current.StationId != pending.Offer.StationId)
        { reason = "StationStateChanged"; Log("REJECTED", pending.Offer.SystemId, reason); return false; }
        if (current.Signature() != signature) { reason = "QuoteChanged"; Log("REJECTED", current.SystemId, reason); return false; }
        if (!current.Available) { reason = current.UnavailableReason; Log("REJECTED", current.SystemId, reason); return false; }
        bool ok = _stations.TryStartOperation(current.SystemId, current.Kind, current.OptionId, current.Level, current.Revision, out reason);
        Log(ok ? "COMMITTED" : "REJECTED", current.SystemId, ok ? current.Kind.ToString() : reason); return ok;
    }
    public void CancelConfirmation() { _pending = null; _pendingSignature = null; _pendingSession = null; }
    private static void Log(string action, string system, string detail) => AppLog.Info(DebugLogChannel.StationInterface,
        "[StationInterface] " + action + " | System=" + system + " | " + detail);
}
