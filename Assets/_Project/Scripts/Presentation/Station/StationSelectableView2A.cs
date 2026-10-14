using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Обрабатывает выбор станции на карте системы.
/// </summary>
public sealed class StationSelectableView2A :
    CustomMonoBehaviour,
    IPointerClickHandler
{
    [SerializeField]
    private StationConfig station;

    private SimpleEventBus _eventBus;
    private ITargetService2A _targetService;

    public void Initialize(
        StationConfig stationConfig)
    {
        station =
            stationConfig;

        ResolveServices();
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (station == null)
        {
            AppLog.Warning(DebugLogChannel.StationUi,
                typeof(StationSelectableView2A),
                "StationConfig is null.");

            return;
        }

        ResolveServices();

        _eventBus?.Publish(
            new SystemObjectsPanelCloseRequestedEvent2A());

        Vector3 position =
            transform.position;

        if (_targetService != null)
        {
            bool selected =
                _targetService.TrySelectTarget(
                    station.Id,
                    SystemGameplayTargetType.Station,
                    new Vector2(
                        position.x,
                        position.y),
                    true,
                    station.IsActive,
                    true,
                    out TargetSelectionFailReason2A failReason);

            if (!selected)
            {
                AppLog.Warning(DebugLogChannel.StationUi,
                    typeof(StationSelectableView2A),
                    "Target selection failed. " +
                    "StationId = " +
                    station.Id +
                    " | Reason = " +
                    failReason);
            }
        }

        if (_eventBus == null)
        {
            AppLog.Error(DebugLogChannel.StationUi,
                typeof(StationSelectableView2A),
                "SimpleEventBus not found.");

            return;
        }

        _eventBus.Publish(
            new StationSelectedEvent(
                station));
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        if (_eventBus == null)
            _eventBus = registry.Get<SimpleEventBus>();

        if (_targetService == null)
            _targetService = registry.Get<ITargetService2A>();
    }
}
