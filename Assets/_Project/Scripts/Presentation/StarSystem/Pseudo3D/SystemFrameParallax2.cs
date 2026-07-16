using System;
using UnityEngine;

public sealed class SystemFrameParallax2 : MonoBehaviour
{
    [Serializable]
    public sealed class ParallaxLayer
    {
        [Header("Layer")]
        public string debugName;
        public Transform layer;

        [Header("Parallax")]
        public Vector2 factor = new Vector2(0.05f, 0.05f);
        public float strength = 1f;

        [HideInInspector] public Vector3 baseLocalPosition;
    }

    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Layers")]
    [SerializeField] private ParallaxLayer[] layers;

    [Header("Zoom Compensation")]
    [SerializeField] private float referenceCameraSize = 1200f;
    [SerializeField] private bool compensateZoom = true;

    [Header("Optional Clamp")]
    [SerializeField] private bool useMaxOffset;
    [SerializeField] private float maxOffsetByCameraSize = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private Vector3 _targetStartPosition;
    private bool _initialized;

    private void Start()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        Initialize();
        ApplyParallax();
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (target != null)
            _targetStartPosition = target.position;

        CacheBasePositions();

        _initialized = true;
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

        float cameraSize = GetCameraSize();
        float zoomMultiplier = GetZoomMultiplier(cameraSize);
        float maxOffset = Mathf.Max(1f, cameraSize * maxOffsetByCameraSize);

        Vector3 targetDelta = target.position - _targetStartPosition;

        foreach (ParallaxLayer layer in layers)
        {
            if (layer == null || layer.layer == null)
                continue;

            Vector3 offset = new Vector3(
                -targetDelta.x * layer.factor.x * layer.strength * zoomMultiplier,
                -targetDelta.y * layer.factor.y * layer.strength * zoomMultiplier,
                0f
            );

            if (useMaxOffset)
            {
                offset.x = Mathf.Clamp(offset.x, -maxOffset, maxOffset);
                offset.y = Mathf.Clamp(offset.y, -maxOffset, maxOffset);
            }

            layer.layer.localPosition = layer.baseLocalPosition + offset;

            if (debugLogs)
            {
                Debug.Log(
                    "[SystemFrameParallax2] " +
                    layer.debugName +
                    " | targetDelta = " + targetDelta +
                    " | offset = " + offset +
                    " | cameraSize = " + cameraSize +
                    " | useMaxOffset = " + useMaxOffset
                );
            }
        }
    }

    private float GetCameraSize()
    {
        if (targetCamera == null)
            return referenceCameraSize;

        if (!targetCamera.orthographic)
            return referenceCameraSize;

        return Mathf.Max(1f, targetCamera.orthographicSize);
    }

    private float GetZoomMultiplier(float cameraSize)
    {
        if (!compensateZoom)
            return 1f;

        if (referenceCameraSize <= 0f)
            return 1f;

        return Mathf.Max(0.25f, cameraSize / referenceCameraSize);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
            _targetStartPosition = target.position;

        CacheBasePositions();
    }

    public void Recenter()
    {
        if (target != null)
            _targetStartPosition = target.position;

        CacheBasePositions();
    }
}