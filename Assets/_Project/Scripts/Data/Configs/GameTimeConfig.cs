using UnityEngine;

[CreateAssetMenu(fileName = "GameTimeConfig", menuName = "StarFrontier/Configs/Game/Game Time")]
public sealed class GameTimeConfig : BaseConfig
{
    public const int DefaultMinutesPerGameTick = 15;

    [SerializeField, Min(0.01f)]
    [Tooltip("Реальная длительность одного игрового тика в секундах.")]
    private float secondsPerGameTick = 1.5f;

    [SerializeField, Min(1)]
    [Tooltip("Игровых минут за тик при расчёте даты и времени в HUD.")]
    private int minutesPerGameTick = DefaultMinutesPerGameTick;

    public float SecondsPerGameTick => Mathf.Max(0.01f, secondsPerGameTick);
    public int MinutesPerGameTick => minutesPerGameTick > 0 ? minutesPerGameTick : DefaultMinutesPerGameTick;
}
