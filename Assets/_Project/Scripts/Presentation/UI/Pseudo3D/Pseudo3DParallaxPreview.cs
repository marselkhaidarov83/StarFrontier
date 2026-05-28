using UnityEngine;

public sealed class Pseudo3DParallaxPreview : MonoBehaviour
{
    [SerializeField] private Transform backLayer;
    [SerializeField] private Transform midLayer;
    [SerializeField] private Transform frontLayer;

    [SerializeField] private float speed = 0.5f;

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * speed);

        if (backLayer != null)
        {
            backLayer.localPosition = new Vector3(offset * 0.2f, 0f, 10f);
        }

        if (midLayer != null)
        {
            midLayer.localPosition = new Vector3(offset * 0.5f, 0f, 0f);
        }

        if (frontLayer != null)
        {
            frontLayer.localPosition = new Vector3(offset * 1.0f, 0f, -6f);
        }
    }
}