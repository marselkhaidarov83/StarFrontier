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
}
