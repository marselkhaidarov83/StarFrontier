using System;

[Serializable]
public sealed class GameState
{
    public MetaState meta = new();
    public PlayerState player = new();
    public StarSystemRuntimeState currentSystem = new();
    public GalaxyState Galaxy = new ();
}