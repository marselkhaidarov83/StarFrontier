using System.Collections.Generic;

/// <summary>
/// Рассчитывает итоговые параметры корабля.
/// </summary>
public interface IShipStatsService
{
    /// <summary>
    /// Рассчитывает параметры только из базового ShipConfig.
    /// </summary>
    ShipFinalStats CalculateFromConfig(
        ShipConfig shipConfig);

    /// <summary>
    /// Рассчитывает параметры из ShipConfig
    /// и списка установленных модулей.
    /// </summary>
    ShipFinalStats Calculate(
        ShipConfig shipConfig,
        IEnumerable<ModuleConfig> equippedModules);
}