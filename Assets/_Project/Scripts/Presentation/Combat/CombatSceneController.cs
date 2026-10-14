using UnityEngine;

public sealed class CombatSceneController : MonoBehaviour
{
    private void Start()
    {
        AppLog.Info(DebugLogChannel.CombatPresentation, "[CombatScene] Combat scene loaded");
    }
}