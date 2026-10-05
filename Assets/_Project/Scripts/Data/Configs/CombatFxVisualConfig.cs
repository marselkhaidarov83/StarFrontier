using UnityEngine;

[CreateAssetMenu(
    fileName = "combatFxVisualConfig",
    menuName = "StarFrontier/Configs/Combat/Combat FX Visual")]
public sealed class CombatFxVisualConfig : BaseConfig
{
    [Header("Civilization")]
    [SerializeField] private Color playerColor = new Color(0.35f, 0.88f, 1f, 1f);
    [SerializeField] private Color allyColor = new Color(0.35f, 0.88f, 1f, 1f);

    [Header("Enemies")]
    [SerializeField] private Color defaultEnemyColor = new Color(1f, 0.25f, 0.18f, 1f);
    [SerializeField] private Color aiEnemyColor = new Color(1f, 0.22f, 0.16f, 1f);
    [SerializeField] private Color ancientsEnemyColor = new Color(1f, 0.72f, 0.18f, 1f);
    [SerializeField] private Color infectedEnemyColor = new Color(0.62f, 1f, 0.22f, 1f);
    [SerializeField] private Color pirateColor = new Color(1f, 0.42f, 0.12f, 1f);

    public Color PlayerColor => playerColor;
    public Color AllyColor => allyColor;
    public Color DefaultEnemyColor => defaultEnemyColor;
    public Color AiEnemyColor => aiEnemyColor;
    public Color AncientsEnemyColor => ancientsEnemyColor;
    public Color InfectedEnemyColor => infectedEnemyColor;
    public Color PirateColor => pirateColor;

    public Color ResolvePlayerColor()
    {
        return playerColor;
    }

    public Color ResolveNpcColor(
        SystemNpcType npcType,
        string configId)
    {
        switch (npcType)
        {
            case SystemNpcType.Ally:
                return allyColor;

            case SystemNpcType.Pirate:
                return pirateColor;

            case SystemNpcType.Enemy:
                return ResolveEnemyColor(configId);

            default:
                return defaultEnemyColor;
        }
    }

    public Color ResolveEnemyColor(string configId)
    {
        string normalizedId = string.IsNullOrWhiteSpace(configId)
            ? string.Empty
            : configId.ToLowerInvariant();

        if (normalizedId.Contains("enemy_ai"))
            return aiEnemyColor;

        if (normalizedId.Contains("enemy_ancients"))
            return ancientsEnemyColor;

        if (normalizedId.Contains("enemy_infected"))
            return infectedEnemyColor;

        return defaultEnemyColor;
    }
}