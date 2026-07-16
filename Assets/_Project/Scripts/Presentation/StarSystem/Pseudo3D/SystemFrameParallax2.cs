using System;
using UnityEngine;

public sealed class SystemFrameParallax2 : MonoBehaviour
{
    [Serializable]
    public sealed class ParallaxLayer
    {
        public Transform layer;
        public Vector2 factor = new Vector2(0.02f, 0.02f);

        [HideInInspector] public Vector3 baseLocalPosition;
    }

    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Layers")]
    [SerializeField] private ParallaxLayer[] layers;

    [Header("Limits")]
    [SerializeField] private float maxOffset = 80f;

    private void Start()
    {
        CacheBasePositions();
    }

    private void LateUpdate()
    {
        ApplyParallax();
    }

    private void CacheBasePositions()
    {
        if (layers == null)
            return;

        foreach (ParallaxLayer layer in layers)
        {
            if (layer == null || layer.layer == null)
                continue;

            layer.baseLocalPosition = layer.layer.localPosition;
        }
    }

    private void ApplyParallax()
    {
        if (target == null)
            return;

        if (layers == null)
            return;

        Vector3 targetPosition = target.position;

        foreach (ParallaxLayer layer in layers)
        {
            if (layer == null || layer.layer == null)
                continue;

            Vector3 offset = new Vector3(
                -targetPosition.x * layer.factor.x,
                -targetPosition.y * layer.factor.y,
                0f
            );

            offset.x = Mathf.Clamp(offset.x, -maxOffset, maxOffset);
            offset.y = Mathf.Clamp(offset.y, -maxOffset, maxOffset);

            layer.layer.localPosition = layer.baseLocalPosition + offset;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}