using UnityEngine;

public sealed class VisualUpdateAggregateFlushBehaviour : MonoBehaviour
{
    private void LateUpdate()
    {
        VisualUpdateAggregateLog.FlushAll();
    }
}