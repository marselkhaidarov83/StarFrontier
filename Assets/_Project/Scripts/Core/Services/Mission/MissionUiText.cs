using System;

/// <summary>Player-facing text for the planet board and the system-map mission helper.</summary>
public static class MissionUiText
{
    public static string Title(MissionInstanceData mission, IConfigService config)
    {
        if (mission == null) return "Миссия";
        if (mission.MissionType == MissionType.Delivery &&
            !string.IsNullOrWhiteSpace(mission.TargetPlanetId) &&
            !string.IsNullOrWhiteSpace(mission.TargetSystemId))
            return "Доставка: " + SystemName(config, mission.TargetSystemId) +
                   " -> " + PlanetName(config, mission.TargetPlanetId);
        if (mission.MissionType == MissionType.Defense)
            return "Защита: " + SystemName(config, mission.TargetSystemId);
        return mission.Title ?? "Миссия";
    }

    public static string Description(MissionInstanceData mission, IConfigService config)
    {
        if (mission == null) return string.Empty;
        string targetSystem = SystemName(config, mission.TargetSystemId);
        switch (mission.MissionType)
        {
            case MissionType.Delivery:
                string item = ItemName(config, mission.Objective?.MissionCargoItemId);
                string planet = PlanetName(config, mission.TargetPlanetId);
                int amount = Math.Max(1, mission.Objective?.MissionCargoAmount ?? 1);
                int volume = Math.Max(0, mission.Objective?.MissionCargoVolume ?? 0);
                return "Доставьте груз «" + item + "» (" + amount + " шт.) на планету " +
                       planet + " системы " + targetSystem + ". Груз займёт " + volume + " ед. в трюме.";
            case MissionType.Recon:
                return "Галактики ведёт войну, и ей нужна помощь. " +
                       "Просканируйте " + ShipCount(Math.Max(0, mission.Objective?.RequiredAmount ?? 0)) +
                       " в системе " + targetSystem + ".";
            case MissionType.Defense:
                return "Система " + targetSystem + " защищена от врагов. Ваш личный вклад в защиту засчитан.";
            case MissionType.Liberation:
                return "Система " + targetSystem + " освобождена от врагов. Ваш личный вклад в освобождение засчитан.";
            case MissionType.RouteSearch:
                return mission.Objective != null && mission.Objective.RouteIsUpgrade
                    ? "Галактика нуждается в более дешевых маршрутах перелета. " +
                      "Соберите навигационную информацию, долетев прямым маршрутом и маршрутом через третью систему до системы " + targetSystem + "."
                    : "Галактика нуждается в новых маршрутах перелета. " +
                      "Соберите навигационную информацию, долетев любым маршрутом до системы " + targetSystem + ".";
            default:
                return mission.Description ?? string.Empty;
        }
    }

