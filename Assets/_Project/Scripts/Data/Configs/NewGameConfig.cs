using UnityEngine;

// Конфиг NewGameConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "NewGameConfig", menuName = "StarFrontier/Configs/Game/New game")]
public class NewGameConfig : BaseConfig
{
    [Tooltip("Звёздная система, в которой начинается новая игра.")]
    public StarSystemConfig StartSystem;
    [Tooltip("Стартовый корабль игрока. Используется при создании состояния новой игры.")]
    public AllyConfig StarterShipConfig;
    [Tooltip("Стартовая позиция корабля игрока в системе при создании новой игры.")]
    public Vector3 StartShipPosition = new Vector3(0f, -360f, -2f);
    [Tooltip("Количество кредитов у игрока при начале новой игры.")]
    public int StartCredit = 1000;
    [Tooltip("Количество алмазов у игрока при начале обычной новой игры.")]
    [Min(0)] public int StartDiamonds = 10;
    [Tooltip("Текущий запас топлива у стартового корабля.")]
    public int CurrentFuel = 100;
    [Tooltip("Максимальный запас топлива у стартового корабля.")]
    public int FuelCapacity = 100;
}
