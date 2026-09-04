using UnityEngine;

public sealed class DirectionalSpritePreview : MonoBehaviour
{
    private const int DirectionCount = 16;
    private const float DegreesPerDirection = 360f / DirectionCount;

    [Header("View")]
    [SerializeField]
    private SpriteRenderer targetRenderer;

    [SerializeField]
    private Sprite[] directions = new Sprite[DirectionCount];

    [Header("Preview")]
    [Range(0f, 359.99f)]
    [SerializeField]
    private float angleDegrees;

    [SerializeField]
    private bool autoRotate = true;

    [SerializeField]
    private float degreesPerSecond = 45f;

    private void Awake()
    {
        ApplySprite();
    }

    private void Update()
    {
        if (!autoRotate)
        {
            return;
        }

        angleDegrees = Mathf.Repeat(
            angleDegrees + degreesPerSecond * Time.deltaTime,
            360f);

        ApplySprite();
    }

    private void OnValidate()
    {
        ApplySprite();
    }

    private void ApplySprite()
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (directions == null ||
            directions.Length != DirectionCount)
        {
            return;
        }

        int index =
            Mathf.RoundToInt(angleDegrees / DegreesPerDirection)
            % DirectionCount;

        targetRenderer.sprite = directions[index];
    }
}