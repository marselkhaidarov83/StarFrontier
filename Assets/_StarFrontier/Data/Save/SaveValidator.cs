using System;

public sealed class SaveValidator
{
    private readonly int _defaultSaveVersion = 1;
    private readonly string _defaultPlayerName = "Captain";
    private readonly string _defaultSystemId = "system_start_01";

    public GameState CreateNewState()
    {
        var now = DateTime.UtcNow.ToString("O");

        return new GameState
        {
            meta = new MetaStateU
            {
                saveVersion = _defaultSaveVersion,
                createdAtUtc = now,
                lastSavedAtUtc = now,
                totalTicks = 0
            },
            player = new PlayerState
            {
                playerName = _defaultPlayerName,
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

        state.meta ??= new MetaStateU();
        state.player ??= new PlayerState();
        state.currentSystem ??= new StarSystemRuntimeState();

        if (state.meta.saveVersion <= 0)
        {
            state.meta.saveVersion = _defaultSaveVersion;
        }

        if (string.IsNullOrWhiteSpace(state.meta.createdAtUtc))
        {
            state.meta.createdAtUtc = System.DateTime.UtcNow.ToString("O");
        }

        if (string.IsNullOrWhiteSpace(state.meta.lastSavedAtUtc))
        {
            state.meta.lastSavedAtUtc = state.meta.createdAtUtc;
        }

        if (string.IsNullOrWhiteSpace(state.player.playerName))
        {
            state.player.playerName = _defaultPlayerName;
        }

        if (string.IsNullOrWhiteSpace(state.player.currentSystemId))
        {
            state.player.currentSystemId = _defaultSystemId;
        }

        if (string.IsNullOrWhiteSpace(state.currentSystem.systemId))
        {
            state.currentSystem.systemId = state.player.currentSystemId;
        }
        
        return state;
    }
}