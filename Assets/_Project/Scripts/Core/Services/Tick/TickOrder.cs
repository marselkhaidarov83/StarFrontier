/// <summary>
/// Единая таблица порядка обновления сервисов,
/// которые регистрируются в TickService.
///
/// Чем меньше значение, тем раньше сервис получает Tick.
/// </summary>
public static class TickOrder
{
    /// <summary>
    /// Внутриигровое время и симуляция галактики.
    ///
    /// Сейчас это единственный сервис,
    /// который реально регистрируется в S3-06.
    /// </summary>
    public const int GameTime = 0;

    /// <summary>
    /// Будущий ввод игрока.
    /// </summary>
    public const int PlayerInput = 100;

    /// <summary>
    /// Будущая обработка управления кораблём.
    /// </summary>
    public const int PlayerControl = 200;

    /// <summary>
    /// Будущее движение корабля.
    /// </summary>
    public const int ShipMovement = 300;

    /// <summary>
    /// Будущий выбор целей.
    /// </summary>
    public const int Targeting = 400;

    /// <summary>
    /// Будущие взаимодействия с объектами системы.
    /// </summary>
    public const int Interaction = 500;

    /// <summary>
    /// Будущая камера.
    /// </summary>
    public const int Camera = 600;

    /// <summary>
    /// Будущий игровой HUD.
    /// </summary>
    public const int Hud = 700;

    /// <summary>
    /// Будущая отладочная визуализация.
    /// </summary>
    public const int Debug = 900;
}