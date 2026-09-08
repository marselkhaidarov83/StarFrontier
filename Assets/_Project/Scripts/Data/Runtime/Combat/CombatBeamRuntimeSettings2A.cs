using UnityEngine;

public static class CombatBeamRuntimeSettings2A
{
    private const float DefaultBeamTickDuration01 = 0.9f;

    private static float _beamTickDuration01 =
        DefaultBeamTickDuration01;

    public static float BeamTickDuration01 =>
        Mathf.Clamp(_beamTickDuration01, 0.01f, 1f);

    public static void SetBeamTickDuration01(float value)
    {
        _beamTickDuration01 =
            Mathf.Clamp(value, 0.01f, 1f);
    }

    public static void ResetToDefault()
    {
        _beamTickDuration01 =
            DefaultBeamTickDuration01;
    }
}