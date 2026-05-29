using System;

public static class StateFactory
{
    public static GameState CreateNewGameState(
        int saveVersion,
        string playerName,
        string startSystemId)
    {
        var now = DateTime.UtcNow.ToString("O");

        return new GameState
        {
            meta = new MetaStateU
            {
                saveVersion = saveVersion,
                createdAtUtc = now,
                lastSavedAtUtc = now,
                totalTicks = 0
            },
            player = new PlayerState
            {
                playerName = playerName,
                currentSystemId = startSystemId
            },
            currentSystem = new StarSystemRuntimeState
            {
                systemId = startSystemId,
                isLoaded = false
            }
        };
    }
}