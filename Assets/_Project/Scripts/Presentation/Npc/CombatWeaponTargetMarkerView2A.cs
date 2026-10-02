using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatWeaponTargetMarkerView2A : MonoBehaviour
{
    private const int CircleSegments = 64;
    private const string MarkerRootName = "CombatWeaponTargetMarker";
    private const string SortingLayerName = "SystemShipFX";

    [Header("Marker")]
    [SerializeField] private GameObject markerRoot;
    [SerializeField] private SpriteRenderer markerRenderer;
    [SerializeField] private Sprite markerSprite;

    [Header("Weapon Badges")]
    [SerializeField] private TMP_Text weaponAssignmentsText;

    [Header("Layout")]
    [SerializeField] private float markerScaleMultiplier = 1.35f;
    [SerializeField] private float markerMinWorldSize = 1.4f;
    [SerializeField] private float textGapMultiplier = 0.14f;
    [SerializeField] private float textFontSizeMultiplier = 1.68f;
    [SerializeField] private float textMinFontSize = 13.5f;
    [SerializeField] private float textMaxFontSize = 27f;

    [Header("Visual")]
    [SerializeField]
    private Color markerColor =
        new Color(1f, 0.08f, 0.06f, 0.95f);

    [SerializeField]
    private Color npcTargetMarkerColor =
        new Color(0.2f, 0.62f, 1f, 0.95f);

    [SerializeField] private Color badgeTextColor = Color.white;
    [SerializeField] private int markerSortingOrder = 220;
    [SerializeField] private int textSortingOrder = 230;

    [Header("Animation")]
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private float pulseStrength = 0.07f;

    private readonly List<int> _assignedWeaponSlots = new();

    private SimpleEventBus _eventBus;
    private SystemNpcView _npcView;
    private LineRenderer _fallbackLineRenderer;
    private SpriteRenderer _shipRendererCache;
    private Material _fallbackMaterial;
    private Coroutine _pulseCoroutine;
    private string _runtimeNpcId = string.Empty;
    private Vector3 _markerBaseScale = Vector3.one;
    private float _lastLayoutShipWorldSize = -1f;
    private bool _hasAnyAssignedWeapon;
    private bool _isSelectedForAiming;
    private bool _isSelectedForTravel;
    private bool _layoutDirty = true;
    private bool _lastVisibleState;

    private void Awake()
    {
        _npcView = GetComponent<SystemNpcView>();
        ResolveMarkerObjects();
        ApplyStaticVisualSettings();

        _layoutDirty = true;
        RefreshLayoutFromShipSize();

        SetVisible(false);
    }

    private void OnEnable()
    {
        ResolveMarkerObjects();
        ApplyStaticVisualSettings();
        ResolveEventBus();

        if (_eventBus != null)
        {
            _eventBus.Subscribe<CombatWeaponTargetAssignmentsChangedEvent2A>(
                OnAssignmentsChanged);

            _eventBus.Subscribe<SystemNpcDestroyedEvent>(
                OnNpcDestroyed);

            _eventBus.Subscribe<DestinationSelectedEvent>(
                OnDestinationSelected);

            _eventBus.Subscribe<SystemTravelCancelledEvent>(
                OnTravelCancelled);

            _eventBus.Subscribe<SystemTravelCompletedEvent>(
                OnTravelCompleted);
        }

        _layoutDirty = true;

        RefreshRuntimeNpcId();
        RefreshMarkerVisibilityAndColor();
    }

    private void OnDisable()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<CombatWeaponTargetAssignmentsChangedEvent2A>(
                OnAssignmentsChanged);

            _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(
                OnNpcDestroyed);

            _eventBus.Unsubscribe<DestinationSelectedEvent>(
                OnDestinationSelected);

            _eventBus.Unsubscribe<SystemTravelCancelledEvent>(
                OnTravelCancelled);

            _eventBus.Unsubscribe<SystemTravelCompletedEvent>(
                OnTravelCompleted);
        }

        StopMarkerPulse();

        _isSelectedForTravel = false;
        _lastVisibleState = false;

        SetVisible(false);
    }

    private void OnDestroy()
    {
        StopMarkerPulse();

        if (_fallbackMaterial != null)
        {
            Destroy(_fallbackMaterial);
            _fallbackMaterial = null;
        }
    }

    private void OnAssignmentsChanged(
        CombatWeaponTargetAssignmentsChangedEvent2A evt)
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double runtimeStartedAt = startedAt;
        double scanStartedAt = startedAt;
        double textStartedAt = startedAt;
        double visibilityStartedAt = startedAt;

        float runtimeMs = 0f;
        float scanMs = 0f;
        float textMs = 0f;
        float visibilityMs = 0f;

        try
        {
            runtimeStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshRuntimeNpcId();

            runtimeMs =
                (float)((Time.realtimeSinceStartupAsDouble - runtimeStartedAt) * 1000d);

            scanStartedAt =
                Time.realtimeSinceStartupAsDouble;

            _assignedWeaponSlots.Clear();

            if (!string.IsNullOrWhiteSpace(_runtimeNpcId) &&
                evt.Assignments != null)
            {
                for (int i = 0; i < evt.Assignments.Length; i++)
                {
                    CombatWeaponTargetAssignment2A assignment =
                        evt.Assignments[i];

                    if (string.Equals(
                            assignment.TargetNpcId,
                            _runtimeNpcId,
                            System.StringComparison.Ordinal))
                    {
                        _assignedWeaponSlots.Add(
                            assignment.WeaponSlotIndex + 1);
                    }
                }
            }

            _hasAnyAssignedWeapon =
                _assignedWeaponSlots.Count > 0;

            _isSelectedForAiming =
                string.Equals(
                    evt.SelectedTargetNpcId,
                    _runtimeNpcId,
                    System.StringComparison.Ordinal);

            scanMs =
                (float)((Time.realtimeSinceStartupAsDouble - scanStartedAt) * 1000d);

            textStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshText();

            textMs =
                (float)((Time.realtimeSinceStartupAsDouble - textStartedAt) * 1000d);

            visibilityStartedAt =
                Time.realtimeSinceStartupAsDouble;

            _layoutDirty = true;
            RefreshMarkerVisibilityAndColor();

            visibilityMs =
                (float)((Time.realtimeSinceStartupAsDouble - visibilityStartedAt) * 1000d);
        }
        finally
        {
            VisualUpdatePerfLog.LogIfSlow(
                "CombatWeaponTargetMarkerView2A.OnAssignmentsChanged",
                startedAt,
                "Name=" + gameObject.name +
                " | RuntimeNpcId=" + (_runtimeNpcId ?? string.Empty) +
                " | AssignmentsCount=" + (evt.Assignments != null ? evt.Assignments.Length : 0) +
                " | AssignedWeaponCount=" + _assignedWeaponSlots.Count +
                " | HasAnyAssignedWeapon=" + _hasAnyAssignedWeapon +
                " | IsSelectedForAiming=" + _isSelectedForAiming +
                " | RuntimeMs=" + runtimeMs.ToString("F3") +
                " | ScanMs=" + scanMs.ToString("F3") +
                " | TextMs=" + textMs.ToString("F3") +
                " | VisibilityMs=" + visibilityMs.ToString("F3"));
        }
    }

    private void OnDestinationSelected(DestinationSelectedEvent evt)
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        RefreshRuntimeNpcId();

        _isSelectedForTravel =
            evt.DestinationType == TravelDestinationType.Npc &&
            string.Equals(
                evt.RuntimeNpcId,
                _runtimeNpcId,
                System.StringComparison.Ordinal);

        RefreshMarkerVisibilityAndColor();

        VisualUpdatePerfLog.LogIfSlow(
            "CombatWeaponTargetMarkerView2A.OnDestinationSelected",
            startedAt,
            "Name=" + gameObject.name +
            " | RuntimeNpcId=" + (_runtimeNpcId ?? string.Empty) +
            " | EventRuntimeNpcId=" + (evt.RuntimeNpcId ?? string.Empty) +
            " | IsSelectedForTravel=" + _isSelectedForTravel);
    }

    private void OnTravelCancelled(SystemTravelCancelledEvent evt)
    {
        _isSelectedForTravel = false;
        RefreshMarkerVisibilityAndColor();
    }

    private void OnTravelCompleted(SystemTravelCompletedEvent evt)
    {
        if (evt.DestinationType != TravelDestinationType.Npc)
            return;

        _isSelectedForTravel = false;
        RefreshMarkerVisibilityAndColor();
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        RefreshRuntimeNpcId();

        if (!string.Equals(
                evt.RuntimeNpcId,
                _runtimeNpcId,
                System.StringComparison.Ordinal))
        {
            return;
        }

        _assignedWeaponSlots.Clear();
        _hasAnyAssignedWeapon = false;
        _isSelectedForAiming = false;
        _isSelectedForTravel = false;
        _layoutDirty = true;

        RefreshText();
        RefreshMarkerVisibilityAndColor();
    }

    private void RefreshText()
    {
        if (weaponAssignmentsText == null)
            return;

        weaponAssignmentsText.text = string.Empty;
    }

    private void RefreshRuntimeNpcId()
    {
        if (_npcView == null)
            _npcView = GetComponent<SystemNpcView>();

        if (_npcView == null ||
            !_npcView.IsBound)
        {
            _runtimeNpcId = string.Empty;
            return;
        }

        _runtimeNpcId =
            _npcView.RuntimeNpcId;
    }

    private void ResolveEventBus()
    {
        if (_eventBus != null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        _eventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();
    }

    private void ResolveMarkerObjects()
    {
        if (markerRoot == null)
        {
            Transform existingRoot =
                transform.Find(MarkerRootName);

            if (existingRoot != null)
                markerRoot = existingRoot.gameObject;
        }

        if (markerRoot != null &&
            markerRoot.transform.parent != transform)
        {
            markerRoot.transform.SetParent(transform, false);
        }

        if (markerRenderer == null &&
            markerRoot != null)
        {
            markerRenderer =
                markerRoot.GetComponent<SpriteRenderer>();
        }

        if (weaponAssignmentsText == null)
        {
            weaponAssignmentsText =
                GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void ApplyStaticVisualSettings()
    {
        if (markerRenderer != null)
        {
            if (markerSprite != null)
                markerRenderer.sprite = markerSprite;

            markerRenderer.color = GetCurrentMarkerColor();
            markerRenderer.sortingLayerName = SortingLayerName;
            markerRenderer.sortingOrder = markerSortingOrder;
        }

        if (weaponAssignmentsText != null)
        {
            weaponAssignmentsText.color = badgeTextColor;
            weaponAssignmentsText.alignment = TextAlignmentOptions.Center;

            MeshRenderer textRenderer =
                weaponAssignmentsText.GetComponent<MeshRenderer>();

            if (textRenderer != null)
            {
                textRenderer.sortingLayerName = SortingLayerName;
                textRenderer.sortingOrder = textSortingOrder;
            }
        }
    }

    private void RefreshLayoutFromShipSize()
    {
        float shipWorldSize = ResolveShipWorldSize();

        if (!_layoutDirty &&
            Mathf.Approximately(shipWorldSize, _lastLayoutShipWorldSize))
        {
            return;
        }

        _layoutDirty = false;
        _lastLayoutShipWorldSize = shipWorldSize;

        float markerWorldSize = Mathf.Max(
            markerMinWorldSize,
            shipWorldSize * markerScaleMultiplier);

        if (markerRoot != null)
        {
            markerRoot.transform.localPosition = Vector3.zero;
        }

        if (markerRenderer != null &&
            markerRenderer.sprite != null)
        {
            Vector2 spriteWorldSize = markerRenderer.sprite.bounds.size;
            float maxSide = Mathf.Max(spriteWorldSize.x, spriteWorldSize.y);

            if (maxSide > 0f)
            {
                float scale = markerWorldSize / maxSide;
                _markerBaseScale = new Vector3(scale, scale, 1f);
            }
        }
        else
        {
            _markerBaseScale = Vector3.one;
            EnsureFallbackRing(markerWorldSize);
        }

        if (markerRoot != null &&
            !_hasAnyAssignedWeapon)
        {
            markerRoot.transform.localScale = _markerBaseScale;
        }

        if (weaponAssignmentsText != null)
        {
            float textY =
                markerWorldSize * 0.5f +
                Mathf.Max(0.05f, shipWorldSize * textGapMultiplier);

            weaponAssignmentsText.transform.localPosition =
                new Vector3(0f, textY, -0.02f);

            weaponAssignmentsText.fontSize =
                Mathf.Clamp(
                    shipWorldSize * textFontSizeMultiplier,
                    textMinFontSize,
                    textMaxFontSize);
        }
    }

    private float ResolveShipWorldSize()
    {
        if (_npcView != null &&
            _npcView.WorldSize > 0f)
        {
            return _npcView.WorldSize;
        }

        SpriteRenderer shipRenderer =
            ResolveShipRenderer();

        if (shipRenderer == null)
            return markerMinWorldSize;

        Bounds bounds = shipRenderer.bounds;
        return Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            markerMinWorldSize);
    }

    private SpriteRenderer ResolveShipRenderer()
    {
        if (_shipRendererCache != null)
            return _shipRendererCache;

        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null ||
                renderer == markerRenderer)
            {
                continue;
            }

            if (markerRoot != null &&
                renderer.transform.IsChildOf(markerRoot.transform))
            {
                continue;
            }

            _shipRendererCache = renderer;
            return _shipRendererCache;
        }

        return null;
    }

    private void EnsureFallbackRing(float markerWorldSize)
    {
        if (markerRoot == null)
            return;

        if (_fallbackLineRenderer == null)
            _fallbackLineRenderer =
                markerRoot.GetComponent<LineRenderer>();

        if (_fallbackLineRenderer == null)
            _fallbackLineRenderer =
                markerRoot.AddComponent<LineRenderer>();

        if (_fallbackMaterial == null)
        {
            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader != null)
                _fallbackMaterial = new Material(shader);
        }

        _fallbackLineRenderer.useWorldSpace = false;
        _fallbackLineRenderer.loop = true;
        _fallbackLineRenderer.positionCount = CircleSegments;
        _fallbackLineRenderer.startWidth = markerWorldSize * 0.025f;
        _fallbackLineRenderer.endWidth = markerWorldSize * 0.025f;
        _fallbackLineRenderer.startColor = GetCurrentMarkerColor();
        _fallbackLineRenderer.endColor = GetCurrentMarkerColor();
        _fallbackLineRenderer.sortingLayerName = SortingLayerName;
        _fallbackLineRenderer.sortingOrder = markerSortingOrder;

        if (_fallbackMaterial != null)
            _fallbackLineRenderer.material = _fallbackMaterial;

        float radius = markerWorldSize * 0.5f;

        for (int i = 0; i < CircleSegments; i++)
        {
            float angle =
                (float)i / CircleSegments * Mathf.PI * 2f;

            _fallbackLineRenderer.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f));
        }
    }

    private void SetVisible(bool visible)
    {
        if (markerRoot != null &&
            markerRoot.activeSelf != visible)
        {
            markerRoot.SetActive(visible);
        }

        if (markerRenderer != null)
        {
            bool rendererVisible =
                visible &&
                markerRenderer.sprite != null;

            if (markerRenderer.enabled != rendererVisible)
                markerRenderer.enabled = rendererVisible;
        }

        if (_fallbackLineRenderer != null)
        {
            bool fallbackVisible =
                visible &&
                markerRenderer == null;

            if (_fallbackLineRenderer.enabled != fallbackVisible)
                _fallbackLineRenderer.enabled = fallbackVisible;
        }

        if (weaponAssignmentsText != null &&
            weaponAssignmentsText.enabled != visible)
        {
            weaponAssignmentsText.enabled = visible;
        }
    }

    private void RefreshMarkerVisibilityAndColor()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double layoutStartedAt = startedAt;
        double colorStartedAt = startedAt;
        double visibleStartedAt = startedAt;
        double pulseStartedAt = startedAt;

        float layoutMs = 0f;
        float colorMs = 0f;
        float visibleMs = 0f;
        float pulseMs = 0f;

        bool visible =
            _hasAnyAssignedWeapon ||
            _isSelectedForAiming ||
            _isSelectedForTravel;

        try
        {
            if (visible)
            {
                layoutStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                RefreshLayoutFromShipSize();

                layoutMs =
                    (float)((Time.realtimeSinceStartupAsDouble - layoutStartedAt) * 1000d);

                colorStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                ApplyMarkerColor();

                colorMs =
                    (float)((Time.realtimeSinceStartupAsDouble - colorStartedAt) * 1000d);
            }

            visibleStartedAt =
                Time.realtimeSinceStartupAsDouble;

            SetVisible(visible);
            _lastVisibleState = visible;

            visibleMs =
                (float)((Time.realtimeSinceStartupAsDouble - visibleStartedAt) * 1000d);

            pulseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            if (visible)
                StartMarkerPulseIfNeeded();
            else
                StopMarkerPulse();

            pulseMs =
                (float)((Time.realtimeSinceStartupAsDouble - pulseStartedAt) * 1000d);
        }
        finally
        {
            VisualUpdatePerfLog.LogIfSlow(
                "CombatWeaponTargetMarkerView2A.RefreshMarkerVisibilityAndColor",
                startedAt,
                "Name=" + gameObject.name +
                " | RuntimeNpcId=" + (_runtimeNpcId ?? string.Empty) +
                " | Visible=" + visible +
                " | HasAnyAssignedWeapon=" + _hasAnyAssignedWeapon +
                " | IsSelectedForAiming=" + _isSelectedForAiming +
                " | IsSelectedForTravel=" + _isSelectedForTravel +
                " | LayoutMs=" + layoutMs.ToString("F3") +
                " | ColorMs=" + colorMs.ToString("F3") +
                " | SetVisibleMs=" + visibleMs.ToString("F3") +
                " | PulseStateMs=" + pulseMs.ToString("F3"));
        }
    }

    private void StartMarkerPulseIfNeeded()
    {
        if (_pulseCoroutine != null)
            return;

        if (markerRoot == null)
            return;

        _pulseCoroutine =
            StartCoroutine(PulseMarkerWhileVisible());
    }

    private void StopMarkerPulse()
    {
        if (_pulseCoroutine == null)
            return;

        StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = null;

        if (markerRoot != null)
            markerRoot.transform.localScale = _markerBaseScale;
    }

    private IEnumerator PulseMarkerWhileVisible()
    {
        while (_lastVisibleState &&
               markerRoot != null)
        {
            double startedAt =
                Time.realtimeSinceStartupAsDouble;

            float pulse =
                1f +
                Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) *
                pulseStrength;

            markerRoot.transform.localScale =
                _markerBaseScale * pulse;

            VisualUpdatePerfLog.LogIfSlow(
                "CombatWeaponTargetMarkerView2A.PulseMarkerWhileVisible",
                startedAt,
                "Name=" + gameObject.name +
                " | RuntimeNpcId=" + (_runtimeNpcId ?? string.Empty) +
                " | HasAnyAssignedWeapon=" + _hasAnyAssignedWeapon +
                " | IsSelectedForAiming=" + _isSelectedForAiming +
                " | IsSelectedForTravel=" + _isSelectedForTravel);

            yield return null;
        }

        _pulseCoroutine = null;
    }

    private void ApplyMarkerColor()
    {
        Color color = GetCurrentMarkerColor();

        if (markerRenderer != null)
            markerRenderer.color = color;

        if (_fallbackLineRenderer != null)
        {
            _fallbackLineRenderer.startColor = color;
            _fallbackLineRenderer.endColor = color;
        }
    }

    private Color GetCurrentMarkerColor()
    {
        if (_hasAnyAssignedWeapon || _isSelectedForAiming)
            return markerColor;

        return _isSelectedForTravel
            ? npcTargetMarkerColor
            : markerColor;
    }
}