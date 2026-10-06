using System.Collections.Generic;
using UnityEngine;

// Конфиг описывает состав галактики: какие сектора входят в игру.
// Используется при создании состояния новой игры, построении карты галактики,
// маршрутов и списка систем.
[CreateAssetMenu(fileName = "GalaxyConfig", menuName = "StarFrontier/Configs/Galaxy/Galaxy")]
public class GalaxyConfig : BaseConfig
{
    [Tooltip(
        "Список секторов галактики. " +
        "Используется при создании состояния галактики, построении карты, маршрутов и поиске систем по их коду.")]
    public List<SectorConfig> Sectors = new();
}