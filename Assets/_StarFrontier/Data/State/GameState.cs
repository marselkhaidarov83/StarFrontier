using System;

[Serializable]
public sealed class GameState
{
    public MetaStateU meta = new();
    public PlayerState player = new();
    public StarSystemRuntimeState currentSystem = new();
}