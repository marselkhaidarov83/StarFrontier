using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Обрабатывает выбор планеты.
///
/// Сохраняет старый PlanetSelectedEvent
/// и дополнительно записывает планету
/// как текущую runtime-цель.
/// </summary>
public sealed class PlanetSelectableView2 :
    CustomMonoBehaviour,
    IPointerClickHandler
{
    [SerializeField]
    private PlanetConfig _planet;

    [SerializeField]
    private TargetMarkerView2A
        targetMarkerView;

    public PlanetConfig Planet =>
        _planet;

    private SimpleEventBus
        _simpleEventBus;

    private ITargetService2A
        _targetService;

    public void Initialize(
        PlanetConfig planet)
    {
        _simpleEventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();

        _targetService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<ITargetService2A>();

        _planet =
            planet;

        if (targetMarkerView == null)
        {
            targetMarkerView =
                GetComponent<
                    TargetMarkerView2A>();
        }

        if (targetMarkerView != null &&
            _planet != null)
        {
            targetMarkerView.Initialize(
                _planet.Id,
                true,
                true);
        }
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (_planet == null)
        {
            Debug.LogWarning(
                "[PlanetSelectableView2] " +
                "PlanetConfig is null.",
                this);

            return;
        }

        if (_targetService == null)
        {
            _targetService =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<ITargetService2A>();
        }

        Vector3 position =
            transform.position;

        bool selected =
            _targetService
                .TrySelectTarget(
                    _planet.Id,
                    SystemGameplayTargetType.Planet,
                    new Vector2(
                        position.x,
                        position.y),
                    true,
                    true,
                    true,
                    out TargetSelectionFailReason2A
                        failReason);

        if (!selected)
        {
            Debug.LogWarning(
                "[PlanetSelectableView2] " +
                "Target selection failed. " +
                "PlanetId = " +
                _planet.Id +
                " | Reason = " +
                failReason,
                this);
        }

        /*
         * Сохраняется существующее событие,
         * чтобы не сломать старый сценарий
         * выбора планеты.
         */
        _simpleEventBus.Publish(
            new PlanetSelectedEvent(
                _planet));
    }
}