#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText;

    private float deltaTime;

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        float fps = 0f;

        try
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

            fps = 1f / deltaTime;

            fpsText.text = $"{fps:0}";

            if (fps < 30f)
            {
                fpsText.color = Color.red;
            }
            else if (fps < 60f)
            {
                fpsText.color = new Color(1f, 0.5f, 0f);
            }
            else
            {
                fpsText.color = Color.green;
            }
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            VisualUpdateAggregateLog.Record(
                "FpsCounter.Update",
                elapsedMs,
                "Name=" + name +
                " | Fps=" + fps.ToString("F1"));
        }
    }
}
#endif