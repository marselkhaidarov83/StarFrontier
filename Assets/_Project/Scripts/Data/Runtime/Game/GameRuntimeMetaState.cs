using System;

[Serializable]
public class GameRuntimeMetaState
{
    /// <summary>
    /// Версия формата данных сохранения.
    ///
    /// 0 означает старое сохранение или состояние,
    /// которое ещё не проходило миграцию.
    /// </summary>
    public int SaveDataVersion;

    /// <summary>
    /// Счётчик фактических сохранений.
    ///
    /// Это поле уже существовало до S3-09.
    /// Не использовать его как версию схемы данных.
    /// </summary>
    public int SaveVersion = 1;

    public long CreatedUtcTicks = DateTime.UtcNow.Ticks;

    public long LastSaveUtc;

    public string LastSaveReason = "new_game";

    /// <summary>
    /// Текущий игровой день.
    /// Раньше в коде назывался CurrentQuantTick.
    /// В пользовательском интерфейсе показываем как "День".
    /// </summary>
    public int CurrentGameDay = 1;

    /// <summary>
    /// Сколько секунд симуляции прошло внутри текущего запуска/сохранения.
    /// </summary>
    public float GameSimulationTimeSeconds;

    /// <summary>
    /// Накопитель времени внутри текущего дня.
    /// Нужен, чтобы после загрузки день не начинался строго с нуля.
    /// </summary>
    public float GameTimeAccumulator;

    /// <summary>
    /// Был ли GameTimeService на паузе в момент сохранения.
    /// </summary>
    public bool IsGameTimePaused = true;
}