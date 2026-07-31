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

        [Header("Manual Position Offset")]
        [Tooltip("Ручное смещение слоя по горизонтали в мировых единицах Unity.")]
        public float offsetX;

        [Tooltip("Ручное смещение слоя по вертикали в мировых единицах Unity.")]
        public float offsetY;

        [Header("Parallax")]
        [Tooltip("Сила параллакса отдельно по X и Y.")]
        public Vector2 factor = new Vector2(0.05f, 0.05f);

        [Tooltip("Общий множитель силы параллакса слоя.")]
        public float strength = 1f;

        [HideInInspector]
        public Vector3 baseLocalPosition;
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

        float maxOffset = Mathf.Max(
            1f,
            cameraSize * maxOffsetByCameraSize
        );

        Vector3 targetDelta =
            target.position - _targetStartPosition;

        foreach (ParallaxLayer layer in layers)
        {
            if (layer == null || layer.layer == null)
                continue;

            Vector3 parallaxOffset = new Vector3(
                -targetDelta.x *
                layer.factor.x *
                layer.strength *
                zoomMultiplier,

                -targetDelta.y *
                layer.factor.y *
                layer.strength *
                zoomMultiplier,

                0f
            );

            if (useMaxOffset)
            {
                parallaxOffset.x = Mathf.Clamp(
                    parallaxOffset.x,
                    -maxOffset,
                    maxOffset
                );

                parallaxOffset.y = Mathf.Clamp(
                    parallaxOffset.y,
                    -maxOffset,
                    maxOffset
                );
            }

            Vector3 manualOffset = new Vector3(
                layer.offsetX,
                layer.offsetY,
                0f
            );

            layer.layer.localPosition =
                layer.baseLocalPosition +
                manualOffset +
                parallaxOffset;

            if (debugLogs)
            {
                Debug.Log(
                    "[SystemFrameParallax2] " +
                    layer.debugName +
                    " | targetDelta = " + targetDelta +
                    " | manualOffset = " + manualOffset +
                    " | parallaxOffset = " + parallaxOffset +
                    " | finalPosition = " + layer.layer.localPosition +
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

        return Mathf.Max(
            1f,
            targetCamera.orthographicSize
        );
    }

    private float GetZoomMultiplier(float cameraSize)
    {
        if (!compensateZoom)
            return 1f;

        if (referenceCameraSize <= 0f)
            return 1f;

        return Mathf.Max(
            0.25f,
            cameraSize / referenceCameraSize
        );
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
            _targetStartPosition = target.position;
    }

    public void Recenter()
    {
        if (target != null)
            _targetStartPosition = target.position;
    }
}