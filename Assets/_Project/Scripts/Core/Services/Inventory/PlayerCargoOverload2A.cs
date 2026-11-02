public static class PlayerCargoOverload2A
{
    public static bool IsOverloaded()
    {
        if (Bootstrapper.Instance?.ServiceRegistry == null ||
            !Bootstrapper.Instance.ServiceRegistry.TryGet<IInventoryService>(out var inventory))
            return false;
        return inventory.GetFreeCargo() < 0;
    }

    public static bool RejectRouteIfOverloaded()
    {
        if (!IsOverloaded()) return false;
        if (Bootstrapper.Instance.ServiceRegistry.TryGet<SimpleEventBus>(out var events))
            events.Publish(new SpaceLootFeedbackEvent2A("Трюм переполнен. Полет невозможен"));
        AppLog.Info(DebugLogChannel.Loot, "[LOOT_OVERLOAD_ROUTE_REJECTED]");
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.CargoOverload, false, "CargoOverloaded");
        return true;
    }
}