    public static string NextStep(MissionInstanceData mission, IConfigService config,
        PlayerState player = null)
    {
        if (mission == null) return string.Empty;
        if (mission.Status == MissionStatus.Available)
        {
            if (mission.MissionType == MissionType.Delivery)
                return "Примите миссию";
            if (mission.MissionType == MissionType.Recon)
                return "Примите миссию";
            if (mission.MissionType == MissionType.RouteSearch)
                return "Примите миссию";
            if (string.IsNullOrWhiteSpace(mission.SourcePlanetId) ||
                string.IsNullOrWhiteSpace(mission.SourceSystemId))
                return "Примите миссию.";
            return "Примите миссию у правительства планеты " +
                   PlanetName(config, mission.SourcePlanetId) + " системы " +
                   SystemName(config, mission.SourceSystemId) + ".";
        }
        if (mission.Status == MissionStatus.ReadyToComplete || mission.IsReadyToTurnIn)
        {
            if (mission.MissionType == MissionType.RouteSearch &&
                mission.Objective?.RouteIsUpgrade != true)
                return AtSourcePlanet(mission, player) ? "Заберите награду" :
                    ReturnToSourcePlanet(mission, config);
            if (mission.MissionType == MissionType.Delivery)
                return "Заберите награду";
            if (mission.MissionType == MissionType.Recon)
                return AtSourcePlanet(mission, player) ? "Заберите награду" :
                    ReturnToSourcePlanet(mission, config);
            if (mission.MissionType == MissionType.RouteSearch)
                return "Заберите награду на планете " + PlanetName(config, mission.SourcePlanetId) +
                       " системы " + SystemName(config, mission.SourceSystemId) + ".";
            if (MissionBattleRules.IsBattle(mission.MissionType))
            {
                if (player != null && player.CurrentSystemId == mission.TargetSystemId &&
                    !string.IsNullOrWhiteSpace(player.CurrentPlanetId) &&
                    config?.GetPlanetConfigById(player.CurrentPlanetId)?.IsInhabited == true)
                    return "Заберите награду";
                return "Заберите награду на ближайшей населённой планете системы " +
                       SystemName(config, mission.TargetSystemId) + ".";
            }
        }

        switch (mission.MissionType)
        {
            case MissionType.Delivery:
                return "Летите на планету " + PlanetName(config, mission.TargetPlanetId) +
                       " системы " + SystemName(config, mission.TargetSystemId) + ".";
            case MissionType.Recon:
                if (IsReconSurveying(mission, player))
                    return "Просканируйте " + ShipCount(Math.Max(0, mission.Objective.RequiredAmount)) +
                           ". Информация получена " +
                           KnownShips(Math.Max(0, mission.Objective.CurrentAmount)) + ".";
                return "Летите в систему " + SystemName(config, mission.TargetSystemId) + ".";
            case MissionType.RouteSearch:
                string target = SystemName(config, mission.TargetSystemId);
                if (mission.Objective != null && mission.Objective.RouteIsUpgrade)
                {
                    if (mission.Objective.RouteIndirectSurveyed && mission.Objective.RouteDirectSurveyed)
                        return ReturnToSourcePlanet(mission, config);
                    if (!mission.Objective.RouteDirectSurveyed)
                        return "Летите в систему " + target + " прямым маршрутом.";
                    return mission.Objective.RouteReturnedAfterDirectSurvey
                        ? "Летите в систему " + target + " маршрутом через третью систему."
                        : "Вернитесь в систему " + SystemName(config, mission.SourceSystemId) + ".";
                }
                if (mission.Objective?.RouteIndirectSurveyed == true)
                    return ReturnToSourcePlanet(mission, config);
                return "Летите в систему " + target + ".";
            default:
                return "Летите в систему " + SystemName(config, mission.TargetSystemId) + ".";
        }
    }

    public static string RewardStepOnPlanet(MissionInstanceData mission, IConfigService config)
    {
        if (mission == null) return string.Empty;
        string targetSystem = SystemName(config, mission.TargetSystemId);
        switch (mission.MissionType)
        {
            case MissionType.RouteSearch:
                return mission.Objective?.RouteIsUpgrade == true
                    ? "Новые навигационные данные помогут нам всем тратить меньше топлива при перелете в систему " +
                      targetSystem + ". Заберите свою награду"
                    : "Новые навигационные данные помогут нам всем открыть новый прямой маршрут в систему " +
                      targetSystem + ". Заберите свою награду";
            case MissionType.Recon:
                return "Новые разведданные помогут нам лучше подготовиться к освобождению системы " +
                       targetSystem + ". Заберите свою награду";
            case MissionType.Delivery:
                return "Вы нас очень выручили, так быстро доставив эти товары. Заберите свою награду";
            case MissionType.Defense:
                return "Вы нас очень выручили, оказав помощь в защите системы от врагов. " +
                       "Мы можем продолжать жить полной жизнью. Заберите свою награду";
            case MissionType.Liberation:
                return "Вы нас очень выручили, оказав помощь в освобождении системы от врагов. " +
                       "Мы можем вновь начать жить полной жизнью. Заберите свою награду";
            default:
                return "Заберите награду";
        }
    }

