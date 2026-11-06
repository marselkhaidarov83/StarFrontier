using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

public sealed class StationCardSnapshot
{
    public string SystemId;
    public StationConfig Config;
    public StationRuntimeState Station;
    public int Damage;
    public bool HasEnemies;
    public bool IsCaptured;
    public bool IsInside;
    public float Distance;
    public int GalaxyLevel;
    public double ResearchPower;
    public double MaximumResearchPower;
    public int MilitaryPopulation;
    public int MaximumMilitaryPopulation;
}
public sealed class StationActionOffer
{
    public string SystemId;
    public string StationId;
    public int Level;
    public int Damage;
    public long Revision;
    public StationOperationKind Kind;
    public string OptionId;
    public StationOperationRuntimeState Quote;
    public string UnavailableReason;
    public bool Available => string.IsNullOrEmpty(UnavailableReason) && Quote != null;
    public string Signature() => StationId + ":" + Kind + ":" + Level + ":" + Damage + ":" + OptionId + ":" +
        (Quote == null ? "missing" : Quote.TargetLevel + ":" + Quote.DurationSeconds.ToString("R", CultureInfo.InvariantCulture) + ":" +
        string.Join(";", (Quote.PaidCosts ?? Array.Empty<StationPaymentCostSnapshot>()).Select(c =>
            c == null ? "null" : c.Resource + ":" + c.ItemConfigId + ":" + c.Amount)));
}
public sealed class StationActionConfirmation
{
    public string TicketId;
    public StationActionOffer Offer;
}
public static class StationInterfaceRules
{
    public static string ResearchPowerText(StationCardSnapshot card) => "Сила исследований: " +
        card.ResearchPower.ToString("0.##", CultureInfo.CurrentCulture) + " / " +
        card.MaximumResearchPower.ToString("0.##", CultureInfo.CurrentCulture);
    public static string StationMetricText(StationCardSnapshot card)
    {
        if (card?.Station?.StationType == StationType.Science) return ResearchPowerText(card);
        if (card?.Station?.StationType == StationType.Military)
            return "Популяция военных: " + card.MilitaryPopulation + " / " + card.MaximumMilitaryPopulation;
        return string.Empty;
    }
    public static string Availability(StationCardSnapshot card, StationOperationKind kind, StationOperationRuntimeState quote)
    {
        if (card?.Station == null || card.Config == null || card.Station.ConfigMissing) return "StationConfigMissing";
        if (card.IsCaptured) return "SystemCaptured";
        if (kind != StationOperationKind.Repair && !StationLifecycleRules.CanBegin(card.Station, kind, out string reason)) return reason;
        if (kind == StationOperationKind.Repair && (!card.Station.IsOperational || card.Damage <= 0)) return "RepairNotRequired";
        if (!card.Station.IsEnabled) return "StationDisabled";
        if (kind != StationOperationKind.Upgrade && card.Distance >= card.Config.OperationInteractionDistance) return "ApproachRequired";
        if (quote == null) return "InvalidPaymentOption";
        var target = card.Config.LevelEffects?.GetLevel(Mathf.Max(1, quote.TargetLevel));
        if (target != null && card.GalaxyLevel < target.RequiredGalaxyLevel) return "GalaxyLevelRequired";
        return string.Empty;
    }
    public static string TypeName(StationType type)
    {
        switch (type)
        {
            case StationType.Military: return "Военная база";
            case StationType.RangerBase: return "База рейнджеров";
            case StationType.Trade: return "Торговая станция";
            case StationType.Science: return "Научная станция";
            case StationType.Medical: return "Медицинская станция";
            default: return "Станция";
        }
    }
    public static string ActionName(StationOperationKind kind)
    {
        switch (kind)
        {
            case StationOperationKind.Construction: return "Построить";
            case StationOperationKind.Upgrade: return "Улучшить";
            case StationOperationKind.Repair: return "Отремонтировать станцию";
            case StationOperationKind.Recovery: return "Восстановить станцию";
            default: return "Действие";
        }
    }
    public static string Reason(string reason)
    {
        switch (reason)
        {
            case "": case null: return string.Empty;
            case "ApproachRequired": return "Подлетите ближе к станции";
            case "NotEnoughCredits": return "Недостаточно кредитов";
            case "NotEnoughCargoSpace": return "Недостаточно места в трюме";
            case "ItemNotOwned": case "NotEnoughQuantity": return "Недостаточно товара для продажи";
            case "HullAlreadyFull": return "Корпус уже полностью восстановлен";
            case "RefuelUnavailable": return "Заправка сейчас недоступна или бак уже полон";
            case "NotInsideScienceStation": return "Исследование доступно внутри станции";
            case "ResearchBranchUnavailable": return "Этой станции доступно только её направление исследований";
            case "ResearchUnavailable": return "Исследование сейчас недоступно";
            case "ScienceStationUnavailable": return "Исследования доступны внутри действующей станции";
            case "ResearchStateChanged": return "Состояние исследования изменилось. Выберите действие снова";
            case "ResearchAlreadyRunning": return "В галактике уже выполняется исследование";
            case "DependenciesNotMet": return "Сначала завершите необходимые исследования";
            case "ScienceUnavailable": return "Нет действующих научных станций";
            case "NotEnoughResources": return "Недостаточно ресурсов";
            case "MaximumLevel": return "Достигнут максимальный уровень";
            case "DevelopmentAlreadyRunning": return "Строительство или улучшение уже выполняется";
            case "StationDestroyed": return "Сначала восстановите станцию";
            case "StationDamaged": return "Сначала полностью восстановите здоровье станции";
            case "SystemCaptured": return "Сначала освободите систему";
            case "GalaxyLevelRequired": return "Требуется более высокий уровень галактики";
            case "NotBuilt": return "Сначала постройте станцию";
            case "RepairNotRequired": return "Ремонт не требуется";
            case "StationStateChanged": case "QuoteChanged": return "Состояние или цена изменились. Выберите действие снова";
            case "ConfirmationExpired": return "Подтверждение уже использовано или закрыто";
            case "NotInsideStation": return "Услуга доступна внутри станции";
            case "StationConfigMissing": return "Настройки станции недоступны";
            case "InvalidPaymentOption": return "Варианты оплаты не настроены";
            case "StationDisabled": return "Станция недоступна";
            case "RecoveryAlreadyRunning": return "Восстановление уже выполняется";
            default: return "Действие сейчас недоступно";
        }
    }
}
