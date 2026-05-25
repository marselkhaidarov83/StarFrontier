using UnityEngine;

public sealed class SystemTravelVisualController2 : CustomMonoBehaviour
{
    [Header("Views")]
    [SerializeField] private TravelLineView2 travelLineView;
    [SerializeField] private ShipEngineGlowView2 engineGlowView;

    [Header("Options")]
    [SerializeField] private bool showLineWhenDestinationSelected = true;
    [SerializeField] private bool showLineWhileFlying = true;

    private ISystemTravelService _travelService;

    private Vector3 _lastShipPosition;

    private void Start()
    {
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();

        if (_travelService == null)
        {
            Debug.LogError("[SystemTravelVisualController] ISystemTravelService not found.");
            return;
        }

        _lastShipPosition = _travelService.State.GetCurrentPosition();

        if (travelLineView != null)
            travelLineView.Hide();

        if (engineGlowView != null)
            engineGlowView.Hide();
    }

    private void Update()
    {
        if (_travelService == null)
            return;

        UpdateTravelLine();
        UpdateEngineGlow();

        _lastShipPosition = _travelService.State.GetCurrentPosition();
    }

    private void UpdateTravelLine()
    {
        if (travelLineView == null)
            return;

        SystemTravelState state = _travelService.State;

        bool shouldShow =
            state.Status == SystemTravelStatus.DestinationSelected && showLineWhenDestinationSelected ||
            state.Status == SystemTravelStatus.Flying && showLineWhileFlying;

        if (!shouldShow || !state.HasDestination)
        {
            travelLineView.Hide();
            return;
        }

        Vector3 from = state.GetCurrentPosition();
        Vector3 to = _travelService.GetCurrentDestinationPosition();

        travelLineView.Show(from, to);
    }

    private void UpdateEngineGlow()
    {
        if (engineGlowView == null)
            return;

        SystemTravelState state = _travelService.State;

        if (state.Status != SystemTravelStatus.Flying)
        {
            engineGlowView.Hide();
            return;
        }

        Vector3 current = state.GetCurrentPosition();
        Vector3 movementDirection = current - _lastShipPosition;

        engineGlowView.Show();
    }
}