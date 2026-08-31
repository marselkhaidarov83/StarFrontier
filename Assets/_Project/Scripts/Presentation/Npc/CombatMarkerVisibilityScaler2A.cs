using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatMarkerVisibilityScaler2A : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform targetMarkerRoot;
    [SerializeField] private Transform targetLabelRoot;
    [SerializeField] private Transform statusMarkerRoot;

    [Header("Player Weapon Range")]
    [SerializeField] private float fullSizeRangeMultiplier = 0.33f;
    [SerializeField] private float fadeStartRangeMultiplier = 1f;
    [SerializeField] private float hideRangeMultiplier = 1.67f;
    [SerializeField] private float fallbackPlayerWeaponRange = 12f;

    [Header("Scale")]
    [SerializeField] private float nearScale = 1f;
    [SerializeField] private float farScale = 0.65f;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenOutsideCamera = true;
    [SerializeField] private float viewportPadding = 0.08f;

    [Header("Density")]
    [SerializeField] private float denseMarkerRadius = 7f;
    [SerializeField] private int maxNearbyVisibleStatusMarkers = 4;

    private PlayerSystemMapShipView _playerView;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private Camera _camera;
    private Vector3 _targetMarkerBaseScale = Vector3.one;
    private Vector3 _targetLabelBaseScale = Vector3.one;
    private Vector3 _statusMarkerBaseScale = Vector3.one;
    private bool _baseScaleCaptured;

    private void Awake()
    {
        ResolveRoots();
        CaptureBaseScales();
    }

    private void OnEnable()
    {
        ResolveRoots();
        CaptureBaseScales();
    }

    private void LateUpdate()
    {
        ResolveRoots();
        ResolveServices();
        ResolvePlayer();
        ResolveCamera();
        CaptureBaseScales();
        RefreshMarkerVisibility();
    }

    private void ResolveRoots()
    {
        if (targetMarkerRoot == null)
        {
            Transform found = transform.Find("CombatWeaponTargetMarker");

            if (found != null)
                targetMarkerRoot = found;
        }

        if (targetLabelRoot == null)
        {
            Transform found = transform.Find("WeaponAssignmentText");

            if (found != null)
                targetLabelRoot = found;
        }

        if (statusMarkerRoot == null)
        {
            Transform found = transform.Find("CombatEnemyStatusMarker");

            if (found != null)
                statusMarkerRoot = found;
        }
    }

    private void ResolvePlayer()
    {
        if (_playerView != null)
            return;

        _playerView =
            FindFirstObjectByType<PlayerSystemMapShipView>();
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (_gameSessionService == null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet<IGameSessionService>(
                out _gameSessionService);
        }

        if (_configService == null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet<IConfigService>(
                out _configService);
        }
    }

    private void ResolveCamera()
    {
        if (_camera != null)
            return;

        _camera = Camera.main;
    }

    private void CaptureBaseScales()
    {
        if (_baseScaleCaptured)
            return;

        if (targetMarkerRoot != null)
            _targetMarkerBaseScale = targetMarkerRoot.localScale;

        if (targetLabelRoot != null)
            _targetLabelBaseScale = targetLabelRoot.localScale;

        if (statusMarkerRoot != null)
            _statusMarkerBaseScale = statusMarkerRoot.localScale;

        _baseScaleCaptured =
            targetMarkerRoot != null ||
            targetLabelRoot != null ||
            statusMarkerRoot != null;
    }

    private void RefreshMarkerVisibility()
    {
        if (_playerView == null)
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                _playerView.transform.position);

        float maxWeaponRange =
            ResolvePlayerMaxWeaponRange();

        float fullSizeDistance =
            maxWeaponRange * Mathf.Max(0.01f, fullSizeRangeMultiplier);

        float fadeStartDistance =
            maxWeaponRange * Mathf.Max(
                fullSizeRangeMultiplier,
                fadeStartRangeMultiplier);

        float hideDistance =
            maxWeaponRange * Mathf.Max(
                fadeStartRangeMultiplier,
                hideRangeMultiplier);

        bool statusDistanceVisible =
            distance <= hideDistance &&
            IsInsideCameraView();

        float distance01 =
            Mathf.InverseLerp(
                fullSizeDistance,
                fadeStartDistance,
                distance);

        float scale =
            Mathf.Lerp(
                nearScale,
                farScale,
                Mathf.Clamp01(distance01));

        ApplyRootScale(statusMarkerRoot, _statusMarkerBaseScale, scale);

        bool statusVisible =
            statusDistanceVisible &&
            (!ShouldHideStatusByDensity() || IsTargetMarkerActive());

        SetRenderersVisible(statusMarkerRoot, statusVisible);
    }

    private float ResolvePlayerMaxWeaponRange()
    {
        ShipRuntimeData activeShip =
            _gameSessionService
                ?.State
                ?.Player
                ?.PlayerShipState
                ?.GetActiveShip();

        if (activeShip?.EquippedWeaponIds == null ||
            _configService == null)
        {
            return Mathf.Max(0.01f, fallbackPlayerWeaponRange);
        }

        float maxRange = 0f;

        for (int i = 0; i < activeShip.EquippedWeaponIds.Count; i++)
        {
            string weaponConfigId =
                activeShip.EquippedWeaponIds[i];

            if (string.IsNullOrWhiteSpace(weaponConfigId))
                continue;

            WeaponConfig weaponConfig =
                _configService.GetWeaponConfigById(weaponConfigId);

            if (weaponConfig == null)
                continue;

            if (weaponConfig.RangeMax > maxRange)
                maxRange = weaponConfig.RangeMax;
        }

        if (maxRange <= 0f)
            return Mathf.Max(0.01f, fallbackPlayerWeaponRange);

        return maxRange;
    }

    private bool IsInsideCameraView()
    {
        if (!hideWhenOutsideCamera ||
            _camera == null)
        {
            return true;
        }

        Vector3 point =
            _camera.WorldToViewportPoint(transform.position);

        if (point.z < 0f)
            return false;

        return point.x >= -viewportPadding &&
               point.x <= 1f + viewportPadding &&
               point.y >= -viewportPadding &&
               point.y <= 1f + viewportPadding;
    }

    private bool ShouldHideStatusByDensity()
    {
        CombatMarkerVisibilityScaler2A[] markers =
            FindObjectsByType<CombatMarkerVisibilityScaler2A>(
                FindObjectsSortMode.None);

        int nearbyCount = 0;

        for (int i = 0; i < markers.Length; i++)
        {
            CombatMarkerVisibilityScaler2A marker = markers[i];

            if (marker == null ||
                marker == this)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    marker.transform.position);

            if (distance <= denseMarkerRadius)
                nearbyCount++;
        }

        return nearbyCount >= maxNearbyVisibleStatusMarkers;
    }

    private bool IsTargetMarkerActive()
    {
        if (targetMarkerRoot != null &&
            targetMarkerRoot.gameObject.activeInHierarchy)
        {
            return true;
        }

        return HasVisibleText(targetLabelRoot);
    }

    private static bool HasVisibleText(Transform root)
    {
        if (root == null ||
            !root.gameObject.activeInHierarchy)
        {
            return false;
        }

        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null &&
                texts[i].enabled &&
                !string.IsNullOrEmpty(texts[i].text))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyRootScale(
        Transform root,
        Vector3 baseScale,
        float scale)
    {
        if (root == null)
            return;

        root.localScale =
            baseScale * Mathf.Max(0.01f, scale);
    }

    private static void SetRenderersVisible(
        Transform root,
        bool visible)
    {
        if (root == null)
            return;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }

        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].enabled = visible;
        }
    }
}
