using System.Collections.Generic;

/// <summary>
/// Рассчитывает итоговые параметры корабля.
/// </summary>
public interface IShipStatsService
{
    /// <summary>
    /// Рассчитывает параметры только из базового AllyConfig.
    /// </summary>
    ShipFinalStats CalculateFromConfig(
        AllyConfig allyConfig);

    /// <summary>
    /// Рассчитывает параметры из AllyConfig
    /// и списка установленных модулей.
    /// </summary>
    ShipFinalStats Calculate(
        AllyConfig allyConfig,
        IEnumerable<ModuleConfig> equippedModules);
}