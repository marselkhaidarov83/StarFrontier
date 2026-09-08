using UnityEngine;

public sealed class CombatBeamView2A :  CustomMonoBehaviour
{
    private const string DefaultSortingLayerName = "SystemShipFX";

    [Header("Runtime")]
    [SerializeField]
    [Range(0.01f, 1f)]
    private float beamTickDuration01 = 0.9f;

    [Header("Line")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] [Min(0.01f)] private float lineWidth = 0.08f;
    [SerializeField] [Min(0f)] private float endpointPadding = 0.45f;
    [SerializeField] private string sortingLayerName = DefaultSortingLayerName;
    [SerializeField] private int sortingOrder = 730;

    private ISystemNpcCombatService _combatService;

    public string BeamId { get; private set; }

    public float BeamTickDuration01 =>
        Mathf.Clamp(beamTickDuration01, 0.01f, 1f);

    private void Awake()
    {
        ApplyRuntimeSettings();
    }

    public void ApplyRuntimeSettings()
    {
        CombatBeamRuntimeSettings2A.SetBeamTickDuration01(
            BeamTickDuration01);
    }

    public void Init(
        CombatBeamStartedEvent2A evt,
        Color color)
    {
        ApplyRuntimeSettings();

        BeamId = evt.BeamId;

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
            ? DefaultSortingLayerName
            : sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        SetEndpoints(evt.StartPosition, evt.TargetPosition);
        ResolveCombatService();

        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (string.IsNullOrWhiteSpace(BeamId))
            return;

        ResolveCombatService();

        if (_combatService == null)
            return;

        if (!_combatService.TryGetBeam(
                BeamId,
                out CombatBeamRuntimeState2A beam))
        {
            Complete();
            return;
        }

        if (beam == null || beam.IsResolved)
        {
            Complete();
            return;
        }

        SetEndpoints(beam.StartPosition, beam.TargetPosition);
    }

    public void SetEndpoints(
        Vector3 from,
        Vector3 to)
    {
        if (lineRenderer == null)
            return;

        Vector3 direction = to - from;

        if (direction.sqrMagnitude > 0.0001f)
        {
            float distance = direction.magnitude;
            float safePadding = Mathf.Min(endpointPadding, distance * 0.4f);
            Vector3 normalized = direction / distance;

            from += normalized * safePadding;
            to -= normalized * safePadding;
        }

        from.z = -0.05f;
        to.z = -0.05f;

        lineRenderer.SetPosition(0, from);
        lineRenderer.SetPosition(1, to);
    }

    public void Complete()
    {
        BeamId = string.Empty;

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        gameObject.SetActive(false);
    }

    private void ResolveCombatService()
    {
        if (_combatService != null)
            return;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return;

        bootstrapper.ServiceRegistry.TryGet(out _combatService);
    }
}