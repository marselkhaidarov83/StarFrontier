using UnityEngine;

[CreateAssetMenu(fileName = "grabber_", menuName = "StarFrontier/Configs/Grabber")]
public sealed class GrabberConfig : BaseConfig
{
    [Header("Loot capture")]
    [SerializeField, Min(1)] private int strength = 5;
    [SerializeField, Min(0f)] private float range = 100f;
    [SerializeField, Min(0f)] private float pullSpeedPerTick = 25f;

    public int Strength => strength;
    public float Range => range;
    public float PullSpeedPerTick => pullSpeedPerTick;

#if UNITY_EDITOR
    private void OnValidate()
    {
        strength = Mathf.Max(1, strength);
        range = Mathf.Max(0f, range);
        pullSpeedPerTick = Mathf.Max(0f, pullSpeedPerTick);
    }
#endif
}
