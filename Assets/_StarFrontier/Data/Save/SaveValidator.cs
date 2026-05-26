using System;

public sealed class SaveValidator
{
    public GameState CreateNewState()
    {
        var now = DateTime.UtcNow.ToString("O");

        return new GameState
        {
            meta = new MetaState
            {
                saveVersion = 1,
                createdAtUtc = now,
                lastSavedAtUtc = now,
                totalTicks = 0
            },
            player = new PlayerState
            {
                playerName = "Captain",
                currentSystemId = "system_start_01"
            },
            currentSystem = new StarSystemRuntimeState
            {
                systemId = "system_start_01",
                isLoaded = false
            }
        };
    }

    public GameState ValidateOrCreate(GameState state)
    {
        if (state == null)
        {
            return CreateNewState();
        }

        state.meta ??= new MetaState();
        state.player ??= new PlayerState();
        state.currentSystem ??= new StarSystemRuntimeState();

        if (string.IsNullOrWhiteSpace(state.player.playerName))
        {
            state.player.playerName = "Captain";
        }

        if (string.IsNullOrWhiteSpace(state.player.currentSystemId))
        {
            state.player.currentSystemId = "system_start_01";
        }

        if (string.IsNullOrWhiteSpace(state.currentSystem.systemId))
        {
            state.currentSystem.systemId = state.player.currentSystemId;
        }

        return state;
    }
}