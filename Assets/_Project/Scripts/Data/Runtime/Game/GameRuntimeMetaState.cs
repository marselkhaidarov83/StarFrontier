using System;

[Serializable]
public class GameRuntimeMetaState
{
    public int SaveVersion = 1;
    public long CreatedUtcTicks = DateTime.UtcNow.Ticks;
    public long LastSaveUtc;
    public string LastSaveReason = "new_game";
}