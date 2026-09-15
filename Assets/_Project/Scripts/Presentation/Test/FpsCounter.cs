#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText;

    private float deltaTime;

    private void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        float fps = 1f / deltaTime;

        fpsText.text = $"{fps:0}";

        if (fps < 30f)
        {
            fpsText.color = Color.red;
        }
        else if (fps < 60f)
        {
            fpsText.color = new Color(1f, 0.5f, 0f); // оранжевый
        }
        else
        {
            fpsText.color = Color.green;
        }
    }
}
#endif