/// <summary>
/// Изменение списка/состояния выходов на карте системы; обновление визуализации карты.
/// </summary>
public sealed class ExitMapChangedEvent
{
    /// <summary>Ссылка на связанную звёздную систему.</summary>
    public StarSystemLink StarSystemLink;
    public ExitMapChangedEvent(StarSystemLink starSystemLink)
    {
        StarSystemLink = starSystemLink;
    }
}