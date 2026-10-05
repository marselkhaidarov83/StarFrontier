using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatMarkerVisibilityScaler2A : MonoBehaviour
{
    private static int[] _densityNearbyCountsCache = new int[0];
    private static int _densityActiveMarkersCount;
    private static int _playerWeaponRangeCacheFrame = -1;
    private static float _playerWeaponRangeCache = -1f;
    private const int PlayerViewMissingRetryFrames = 30;

    private static PlayerSystemMapShipView _cachedPlayerView;
    private static int _playerViewResolveAttemptFrame = -1;
    private static int _playerViewMissingRetryFrame = -1;

    private Renderer[] _statusMarkerRenderers = new Renderer[0];
    private TMP_Text[] _statusMarkerTexts = new TMP_Text[0];
    private TMP_Text[] _targetLabelTexts = new TMP_Text[0];

    private bool _childComponentsCaptured;
    private bool _hasCachedStatusVisible;
    private bool _cachedStatusVisible;

    private int _densityDecisionFrame = -1;
    private bool _densityDecisionHidden;
    private int _densityDecisionNearbyCount;

    private int _nextDensityRefreshFrame;
    private bool _lastStatusDistanceVisible;
    private bool _lastHiddenByDensity;
    private int _lastNearbyMarkersCount;
    private int _lastDensityMarkersCount;

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

    private const double DetailedVisualLogThresholdMs = 1.0;
    private const int DensityRefreshIntervalFrames = 12;
    private static int _aggregateFrame = -1;
    private static int _aggregateCount;
    private static int _aggregateStatusDistanceVisibleCount;
    private static int _aggregateHiddenByDensityCount;
    private static int _aggregateMaxDensityMarkers;
    private static int _aggregateMaxNearbyMarkers;
    private static double _aggregateTotalMs;
    private static double _aggregateResolveRootsMs;
    private static double _aggregateResolveServicesMs;
    private static double _aggregateResolvePlayerMs;
    private static double _aggregateResolveCameraMs;
    private static double _aggregateCaptureBaseScalesMs;
    private static double _aggregateCaptureChildComponentsMs;
    private static double _aggregateRefreshMarkerVisibilityMs;
    private static double _aggregateDensityMs;
    private static double _aggregateMaxSingleMs;
    private static string _aggregateMaxObjectName = string.Empty;

    private static CombatMarkerVisibilityScaler2A[] _densityMarkersCache;
    private static int _densityMarkersCacheFrame = -1;
    private static int _densityDecisionCacheFrame = -1;

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
        CaptureChildComponentCaches();
    }

    private void OnEnable()
    {
        ResolveRoots();
        CaptureBaseScales();
        CaptureChildComponentCaches();

        _hasCachedStatusVisible = false;
        _densityDecisionFrame = -1;
        _densityDecisionHidden = false;
        _densityDecisionNearbyCount = 0;

        _lastStatusDistanceVisible = false;
        _lastHiddenByDensity = false;
        _lastNearbyMarkersCount = 0;
        _lastDensityMarkersCount = 0;

        _nextDensityRefreshFrame =
            Time.frameCount +
            Mathf.Abs(GetInstanceID()) % DensityRefreshIntervalFrames;
    }

    private void LateUpdate()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double resolveRootsMs = 0.0;
        double resolveServicesMs = 0.0;
        double resolvePlayerMs = 0.0;
        double resolveCameraMs = 0.0;
        double captureBaseScalesMs = 0.0;
        double captureChildComponentsMs = 0.0;
        double refreshMarkerVisibilityMs = 0.0;
        double densityMs = 0.0;

        int densityMarkersCount = 0;
        int nearbyMarkersCount = 0;
        bool statusDistanceVisible = false;
        bool hiddenByDensity = false;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ResolveRoots();

            resolveRootsMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ResolveServices();

            resolveServicesMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ResolvePlayer();

            resolvePlayerMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ResolveCamera();

            resolveCameraMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            CaptureBaseScales();

            captureBaseScalesMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            CaptureChildComponentCaches();

            captureChildComponentsMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshMarkerVisibility(
                out densityMs,
                out densityMarkersCount,
                out nearbyMarkersCount,
                out statusDistanceVisible,
                out hiddenByDensity);

            refreshMarkerVisibilityMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            RecordLateUpdateAggregate(
                elapsedMs,
                resolveRootsMs,
                resolveServicesMs,
                resolvePlayerMs,
                resolveCameraMs,
                captureBaseScalesMs,
                captureChildComponentsMs,
                refreshMarkerVisibilityMs,
                densityMs,
                densityMarkersCount,
                nearbyMarkersCount,
                statusDistanceVisible,
                hiddenByDensity,
                gameObject.name);

            if (elapsedMs >= DetailedVisualLogThresholdMs)
            {
                LogSlowVisualUpdate(
                    "CombatMarkerVisibilityScaler2A.LateUpdate",
                    elapsedMs,
                    "Name=" + gameObject.name +
                    " | ResolveRootsMs=" + resolveRootsMs.ToString("F3") +
                    " | ResolveServicesMs=" + resolveServicesMs.ToString("F3") +
                    " | ResolvePlayerMs=" + resolvePlayerMs.ToString("F3") +
                    " | ResolveCameraMs=" + resolveCameraMs.ToString("F3") +
                    " | CaptureBaseScalesMs=" + captureBaseScalesMs.ToString("F3") +
                    " | CaptureChildComponentsMs=" + captureChildComponentsMs.ToString("F3") +
                    " | RefreshMarkerVisibilityMs=" + refreshMarkerVisibilityMs.ToString("F3") +
                    " | DensityMs=" + densityMs.ToString("F3") +
                    " | DensityMarkers=" + densityMarkersCount +
                    " | NearbyMarkers=" + nearbyMarkersCount +
                    " | StatusDistanceVisible=" + statusDistanceVisible +
                    " | HiddenByDensity=" + hiddenByDensity);
            }
        }
    }

    private static void RecordLateUpdateAggregate(
        double elapsedMs,
        double resolveRootsMs,
        double resolveServicesMs,
        double resolvePlayerMs,
        double resolveCameraMs,
        double captureBaseScalesMs,
        double captureChildComponentsMs,
        double refreshMarkerVisibilityMs,
        double densityMs,
        int densityMarkersCount,
        int nearbyMarkersCount,
        bool statusDistanceVisible,
        bool hiddenByDensity,
        string objectName)
    {
        int frame =
            Time.frameCount;

        if (_aggregateFrame != frame)
        {
            FlushLateUpdateAggregate();
            ResetLateUpdateAggregate(frame);
        }

        _aggregateCount++;
        _aggregateTotalMs += elapsedMs;
        _aggregateResolveRootsMs += resolveRootsMs;
        _aggregateResolveServicesMs += resolveServicesMs;
        _aggregateResolvePlayerMs += resolvePlayerMs;
        _aggregateResolveCameraMs += resolveCameraMs;
        _aggregateCaptureBaseScalesMs += captureBaseScalesMs;
        _aggregateCaptureChildComponentsMs += captureChildComponentsMs;
        _aggregateRefreshMarkerVisibilityMs += refreshMarkerVisibilityMs;
        _aggregateDensityMs += densityMs;

        if (statusDistanceVisible)
            _aggregateStatusDistanceVisibleCount++;

        if (hiddenByDensity)
            _aggregateHiddenByDensityCount++;

        if (densityMarkersCount > _aggregateMaxDensityMarkers)
            _aggregateMaxDensityMarkers = densityMarkersCount;

        if (nearbyMarkersCount > _aggregateMaxNearbyMarkers)
            _aggregateMaxNearbyMarkers = nearbyMarkersCount;

        if (elapsedMs > _aggregateMaxSingleMs)
        {
            _aggregateMaxSingleMs = elapsedMs;
            _aggregateMaxObjectName = objectName ?? string.Empty;
        }
    }

    private static void ResetLateUpdateAggregate(int frame)
    {
        _aggregateFrame = frame;
        _aggregateCount = 0;
        _aggregateStatusDistanceVisibleCount = 0;
        _aggregateHiddenByDensityCount = 0;
        _aggregateMaxDensityMarkers = 0;
        _aggregateMaxNearbyMarkers = 0;
        _aggregateTotalMs = 0.0;
        _aggregateResolveRootsMs = 0.0;
        _aggregateResolveServicesMs = 0.0;
        _aggregateResolvePlayerMs = 0.0;
        _aggregateResolveCameraMs = 0.0;
        _aggregateCaptureBaseScalesMs = 0.0;
        _aggregateCaptureChildComponentsMs = 0.0;
        _aggregateRefreshMarkerVisibilityMs = 0.0;
        _aggregateDensityMs = 0.0;
        _aggregateMaxSingleMs = 0.0;
        _aggregateMaxObjectName = string.Empty;
    }

    private static void FlushLateUpdateAggregate()
    {
        if (_aggregateFrame < 0 ||
            _aggregateCount <= 0)
        {
            return;
        }

        if (!VisualUpdatePerfLog.ShouldLog(_aggregateTotalMs))
            return;

        VisualUpdatePerfLog.LogMeasured(
            "CombatMarkerVisibilityScaler2A.LateUpdate.Aggregate",
            _aggregateTotalMs,
            "AggregateFrame=" + _aggregateFrame +
            " | ViewCount=" + _aggregateCount +
            " | StatusDistanceVisibleCount=" + _aggregateStatusDistanceVisibleCount +
            " | HiddenByDensityCount=" + _aggregateHiddenByDensityCount +
            " | MaxDensityMarkers=" + _aggregateMaxDensityMarkers +
            " | MaxNearbyMarkers=" + _aggregateMaxNearbyMarkers +
            " | MaxSingleMs=" + _aggregateMaxSingleMs.ToString("F3") +
            " | MaxObject=" + _aggregateMaxObjectName +
            " | ResolveRootsMs=" + _aggregateResolveRootsMs.ToString("F3") +
            " | ResolveServicesMs=" + _aggregateResolveServicesMs.ToString("F3") +
            " | ResolvePlayerMs=" + _aggregateResolvePlayerMs.ToString("F3") +
            " | ResolveCameraMs=" + _aggregateResolveCameraMs.ToString("F3") +
            " | CaptureBaseScalesMs=" + _aggregateCaptureBaseScalesMs.ToString("F3") +
            " | CaptureChildComponentsMs=" + _aggregateCaptureChildComponentsMs.ToString("F3") +
            " | RefreshMarkerVisibilityMs=" + _aggregateRefreshMarkerVisibilityMs.ToString("F3") +
            " | DensityMs=" + _aggregateDensityMs.ToString("F3"));
    }

    private void LogSlowVisualUpdate(
    string marker,
    double elapsedMs,
    string details)
    {
        DebugLogConfig config =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        double thresholdMs =
            config != null
                ? config.VisualUpdateSpikeThresholdMs
                : DetailedVisualLogThresholdMs;

        if (elapsedMs < thresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.GameTimeLoadAnalytics))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GameTimeLoadAnalytics,
            "[VISUAL_UPDATE_SPIKE]" +
            " Marker=" + marker +
            " | UnityFrame=" + Time.frameCount +
            " | Ms=" + elapsedMs.ToString("F2") +
            " | ThresholdMs=" + thresholdMs.ToString("F2") +
            " | " + details);
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

        if (_cachedPlayerView != null)
        {
            _playerView = _cachedPlayerView;
            return;
        }

        int currentFrame =
            Time.frameCount;

        if (_playerViewResolveAttemptFrame == currentFrame)
            return;

        if (currentFrame < _playerViewMissingRetryFrame)
            return;

        _playerViewResolveAttemptFrame =
            currentFrame;

        _cachedPlayerView =
            FindFirstObjectByType<PlayerSystemMapShipView>();

        if (_cachedPlayerView != null)
        {
            _playerView = _cachedPlayerView;
            _playerViewMissingRetryFrame = -1;
            return;
        }

        _playerViewMissingRetryFrame =
            currentFrame + PlayerViewMissingRetryFrames;
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

    private void RefreshMarkerVisibility(
        out double densityMs,
        out int densityMarkersCount,
        out int nearbyMarkersCount,
        out bool statusDistanceVisible,
        out bool hiddenByDensity)
    {
        densityMs = 0.0;
        densityMarkersCount = 0;
        nearbyMarkersCount = 0;
        statusDistanceVisible = false;
        hiddenByDensity = false;

        if (_playerView == null)
        {
            SetStatusVisible(false);
            return;
        }

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

        statusDistanceVisible =
            distance <= hideDistance &&
            IsInsideCameraView();

        if (statusDistanceVisible != _lastStatusDistanceVisible)
        {
            _nextDensityRefreshFrame = Time.frameCount;
            _lastStatusDistanceVisible = statusDistanceVisible;
        }

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

        if (!statusDistanceVisible)
        {
            SetStatusVisible(false);
            return;
        }

        double densityStartedAt =
            Time.realtimeSinceStartupAsDouble;

        hiddenByDensity =
            ShouldHideStatusByDensity(
                out densityMarkersCount,
                out nearbyMarkersCount);

        densityMs =
            (Time.realtimeSinceStartupAsDouble - densityStartedAt) * 1000.0;

        bool statusVisible =
            !hiddenByDensity ||
            IsTargetMarkerActive();

        SetStatusVisible(statusVisible);
    }

    private float ResolvePlayerMaxWeaponRange()
    {
        int currentFrame =
            Time.frameCount;

        if (_playerWeaponRangeCacheFrame == currentFrame &&
            _playerWeaponRangeCache > 0f)
        {
            return _playerWeaponRangeCache;
        }

        ShipRuntimeData activeShip =
            _gameSessionService
                ?.State
                ?.Player
                ?.PlayerShipState
                ?.GetActiveShip();

        if (activeShip?.EquippedWeaponIds == null ||
            _configService == null)
        {
            _playerWeaponRangeCacheFrame = currentFrame;
            _playerWeaponRangeCache = Mathf.Max(0.01f, fallbackPlayerWeaponRange);

            return _playerWeaponRangeCache;
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
            maxRange = Mathf.Max(0.01f, fallbackPlayerWeaponRange);

        _playerWeaponRangeCacheFrame = currentFrame;
        _playerWeaponRangeCache = maxRange;

        return _playerWeaponRangeCache;
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

    private bool ShouldHideStatusByDensity(
    out int densityMarkersCount,
    out int nearbyMarkersCount)
    {
        int currentFrame =
            Time.frameCount;

        if (_densityDecisionFrame == currentFrame ||
            currentFrame < _nextDensityRefreshFrame)
        {
            densityMarkersCount = _lastDensityMarkersCount;
            nearbyMarkersCount = _lastNearbyMarkersCount;

            return _lastHiddenByDensity;
        }

        CombatMarkerVisibilityScaler2A[] markers =
            GetDensityMarkersForCurrentFrame();

        densityMarkersCount = 0;
        nearbyMarkersCount = 0;

        if (markers == null ||
            markers.Length == 0 ||
            denseMarkerRadius <= 0f)
        {
            _densityDecisionFrame = currentFrame;
            _densityDecisionNearbyCount = 0;
            _densityDecisionHidden = false;

            _lastDensityMarkersCount = 0;
            _lastNearbyMarkersCount = 0;
            _lastHiddenByDensity = false;

            _nextDensityRefreshFrame =
                currentFrame + DensityRefreshIntervalFrames;

            return false;
        }

        Vector3 selfPosition =
            transform.position;

        float radiusSqr =
            denseMarkerRadius * denseMarkerRadius;

        for (int i = 0; i < markers.Length; i++)
        {
            CombatMarkerVisibilityScaler2A marker =
                markers[i];

            if (marker == null ||
                !marker.isActiveAndEnabled)
            {
                continue;
            }

            densityMarkersCount++;

            if (marker == this)
                continue;

            Vector3 delta =
                selfPosition - marker.transform.position;

            if (delta.sqrMagnitude <= radiusSqr)
                nearbyMarkersCount++;
        }

        bool hidden =
            nearbyMarkersCount >= maxNearbyVisibleStatusMarkers;

        _densityDecisionFrame = currentFrame;
        _densityDecisionNearbyCount = nearbyMarkersCount;
        _densityDecisionHidden = hidden;

        _lastDensityMarkersCount = densityMarkersCount;
        _lastNearbyMarkersCount = nearbyMarkersCount;
        _lastHiddenByDensity = hidden;

        _nextDensityRefreshFrame =
            currentFrame + DensityRefreshIntervalFrames;

        return hidden;
    }

    private void CaptureChildComponentCaches()
    {
        if (_childComponentsCaptured)
            return;

        _statusMarkerRenderers =
            statusMarkerRoot != null
                ? statusMarkerRoot.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];

        _statusMarkerTexts =
            statusMarkerRoot != null
                ? statusMarkerRoot.GetComponentsInChildren<TMP_Text>(true)
                : new TMP_Text[0];

        _targetLabelTexts =
            targetLabelRoot != null
                ? targetLabelRoot.GetComponentsInChildren<TMP_Text>(true)
                : new TMP_Text[0];

        _childComponentsCaptured =
            statusMarkerRoot != null ||
            targetLabelRoot != null;
    }

    private void SetStatusVisible(bool visible)
    {
        if (_hasCachedStatusVisible &&
            _cachedStatusVisible == visible)
        {
            return;
        }

        _hasCachedStatusVisible = true;
        _cachedStatusVisible = visible;

        SetRenderersVisible(
            _statusMarkerRenderers,
            _statusMarkerTexts,
            visible);
    }

    private static void EnsureDensityDecisionsForCurrentFrame()
    {
        int currentFrame =
            Time.frameCount;

        if (_densityDecisionCacheFrame == currentFrame)
            return;

        CombatMarkerVisibilityScaler2A[] markers =
            GetDensityMarkersForCurrentFrame();

        int markerCount =
            markers != null
                ? markers.Length
                : 0;

        if (_densityNearbyCountsCache == null ||
            _densityNearbyCountsCache.Length < markerCount)
        {
            _densityNearbyCountsCache =
                new int[markerCount];
        }

        for (int i = 0; i < markerCount; i++)
        {
            _densityNearbyCountsCache[i] = 0;

            CombatMarkerVisibilityScaler2A marker =
                markers[i];

            if (marker == null)
                continue;

            marker._densityDecisionFrame = currentFrame;
            marker._densityDecisionHidden = false;
            marker._densityDecisionNearbyCount = 0;
        }

        _densityActiveMarkersCount = 0;

        for (int i = 0; i < markerCount; i++)
        {
            CombatMarkerVisibilityScaler2A first =
                markers[i];

            if (first == null ||
                !first.isActiveAndEnabled)
            {
                continue;
            }

            _densityActiveMarkersCount++;

            Vector3 firstPosition =
                first.transform.position;

            float firstRadiusSqr =
                first.denseMarkerRadius * first.denseMarkerRadius;

            for (int j = i + 1; j < markerCount; j++)
            {
                CombatMarkerVisibilityScaler2A second =
                    markers[j];

                if (second == null ||
                    !second.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 delta =
                    firstPosition - second.transform.position;

                float distanceSqr =
                    delta.sqrMagnitude;

                if (firstRadiusSqr > 0f &&
                    distanceSqr <= firstRadiusSqr)
                {
                    _densityNearbyCountsCache[i]++;
                }

                float secondRadiusSqr =
                    second.denseMarkerRadius * second.denseMarkerRadius;

                if (secondRadiusSqr > 0f &&
                    distanceSqr <= secondRadiusSqr)
                {
                    _densityNearbyCountsCache[j]++;
                }
            }
        }

        for (int i = 0; i < markerCount; i++)
        {
            CombatMarkerVisibilityScaler2A marker =
                markers[i];

            if (marker == null)
                continue;

            int nearbyCount =
                _densityNearbyCountsCache[i];

            marker._densityDecisionFrame = currentFrame;
            marker._densityDecisionNearbyCount = nearbyCount;
            marker._densityDecisionHidden =
                nearbyCount >= marker.maxNearbyVisibleStatusMarkers;
        }

        _densityDecisionCacheFrame = currentFrame;
    }

    private static CombatMarkerVisibilityScaler2A[] GetDensityMarkersForCurrentFrame()
    {
        int currentFrame =
            Time.frameCount;

        if (_densityMarkersCacheFrame == currentFrame &&
            _densityMarkersCache != null)
        {
            return _densityMarkersCache;
        }

        _densityMarkersCache =
            FindObjectsByType<CombatMarkerVisibilityScaler2A>(
                FindObjectsSortMode.None);

        _densityMarkersCacheFrame =
            currentFrame;

        return _densityMarkersCache;
    }

    private bool IsTargetMarkerActive()
    {
        if (targetMarkerRoot != null &&
            targetMarkerRoot.gameObject.activeInHierarchy)
        {
            return true;
        }

        return HasVisibleText(_targetLabelTexts);
    }

    private static bool HasVisibleText(TMP_Text[] texts)
    {
        if (texts == null)
            return false;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text =
                texts[i];

            if (text != null &&
                text.enabled &&
                text.gameObject.activeInHierarchy &&
                !string.IsNullOrEmpty(text.text))
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

        Vector3 targetScale =
            baseScale * Mathf.Max(0.01f, scale);

        if ((root.localScale - targetScale).sqrMagnitude <= 0.000001f)
            return;

        root.localScale =
            targetScale;
    }

    private static void SetRenderersVisible(
        Renderer[] renderers,
        TMP_Text[] texts,
        bool visible)
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null &&
                    renderers[i].enabled != visible)
                {
                    renderers[i].enabled = visible;
                }
            }
        }

        if (texts != null)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null &&
                    texts[i].enabled != visible)
                {
                    texts[i].enabled = visible;
                }
            }
        }
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