    public static string Goal(MissionInstanceData mission, IConfigService config)
    {
        if (mission == null) return string.Empty;
        bool reward = mission.Status == MissionStatus.ReadyToComplete || mission.IsReadyToTurnIn;
        if (reward && (mission.MissionType == MissionType.Recon || mission.MissionType == MissionType.RouteSearch))
            return "Планета " + PlanetName(config, mission.SourcePlanetId);
        if (mission.MissionType == MissionType.Delivery)
            return "Планета " + PlanetName(config, mission.TargetPlanetId);
        if (reward && MissionBattleRules.IsBattle(mission.MissionType))
            return "Система " + SystemName(config, mission.TargetSystemId);
        return "Система " + SystemName(config, mission.TargetSystemId);
    }

    private static bool AtSourcePlanet(MissionInstanceData mission, PlayerState player) =>
        player != null && mission.SourceSystemId == player.CurrentSystemId &&
        mission.SourcePlanetId == player.CurrentPlanetId;

    public static bool IsReconSurveying(MissionInstanceData mission, PlayerState player) =>
        mission?.MissionType == MissionType.Recon && mission.Objective != null &&
        mission.Objective.CurrentAmount < mission.Objective.RequiredAmount &&
        mission.Objective.ReconIsInsideTarget && player != null &&
        mission.TargetSystemId == player.CurrentSystemId;

    private static string ShipCount(int count)
    {
        int lastTwo = count % 100;
        int last = count % 10;
        string word = lastTwo >= 11 && lastTwo <= 14 ? "кораблей" :
            last == 1 ? "корабль" :
            last >= 2 && last <= 4 ? "корабля" : "кораблей";
        return count + " " + word;
    }

    private static string KnownShips(int count)
    {
        bool singular = count % 10 == 1 && count % 100 != 11;
        return (singular || count % 100 == 11 ? "об " : "о ") + count +
               (singular ? " корабле" : " кораблях");
    }

    private static string ReturnToSourcePlanet(MissionInstanceData mission, IConfigService config) =>
        "Вернитесь на планету " + PlanetName(config, mission.SourcePlanetId) +
        " системы " + SystemName(config, mission.SourceSystemId) +
        " для получения награды.";

    public static string Deadline(MissionInstanceData mission, int currentDay)
    {
        if (mission == null || mission.DurationDays <= 0) return string.Empty;
        if (mission.Status == MissionStatus.Available)
            return "Срок после принятия: " + mission.DurationDays + " дн.";
        if (mission.ExpiresAtDay <= 0 || MissionLifecycleRules.IsTerminal(mission.Status))
            return string.Empty;
        return "До истечения: " + Math.Max(0, mission.ExpiresAtDay - currentDay) + " дн.";
    }

    public static string Reward(MissionInstanceData mission)
    {
        if (mission?.Reward == null) return "Ожидаемая награда: —";
        string label = mission.Status == MissionStatus.ReadyToComplete
            ? "Награда: " : "Ожидаемая награда: ";
        return label + mission.Reward.Credits + " кр. · " + mission.Reward.Xp + " опыта";
    }

    public static string SystemName(IConfigService config, string id)
    {
        var item = config?.GetStarSystemConfigById(id);
        return !string.IsNullOrWhiteSpace(item?.DisplayName) ? item.DisplayName : (id ?? "неизвестная система");
    }

    public static string PlanetName(IConfigService config, string id)
    {
        var item = config?.GetPlanetConfigById(id);
        return !string.IsNullOrWhiteSpace(item?.DisplayName) ? item.DisplayName : (id ?? "неизвестная планета");
    }

    private static string ItemName(IConfigService config, string id)
    {
        var item = config?.GetItemConfigById(id);
        return !string.IsNullOrWhiteSpace(item?.DisplayName) ? item.DisplayName : "ценный груз";
    }
}
